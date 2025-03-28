// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using BindingsGeneration.Tests;
using Swift;
using Swift.MemoryTests;
using Swift.Runtime;
using Swift.Runtime.InteropServices;
using Xunit;

using Bindings = Swift.MemoryTests;


namespace BindingsGeneration.FunctionalTests
{
    public static unsafe class MemoryExtensions
    {
        /// <summary>
        /// Reads a pointer stored at the given index.
        /// </summary>
        public static IntPtr At(this IntPtr ptr, int index)
        {
            byte* bytePtr = (byte*)ptr.ToPointer();
            return *(IntPtr*)(bytePtr + index * IntPtr.Size);
        }
    }

    public class MemoryTests : IClassFixture<MemoryTests.TestFixture>
    {
        private readonly TestFixture _fixture;

        public MemoryTests(TestFixture fixture)
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
        public unsafe void TestInitWithCopyVType()
        {
            Bindings.VType vType = new Bindings.VType();
            GC.SuppressFinalize(vType);
            GC.SuppressFinalize(vType.Payload);
            IntPtr payload = (IntPtr)vType.Payload.Handle;

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, payload.At(0).At(1));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest);

            var metadata = SwiftObjectHelper<Bindings.VType>.GetTypeMetadata();

            // Creates a copy of the vType
            IntPtr payloadCopy = (IntPtr)NativeMemory.Alloc(metadata.ValueWitnessTable->Size);
            metadata.ValueWitnessTable->InitializeWithCopy((void*)payloadCopy, (void*)payload, metadata);

            // Check the copy ref is the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));

            // Check the count after copy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));

