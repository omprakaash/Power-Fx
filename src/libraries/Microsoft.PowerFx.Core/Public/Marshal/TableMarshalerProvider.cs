﻿// Copyright (c) Microsoft Corporation.
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
    public class TableMarshalerProvider : ITypeMashalerProvider
    {
        public ITypeMarshaler New(Type type, TypeMarshallerCache cache, int maxDepth)
        {
            if (!type.IsArray)
            {
                return null;
            }

            var et = type.GetElementType();

            var tm = cache.New(et, maxDepth);
                        
            if (tm.Type is RecordType recordType)
            {
                // Array of records 
            }       
            else
            {
                // Single Column table. Wrap the scalar in a record. 
                recordType = new RecordType().Add("Value", tm.Type);

                tm = new SCTMarshaler
                {
                    _inner = tm,
                    Type = recordType
                };
            }

            var tableType = recordType.ToTable();

            return new TableMarshaler 
            { 
                Type = tableType,
                _rowMarshaler = tm
            };            
        }

        // Convert a single value into a Record for a SCT,  { value : x }
        internal class SCTMarshaler : ITypeMarshaler
        {
            public FormulaType Type { get; set; }

            public ITypeMarshaler _inner;

            public FormulaValue Marshal(object value)
            {
                var scalar = _inner.Marshal(value);
                var defaultField = new NamedValue("Value", scalar);                                

                var record = FormulaValue.RecordFromFields(defaultField);
                return record;
            }
        }

        internal class TableMarshaler : ITypeMarshaler
        {
            public FormulaType Type { get; set; }

            public ITypeMarshaler _rowMarshaler;

            public FormulaValue Marshal(object value)
            {
                var array = (Array)value;

                var ir = IRContext.NotInSource(Type);

                return new LazyTableValue(ir, array, _rowMarshaler);
            }
        }
    }

    // $$$ Merge with Index() function to do this lazily...
    internal class LazyTableValue : TableValue
    {
        private readonly IEnumerable<DValue<RecordValue>> _rows;

        public override IEnumerable<DValue<RecordValue>> Rows => _rows;

        private readonly Array _source;

        private readonly ITypeMarshaler _rowMarshaler;

        internal LazyTableValue(IRContext irContext, Array source, ITypeMarshaler rowMarshaler) 
            : base(irContext)
        {
            _source = source;
            _rowMarshaler = rowMarshaler;

            var rows = new List<DValue<RecordValue>>();
            foreach (var item in source)
            {
                var i2 = (RecordValue)_rowMarshaler.Marshal(item);
                rows.Add(DValue<RecordValue>.Of(i2));
            }

            _rows = rows;
        }
    }
}
