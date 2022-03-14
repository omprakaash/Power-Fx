// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using Microsoft.PowerFx.Core.Public.Types;
using Microsoft.PowerFx.Core.Public.Values;

namespace Microsoft.PowerFx
{
    /// <summary>
    /// Handles marshaling a given type. Invoked by the <see cref="TypeMarshallerCache"/>.
    /// </summary>
    public interface ITypeMashalerProvider
    {
        // Return null if it doesn't handle it. 
        // A single ITypeMarshaler can be created once per type and then reused for each instance.
        // Pass in a cache for aggregate types that need to marshal sub types. 
        public ITypeMarshaler New(Type type, TypeMarshallerCache cache, int maxDepth);
    }

    /// <summary>
    /// A mrashaller for a given System.Type. 
    /// </summary>
    public interface ITypeMarshaler
    {
        public FormulaType Type { get; }

        // Throws on error. 
        // The provider should have ensured that this marshaller can handle the type. 
        public FormulaValue Marshal(object value);
    }
}
