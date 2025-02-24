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


namespace BindingsGeneration.FunctionalTests
{
    public class StructTests : IClassFixture<StructTests.TestFixture>
    {
        private readonly TestFixture _fixture;

        public StructTests(TestFixture fixture)
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
        public void TestFrozenStructCreation()
        {
            IntPtr x = 1;
            IntPtr y = 2;

            var frozen = new FrozenStruct(x, y);
            var gotX = frozen.getX();
            var gotY = frozen.getY();

            Assert.Equal(x, gotX);
            Assert.Equal(y, gotY);
        }

        [Fact]
        public void TestNonFrozenStructCreation()
        {
            IntPtr x = 1;
            IntPtr y = 2;

            var nonFrozen = new NonFrozenStruct(x, y);
            var gotX = nonFrozen.getX();
            var gotY = nonFrozen.getY();

            Assert.Equal(x, gotX);
            Assert.Equal(y, gotY);
        }

        [Fact]
        public void TestNonFrozenStructWithNonFrozenMemberCreation()
        {
            IntPtr frozenX = 1;
            IntPtr frozenY = 2;
            IntPtr nonFrozenX = 30;
            IntPtr nonFrozenY = 40;

            var frozen = new FrozenStruct(frozenX, frozenY);
            var nonFrozen = new NonFrozenStruct(nonFrozenX, nonFrozenY);

            var complexStruct = new NonFrozenStructWithNonFrozenMember(frozen, nonFrozen);
            var gotF = complexStruct.getX();
            var gotNF = complexStruct.getY();

            Assert.Equal(frozenX, gotF.getX());
            Assert.Equal(frozenY, gotF.getY());
            Assert.Equal(nonFrozenX, gotNF.getX());
            Assert.Equal(nonFrozenY, gotNF.getY());
        }

        [Fact]
        public void TestFrozenStructWithNonFrozenMemberCreation()
        {
            IntPtr frozenX = 1;
            IntPtr frozenY = 2;
            IntPtr nonFrozenX = 30;
            IntPtr nonFrozenY = 40;

            var frozen = new FrozenStruct(frozenX, frozenY);
            var nonFrozen = new NonFrozenStruct(nonFrozenX, nonFrozenY);

            var complexStruct = new FrozenStructWithNonFrozenMember(frozen, nonFrozen);
            var gotF = complexStruct.getX();
            var gotNF = complexStruct.getY();

            Assert.Equal(frozenX, gotF.getX());
            Assert.Equal(frozenY, gotF.getY());
            Assert.Equal(nonFrozenX, gotNF.getX());
            Assert.Equal(nonFrozenY, gotNF.getY());
        }

        [Fact]
        public void TestFrozenStructWithNonFrozenMemberDeclaredWithinTheStruct()
        {
            IntPtr innerFieldValue = 123;

            var innerStruct = new FrozenStructWithNonFrozenMemberDeclaredWithinTheStruct.InnerStruct(innerFieldValue);
            var outerStruct = new FrozenStructWithNonFrozenMemberDeclaredWithinTheStruct(innerStruct);

            var gotInner = outerStruct.getInnerFieldValue();

            Assert.Equal(innerFieldValue, gotInner);
        }

        [Fact]
        public void TestInstanceMethodOnFrozenStruct()
        {
            IntPtr x = 1;
            IntPtr y = 2;

            var frozen = new FrozenStruct(x, y);

            var result = frozen.sum();

            Assert.Equal(1 + 2, result);
        }

        [Fact]
        public void TestInstanceMethodOnNonFrozenStruct()
        {
            IntPtr x = 1;
            IntPtr y = 2;

            var nonFrozen = new NonFrozenStruct(x, y);

            var result = nonFrozen.sum();

            Assert.Equal(1 + 2, result);
        }

        [Fact]
        public void TestInstanceMethodOnNonFrozenStructWithNonFrozenMember()
        {
            IntPtr frozenX = 1;
            IntPtr frozenY = 2;
            IntPtr nonFrozenX = 30;
            IntPtr nonFrozenY = 40;

            var frozen = new FrozenStruct(frozenX, frozenY);
            var nonFrozen = new NonFrozenStruct(nonFrozenX, nonFrozenY);
            var complexStruct = new NonFrozenStructWithNonFrozenMember(frozen, nonFrozen);

            var result = complexStruct.sum();

            Assert.Equal(1 + 2 + 30 + 40, result);
        }

        [Fact]
        public void TestInstanceMethodOnFrozenStructWithNonFrozenMember()
        {
            IntPtr frozenX = 1;
            IntPtr frozenY = 2;
            IntPtr nonFrozenX = 30;
            IntPtr nonFrozenY = 40;

            var frozen = new FrozenStruct(frozenX, frozenY);
            var nonFrozen = new NonFrozenStruct(nonFrozenX, nonFrozenY);
            var complexStruct = new FrozenStructWithNonFrozenMember(frozen, nonFrozen);

            var result = complexStruct.sum();

            Assert.Equal(1 + 2 + 30 + 40, result);
        }

