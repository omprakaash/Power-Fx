// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.PowerFx.Core.IR;
using Microsoft.PowerFx.Core.Public.Types;
using Microsoft.PowerFx.Core.Public.Values;

namespace Microsoft.PowerFx
{
    /// <summary>
    /// Marshal .Net classes (with fields). This supports strong typing and lazy marshalling. 
    /// </summary>
    public class ObjectMarshalerProvider : ITypeMashalerProvider
    {
        // $$$ To marshal an object, we need:
        //  - fxName,
        //  - .Net type (for getting the marhsaler)
        //  - getter for runtime value
        // Customization point 
        public Func<PropertyInfo, string> PropertyMapperFunc = (propInfo) => propInfo.Name;

        public ITypeMarshaler New(Type type, TypeMarshallerCache cache, int maxDepth)
        {        
            if (!type.IsClass)
            {
                return null;
            }

            var mapping = new Dictionary<string, Func<object, FormulaValue>>(); // $$$ Casing?

            var fxType = new RecordType();

            // $$$ What if we want properties that aren't in .net? (do we care)
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead)
                {
                    continue;
                }

                var fxName = PropertyMapperFunc(prop);
                if (fxName == null)
                {
                    continue;
                }

                var tm = cache.New(prop.PropertyType, maxDepth);
                var fxFieldType = tm.Type;

                // Basic .net property
                mapping[fxName] = (object objSource) =>
                {
                    var propValue = prop.GetValue(objSource);
                    return tm.Marshal(propValue);
                };

                fxType = fxType.Add(fxName, fxFieldType);
            }

            return new ObjectMarshaler(fxType, mapping);
        }      
    }

    /// <summary>
    /// Marshal a specific type of object to a record. 
    /// </summary>
    public class ObjectMarshaler : ITypeMarshaler
    {
        // Map fx field name to a function produces the formula value given the dotnet object.
        private readonly IReadOnlyDictionary<string, Func<object, FormulaValue>> _mapping;

        public FormulaType Type { get; private set; }

        // FormulaType must be a record, and the dictionary provders getters for each field in that record. 
        public ObjectMarshaler(FormulaType type, IReadOnlyDictionary<string, Func<object, FormulaValue>> fieldMap)
        {
            if (!(type is RecordType))
            {
                throw new ArgumentException($"type must be a record, not ${type}");
            }

            Type = type;
            _mapping = fieldMap;
        }

        public FormulaValue Marshal(object source)
        {
            var value = new ObjectRecordValue(IRContext.NotInSource(Type), source, this);
            return value;
        }

        // Get the value of the field. 
        // Return null on missing
        public FormulaValue TryGetField(object source, string name)
        {
            if (_mapping.TryGetValue(name, out var getter))
            {
                var fieldValue = getter(source);
                return fieldValue;
            }

            return null;
        }
    }
}
