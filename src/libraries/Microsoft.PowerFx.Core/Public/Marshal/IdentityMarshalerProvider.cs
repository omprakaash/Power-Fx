// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using Microsoft.PowerFx.Core.Public.Types;
using Microsoft.PowerFx.Core.Public.Values;

namespace Microsoft.PowerFx
{
    /// <summary>
    /// Marshal dotnet types that already are FormulaValues. 
    /// </summary>
    public class IdentityMarshalerProvider : ITypeMashalerProvider
    {
        public ITypeMarshaler New(Type type, TypeMarshallerCache cache, int maxDepth)
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
}
