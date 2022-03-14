﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.PowerFx.Core.Public.Types;
using Microsoft.PowerFx.Core.Public.Values;
using Xunit;

namespace Microsoft.PowerFx.Tests
{
    // Test marshaling between C# objectrs and Power Fx values. 
    public class MarshalTests
    {
        // Basic marshalling hook. 
        private static string Hook(PropertyInfo propInfo) => propInfo.Name.StartsWith("_") ?
            null : // skip
            propInfo.Name + "Prop";

        // Do a trivial marshal.
        [Fact]
        public void Primitive()
        {
            var cache = new TypeMarshallerCache();
            var tm = cache.New(typeof(int));

            var value = tm.Marshal(5);

            Assert.Equal(5.0, ((NumberValue)value).Value);
        }

        [Fact]
        public void Nesting()
        {
            // Be sure to use 'var' instead of 'object' so that we have compiler-time access to fields.           
            var oneObj = new
            {
                data1 = "one",
                two = new
                {
                    data2 = "two",
                    three = new
                    {
                        data3 = "three"
                    }
                }
            };

            var cache = new TypeMarshallerCache();
            var t = cache.New(oneObj.GetType());

            var x = t.Marshal(oneObj);

            var engine = new RecalcEngine();
            engine.UpdateVariable("x", x);

            var result1 = engine.Eval("x.two.three.data3");
            Assert.Equal("three", ((StringValue)result1).Value);
        }

        private class TestNode
        {
            public int Data { get; set; }

            public TestNode Next { get; set; }
        }

        [Fact]
        public void TestRecursion()
        {
            // Be sure to use 'var' instead of 'object' so that we have compiler-time access to fields.           
            var node1 = new TestNode
            {
                Data = 10,
                Next = new TestNode
                {
                    Data = 20,
                    Next = new TestNode
                    {
                        Data = 30
                    }
                }
            };

            // create a cycle. 
            // Recursion has a default marshalling depth. 
            node1.Next.Next.Next = node1;

            var cache = new TypeMarshallerCache();
            var x = cache.Marshal(node1);

            // If we made it here, at leat we didn't hang marshalling the infinite cycle. 

            var engine = new RecalcEngine();
            engine.UpdateVariable("x", x);

            var result1 = engine.Eval("x.Data");
            Assert.Equal(10.0, ((NumberValue)result1).Value);

            var result2 = engine.Eval("x.Next.Data");
            Assert.Equal(20.0, ((NumberValue)result2).Value);

            var result3 = engine.Eval("x.Next.Next.Data");
            Assert.Equal(30.0, ((NumberValue)result3).Value);

            // $$$ test that it was Truncated...
        }

        // Marshal objects with a custom hook. 
        [Fact]
        public void CustomMarshaling()
        {
            // Be sure to use 'var' instead of 'object' so that we have compiler-time access to fields.           
            var fileObj = new
            {
                Filename = "foo.txt",
                Length = 12.0,
                _Skip = "skip me!"
            };

            var cache = new TypeMarshallerCache();
            cache.Marshallers.OfType<ObjectMarshalerProvider>().First().PropertyMapperFunc = Hook;

            var t = cache.New(fileObj.GetType());

            var x = t.Marshal(fileObj);

            var engine = new RecalcEngine();
            engine.UpdateVariable("x", x);

            // Properties are renamed. 
            var result1 = engine.Eval("x.LengthProp");
            Assert.Equal(((NumberValue)result1).Value, fileObj.Length);

            // Get object back out
            var result2 = engine.Eval("If(true, x, Blank())");
            var fileObj2 = ((ObjectRecordValue)result2).Source;
            Assert.True(ReferenceEquals(fileObj, fileObj2));

            // Ensure skipped property is not visible. 
            var check3 = engine.Check("x._SkipProp");
            Assert.False(check3.IsSuccess);
        }

        private class TestObj
        {
            internal int _counter;

            public int Field1
            {
                get
                {
                    _counter++; // for observing number of Gets
                    return _counter;
                }
            }
        }

        // Verify that marshalling doesn't eagerly evaluate fields.
        [Fact]
        public void LazyFields()
        {
            var obj1 = new TestObj();

            Assert.Equal(1, obj1.Field1);
            Assert.Equal(2, obj1.Field1);
            Assert.Equal(2, obj1._counter);

            var cache = new TypeMarshallerCache();

            var x = cache.Marshal(obj1);
            Assert.Equal(2, obj1._counter); // doesn't increment

            var engine = new RecalcEngine();
            engine.UpdateVariable("x", x);

            Assert.Equal(2, obj1._counter); // doesn't increment
            var result1 = engine.Eval("x.Field1");
            Assert.Equal(3.0, ((NumberValue)result1).Value);
        }

        // Marshal an array of records to a table. 
        [Fact]
        public void TableFromRecordArray()
        {
            var array = new TestObj[]
            {
                new TestObj { _counter = 10 },
                new TestObj { _counter = 20 }
            };

            var cache = new TypeMarshallerCache();
            var x = cache.Marshal(array);

            var engine = new RecalcEngine();
            engine.UpdateVariable("x", x);

            var result1 = engine.Eval("Last(x).Field1");
            Assert.Equal(21.0, ((NumberValue)result1).Value);

            var result2 = engine.Eval("First(x).Field1");
            Assert.Equal(11.0, ((NumberValue)result2).Value);
        }

        // Marshal a SCT from an array of primitive. 
        [Fact]
        public void SingleColumnTableFromPrimitiveArray()
        {
            var array = new int[] { 10, 20, 30 };

            var cache = new TypeMarshallerCache();
            var x = cache.Marshal(array);

            var engine = new RecalcEngine();
            engine.UpdateVariable("x", x);

            var result1 = engine.Eval("Last(x).Value");
            Assert.Equal(30.0, ((NumberValue)result1).Value);
        }

        public class Widget
        {
            public string Data { get; set; }
        }

        // Custom marshaller. Marshal Widget objects as Strings with a "W" prefix. 
        private class WidgetMarshalerProvider : ITypeMashalerProvider
        {
            public ITypeMarshaler New(Type type, TypeMarshallerCache cache, int maxDepth)
            {
                if (type != typeof(Widget))
                {
                    return null;
                }

                return new WidgetMarshaler();
            }

            private class WidgetMarshaler : ITypeMarshaler
            {
                public FormulaType Type => FormulaType.String;

                public FormulaValue Marshal(object value)
                {
                    // adding a "W" prefix ensures this code is run and it's not just some cast. 
                    var w = (Widget)value;
                    return FormulaValue.New("W" + w.Data);
                }
            }
        }

        // Test a custom marshaler. 
        [Fact]
        public void CustomMarshalerType()
        {
            var cache = new TypeMarshallerCache();

            // Insert at 0 to get precedence over generic object marshaller
            cache.Marshallers.Insert(0, new WidgetMarshalerProvider());

            var obj = new
            {
                Length = 12.0,
                Widget1 = new Widget
                {
                    Data = "A"
                },
                Widget2 = new Widget
                {
                    Data = "B"
                }
            };

            var x = cache.Marshal(obj);

            var engine = new RecalcEngine();
            engine.UpdateVariable("x", x);

            // Properties are renamed. 
            var result1 = engine.Eval("x.Widget1 & x.Widget2");
            Assert.Equal("WAWB", ((StringValue)result1).Value);
        }             
    }
}
