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
        public void TestSwiftArray()
        {
            var array = GetArray(42, 17);
            Assert.Equal(2, array.Count);
            Assert.Equal(42, array[0]);
            Assert.Equal(17, array[1]);
            int sum = SumArray(array);
            Assert.Equal(42 + 17, sum);
        }

        // TODO: Remove helper methods when https://github.com/dotnet/runtimelab/issues/2970
        private static unsafe SwiftArray<int> GetArray(int a, int b)
        {
            ArrayBuffer buffer = PInvoke_GetArray(a, b);
            return SwiftMarshal.MarshalFromSwift<SwiftArray<int>>((SwiftHandle)new IntPtr(&buffer));
        }

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Structs/libStructsTests.dylib", EntryPoint = "$s12StructsTests8getArray1a1bSays5Int32VGAF_AFtF")]
        private static extern ArrayBuffer PInvoke_GetArray(int a, int b);

        private static unsafe int SumArray(SwiftArray<int> array)
        {
            ArrayBuffer buffer = new ArrayBuffer();
            SwiftMarshal.MarshalToSwift<SwiftArray<int>>(array, new IntPtr(&buffer));
            return PInvoke_SumArray(buffer);
        }

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Structs/libStructsTests.dylib", EntryPoint = "$s12StructsTests8sumArray5arrays5Int32VSayAEG_tF")]
        private static extern int PInvoke_SumArray(ArrayBuffer array);

        [Fact]
        public void TestSwiftSet()
        {
            var set = GetSet(42, 17);
            Assert.Equal(2, set.Count);
            int sum = SumSet(set);
            Assert.Equal(42 + 17, sum);

            set = new SwiftSet<SwiftIntMock>();
            sum = SumSet(set);
            Assert.Equal(0, sum);
        }

        // TODO: Remove helper methods when https://github.com/dotnet/runtimelab/issues/2970
        private static unsafe SwiftSet<SwiftIntMock> GetSet(int a, int b)
        {
            Variant variant = PInvoke_GetSet(a, b);
            return SwiftMarshal.MarshalFromSwift<SwiftSet<SwiftIntMock>>((SwiftHandle)new IntPtr(&variant));
        }

        private static unsafe int SumSet(SwiftSet<SwiftIntMock> set)
        {
            Variant variant = new Variant();
            SwiftMarshal.MarshalToSwift<SwiftSet<SwiftIntMock>>(set, new IntPtr(&variant));
            return PInvoke_SumSet(variant);
        }

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Structs/libStructsTests.dylib", EntryPoint = "$s12StructsTests8getArray1a1bSays5Int32VGAF_AFtF")]
        private static extern Variant PInvoke_GetSet(int a, int b);

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Structs/libStructsTests.dylib", EntryPoint = "$s12StructsTests8sumArray5arrays5Int32VSayAEG_tF")]
        private static extern int PInvoke_SumSet(Variant array);

        [Fact]
        public void TestFrozenStructProperties()
        {
            var struct1 = new PropertiesTestStruct(letValue: 10, varValue: 20.5, multiplier: 3.0f);

            Assert.Equal(10, struct1.letProperty);
            Assert.Equal(20.5, struct1.varProperty);
            Assert.Equal(30.0, struct1.computedProperty);
        }

        [Fact]
        public void TestNonFrozenStructProperties()
        {
            var struct1 = new NonFrozenPropertiesTestStruct(letValue: 10, varValue: 20.5, multiplier: 3.0f);

            Assert.Equal(10, struct1.letProperty);
            Assert.Equal(20.5, struct1.varProperty);
            Assert.Equal(30.0, struct1.computedProperty);
        }
    }
}
