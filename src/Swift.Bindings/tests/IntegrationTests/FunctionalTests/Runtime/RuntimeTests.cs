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
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)array.Payload.DangerousGetHandle()));

            var arrayCopy = Bindings.RuntimeTests.passThroughArray(array);
            Assert.Equal(3, arrayCopy.Count);
            Assert.Equal(0, arrayCopy[0]);
            Assert.Equal(1, arrayCopy[1]);
            Assert.Equal(2, arrayCopy[2]);
            Assert.Equal(3, Bindings.RuntimeTests.sumArray(arrayCopy));

            // Check the references are not the same
            Assert.NotEqual(array.Payload.DangerousGetHandle(), arrayCopy.Payload.DangerousGetHandle());
            // Check the payloads are the same
            Assert.Equal(*(IntPtr*)array.Payload.DangerousGetHandle(), *(IntPtr*)arrayCopy.Payload.DangerousGetHandle());

            // Check the count after the copy
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)array.Payload.DangerousGetHandle()));
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)arrayCopy.Payload.DangerousGetHandle()));

            Assert.False(arrayCopy.Payload.IsClosed);
            Assert.False(arrayCopy.Payload.IsInvalid);
            Assert.False(array.Payload.IsClosed);
            Assert.False(array.Payload.IsInvalid);

            arrayCopy.Payload.Dispose();

            Assert.True(arrayCopy.Payload.IsClosed);

            Assert.False(array.Payload.IsClosed);
            Assert.False(array.Payload.IsInvalid);
            // Check the count after the copy is disposed
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)array.Payload.DangerousGetHandle()));
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
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)array.Payload.DangerousGetHandle()));

            var arrayCopy = Bindings.RuntimeTests.passThroughArray(array);
            Assert.Equal(3, arrayCopy.Count);
            Assert.Equal(0, arrayCopy[0]);
            Assert.Equal(1, arrayCopy[1]);
            Assert.Equal(2, arrayCopy[2]);
            Assert.Equal(3, Bindings.RuntimeTests.sumArray(arrayCopy));

            // Check the count after the copy
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)array.Payload.DangerousGetHandle()));
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)arrayCopy.Payload.DangerousGetHandle()));

            // Check the payloads are the same
            Assert.Equal(*(IntPtr*)array.Payload.DangerousGetHandle(), *(IntPtr*)arrayCopy.Payload.DangerousGetHandle());

            array[0] = 9;
            array[1] = 8;
            array[2] = 7;

            // Check the count after the change
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)array.Payload.DangerousGetHandle()));
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)arrayCopy.Payload.DangerousGetHandle()));

            var arrayCopyCopy = Bindings.RuntimeTests.passThroughArray(arrayCopy);
            Assert.Equal(arrayCopy.Count, arrayCopyCopy.Count);
            Assert.Equal(arrayCopy[0], arrayCopyCopy[0]);
            Assert.Equal(arrayCopy[1], arrayCopyCopy[1]);
            Assert.Equal(arrayCopy[2], arrayCopyCopy[2]);

            // Check the count after the copy
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)array.Payload.DangerousGetHandle()));
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)arrayCopy.Payload.DangerousGetHandle()));
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)arrayCopyCopy.Payload.DangerousGetHandle()));

            arrayCopy.Payload.Dispose();

            Assert.False(array.Payload.IsClosed);
            Assert.True(arrayCopy.Payload.IsClosed);
            Assert.False(arrayCopyCopy.Payload.IsClosed);

            // // Check the count after the copy is disposed
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)array.Payload.DangerousGetHandle()));
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)arrayCopyCopy.Payload.DangerousGetHandle()));
        }

        [Fact]
        public unsafe void TestSwiftMarshalArray()
        {
            SwiftArray<Int32> vtype = new SwiftArray<Int32>();
            vtype.Append(42);
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)vtype.Payload.DangerousGetHandle()));

            var metadata = SwiftObjectHelper<SwiftArray<Int32>>.GetTypeMetadata();
            Span<byte> payloadSpan = stackalloc byte[(int)metadata.Size];
            IntPtr payloadPtr = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetReference(payloadSpan));

            // Marshal the object to Swift
            SwiftMarshal.MarshalToSwift(vtype, ref payloadSpan);
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)vtype.Payload.DangerousGetHandle()));

            // Marshal back from Swift
            var copy = SwiftMarshal.MarshalFromSwift<SwiftArray<Int32>>(payloadPtr);
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)copy.Payload.DangerousGetHandle()));

            // Dispose the copy and verify retain count
            copy.Payload.Dispose();
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)vtype.Payload.DangerousGetHandle()));
        }

        [Fact]
        public async Task ConcurrentArray()
        {
            for (int i = 0; i < 10; i++)
            {
                SwiftArray<Int32> resource = new SwiftArray<Int32>();
                resource.Append(42);
                var barrier = new Barrier(4);

                var getterTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        Assert.Equal(42, resource[0]);
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Assert.IsType<ObjectDisposedException>(ex);
                    }
                });

                var methodTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        resource.Append(42);
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Assert.IsType<ObjectDisposedException>(ex);
                    }
                });

                var passThroughTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        var copy = Bindings.RuntimeTests.passThroughArray(resource);
                        var genericCopy = Bindings.RuntimeTests.passThroughGeneric<SwiftArray<Int32>>(resource);
                        copy.Payload.Dispose();
                        genericCopy.Payload.Dispose();
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Assert.IsType<ObjectDisposedException>(ex);
                    }
                });

                var disposeTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    resource.Payload.Dispose();
                });

                await Task.WhenAll(methodTask, getterTask, passThroughTask, disposeTask);

                Assert.True(resource.Payload.IsClosed);
            }
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
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)set.Payload.DangerousGetHandle()));

            var setCopy = PassThroughSet(set);
            Assert.Equal(3, setCopy.Count);
            Assert.Equal(3, SumSet(setCopy));

            // Check the references are not the same
            Assert.NotEqual(set.Payload.DangerousGetHandle(), setCopy.Payload.DangerousGetHandle());
            // Check the payloads are the same
            Assert.Equal(*(IntPtr*)set.Payload.DangerousGetHandle(), *(IntPtr*)setCopy.Payload.DangerousGetHandle());

            // Check the count after the copy
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)set.Payload.DangerousGetHandle()));
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)setCopy.Payload.DangerousGetHandle()));

            setCopy.Payload.Dispose();
            // Check the count after the copy is disposed
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)set.Payload.DangerousGetHandle()));
        }

        // TODO: Remove helper methods when https://github.com/dotnet/runtimelab/issues/2970
        private static unsafe SwiftSet<SwiftIntMock> GetSet(int count)
        {
            IntPtr variant = PInvoke_GetSet(count);
            return SwiftMarshal.MarshalFromSwift<SwiftSet<SwiftIntMock>>(new IntPtr(&variant));
        }

        private static unsafe int SumSet(SwiftSet<SwiftIntMock> set)
        {
            IntPtr variant = *(IntPtr*)set.Payload.DangerousGetHandle();
            return PInvoke_SumSet(variant);
        }

        private static unsafe SwiftSet<SwiftIntMock> PassThroughSet(SwiftSet<SwiftIntMock> set)
        {
            IntPtr variant = *(IntPtr*)set.Payload.DangerousGetHandle();
            variant = PInvoke_PassThroughSet(variant);
            return SwiftMarshal.MarshalFromSwift<SwiftSet<SwiftIntMock>>(new IntPtr(&variant));
        }

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests8getArray5countSays5Int32VGAE_tF")]
        private static extern IntPtr PInvoke_GetSet(int count);

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests8sumArray5arrays5Int32VSayAEG_tF")]
        private static extern int PInvoke_SumSet(IntPtr set);

        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
        [DllImport("Runtime/libRuntimeTests.dylib", EntryPoint = "$s12RuntimeTests16passThroughArray5arraySays5Int32VGAF_tF")]
        private static extern IntPtr PInvoke_PassThroughSet(IntPtr set);

        [Fact]
        public unsafe void TestSwiftMarshalSet()
        {
            SwiftSet<SwiftIntMock> vtype = GetSet(1);
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)vtype.Payload.DangerousGetHandle()));

            var metadata = SwiftObjectHelper<SwiftSet<SwiftIntMock>>.GetTypeMetadata();
            Span<byte> payloadSpan = stackalloc byte[(int)metadata.Size];
            IntPtr payloadPtr = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetReference(payloadSpan));

            // Marshal the object to Swift
            SwiftMarshal.MarshalToSwift(vtype, ref payloadSpan);
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)vtype.Payload.DangerousGetHandle()));

            // Marshal back from Swift
            var copy = SwiftMarshal.MarshalFromSwift<SwiftSet<SwiftIntMock>>(payloadPtr);
            Assert.Equal(2, Arc.RetainCount(*(IntPtr*)copy.Payload.DangerousGetHandle()));

            // Dispose the copy and verify retain count
            copy.Payload.Dispose();
            Assert.Equal(1, Arc.RetainCount(*(IntPtr*)vtype.Payload.DangerousGetHandle()));
        }

        [Fact]
        public async Task ConcurrentSet()
        {
            for (int i = 0; i < 10; i++)
            {
                SwiftSet<SwiftIntMock> resource = GetSet(1);
                var barrier = new Barrier(2);

                var getterTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        Assert.Equal(1, resource.Count);
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Assert.IsType<ObjectDisposedException>(ex);
                    }
                });

                var disposeTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    resource.Payload.Dispose();
                });

                await Task.WhenAll(getterTask, disposeTask);

                Assert.True(resource.Payload.IsClosed);
            }
        }

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
            var inlineString = Bindings.RuntimeTests.getString(15);
            Assert.Equal(15, inlineString.Length);
            // No reference counting for inline strings
            Assert.Equal(0, Arc.RetainCount(inlineString.Payload.DangerousGetHandle().At(1)));

            var heapString = Bindings.RuntimeTests.getString(16);
            Assert.Equal(16, heapString.Length);

            Assert.Equal(1, Arc.RetainCount(heapString.Payload.DangerousGetHandle().At(1)));

            var strCopy = Bindings.RuntimeTests.passThroughString(heapString);
            Assert.Equal(16, strCopy.Length);

            // Check the pointers are not the same
            Assert.NotEqual(heapString.Payload.DangerousGetHandle(), strCopy.Payload.DangerousGetHandle());

            // Check the count after the copy
            Assert.Equal(2, Arc.RetainCount(heapString.Payload.DangerousGetHandle().At(1)));
            Assert.Equal(2, Arc.RetainCount(strCopy.Payload.DangerousGetHandle().At(1)));

            Assert.False(heapString.Payload.IsClosed);
            Assert.False(heapString.Payload.IsInvalid);
            Assert.False(strCopy.Payload.IsClosed);
            Assert.False(strCopy.Payload.IsInvalid);

            strCopy.Payload.Dispose();
            Assert.True(strCopy.Payload.IsClosed);
            Assert.True(strCopy.Payload.IsInvalid);
            Assert.False(heapString.Payload.IsClosed);
            Assert.False(heapString.Payload.IsInvalid);

            // Check the count after the copy is disposed
            Assert.Equal(1, Arc.RetainCount(heapString.Payload.DangerousGetHandle().At(1)));
        }

        [Fact]
        public unsafe void TestSwiftMarshalString()
        {
            SwiftString vtype = Bindings.RuntimeTests.getString(16);
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.DangerousGetHandle().At(1)));

            var metadata = SwiftObjectHelper<SwiftString>.GetTypeMetadata();
            Span<byte> payloadSpan = stackalloc byte[(int)metadata.Size];
            IntPtr payloadPtr = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetReference(payloadSpan));

            // Marshal the object to Swift
            SwiftMarshal.MarshalToSwift(vtype, ref payloadSpan);
            Assert.Equal(2, Arc.RetainCount(vtype.Payload.DangerousGetHandle().At(1)));

            // Marshal back from Swift
            var copy = SwiftMarshal.MarshalFromSwift<SwiftString>(payloadPtr);
            Assert.Equal(2, Arc.RetainCount(copy.Payload.DangerousGetHandle().At(1)));

            // Dispose the copy and verify retain count
            copy.Payload.Dispose();
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.DangerousGetHandle().At(1)));
        }

        [Fact]
        public async Task ConcurrentString()
        {
            for (int i = 0; i < 10; i++)
            {
                SwiftString resource = Bindings.RuntimeTests.getString(16);
                var barrier = new Barrier(2);

                var passThroughTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        var copy = Bindings.RuntimeTests.passThroughString(resource);
                        var genericCopy = Bindings.RuntimeTests.passThroughGeneric<SwiftString>(resource);
                        copy.Payload.Dispose();
                        genericCopy.Payload.Dispose();
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Assert.IsType<ObjectDisposedException>(ex);
                    }
                });

                var disposeTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    resource.Payload.Dispose();
                });

                await Task.WhenAll(passThroughTask, disposeTask);

                Assert.True(resource.Payload.IsClosed);
            }
        }
    }
}
