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
        private Dictionary<string, PropertyInfo> _mapping;

        private FormulaType _fxType;

        public static TypeMarshaller New(Type type, Func<PropertyInfo, string> mapper)
        {
            var mapping = new Dictionary<string, PropertyInfo>(); // $$$ Casing?

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

                mapping[fxName] = prop;
                FormulaType fxFieldType = null;
                if (prop.PropertyType == typeof(string))
                {
                    fxFieldType = FormulaType.String;
                } 
                else if (prop.PropertyType == typeof(double))
                {
                    fxFieldType = FormulaType.Number;
                }
                else
                {
                }

                fxType = fxType.Add(fxName, fxFieldType);
            }

            return new TypeMarshaller
            {
                _fxType = fxType,
                _mapping = mapping
            };
        }

        public RecordValue Marshal(object o)
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
