// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Test
{
    /// <summary>
    /// Represents a Swift type in C#.
    /// </summary>
    public unsafe interface ISwiftObject
    {
        public static abstract void* Metadata { get; }
    }

    // <summary>
    // Represents Swift UnsafePointer in C#.
    // </summary>
    public readonly unsafe struct UnsafePointer<T> where T : unmanaged
    {
        private readonly T* _pointee;
        public UnsafePointer(T* pointee)
        {
            this._pointee = pointee;
        }

        public T* Pointee => _pointee;

        public static implicit operator T*(UnsafePointer<T> pointer) => pointer.Pointee;

        public static implicit operator UnsafePointer<T>(T* pointee) => new(pointee);
    }

    // <summary>
    // Represents Swift UnsafeMutablePointer in C#.
    // </summary>
    public readonly unsafe struct UnsafeMutablePointer<T> where T : unmanaged
    {
        private readonly T* _pointee;
        public UnsafeMutablePointer(T* pointee)
        {
            _pointee = pointee;
        }

        public T* Pointee => _pointee;

        public static implicit operator T*(UnsafeMutablePointer<T> pointer) => pointer.Pointee;

        public static implicit operator UnsafeMutablePointer<T>(T* pointee) => new(pointee);
    }

    // <summary>
    // Represents Swift UnsafeRawPointer in C#.
    // </summary>
    public readonly unsafe struct UnsafeRawPointer
    {
        private readonly void* _pointee;
        public UnsafeRawPointer(void* pointee)
        {
            _pointee = pointee;
        }

        public void* Pointee => _pointee;

        public static implicit operator void*(UnsafeRawPointer pointer) => pointer.Pointee;

        public static implicit operator UnsafeRawPointer(void* pointee) => new(pointee);
    }

    // <summary>
    // Represents Swift UnsafeMutableRawPointer in C#.
    // </summary>
    public readonly unsafe struct UnsafeMutableRawPointer
    {
        private readonly void* _pointee;
        public UnsafeMutableRawPointer(void* pointee)
        {
            _pointee = pointee;
        }

        public void* Pointee => _pointee;

        public static implicit operator void*(UnsafeMutableRawPointer pointer) => pointer.Pointee;

        public static implicit operator UnsafeMutableRawPointer(void* pointee) => new(pointee);
    }

    // <summary>
    // Represents Swift UnsafeBufferPointer in C#.
    // </summary>
    public readonly unsafe struct UnsafeBufferPointer<T> where T : unmanaged
    {
        private readonly T* _baseAddress;
        private readonly nint _count;
        public UnsafeBufferPointer(T* baseAddress, nint count)
        {
            _baseAddress = baseAddress;
            _count = count;
        }

        public T* BaseAddress => _baseAddress;
        public nint Count => _count;
    }

    // <summary>
    // Represents Swift UnsafeMutableBufferPointer in C#.
    // </summary>
    public readonly unsafe struct UnsafeMutableBufferPointer<T> where T : unmanaged
    {
        private readonly T* _baseAddress;
        private readonly nint _count;
        public UnsafeMutableBufferPointer(T* baseAddress, nint count)
        {
            _baseAddress = baseAddress;
            _count = count;
        }

        public T* BaseAddress => _baseAddress;
        public nint Count => _count;
    }

    // <summary>
    // Represents Swift Foundation.Data in C#.
    // </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    [InlineArray(16)]
    public unsafe struct Data : ISwiftObject
    {
        private byte _payload;

        public unsafe Data(UnsafeRawPointer pointer, nint count)
        {
            this = Foundation.PInvoke_Data_InitWithBytes(pointer, count);
        }

        public byte Payload => _payload;

        public readonly nint Count => Foundation.PInvoke_Data_GetCount(this);

        public unsafe void CopyBytes(UnsafeMutablePointer<byte> buffer, nint count)
        {
            Foundation.PInvoke_Data_CopyBytes(buffer, count, this);
        }

        public static void* Metadata => Foundation.PInvoke_Data_GetMetadata();
    }

    /// <summary>
    /// Represents Swift Foundation.DataProtocol in C#.
    /// </summary>
    public unsafe interface IDataProtocol
    {
        public static void* GetConformanceDescriptor => Runtime.GetConformanceDescriptor("$s10Foundation4DataVAA0B8ProtocolAAMc");
    }

    /// <summary>
    /// Represents Swift Foundation.ContiguousBytes in C#.
    /// </summary>
    public unsafe interface IContiguousBytes
    {
        public static void* GetConformanceDescriptor => Runtime.GetConformanceDescriptor("$s10Foundation4DataVAA15ContiguousBytesAAMc");
    }

    /// <summary>
    /// Swift Foundation PInvoke methods in C#.
    /// </summary>
    public static class Foundation
    {
        public const string Path = "/System/Library/Frameworks/Foundation.framework/Foundation";

        [DllImport(Path, EntryPoint = "$s10Foundation4DataV5bytes5countACSV_SitcfC")]
        [UnmanagedCallConv(CallConvs = [ typeof(CallConvSwift) ])]
        public static unsafe extern Data PInvoke_Data_InitWithBytes(UnsafeRawPointer pointer, nint count);

        [DllImport(Path, EntryPoint = "$s10Foundation4DataV5countSivg")]
        [UnmanagedCallConv(CallConvs = [ typeof(CallConvSwift) ])]
        public static unsafe extern nint PInvoke_Data_GetCount(Data data);

        [DllImport(Path, EntryPoint = "$s10Foundation4DataV9copyBytes2to5countySpys5UInt8VG_SitF")]
        [UnmanagedCallConv(CallConvs = [ typeof(CallConvSwift) ])]
        public static unsafe extern void PInvoke_Data_CopyBytes(UnsafeMutablePointer<byte> buffer, nint count, Data data);

        [DllImport(Path, EntryPoint = "swift_getWitnessTable")]
        [UnmanagedCallConv(CallConvs = [ typeof(CallConvSwift) ])]
        public static unsafe extern void* PInvoke_Swift_GetWitnessTable(void* conformanceDescriptor, void* typeMetadata, void* instantiationArgs);

        [DllImport(Path, EntryPoint = "$s10Foundation4DataVMa")]
        [UnmanagedCallConv(CallConvs = [ typeof(CallConvSwift) ])]
        public static unsafe extern void* PInvoke_Data_GetMetadata();
    }

    /// <summary>
    /// Swift runtime helper methods in C#.
    /// </summary>
    public static class Runtime
    {
        /// <summary>
        /// https://github.com/apple/swift/blob/main/include/swift/ABI/MetadataValues.h#L117
        /// </summary>
        [Flags]
        public enum ValueWitnessFlags
        {
            AlignmentMask = 0x0000FFFF,
            IsNonPOD = 0x00010000,
            IsNonInline = 0x00020000,
            HasSpareBits = 0x00080000,
            IsNonBitwiseTakable = 0x00100000,
            HasEnumWitnesses = 0x00200000,
            Incomplete = 0x00400000,
        }

        /// <summary>
        /// See https://github.com/apple/swift/blob/main/include/swift/ABI/ValueWitness.def
        /// </summary>
        [StructLayout (LayoutKind.Sequential)]
        public ref struct ValueWitnessTable
        {
            public IntPtr InitializeBufferWithCopyOfBuffer;
            public IntPtr Destroy;
            public IntPtr InitWithCopy;
            public IntPtr AssignWithCopy;
            public IntPtr InitWithTake;
            public IntPtr AssignWithTake;
            public IntPtr GetEnumTagSinglePayload;
            public IntPtr StoreEnumTagSinglePayload;
            private IntPtr _Size;
            private IntPtr _Stride;
            public ValueWitnessFlags Flags;
            public uint ExtraInhabitantCount;
            public int Size => _Size.ToInt32();
            public int Stride => _Stride.ToInt32();
            public int Alignment => (int)((Flags & ValueWitnessFlags.AlignmentMask) + 1);
            public bool IsNonPOD => Flags.HasFlag (ValueWitnessFlags.IsNonPOD);
            public bool IsNonBitwiseTakable => Flags.HasFlag (ValueWitnessFlags.IsNonBitwiseTakable);
            public bool HasExtraInhabitants => ExtraInhabitantCount != 0;
        }

        public static unsafe void* GetMetadata<T>(T type) where T: ISwiftObject
        {
            return T.Metadata;
        }

        public static unsafe void* GetValueWitnessTable(void* metadata)
        {
            void* valueWitnessTable = (void*)Marshal.ReadIntPtr((IntPtr)metadata, -IntPtr.Size);
            return valueWitnessTable;
        }

        public static unsafe void* GetConformanceDescriptor(string symbol)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = NativeLibrary.Load(Foundation.Path);
                void* conformanceDescriptor = NativeLibrary.GetExport(handle, symbol).ToPointer();
                return conformanceDescriptor;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get conformance descriptor for symbol: {symbol}", ex);
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    NativeLibrary.Free(handle);
                }
            }
        }
    }
}
