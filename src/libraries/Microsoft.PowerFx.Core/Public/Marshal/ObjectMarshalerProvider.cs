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
        // Customization point 
        public Func<PropertyInfo, string> _mapper = (propInfo) => propInfo.Name;

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

                var fxName = _mapper(prop);
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

            return new ObjectMarshaler
            {
                _fxType = fxType,
                _mapping = mapping
            };
        }

        internal class ObjectMarshaler : ITypeMarshaler
        {
            // Map fx field name to a function produces the formula value given the dotnet object.
            internal Dictionary<string, Func<object, FormulaValue>> _mapping;

            internal FormulaType _fxType;

            public FormulaType Type => _fxType;

            public FormulaValue Marshal(object o)
            {
                var value = new ObjectRecordValue(IRContext.NotInSource(_fxType))
                {
                    Source = o,
                    _mapping = this
                };
                return value;
            }

            // Get the value of the field. 
            // Return null on missing
            internal FormulaValue TryGetField(object source, string name)
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
}
