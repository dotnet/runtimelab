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
            var array = Bindings.RuntimeTests.getArray(3);
            Assert.Equal(3, array.Count);
            Assert.Equal(0, array[0]);
            Assert.Equal(1, array[1]);
            Assert.Equal(2, array[2]);
            Assert.Equal(3, Bindings.RuntimeTests.sumArray(array));
        }

        [Fact]
        public void TestEmptyArray()
        {
            var array = Bindings.RuntimeTests.getArray(0);
            Assert.Equal(0, array.Count);
            Assert.Equal(0, Bindings.RuntimeTests.sumArray(array));
        }

        [Fact]
        public void TestOneElementArray()
        {
            var array = Bindings.RuntimeTests.getArray(1);
            Assert.Equal(1, array.Count);
            Assert.Equal(0, array[0]);
            Assert.Equal(0, Bindings.RuntimeTests.sumArray(array));
        }

        [Fact]
        public void TestBigArray()
        {
            var array = Bindings.RuntimeTests.getArray(10000);
            Assert.Equal(10000, array.Count);
            var sum = 0;
            for (int i = 0; i < 10000; i++)
            {
                Assert.Equal(i, array[i]);
                sum += i;
            }
            Assert.Equal(sum, Bindings.RuntimeTests.sumArray(array));
        }

        [Fact]
        public unsafe void TestArrayPassThrough()
        {
            var array = Bindings.RuntimeTests.getArray(3);
            Assert.Equal(3, array.Count);
            Assert.Equal(0, array[0]);
            Assert.Equal(1, array[1]);
            Assert.Equal(2, array[2]);
            Assert.Equal(3, Bindings.RuntimeTests.sumArray(array));

            // Check the initial count
            var bufferPayload = array.Payload;
            IntPtr payload = new IntPtr(&bufferPayload);
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));

            var arrayCopy = Bindings.RuntimeTests.passThroughArray(array);
            Assert.Equal(3, arrayCopy.Count);
            Assert.Equal(0, arrayCopy[0]);
            Assert.Equal(1, arrayCopy[1]);
            Assert.Equal(2, arrayCopy[2]);
            Assert.Equal(3, Bindings.RuntimeTests.sumArray(arrayCopy));

            // Check the references are not the same
            var bufferPayloadCopy = arrayCopy.Payload;
            IntPtr payloadCopy = new IntPtr(&bufferPayloadCopy);
            Assert.NotEqual(payload, payloadCopy);

            // Check the payloads are the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));

            // Check the count after the copy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));

            arrayCopy.Dispose();
            // Check the count after the copy is disposed
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(0)));
        }

        [Fact]
        public unsafe void TestArrayPassThroughDifferentPayloads()
        {
            var array = Bindings.RuntimeTests.getArray(3);
            Assert.Equal(3, array.Count);
            Assert.Equal(0, array[0]);
            Assert.Equal(1, array[1]);
            Assert.Equal(2, array[2]);
            Assert.Equal(3, Bindings.RuntimeTests.sumArray(array));

            // Check the initial count
            var bufferPayload = array.Payload;
            IntPtr payload = new IntPtr(&bufferPayload);
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));

            var arrayCopy = Bindings.RuntimeTests.passThroughArray(array);
            Assert.Equal(3, arrayCopy.Count);
            Assert.Equal(0, arrayCopy[0]);
            Assert.Equal(1, arrayCopy[1]);
            Assert.Equal(2, arrayCopy[2]);
            Assert.Equal(3, Bindings.RuntimeTests.sumArray(arrayCopy));

            // Check the count after the copy
            var bufferPayloadCopy = arrayCopy.Payload;
            IntPtr payloadCopy = new IntPtr(&bufferPayloadCopy);

            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));

            // Check the payloads are the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));

            array[0] = 9;
            array[1] = 8;
            array[2] = 7;

            // Check the count after the change
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(0)));

            var arrayCopyCopy = Bindings.RuntimeTests.passThroughArray(arrayCopy);
            Assert.Equal(arrayCopy.Count, arrayCopyCopy.Count);
            Assert.Equal(arrayCopy[0], arrayCopyCopy[0]);
            Assert.Equal(arrayCopy[1], arrayCopyCopy[1]);
            Assert.Equal(arrayCopy[2], arrayCopyCopy[2]);

            // Check the count after the copy
            Assert.Equal(1, Arc.RetainCount(array.Payload));
            Assert.Equal(2, Arc.RetainCount(arrayCopy.Payload));
            Assert.Equal(2, Arc.RetainCount(arrayCopyCopy.Payload));

            arrayCopy.Dispose();

            // Check the count after the copy is disposed
            Assert.Equal(1, Arc.RetainCount(array.Payload));
            Assert.Equal(1, Arc.RetainCount(arrayCopy.Payload));
            Assert.Equal(1, Arc.RetainCount(arrayCopyCopy.Payload));
        }

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

        [Fact]
        public unsafe void TestSetPassThrough()
        {
            var set = GetSet(3);
            Assert.Equal(3, set.Count);
            Assert.Equal(3, SumSet(set));

            // Check the initial count
            var bufferPayload = set.Payload;
            var payload = (IntPtr*)&bufferPayload;
            Assert.Equal(1, Arc.RetainCount(*payload));

            var setCopy = PassThroughSet(set);
            Assert.Equal(3, setCopy.Count);
            Assert.Equal(3, SumSet(setCopy));

            // Check the references are not the same
            var bufferPayloadCopy = setCopy.Payload;
            var payloadCopy = (IntPtr*)&bufferPayloadCopy;
            Assert.NotEqual((IntPtr)payload, (IntPtr)payloadCopy);

            // Check the payloads are the same
            Assert.Equal(*payload, *payloadCopy);

            // Check the count after the copy
            Assert.Equal(2, Arc.RetainCount(*payload));
            Assert.Equal(2, Arc.RetainCount(*payloadCopy));

            setCopy.Dispose();
            // Check the count after the copy is disposed
            Assert.Equal(1, Arc.RetainCount(*payload));
            Assert.Equal(1, Arc.RetainCount(*payloadCopy));
        }

        // TODO: Remove helper methods when https://github.com/dotnet/runtimelab/issues/2970
        private static unsafe SwiftSet<SwiftIntMock> GetSet(int count)
        {
            SwiftHandle variant = PInvoke_GetSet(count);
            return SwiftMarshal.MarshalFromSwift<SwiftSet<SwiftIntMock>>((SwiftHandle)new IntPtr(&variant));
        }

        private static unsafe int SumSet(SwiftSet<SwiftIntMock> set)
        {
            SwiftHandle variant = set.Payload;
            return PInvoke_SumSet(variant);
        }

        private static unsafe SwiftSet<SwiftIntMock> PassThroughSet(SwiftSet<SwiftIntMock> set)
        {
            SwiftHandle variant = set.Payload;
            variant = PInvoke_PassThroughSet(variant);
            return SwiftMarshal.MarshalFromSwift<SwiftSet<SwiftIntMock>>((SwiftHandle)new IntPtr(&variant));
        }

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests8getArray5countSays5Int32VGAE_tF")]
        private static extern SwiftHandle PInvoke_GetSet(int count);

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests8sumArray5arrays5Int32VSayAEG_tF")]
        private static extern int PInvoke_SumSet(SwiftHandle set);

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests16passThroughArray5arraySays5Int32VGAF_tF")]
        private static extern SwiftHandle PInvoke_PassThroughSet(SwiftHandle set);

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

        [Fact]
        public unsafe void TestStringPassThrough()
        {
            var str = Bindings.RuntimeTests.getString(3);
            Assert.Equal(3, str.Length);
            Assert.Equal("aaa", str.ToString());

            // Check the initial count
            var bufferPayload = str.Payload;
            IntPtr payload = new IntPtr(&bufferPayload);
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));

            var strCopy = Bindings.RuntimeTests.passThroughString(str);
            Assert.Equal(3, strCopy.Length);
            Assert.Equal("aaa", strCopy.ToString());

            // Check the references are not the same
            var bufferPayloadCopy = strCopy.Payload;
            IntPtr payloadCopy = new IntPtr(&bufferPayloadCopy);
            Assert.NotEqual(payload, payloadCopy);

            // Check the payloads are the same
            Assert.Equal(*(IntPtr*)payload, *(IntPtr*)payloadCopy);

            // Check the count after the copy
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(2)));
        }
    }
}
