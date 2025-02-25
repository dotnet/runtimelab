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
            IntPtr payload = (IntPtr)vType.Payload;

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
            // Check the count after release
            Assert.Equal(1, Arc.RetainCount(payload.At(0)));
            Assert.Equal(1, Arc.RetainCount(payloadCopy.At(0)));

            Arc.Release(payload.At(0));

            // Check deinit is called
            Assert.Equal(1, vType.refTypeTest);

            NativeMemory.Free((void*)payloadCopy);
        }

        [Fact]
        private static unsafe void TestDestroyVType()
        {
            Bindings.VType vType = new Bindings.VType();
            GC.SuppressFinalize(vType);
            IntPtr payload = (IntPtr)vType.Payload;

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
            IntPtr payload = (IntPtr)vType.Payload;

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
            IntPtr payload = (IntPtr)vType.Payload;

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
            IntPtr payload = (IntPtr)vType.Payload;

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
            IntPtr payload = (IntPtr)vType.Payload;

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
            IntPtr payload = (IntPtr)vType.Payload;

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
            IntPtr payload = (IntPtr)vType.Payload;

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
            IntPtr payload = (IntPtr)vType.Payload;

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
    }
}
