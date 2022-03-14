// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerFx.Core;
using Microsoft.PowerFx.Core.Public;
using Microsoft.PowerFx.Core.Public.Types;
using Microsoft.PowerFx.Core.Public.Values;
using Microsoft.PowerFx.Core.Texl;
using Microsoft.PowerFx.Core.Utils;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.PowerFx.Tests
{
    // $$$ - recursive
    // $$$ set-only property ignored. 
    // $$$ Lazy ... prop that throws...
    public class Class1Tests
    {
        // Basic marshalling hook. 
        private static string Hook(PropertyInfo propInfo) => propInfo.Name.StartsWith("_") ?
            null : // skip
            propInfo.Name + "Prop";

        [Fact]
        public void Primitive()
        {
            var cache = new TypeMarshallerCache();
            var tm = cache.New(typeof(int));

            var value = tm.Marshal(5);

            Assert.Equal(5.0, ((NumberValue)value).Value);
        }

        [Fact]
        public void Test2()
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

            // $$$ Test that this doesn't actually evaluate any fields...
            var x = t.Marshal(oneObj);
            
            var engine = new RecalcEngine();
            engine.UpdateVariable("x", x);

            var result1 = engine.Eval("x.two.three.data3");
            Assert.Equal("three", ((StringValue)result1).Value);
        }

        [Fact]
        public void Test1()
        {
            // Be sure to use 'var' instead of 'object' so that we have compiler-time access to fields.           
            var fileObj = new
            {
                Filename = "foo.txt",
                Length = 12.0,            
                _Skip = "skip me!"
            };

            var cache = new TypeMarshallerCache();
            cache.Marshallers.OfType<ObjectMarshalerProvider>().First()._mapper = Hook;

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

            /*
            // Test hook. Ignore properties with "_". Append "Prop" to name. 
            Func<PropertyInfo, string> hook = (propInfo) => propInfo.Name.StartsWith("_") ?
                null : // skip
                propInfo.Name + "Prop";

            var val = ObjectRecordValue.New(fileObj, hook);

            var fileObj2 = val.Source; // unwrap

            Assert.IsTrue(Object.ReferenceEquals(fileObj, fileObj2));

            // Ensure we can pass through the engine and get out. 
            var engine = new RecalcEngine();
            engine.UpdateVariable("x", val);

            var result1 = engine.Eval("x.LengthProp");
            Assert.IsTrue(((double)result1.ToObject()) == fileObj.Length);

            var result2 = engine.Eval("x.DetailsProp.DataProp");
            Assert.AreEqual(result2.ToObject(), innerObj.Data);

            // Non trivial expression. 
            var result = engine.Eval("If(true, x, Blank())");
            var fileObj3 = ((ObjectRecordValue)result).Source;

            Assert.IsTrue(Object.ReferenceEquals(fileObj, fileObj3));

            // Ensure skipped property is not visible. 
            var check1 = engine.Check("x._SkipProp");
            Assert.IsFalse(check1.IsSuccess);
            */
        }
    }
}