            Arc.Release(payload.At(0));
            // // Check the count after release
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(0)));

            Arc.Release(payload.At(0));

            // // Check deinit is called
            Assert.Equal(1, vType.refTypeTest);

            NativeMemory.Free((void*)payloadCopy);
        }

        [Fact]
        private static unsafe void TestDestroyVType()
        {
            Bindings.VType vType = new Bindings.VType();
            GC.SuppressFinalize(vType);
            GC.SuppressFinalize(vType.Payload);
            IntPtr payload = (IntPtr)vType.Payload.Handle;

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, payload.At(0).At(1));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest);

            Arc.Retain(payload.At(0));
            Arc.Retain(payload.At(0));

            // Check the count after retain
            Assert.Equal(3, Arc.RetainCount(payload.At(0)));

            var metadata = SwiftObjectHelper<Bindings.VType>.GetTypeMetadata();
            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);

            // Check deinit is called
            Assert.Equal(1, vType.refTypeTest);
        }

        [Fact]
        public unsafe void TestInitWithCopyAndDestroyVType()
        {
            Bindings.VType vType = new Bindings.VType();
            GC.SuppressFinalize(vType);
            GC.SuppressFinalize(vType.Payload);
            IntPtr payload = (IntPtr)vType.Payload.Handle;

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, payload.At(0).At(1));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest);

            var metadata = SwiftObjectHelper<Bindings.VType>.GetTypeMetadata();

            IntPtr payloadCopy = (IntPtr)NativeMemory.Alloc(metadata.ValueWitnessTable->Size);
            metadata.ValueWitnessTable->InitializeWithCopy((void*)payloadCopy, (void*)payload, metadata);

            // Check the copy ref is the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));

            // Check the count after copy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));

            IntPtr payloadCopyCopy = (IntPtr)NativeMemory.Alloc(metadata.ValueWitnessTable->Size);
            metadata.ValueWitnessTable->InitializeWithCopy((void*)payloadCopyCopy, (void*)payloadCopy, metadata);

            // Check the copy ref is the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));
            Assert.Equal(payload.At(0), payloadCopyCopy.At(0));

            // Check the count after copy
            Assert.Equal(3, Arc.RetainCount(payload.At(0)));
            Assert.Equal(3, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(3, Arc.RetainCount(payloadCopyCopy.At(0)));

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopyCopy.At(0)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopyCopy.At(0)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);

            // Check deinit is called
            Assert.Equal(1, vType.refTypeTest);

            NativeMemory.Free((void*)payloadCopy);
            NativeMemory.Free((void*)payloadCopyCopy);
        }

        [Fact]
        public unsafe void TestInitWithCopyNestedVType()
        {
            Bindings.NestedVType vType = new Bindings.NestedVType();
            GC.SuppressFinalize(vType);
            GC.SuppressFinalize(vType.Payload);
            IntPtr payload = (IntPtr)vType.Payload.Handle;

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, payload.At(0).At(1));
            Assert.Equal(0x3, payload.At(2).At(1));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);

            var metadata = SwiftObjectHelper<Bindings.NestedVType>.GetTypeMetadata();

            // Creates a copy of the vType
            IntPtr payloadCopy = (IntPtr)NativeMemory.Alloc(metadata.ValueWitnessTable->Size);
            metadata.ValueWitnessTable->InitializeWithCopy((void*)payloadCopy, (void*)payload, metadata);

            // Check the copy ref is the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));
            Assert.Equal(payload.At(2), payloadCopy.At(2));

            // Check the count after copy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(2, Arc.RetainCount(payload.At(2)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(2)));

            Arc.Release(payload.At(0));
            Arc.Release(payload.At(2));
            // Check the count after release
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(2)));

            Arc.Release(payload.At(0));
            Arc.Release(payload.At(2));

            // Check deinit is called
            Assert.Equal(1, vType.refTypeTest1);
            Assert.Equal(1, vType.refTypeTest2);

            NativeMemory.Free((void*)payloadCopy);
        }

        [Fact]
        private static unsafe void TestDestroyNestedVType()
        {
            Bindings.NestedVType vType = new Bindings.NestedVType();
            GC.SuppressFinalize(vType);
            GC.SuppressFinalize(vType.Payload);
            IntPtr payload = (IntPtr)vType.Payload.Handle;

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, payload.At(0).At(1));
            Assert.Equal(0x3, payload.At(2).At(1));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);

            Arc.Retain(payload.At(0));
            Arc.Retain(payload.At(0));
            Arc.Retain(payload.At(2));
            Arc.Retain(payload.At(2));

            // Check the count after retain
            Assert.Equal(3, Arc.RetainCount(payload.At(0)));
            Assert.Equal(3, Arc.RetainCount(payload.At(2)));

            var metadata = SwiftObjectHelper<Bindings.NestedVType>.GetTypeMetadata();
            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payload.At(2)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);

            // Check deinit is called
            Assert.Equal(1, vType.refTypeTest1);
            Assert.Equal(1, vType.refTypeTest2);
        }

        [Fact]
        public unsafe void TestInitWithCopyAndDestroyNestedVType()
        {
            Bindings.NestedVType vType = new Bindings.NestedVType();
            GC.SuppressFinalize(vType);
            GC.SuppressFinalize(vType.Payload);
            IntPtr payload = (IntPtr)vType.Payload.Handle;

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, payload.At(0).At(1));
            Assert.Equal(0x3, payload.At(2).At(1));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);

            var metadata = SwiftObjectHelper<Bindings.NestedVType>.GetTypeMetadata();

            IntPtr payloadCopy = (IntPtr)NativeMemory.Alloc(metadata.ValueWitnessTable->Size);
            metadata.ValueWitnessTable->InitializeWithCopy((void*)payloadCopy, (void*)payload, metadata);

            // Check the copy ref is the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));
            Assert.Equal(payload.At(2), payloadCopy.At(2));

            // Check the count after copy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(2, Arc.RetainCount(payload.At(2)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(2)));

            IntPtr payloadCopyCopy = (IntPtr)NativeMemory.Alloc(metadata.ValueWitnessTable->Size);
            metadata.ValueWitnessTable->InitializeWithCopy((void*)payloadCopyCopy, (void*)payloadCopy, metadata);

            // Check the copy ref is the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));
            Assert.Equal(payload.At(0), payloadCopyCopy.At(0));
            Assert.Equal(payload.At(2), payloadCopy.At(2));
            Assert.Equal(payload.At(2), payloadCopyCopy.At(2));

            // Check the count after copy
            Assert.Equal(3, Arc.RetainCount(payload.At(0)));
            Assert.Equal(3, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(3, Arc.RetainCount(payloadCopyCopy.At(0)));
            Assert.Equal(3, Arc.RetainCount(payload.At(2)));
            Assert.Equal(3, Arc.RetainCount(payloadCopy.At(2)));
            Assert.Equal(3, Arc.RetainCount(payloadCopyCopy.At(2)));

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopyCopy.At(0)));
            Assert.Equal(2, Arc.RetainCount(payload.At(2)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(2)));
            Assert.Equal(2, Arc.RetainCount(payloadCopyCopy.At(2)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopyCopy.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(2)));
            Assert.Equal(1, Arc.RetainCount(payloadCopyCopy.At(2)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);

            // Check deinit is called
            Assert.Equal(1, vType.refTypeTest1);
            Assert.Equal(1, vType.refTypeTest2);

            NativeMemory.Free((void*)payloadCopy);
            NativeMemory.Free((void*)payloadCopyCopy);
        }

        [Fact]
        public unsafe void TestInitWithCopyNestedNestedVType()
        {
            Bindings.NestedNestedVType vType = new Bindings.NestedNestedVType();
            GC.SuppressFinalize(vType);
            GC.SuppressFinalize(vType.Payload);
            IntPtr payload = (IntPtr)vType.Payload.Handle;

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            Assert.Equal(1, Arc.RetainCount(payload.At(4)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, payload.At(0).At(1));
            Assert.Equal(0x3, payload.At(2).At(1));
            Assert.Equal(0x3, payload.At(4).At(1));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);
            Assert.Equal(0, vType.refTypeTest3);

            var metadata = SwiftObjectHelper<Bindings.NestedNestedVType>.GetTypeMetadata();

            // Creates a copy of the vType
            IntPtr payloadCopy = (IntPtr)NativeMemory.Alloc(metadata.ValueWitnessTable->Size);
            metadata.ValueWitnessTable->InitializeWithCopy((void*)payloadCopy, (void*)payload, metadata);

            // Check the copy ref is the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));
            Assert.Equal(payload.At(2), payloadCopy.At(2));
            Assert.Equal(payload.At(4), payloadCopy.At(4));

            // Check the count after copy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(2, Arc.RetainCount(payload.At(2)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(2)));
            Assert.Equal(2, Arc.RetainCount(payload.At(4)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(4)));

            Arc.Release(payload.At(0));
            Arc.Release(payload.At(2));
            Arc.Release(payload.At(4));
            // Check the count after release
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(2)));
            Assert.Equal(1, Arc.RetainCount(payload.At(4)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(4)));

            Arc.Release(payload.At(0));
            Arc.Release(payload.At(2));
            Arc.Release(payload.At(4));

            // Check deinit is called
            Assert.Equal(1, vType.refTypeTest1);
            Assert.Equal(1, vType.refTypeTest2);
            Assert.Equal(1, vType.refTypeTest3);

            NativeMemory.Free((void*)payloadCopy);
        }

        [Fact]
        private static unsafe void TestDestroyNestedNestedVType()
        {
            Bindings.NestedNestedVType vType = new Bindings.NestedNestedVType();
            GC.SuppressFinalize(vType);
            GC.SuppressFinalize(vType.Payload);
            IntPtr payload = (IntPtr)vType.Payload.Handle;

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            Assert.Equal(1, Arc.RetainCount(payload.At(4)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, payload.At(0).At(1));
            Assert.Equal(0x3, payload.At(2).At(1));
            Assert.Equal(0x3, payload.At(4).At(1));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);
            Assert.Equal(0, vType.refTypeTest3);

            Arc.Retain(payload.At(0));
            Arc.Retain(payload.At(0));
            Arc.Retain(payload.At(2));
            Arc.Retain(payload.At(2));
            Arc.Retain(payload.At(4));
            Arc.Retain(payload.At(4));

            // Check the count after retain
            Assert.Equal(3, Arc.RetainCount(payload.At(0)));
            Assert.Equal(3, Arc.RetainCount(payload.At(2)));
            Assert.Equal(3, Arc.RetainCount(payload.At(4)));

            var metadata = SwiftObjectHelper<Bindings.NestedNestedVType>.GetTypeMetadata();
            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payload.At(2)));
            Assert.Equal(2, Arc.RetainCount(payload.At(4)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);
            Assert.Equal(0, vType.refTypeTest3);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            Assert.Equal(1, Arc.RetainCount(payload.At(4)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);
            Assert.Equal(0, vType.refTypeTest3);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);

            // Check deinit is called
            Assert.Equal(1, vType.refTypeTest1);
            Assert.Equal(1, vType.refTypeTest2);
            Assert.Equal(1, vType.refTypeTest3);
        }

        [Fact]
        public unsafe void TestInitWithCopyAndDestroyNestedNestedVType()
        {
            Bindings.NestedNestedVType vType = new Bindings.NestedNestedVType();
            GC.SuppressFinalize(vType);
            GC.SuppressFinalize(vType.Payload);
            IntPtr payload = (IntPtr)vType.Payload.Handle;

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            Assert.Equal(1, Arc.RetainCount(payload.At(4)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, payload.At(0).At(1));
            Assert.Equal(0x3, payload.At(2).At(1));
            Assert.Equal(0x3, payload.At(4).At(1));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);
            Assert.Equal(0, vType.refTypeTest3);

            var metadata = SwiftObjectHelper<Bindings.NestedNestedVType>.GetTypeMetadata();

            IntPtr payloadCopy = (IntPtr)NativeMemory.Alloc(metadata.ValueWitnessTable->Size);
            metadata.ValueWitnessTable->InitializeWithCopy((void*)payloadCopy, (void*)payload, metadata);

            // Check the copy ref is the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));
            Assert.Equal(payload.At(2), payloadCopy.At(2));
            Assert.Equal(payload.At(4), payloadCopy.At(4));

            // Check the count after copy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(2, Arc.RetainCount(payload.At(2)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(2)));
            Assert.Equal(2, Arc.RetainCount(payload.At(4)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(4)));

            IntPtr payloadCopyCopy = (IntPtr)NativeMemory.Alloc(metadata.ValueWitnessTable->Size);
            metadata.ValueWitnessTable->InitializeWithCopy((void*)payloadCopyCopy, (void*)payloadCopy, metadata);

            // Check the copy ref is the same
            Assert.Equal(payload.At(0), payloadCopy.At(0));
            Assert.Equal(payload.At(0), payloadCopyCopy.At(0));
            Assert.Equal(payload.At(2), payloadCopy.At(2));
            Assert.Equal(payload.At(2), payloadCopyCopy.At(2));
            Assert.Equal(payload.At(4), payloadCopy.At(4));
            Assert.Equal(payload.At(4), payloadCopyCopy.At(4));

            // Check the count after copy
            Assert.Equal(3, Arc.RetainCount(payload.At(0)));
            Assert.Equal(3, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(3, Arc.RetainCount(payloadCopyCopy.At(0)));
            Assert.Equal(3, Arc.RetainCount(payload.At(2)));
            Assert.Equal(3, Arc.RetainCount(payloadCopy.At(2)));
            Assert.Equal(3, Arc.RetainCount(payloadCopyCopy.At(2)));
            Assert.Equal(3, Arc.RetainCount(payload.At(4)));
            Assert.Equal(3, Arc.RetainCount(payloadCopy.At(4)));
            Assert.Equal(3, Arc.RetainCount(payloadCopyCopy.At(4)));

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(2, Arc.RetainCount(payload.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(2, Arc.RetainCount(payloadCopyCopy.At(0)));
            Assert.Equal(2, Arc.RetainCount(payload.At(2)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(2)));
            Assert.Equal(2, Arc.RetainCount(payloadCopyCopy.At(2)));
            Assert.Equal(2, Arc.RetainCount(payload.At(4)));
            Assert.Equal(2, Arc.RetainCount(payloadCopy.At(4)));
            Assert.Equal(2, Arc.RetainCount(payloadCopyCopy.At(4)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);
            Assert.Equal(0, vType.refTypeTest3);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);
            // Check the count after destroy
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopyCopy.At(0)));
            Assert.Equal(1, Arc.RetainCount(payload.At(2)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(2)));
            Assert.Equal(1, Arc.RetainCount(payloadCopyCopy.At(2)));
            Assert.Equal(1, Arc.RetainCount(payload.At(4)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(4)));
            Assert.Equal(1, Arc.RetainCount(payloadCopyCopy.At(4)));
            // Check deinit is not called
            Assert.Equal(0, vType.refTypeTest1);
            Assert.Equal(0, vType.refTypeTest2);
            Assert.Equal(0, vType.refTypeTest3);

            metadata.ValueWitnessTable->Destroy((void*)payload, metadata);

            // Check deinit is called
            Assert.Equal(1, vType.refTypeTest1);
            Assert.Equal(1, vType.refTypeTest2);
            Assert.Equal(1, vType.refTypeTest3);

            NativeMemory.Free((void*)payloadCopy);
            NativeMemory.Free((void*)payloadCopyCopy);
        }

        [Fact]
        public void TestProjectionTypes()
        {
            Assert.True(typeof(Bindings.FrozenStruct).IsValueType);
            Assert.True(typeof(Bindings.FrozenStructRequiresMemoryManagement).IsClass);
            Assert.True(typeof(Bindings.NestedFrozenStructRequiresMemoryManagement).IsClass);
            Assert.True(typeof(Bindings.NonFrozenStruct).IsClass);
            Assert.True(typeof(Bindings.NonFrozenStructRequiresMemoryManagement).IsClass);
        }

        [Fact]
        public unsafe void TestDisposeInvokesDestroy()
        {
            var frozenRequiresMemoryManagement = new Bindings.FrozenStructRequiresMemoryManagement(42);

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(frozenRequiresMemoryManagement.Payload.Handle.At(0)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, frozenRequiresMemoryManagement.Payload.Handle.At(0).At(1));

            // Retain the payload count
            Arc.Retain(frozenRequiresMemoryManagement.Payload.Handle.At(0));
            Assert.Equal(2, Arc.RetainCount(frozenRequiresMemoryManagement.Payload.Handle.At(0)));

            // Dispose the frozenRequiresMemoryManagement
            Assert.False(frozenRequiresMemoryManagement.Payload.IsClosed);
            Assert.False(frozenRequiresMemoryManagement.Payload.IsInvalid);
            var handle = frozenRequiresMemoryManagement.PayloadBuffer;
            frozenRequiresMemoryManagement.Dispose();
            Assert.True(frozenRequiresMemoryManagement.Payload.IsClosed);
            Assert.True(frozenRequiresMemoryManagement.Payload.IsInvalid);
            Assert.Equal(1, Arc.RetainCount(new IntPtr(&handle).At(0)));

            var nestedFrozenRequiresMemoryManagement = new Bindings.NestedFrozenStructRequiresMemoryManagement(42);

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(nestedFrozenRequiresMemoryManagement.Payload.Handle.At(0)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, nestedFrozenRequiresMemoryManagement.Payload.Handle.At(0).At(1));

            // Retain the payload count
            Arc.Retain(nestedFrozenRequiresMemoryManagement.Payload.Handle.At(0));
            Assert.Equal(2, Arc.RetainCount(nestedFrozenRequiresMemoryManagement.Payload.Handle.At(0)));

            // Dispose the NestedFrozenRequiresMemoryManagement
            Assert.False(nestedFrozenRequiresMemoryManagement.Payload.IsClosed);
            Assert.False(nestedFrozenRequiresMemoryManagement.Payload.IsInvalid);
            var nestedHandle = nestedFrozenRequiresMemoryManagement.PayloadBuffer;
            nestedFrozenRequiresMemoryManagement.Dispose();
            Assert.True(nestedFrozenRequiresMemoryManagement.Payload.IsClosed);
            Assert.True(nestedFrozenRequiresMemoryManagement.Payload.IsInvalid);
            Assert.Equal(1, Arc.RetainCount(new IntPtr(&nestedHandle).At(0)));

            var nonfrozenRequiresMemoryManagement = new Bindings.NonFrozenStructRequiresMemoryManagement(42);

            // Check the initial count
            Assert.Equal(1, Arc.RetainCount(nonfrozenRequiresMemoryManagement.Payload.Handle.At(0)));
            // Check the metadata flags for a class
            Assert.Equal(0x3, nonfrozenRequiresMemoryManagement.Payload.Handle.At(0).At(1));

            // Retain the payload count
            Arc.Retain(nonfrozenRequiresMemoryManagement.Payload.Handle.At(0));
            Assert.Equal(2, Arc.RetainCount(nonfrozenRequiresMemoryManagement.Payload.Handle.At(0)));

            Assert.False(nonfrozenRequiresMemoryManagement.Payload.IsClosed);
            Assert.False(nonfrozenRequiresMemoryManagement.Payload.IsInvalid);
            // Memory is allocated from C# side and released in Dispose
            // Take the payload to check the retain count
            var nonFrozenHandle = nonfrozenRequiresMemoryManagement.Payload.Handle.At(0);
            nonfrozenRequiresMemoryManagement.Dispose();
            Assert.True(nonfrozenRequiresMemoryManagement.Payload.IsClosed);
            Assert.True(nonfrozenRequiresMemoryManagement.Payload.IsInvalid);
            // Check the count after destroy
            Assert.Equal(1, Arc.RetainCount(nonFrozenHandle));
            Assert.Equal(IntPtr.Zero, nonfrozenRequiresMemoryManagement.Payload.Handle);
        }

        [Fact]
        public unsafe void TestSwiftMarshalFrozenStruct()
        {
            var vtype = new Bindings.FrozenStructRequiresMemoryManagement(42);
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(0)));

            var metadata = SwiftObjectHelper<Bindings.FrozenStructRequiresMemoryManagement>.GetTypeMetadata();
            byte* payloadPtr = stackalloc byte[(int)metadata.Size];
            Span<byte> payloadSpan = new Span<byte>(payloadPtr, (int)metadata.Size);

            // Marshal the object to Swift
            SwiftMarshal.MarshalToSwift(vtype, payloadSpan);
            Assert.Equal(2, Arc.RetainCount(vtype.Payload.Handle.At(0)));

            // Marshal back from Swift
            var copy = SwiftMarshal.MarshalFromSwift<Bindings.FrozenStructRequiresMemoryManagement>((IntPtr)payloadPtr);
            Assert.Equal(2, Arc.RetainCount(copy.Payload.Handle.At(0)));

            // Dispose the copy and verify retain count
            copy.Dispose();
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(0)));
        }

        [Fact]
        public unsafe void TestSwiftMarshalMethodsNestedFrozenStruct()
        {
            var vtype = new Bindings.NestedFrozenStructRequiresMemoryManagement(42);
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(0)));

            var metadata = SwiftObjectHelper<Bindings.NestedFrozenStructRequiresMemoryManagement>.GetTypeMetadata();
            byte* payloadPtr = stackalloc byte[(int)metadata.Size];
            Span<byte> payloadSpan = new Span<byte>(payloadPtr, (int)metadata.Size);

            // Marshal the object to Swift
            SwiftMarshal.MarshalToSwift(vtype, payloadSpan);
            Assert.Equal(2, Arc.RetainCount(vtype.Payload.Handle.At(0)));

            // Marshal back from Swift
            var copy = SwiftMarshal.MarshalFromSwift<Bindings.FrozenStructRequiresMemoryManagement>((IntPtr)payloadPtr);
            Assert.Equal(2, Arc.RetainCount(copy.Payload.Handle.At(0)));

            // Dispose the copy and verify retain count
            copy.Dispose();
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(0)));
        }

        [Fact]
        public unsafe void TestSwiftMarshalMethodsNonFrozenStruct()
        {
            var vtype = new Bindings.NonFrozenStructRequiresMemoryManagement(42);
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(0)));

            var metadata = SwiftObjectHelper<Bindings.NonFrozenStructRequiresMemoryManagement>.GetTypeMetadata();
            byte* payloadPtr = stackalloc byte[(int)metadata.Size];
            Span<byte> payloadSpan = new Span<byte>(payloadPtr, (int)metadata.Size);

            // Marshal the object to Swift
            SwiftMarshal.MarshalToSwift(vtype, payloadSpan);
            Assert.Equal(2, Arc.RetainCount(vtype.Payload.Handle.At(0)));

            // Marshal back from Swift
            var copy = SwiftMarshal.MarshalFromSwift<Bindings.NonFrozenStructRequiresMemoryManagement>((IntPtr)payloadPtr);
            Assert.Equal(2, Arc.RetainCount(copy.Payload.Handle.At(0)));

            // Dispose the copy and verify retain count
            copy.Dispose();
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(0)));
        }

        [Fact]
        public unsafe void TestPassThroughEmbeddedStruct()
        {
            EmbeddedStruct vtype = new EmbeddedStruct();
            Assert.Equal(1, vtype.x.x);
            Assert.Equal(2, vtype.x.y);
            Assert.Equal(3, vtype.y);

            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(1)));
            EmbeddedStruct copy = Bindings.MemoryTests.PassThroughEmbeddedStruct(vtype);

            Assert.Equal(1, copy.x.x);
            Assert.Equal(2, copy.x.y);
            Assert.Equal(3, copy.y);

            Assert.Equal(2, Arc.RetainCount(vtype.Payload.Handle.At(1)));
            Assert.Equal(2, Arc.RetainCount(copy.Payload.Handle.At(1)));

            copy.Dispose();

            Assert.True(copy.Payload.IsClosed);
            Assert.True(copy.Payload.IsInvalid);

            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(1)));
        }

        [Fact]
        public unsafe void TestSwiftMarshalEmbeddedStruct()
        {
            EmbeddedStruct vtype = new EmbeddedStruct();
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(1)));

            var metadata = SwiftObjectHelper<EmbeddedStruct>.GetTypeMetadata();
            byte* payloadPtr = stackalloc byte[(int)metadata.Size];
            Span<byte> payloadSpan = new Span<byte>(payloadPtr, (int)metadata.Size);

            // Marshal the object to Swift
            SwiftMarshal.MarshalToSwift(vtype, payloadSpan);
            Assert.Equal(2, Arc.RetainCount(vtype.Payload.Handle.At(1)));

            // Marshal back from Swift
            var copy = SwiftMarshal.MarshalFromSwift<EmbeddedStruct>((IntPtr)payloadPtr);
            Assert.Equal(2, Arc.RetainCount(copy.Payload.Handle.At(1)));

            // Dispose the copy and verify retain count
            copy.Dispose();
            Assert.Equal(1, Arc.RetainCount(vtype.Payload.Handle.At(1)));
        }

        class FrozenStructExtension : FrozenStructRequiresMemoryManagement
        {
            public FrozenStructExtension() : base(42)
            {
            }

            public void CallDispose()
            {
                bool _success = false;
                this.Payload.DangerousAddRef(ref _success);
#pragma warning disable CS8500
                unsafe
                {
                    var handle = this.Payload;
                    PInvoke_CallDispose(&Callback, &handle);

                    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvSwift) })]
                    static void Callback(SwiftSelf context)
                    {
                        SwiftHandle<FrozenStructExtension> pContext = *(SwiftHandle<FrozenStructExtension>*)context.Value;
                        pContext.Dispose();
                    }
                }

                Assert.False(Payload.IsClosed);
                Assert.False(Payload.IsInvalid);

                Assert.Equal(1, Arc.RetainCount(Payload.Handle.At(0)));
                Assert.Equal(42, b);

