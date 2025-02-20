// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Swift;
using Swift.Runtime;


/// <summary>
/// Represents Swift Foundation.DataProtocol in C#.
/// </summary>
public interface ISwiftDataProtocol { }

/// <summary>
/// Represents Swift Foundation.ContiguousBytes in C#.
/// </summary>
public interface ISwiftContiguousBytes { }

/// <summary>
/// Represents Foundation.Data type.
/// </summary>
public struct Data : ISwiftObject
{
    private long _flags;
    private IntPtr _object;

    private static nuint _payloadSize = SwiftObjectHelper<Data>.GetTypeMetadata().Size;

    private static Dictionary<Type, string> _protocolConformanceSymbols;

    static Data()
    {
        _protocolConformanceSymbols = new Dictionary<Type, string> {
            { typeof(ISwiftDataProtocol), "$s10Foundation4DataVAA0B8ProtocolAAMc" },
            { typeof(ISwiftContiguousBytes), "$s10Foundation4DataVAA15ContiguousBytesAAMc" },
        };
    }

    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Cache.GetOrAdd(typeof(Data), _ => PInvoke_getMetadata());
    }

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftFoundation, EntryPoint = "$s10Foundation4DataVMa")]
    public static unsafe extern TypeMetadata PInvoke_getMetadata();

    static unsafe ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle handle)
    {
        return new Data(handle);
    }

    IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
    {
        var metadata = SwiftObjectHelper<Data>.GetTypeMetadata();
        unsafe
        {
            fixed (void* _payloadPtr = &this)
            {
                metadata.ValueWitnessTable->InitializeWithCopy((void*)swiftDest, (void*)_payloadPtr, metadata);
            }
        }
        return swiftDest;
    }

    /// <summary>
    /// Gets the protocol conformance descriptor for the given type.
    /// </summary>
    /// <typeparam name="TProtocol"></typeparam>
    /// <returns></returns>
    static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>()
        where TProtocol : class
    {
        if (!_protocolConformanceSymbols.TryGetValue(typeof(TProtocol), out var symbolName))
        {
            throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type Data and protocol {typeof(TProtocol).Name}, but no conformance was found.");
        }
        return ProtocolConformanceDescriptor.LoadFromSymbol(KnownLibraries.SwiftFoundation, symbolName);
    }

    /// <summary>
    /// Constructs a new Data from the given handle.
    /// </summary>
    unsafe Data(SwiftHandle handle)
    {
        this = *(Data*)handle;
    }

    /// <summary>
    /// Constructs a new Data from the given buffer.
    /// </summary>
    public unsafe Data(UnsafeRawPointer pointer, nint count)
    {
        this = PInvoke_InitWithBytes(pointer, count);
    }

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftFoundation, EntryPoint = "$s10Foundation4DataV5bytes5countACSV_SitcfC")]
    public static unsafe extern Data PInvoke_InitWithBytes(UnsafeRawPointer pointer, nint count);

    public readonly nint Count => PInvoke_GetCount(this);


    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftFoundation, EntryPoint = "$s10Foundation4DataV5countSivg")]
    public static unsafe extern nint PInvoke_GetCount(Data data);

    public unsafe void CopyBytes(UnsafeMutablePointer<byte> buffer, nint count)
    {
        PInvoke_CopyBytes(buffer, count, this);
    }

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftFoundation, EntryPoint = "$s10Foundation4DataV9copyBytes2to5countySpys5UInt8VG_SitF")]
    public static unsafe extern void PInvoke_CopyBytes(UnsafeMutablePointer<byte> buffer, nint count, Data data);
}
