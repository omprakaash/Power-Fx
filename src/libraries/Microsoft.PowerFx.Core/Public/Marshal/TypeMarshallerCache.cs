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
    /// Marshal .net objects into Power Fx values. 
    /// </summary>
    public class TypeMarshallerCache
    {
        // Map from a .net type to the marshaller for that type
        private Dictionary<Type, ITypeMarshaler> _cache = new Dictionary<Type, ITypeMarshaler>();

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

        private TypeMarshallerCache _outer;
        private int _depth;

        /// <summary>
        /// Returns a marshaller for the given type. 
        /// </summary>
        /// <param name="type">type to marshal.</param>
        /// <returns>marshaller.</returns>
        public ITypeMarshaler New(Type type)
        {
            if (_outer == null)
            {
                var inner = new TypeMarshallerCache
                {
                    _cache = _cache,
                    Marshallers = Marshallers,
                    _outer = this,
                    _depth = 3
                };
                return inner.New(type);
            } 
            else
            {
                _depth--;
                if (_depth < 0)
                {
                    return new Empty();
                }
            }

            // The ccahe requires an exact type match and doesn't handle base types.
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

        public FormulaValue Marshal(object value)
        {
            if (value == null)
            {
                return FormulaValue.NewBlank();
            }

            var tm = New(value.GetType());
            return tm.Marshal(value);
        }

        private class Empty : ITypeMarshaler
        {
            public FormulaType Type => FormulaType.Blank;

            public FormulaValue Marshal(object value)
            {
                return RecordValue.Empty();
            }
        }
    }
}
