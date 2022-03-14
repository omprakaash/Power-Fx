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

namespace Microsoft.PowerFx.Core.Public.Values
{    
    // These can be cached....
    public class TypeMarshaller
    {
        // Map fieldName --> function that takes runtime object and returns the value. 
        private Dictionary<string, Func<object, FormulaValue>> _mapping;

        private FormulaType _fxType;

        public static TypeMarshaller New(Type type, Func<PropertyInfo, string> mapper)
        {
            var mapping = new Dictionary<string, Func<object, FormulaValue>>(); // $$$ Casing?

            var fxType = new RecordType();                        

            // $$$ What if we want properties that aren't in .net? (do we care)
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead)
                {
                    continue;
                }

                var fxName = mapper(prop);
                if (fxName == null)
                {
                    continue;
                }
                              
                // $$$ Avoid switch case here...
                FormulaType fxFieldType = null;
                Func<object, FormulaValue> getter = null;
                if (prop.PropertyType == typeof(string))
                {
                    fxFieldType = FormulaType.String;
                    getter = (objSource) => FormulaValue.New((string)objSource);
                } 
                else if (prop.PropertyType == typeof(double))
                {
                    fxFieldType = FormulaType.Number;
                    getter = (objSource) => FormulaValue.New((double)objSource);
                }
                else if (prop.PropertyType.IsClass)
                {
                    // Recursive!
                    var tm2 = New(prop.PropertyType, mapper);
                    fxFieldType = tm2._fxType;
                    getter = (objSource) => tm2.Marshal(objSource);
                } 
                else
                {
                }

                // Basic .net property
                mapping[fxName] = (object objSource) =>
                {
                    var propValue = prop.GetValue(objSource);
                    return getter(propValue);
                };

                fxType = fxType.Add(fxName, fxFieldType);
            }

            return new TypeMarshaller
            {
                _fxType = fxType,
                _mapping = mapping
            };
        }

        public ObjectRecordValue Marshal(object o)
        {
            var value = new ObjectRecordValue(IRContext.NotInSource(_fxType))
            {
                Source = o,
                _mapping = _mapping
            };
            return value;
        }
    }

    /// <summary>
    /// The backing implementation for UntypedObjectValue, for example Json, Xml,
    /// or the Ast or Value system from another language.
    /// </summary>
    public interface IUntypedObject
    {
        /// <summary>
        /// Use ExternalType if the type is incompatible with PowerFx.
        /// </summary>
        FormulaType Type { get; }

        int GetArrayLength();

        /// <summary>
        /// 0-based index.
        /// </summary>
        IUntypedObject this[int index] { get; }

        bool TryGetProperty(string value, out IUntypedObject result);

        string GetString();

        double GetDouble();

        bool GetBoolean();
    }

    [DebuggerDisplay("UntypedObjectValue({Impl})")]
    public class UntypedObjectValue : ValidFormulaValue
    {
        public IUntypedObject Impl { get; }

        internal UntypedObjectValue(IRContext irContext, IUntypedObject impl)
            : base(irContext)
        {
            Contract.Assert(IRContext.ResultType == FormulaType.UntypedObject);
            Impl = impl;
        }

        public override object ToObject()
        {
            throw new NotImplementedException();
        }

        public override void Visit(IValueVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
