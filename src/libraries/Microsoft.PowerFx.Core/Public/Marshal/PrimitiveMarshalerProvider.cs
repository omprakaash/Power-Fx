// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.PowerFx.Core.Public.Types;
using Microsoft.PowerFx.Core.Public.Values;

namespace Microsoft.PowerFx
{
    public class PrimitiveMarshalerProvider : ITypeMashalerProvider
    {
        // Map from .net types to formulaTypes
        private static readonly Dictionary<Type, FormulaType> _map = new Dictionary<Type, FormulaType>()
        {
            { typeof(double), FormulaType.Number },
            { typeof(int), FormulaType.Number },
            { typeof(string), FormulaType.String }

            // $$$ add others...
        };

        public ITypeMarshaler New(Type type, TypeMarshallerCache cache, int maxDepth)
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
}
