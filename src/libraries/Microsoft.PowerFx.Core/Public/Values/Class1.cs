// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Reflection;
using Microsoft.PowerFx.Core.IR;
using Microsoft.PowerFx.Core.Public.Types;

namespace Microsoft.PowerFx.Core.Public.Values
{
    public class ObjectRecordValue : RecordValue
    {
        public object Source { get; set; }

        // FxField name back to .net property ($$$ or field, or arbitrary getter?)
        internal Dictionary<string, PropertyInfo> _mapping;

        internal ObjectRecordValue(IRContext irContext) 
            : base(irContext)
        {
        }

        public override IEnumerable<NamedValue> Fields => throw new NotImplementedException();

        internal override FormulaValue GetField(IRContext irContext, string name)
        {
            if (_mapping.TryGetValue(name, out var propInfo))
            {
                var value = propInfo.GetValue(Source);

                // Marshal back to Fx...
                // $$$ Ensure types match
                // $$$ Recursive?
                var expectedType = ((RecordType)Type).GetFieldType(name);

                return FormulaValue.New(value, propInfo.PropertyType);
            }
            else
            {
                // Missing field. Should error...
                return new ErrorValue(irContext);
            }
        }
    }
}