        [Fact]
        public void TestModuleFuncWithFrozenAndNonFrozenParameters()
        {
            IntPtr frozenX = 1;
            IntPtr frozenY = 2;
            IntPtr nonFrozenX = 30;
            IntPtr nonFrozenY = 40;

            var frozen = new FrozenStruct(frozenX, frozenY);
            var nonFrozen = new NonFrozenStruct(nonFrozenX, nonFrozenY);

            var result = StructsTests.sumFrozenAndNonFrozen(frozen, nonFrozen);

            Assert.Equal(1 + 2 + 30 + 40, result);
        }

        [Fact]
        public void TestModuleFuncReturningFrozenStruct()
        {
            IntPtr x = 1;
            IntPtr y = 2;

            var result = StructsTests.createFrozenStruct(x, y);

            Assert.Equal(x, result.getX());
            Assert.Equal(y, result.getY());
        }

        [Fact]
        public void TestModuleFuncReturningNonFrozenStruct()
        {
            IntPtr x = 1;
            IntPtr y = 2;

            var result = StructsTests.createNonFrozenStruct(x, y);

            Assert.Equal(x, result.getX());
            Assert.Equal(y, result.getY());
        }

        [Fact]
        public void TestInstanceMethodReturningFrozenStruct()
        {
            IntPtr x = 1;
            IntPtr y = 2;
            var structBuilder = new StructBuilder(x, y);

            var result = structBuilder.createFrozenStruct();

            Assert.Equal(x, result.getX());
            Assert.Equal(y, result.getY());
        }

        [Fact]
        public void TestInstanceMethodReturningNonFrozenStruct()
        {
            IntPtr x = 1;
            IntPtr y = 2;
            var structBuilder = new StructBuilder(x, y);

            var result = structBuilder.createNonFrozenStruct();

            Assert.Equal(x, result.getX());
            Assert.Equal(y, result.getY());
        }

        [Fact]
        public void TestStaticMethodReturningFrozenStruct()
        {
            IntPtr x = 1;
            IntPtr y = 2;

            var result = StructBuilder.createFrozenStruct(x, y);

            Assert.Equal(x, result.getX());
            Assert.Equal(y, result.getY());
        }

        [Fact]
        public void TestStaticMethodReturningNonFrozenStruct()
        {
            IntPtr x = 1;
            IntPtr y = 2;

            var result = StructBuilder.createNonFrozenStruct(x, y);

            Assert.Equal(x, result.getX());
            Assert.Equal(y, result.getY());
        }

        [Fact]
        public void TestInitMethodThrowingError()
        {
            Assert.Throws<SwiftRuntimeException>(() => new StructWithThrowingInit(0, 0));
        }

        [Fact]
        public void TestInstanceMethodThrowingError()
        {
            var structWithThrowingMethods = new StructWithThrowingMethods(0, 0);
            Assert.Throws<SwiftRuntimeException>(() => structWithThrowingMethods.sum());
        }

        [Fact]
        public void TestStaticMethodThrowingError()
        {
            Assert.Throws<SwiftRuntimeException>(() => StructWithThrowingMethods.sum(0, 0));
        }

        [Fact]
        public async Task TestAsyncStruct()
        {
            int expectedValue = 42;
            ulong seconds = 5;
            TimerStruct timerStruct = new TimerStruct(expectedValue);
            var tasks = new[]
            {
                timerStruct.waitFor(seconds - 1),
                timerStruct.waitFor(seconds - 2),
                timerStruct.waitFor(seconds - 3),
                timerStruct.waitFor(seconds - 4),
                timerStruct.waitFor(seconds - 5)
            };
            var stopwatch = Stopwatch.StartNew();
            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            foreach (var result in results)
                Assert.Equal(expectedValue, result);

            Assert.True(Math.Abs(stopwatch.Elapsed.TotalSeconds - seconds) <= 1);

            var tasks2 = new[]
            {
                timerStruct.waitFor5Seconds(),
                TimerStruct.waitFor5SecondsStatic()
            };

            stopwatch = Stopwatch.StartNew();
            await Task.WhenAll(tasks2);
            stopwatch.Stop();
            Assert.True(Math.Abs(stopwatch.Elapsed.TotalSeconds - seconds) <= 1);
        }

        [Fact]
        public void TestFrozenStructProperties()
        {
            var struct1 = new PropertiesTestStruct(letValue: 10, varValue: 20, multiplier: 3);

            Assert.Equal(10, struct1.letProperty);

            Assert.Equal(20, struct1.varProperty);

            Assert.Equal(30, struct1.computedProperty);
        }

        [Fact]
        public void TestNonFrozenStructProperties()
        {
            var struct1 = new NonFrozenPropertiesTestStruct(letValue: 10, varValue: 20, multiplier: 3);

            Assert.Equal(10, struct1.letProperty);

            Assert.Equal(20, struct1.varProperty);

            Assert.Equal(30, struct1.computedProperty);
        }
    }
}