#pragma warning restore CS8500
                if (_success)
                    this.Payload.DangerousRelease();
            }

            [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
            [DllImport("MemoryTests/libMemoryTests.dylib", EntryPoint = "$s11MemoryTests020FrozenStructRequiresA10ManagementV11callDispose8callbackyyyc_tF")]
            private unsafe static extern void PInvoke_CallDispose(delegate* unmanaged[Swift]<SwiftSelf, void> callback, void* context);
        }

        [Fact]
        public unsafe void TestSafeHandleDispose()
        {
            FrozenStructExtension frozenRequiresMemoryManagement = new FrozenStructExtension();
            Assert.Equal(42, frozenRequiresMemoryManagement.b);

            frozenRequiresMemoryManagement.CallDispose();
            Assert.True(frozenRequiresMemoryManagement.Payload.IsClosed);
        }

        [Fact]
        public async Task ConcurrentFrozenStructDispose()
        {
            for (int i = 0; i < 10; i++)
            {
                var resource = new Bindings.FrozenStructRequiresMemoryManagement(42);
                var barrier = new Barrier(4);

                var getterTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        Assert.Equal(42, resource.b);
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
                        Assert.Equal(42, resource.getValue());
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
                        var copy = Bindings.MemoryTests.PassThroughFrozenStruct(resource);
                        var genericCopy = Bindings.MemoryTests.PassThroughGeneric<FrozenStructRequiresMemoryManagement>(resource);
                        copy.Dispose();
                        genericCopy.Dispose();
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Assert.IsType<ObjectDisposedException>(ex);
                    }
                });


                var disposeTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    resource.Dispose();
                });

                await Task.WhenAll(methodTask, getterTask, passThroughTask, disposeTask);

                Assert.True(resource.Payload.IsClosed);
                Assert.True(resource.Payload.IsInvalid);
            }
        }

        [Fact]
        public async Task ConcurrentNonFrozenStruct()
        {
            for (int i = 0; i < 10; i++)
            {
                var resource = new Bindings.NonFrozenStructRequiresMemoryManagement(42);
                var barrier = new Barrier(4);

                var getterTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        Assert.Equal(42, resource.b);
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
                        Assert.Equal(42, resource.getValue());
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
                        var copy = Bindings.MemoryTests.PassThroughNonFrozenStruct(resource);
                        var genericCopy = Bindings.MemoryTests.PassThroughGeneric<NonFrozenStructRequiresMemoryManagement>(resource);
                        copy.Dispose();
                        genericCopy.Dispose();
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Assert.IsType<ObjectDisposedException>(ex);
                    }
                });

                var disposeTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    resource.Dispose();
                });

                await Task.WhenAll(methodTask, getterTask, passThroughTask, disposeTask);

                Assert.True(resource.Payload.IsClosed);
                Assert.True(resource.Payload.IsInvalid);
            }
        }
    }
}
