// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Reflection;
using Microsoft.PowerFx.Core.IR;
using Microsoft.PowerFx.Core.Public.Types;
using static Microsoft.PowerFx.ObjectMarshalerProvider;

namespace Microsoft.PowerFx.Core.Public.Values
{
    // $$$ Move to interpreter?
    public class ObjectRecordValue : RecordValue
    {
        public object Source { get; set; }

        internal ObjectMarshaler _mapping;

        internal ObjectRecordValue(IRContext irContext) 
            : base(irContext)
        {
        }

        public override IEnumerable<NamedValue> Fields => throw new NotImplementedException();

        internal override FormulaValue GetField(IRContext irContext, string name)
        {
            var value = _mapping.TryGetField(Source, name);
            if (value != null)
            {
                return value;
            }
            else
            {
                // Missing field. Should be compiler time error...
                return new ErrorValue(irContext);
            }
        }
    }
}
