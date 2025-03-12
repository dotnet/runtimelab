// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using BindingsGeneration.Tests;
using Swift;
using Swift.Runtime;
using Swift.Runtime.InteropServices;
using Swift.StructsTests;
using Xunit;

using Bindings = Swift.AsyncTests;


namespace BindingsGeneration.FunctionalTests
{
    public class AsyncTests : IClassFixture<AsyncTests.TestFixture>
    {
        private readonly TestFixture _fixture;

        public AsyncTests(TestFixture fixture)
        {
            _fixture = fixture;
        }

        public class TestFixture
        {
            static TestFixture()
            {
                InitializeResources();
            }

            private static void InitializeResources()
            {
                // Initialize
            }
        }

        [Fact]
        public async Task TestInstanceMethods()
        {
            var myStruct = new Bindings.AsyncStruct(42);

            var stopwatch = Stopwatch.StartNew();
            await myStruct.AsyncVoid();
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);

            stopwatch.Restart();
            ulong seconds = 2;
            ulong result = await myStruct.AsyncNonVoid(seconds);
            stopwatch.Stop();
            Assert.Equal(seconds, result);
            Assert.True(stopwatch.Elapsed.TotalSeconds >= seconds);
        }

        [Fact]
        public async Task TestStaticMethods()
        {
            var stopwatch = Stopwatch.StartNew();
            await Bindings.AsyncStruct.AsyncVoidStatic();
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);

            stopwatch.Restart();
            ulong seconds = 2;
            ulong result = await Bindings.AsyncStruct.AsyncNonVoidStatic(seconds);
            stopwatch.Stop();
            Assert.Equal(seconds, result);
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);
        }

        [Fact]
        public async Task TestGenericUnconstrained()
        {
            var myStruct = new Bindings.AsyncStruct(42);

            var stopwatch = Stopwatch.StartNew();
            await myStruct.GenericUnconstrained(123);
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);

            stopwatch.Restart();
            await Bindings.AsyncStruct.GenericUnconstrainedStatic(123);
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);
        }

        [Fact]
        public async Task TestGenericCollectionConstraint()
        {
            var myStruct = new Bindings.AsyncStruct(0);
            var strings = new SwiftArray<SwiftString>();
            strings.Append(new SwiftString("one"));
            strings.Append(new SwiftString("two"));
            strings.Append(new SwiftString("three"));

            var stopwatch = Stopwatch.StartNew();
            nint countInstance = await myStruct.GenericCollectionConstraint(strings);
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);
            Assert.Equal(3, countInstance);

            stopwatch.Restart();
            nint countStatic = await Bindings.AsyncStruct.GenericCollectionConstraintStatic(strings);
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);
            Assert.Equal(3, countStatic);

            strings.Append(new SwiftString("error"));

            stopwatch.Restart();
            countInstance = await myStruct.GenericCollectionConstraint(strings);
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds < 1);
            Assert.Equal(-1, countInstance);

            stopwatch.Restart();
            countStatic = await Bindings.AsyncStruct.GenericCollectionConstraintStatic(strings);
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds < 1);
            Assert.Equal(-1, countStatic);
        }

        [Fact]
        public async Task TestArray()
        {
            var myStruct = new Bindings.AsyncStruct(0);
            var strings = new SwiftArray<SwiftString>();
            strings.Append(new SwiftString("one"));
            strings.Append(new SwiftString("two"));
            strings.Append(new SwiftString("three"));

            var stopwatch = Stopwatch.StartNew();
            var stringsInstance = await myStruct.ArrayPassThrough(strings);
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);
            Assert.Equal(3, stringsInstance.Count);
            Assert.Equal(strings[0].ToString(), stringsInstance[0].ToString());
            Assert.Equal(strings[1].ToString(), stringsInstance[1].ToString());
            Assert.Equal(strings[2].ToString(), stringsInstance[2].ToString());


            strings.Append(new SwiftString("four"));
            stringsInstance = await myStruct.ArrayPassThrough(strings);
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);
            Assert.Equal(4, stringsInstance.Count);
            Assert.Equal(strings[0].ToString(), stringsInstance[0].ToString());
            Assert.Equal(strings[1].ToString(), stringsInstance[1].ToString());
            Assert.Equal(strings[2].ToString(), stringsInstance[2].ToString());
            Assert.Equal(strings[3].ToString(), stringsInstance[3].ToString());
        }

        [Fact]
        public async Task TestString()
        {
            var myStruct = new Bindings.AsyncStruct(0);
            var str = new SwiftString("one");

            var stopwatch = Stopwatch.StartNew();
            var stringInstance = await myStruct.StringPassThrough(str);
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 1);
            Assert.Equal(str.Length, stringInstance.Length);
            Assert.Equal(str.ToString(), stringInstance.ToString());
        }
    }
}
