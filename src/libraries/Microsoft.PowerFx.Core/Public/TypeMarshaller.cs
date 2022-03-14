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
    public interface ITypeMashalerProvider
    {
        // Return null if it doesn't handle it. 
        // A single ITypeMarshaler can be created once per type and then reused for each instance.
        // Pass in a cache for aggregate types that need to marshal sub types. 
        public ITypeMarshaler New(Type type, TypeMarshallerCache cache);
    }

    public interface ITypeMarshaler
    {
        public FormulaType Type { get; }

        // Throws on error. 
        // The provider should have ensured that this marshaller can handle the type. 
        public FormulaValue Marshal(object value);
    }

    public class TypeMarshallerCache
    {
        // Map from a .net type to the marshaller for that type
        private readonly Dictionary<Type, ITypeMarshaler> _cache = new Dictionary<Type, ITypeMarshaler>();

        /// <summary>
        /// Ordered list of type marshallers. First marshaller to handle is used. 
        /// </summary>
        public List<ITypeMashalerProvider> Marshallers { get; set; } = new List<ITypeMashalerProvider>
        { 
            // JsonElement? 
            // new IdentityMarshalerProvider(),
            new PrimitiveMarshalerProvider(),
            new ObjectMarshalerProvider()
        };

        public ITypeMarshaler New(Type type)
        {
            if (_cache.TryGetValue(type, out var tm))
            {
                return tm;
            }

            foreach (var marshaller in Marshallers)
            {
                tm = marshaller.New(type, this);
                if (tm != null)
                {
                    _cache[type] = tm;

                    return tm;
                }
            }

            // Failed to marshal!
            throw new InvalidOperationException($"Can't marshal {type.FullName}");
        }
    }

    // Handle FormulaValue
    public class IdentityMarshalerProvider : ITypeMashalerProvider
    {
        public ITypeMarshaler New(Type type, TypeMarshallerCache cache)
        {
            if (typeof(FormulaValue).IsAssignableFrom(type))
            {
                return new IdentityMarshaler
                {
                    Type = null // $$$ Fix this 
                };
            }

            return null;
        }

        public class IdentityMarshaler : ITypeMarshaler
        {
            public FormulaType Type { get; set; }

            public FormulaValue Marshal(object value)
            {
                return (FormulaValue)value;
            }
        }
    }

    public class PrimitiveMarshalerProvider : ITypeMashalerProvider
    {
        // Map from .net types to formulaTypes
        private static readonly Dictionary<Type, FormulaType> _map = new Dictionary<Type, FormulaType>()
        {
            { typeof(double), FormulaType.Number },
            { typeof(int), FormulaType.Number },
            { typeof(string), FormulaType.String }
        };

        public ITypeMarshaler New(Type type, TypeMarshallerCache cache)
        {
            if (_map.TryGetValue(type, out var fxType))
            {
                return new PrimitiveTypeMarshaler(fxType);
            }

            // Not supported
            return null;
        }

        private class PrimitiveTypeMarshaler : ITypeMarshaler
        {
            public FormulaType Type { get; }

            public PrimitiveTypeMarshaler(FormulaType fxType)
            {
                Type = fxType;
            }

            // $$$ FormulaNew should use this..
            public FormulaValue Marshal(object value)
            {
                if (Type == FormulaType.Number)
                {
                    if (value is int i)
                    {
                        return FormulaValue.New(i);
                    }
                    else
                    {
                        return FormulaValue.New((double)value);
                    }
                }

                if (Type == FormulaType.String)
                {
                    return FormulaValue.New((string)value);
                }

                throw new InvalidOperationException($"Unsupported type {value.GetType().FullName}");
            }
        }
    }

    public class ObjectMarshalerProvider : ITypeMashalerProvider
    {
        public Func<PropertyInfo, string> _mapper = (propInfo) => propInfo.Name;

        public ITypeMarshaler New(Type type, TypeMarshallerCache cache)
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

                var tm = cache.New(prop.PropertyType);
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

        private class ObjectMarshaler : ITypeMarshaler
        {
            internal Dictionary<string, Func<object, FormulaValue>> _mapping;

            internal FormulaType _fxType;

            public FormulaType Type => _fxType;

            public FormulaValue Marshal(object o)
            {
                var value = new ObjectRecordValue(IRContext.NotInSource(_fxType))
                {
                    Source = o,
                    _mapping = _mapping
                };
                return value;
            }         
        }
    }
}
