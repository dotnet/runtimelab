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
using Xunit;

using Bindings = Swift.RuntimeTests;


namespace BindingsGeneration.FunctionalTests
{
    public class RuntimeTests : IClassFixture<RuntimeTests.TestFixture>
    {
        private readonly TestFixture _fixture;

        public RuntimeTests(TestFixture fixture)
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
        public void SmokeTestArray()
        {
            var array = GetArray(3);
            Assert.Equal(3, array.Count);
            Assert.Equal(0, array[0]);
            Assert.Equal(1, array[1]);
            Assert.Equal(2, array[2]);
            Assert.Equal(3, SumArray(array));
        }

        [Fact]
        public void TestEmptyArray()
        {
            var array = GetArray(0);
            Assert.Equal(0, array.Count);
            Assert.Equal(0, SumArray(array));
        }

        [Fact]
        public void TestOneElementArray()
        {
            var array = GetArray(1);
            Assert.Equal(1, array.Count);
            Assert.Equal(0, array[0]);
            Assert.Equal(0, SumArray(array));
        }

        [Fact]
        public void TestBigArray()
        {
            var array = GetArray(10000);
            Assert.Equal(10000, array.Count);
            var sum = 0;
            for (int i = 0; i < 10000; i++)
            {
                Assert.Equal(i, array[i]);
                sum += i;
            }
            Assert.Equal(sum, SumArray(array));
        }

        // TODO: Remove helper methods when https://github.com/dotnet/runtimelab/issues/2970
        private static unsafe SwiftArray<int> GetArray(int count)
        {
            ArrayBuffer buffer = PInvoke_GetArray(count);
            return SwiftMarshal.MarshalFromSwift<SwiftArray<int>>((SwiftHandle)new IntPtr(&buffer));
        }

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests8getArray5countSays5Int32VGAE_tF")]
        private static extern ArrayBuffer PInvoke_GetArray(int count);

        private static unsafe int SumArray(SwiftArray<int> array)
        {
            ArrayBuffer buffer = new ArrayBuffer();
            SwiftMarshal.MarshalToSwift<SwiftArray<int>>(array, new IntPtr(&buffer));
            return PInvoke_SumArray(buffer);
        }

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests8sumArray5arrays5Int32VSayAEG_tF")]
        private static extern int PInvoke_SumArray(ArrayBuffer array);

        [Fact]
        public void SmokeTestSet()
        {
            var set = GetSet(3);
            Assert.Equal(3, set.Count);
            Assert.Equal(3, SumSet(set));
        }

        [Fact]
        public void TestEmptySet()
        {
            var set = GetSet(0);
            Assert.Equal(0, set.Count);
            Assert.Equal(0, SumSet(set));
        }

        [Fact]
        public void TestOneElementSet()
        {
            var set = GetSet(1);
            Assert.Equal(1, set.Count);
            Assert.Equal(0, SumSet(set));
        }

        [Fact]
        public void TestBigSet()
        {
            var set = GetSet(10000);
            Assert.Equal(10000, set.Count);
            Assert.Equal(49995000, SumSet(set));

        }

        // TODO: Remove helper methods when https://github.com/dotnet/runtimelab/issues/2970
        private static unsafe SwiftSet<SwiftIntMock> GetSet(int count)
        {
            Variant variant = PInvoke_GetSet(count);
            return SwiftMarshal.MarshalFromSwift<SwiftSet<SwiftIntMock>>((SwiftHandle)new IntPtr(&variant));
        }

        private static unsafe int SumSet(SwiftSet<SwiftIntMock> set)
        {
            Variant variant = new Variant();
            SwiftMarshal.MarshalToSwift<SwiftSet<SwiftIntMock>>(set, new IntPtr(&variant));
            return PInvoke_SumSet(variant);
        }

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests8getArray5countSays5Int32VGAE_tF")]
        private static extern Variant PInvoke_GetSet(int count);

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests8sumArray5arrays5Int32VSayAEG_tF")]
        private static extern int PInvoke_SumSet(Variant set);

        [Fact]
        public void SmokeTestString()
        {
            var str = Bindings.RuntimeTests.getString(3);
            Assert.Equal(3, str.Length);
            Assert.Equal("aaa", str.ToString());
        }

        [Fact]
        public void TestEmptyString()
        {
            var str = Bindings.RuntimeTests.getString(0);
            Assert.Equal(0, str.Length);
            Assert.Equal(string.Empty, str.ToString());
            Assert.Equal(str.Length, Bindings.RuntimeTests.verifyString(str));
        }

        [Fact]
        public void TestOneElementString()
        {
            var str = Bindings.RuntimeTests.getString(1);
            Assert.Equal(1, str.Length);
            Assert.Equal("a", str.ToString());
            Assert.Equal(str.Length, Bindings.RuntimeTests.verifyString(str));
        }

        [Fact]
        public void TestBigString()
        {
            var str = Bindings.RuntimeTests.getString(10000);
            Assert.Equal(10000, str.Length);
            Assert.Equal(new string('a', 10000), str.ToString());
            Assert.Equal(str.Length, Bindings.RuntimeTests.verifyString(str));
        }
    }
}
