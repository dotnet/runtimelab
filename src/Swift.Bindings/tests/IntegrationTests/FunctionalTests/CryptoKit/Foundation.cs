// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using Swift;
using Swift.Runtime;

namespace BindingsGeneration.FunctionalTests
{
    /// <summary>
    /// Represents a Swift type in C#.
    /// </summary>
    public unsafe interface ISwiftObject
    {
        public static abstract TypeMetadata Metadata { get; }
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

        public static TypeMetadata Metadata => Foundation.PInvoke_Data_GetMetadata();
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
        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        public static unsafe extern Data PInvoke_Data_InitWithBytes(UnsafeRawPointer pointer, nint count);

        [DllImport(Path, EntryPoint = "$s10Foundation4DataV5countSivg")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        public static unsafe extern nint PInvoke_Data_GetCount(Data data);

        [DllImport(Path, EntryPoint = "$s10Foundation4DataV9copyBytes2to5countySpys5UInt8VG_SitF")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        public static unsafe extern void PInvoke_Data_CopyBytes(UnsafeMutablePointer<byte> buffer, nint count, Data data);

        [DllImport(Path, EntryPoint = "swift_getWitnessTable")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        public static unsafe extern void* PInvoke_Swift_GetWitnessTable(void* conformanceDescriptor, TypeMetadata typeMetadata, void* instantiationArgs);

        [DllImport(Path, EntryPoint = "$s10Foundation4DataVMa")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
        public static unsafe extern TypeMetadata PInvoke_Data_GetMetadata();
    }

    /// <summary>
    /// Swift runtime helper methods in C#.
    /// </summary>
    public static class Runtime
    {
        public static unsafe TypeMetadata GetMetadata<T>(T type) where T : ISwiftObject
        {
            return T.Metadata;
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
