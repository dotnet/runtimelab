// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using Swift.Runtime;
using Swift.Runtime.InteropServices;

namespace Swift;

/// <summary>
/// Represents a Swift hashable protocol.
/// </summary>
public interface ISwiftHashable { }

/// <summary>
/// Represents a Swift set.
/// </summary>
/// <typeparam name="Element">The element type contained in the set.</typeparam>
public class SwiftSet<Element> : IDisposable, ISwiftObject
{
    static nuint _payloadSize = SwiftObjectHelper<SwiftSet<Element>>.GetTypeMetadata().Size;

    static nuint _elementSize = ElementTypeMetadata.Size;

    private SwiftHandle _payload;

    public SwiftHandle Payload => _payload;

    public IntPtr Buffer => Marshal.PtrToStructure<IntPtr>(_payload.Handle);

    private static Dictionary<Type, string> _protocolConformanceSymbols;

    static SwiftSet()
    {
        _protocolConformanceSymbols = new Dictionary<Type, string>
        {
            { typeof(ISwiftCollection), "$sShyxGSlsMc" }
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_payload.IsInvalid)
        {
            unsafe
            {
                _payload.Dispose();
            }
        }
    }

    ~SwiftSet()
    {
        Dispose(disposing: false);
    }

    public static nuint PayloadSize => _payloadSize;

    public static nuint ElementSize => _elementSize;

    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        var witnessTable = ProtocolWitnessTable.GetOrThrow<Element, ISwiftHashable>();
        return TypeMetadata.Cache.GetOrAdd(typeof(SwiftSet<Element>), _ => SwiftSetPInvokes.PInvoke_getMetadata(TypeMetadataRequest.Complete, ElementTypeMetadata, witnessTable));
    }

    static TypeMetadata ElementTypeMetadata
    {
        get => TypeMetadata.GetTypeMetadataOrThrow<Element>();
    }

    static ISwiftObject ISwiftObject.NewFromPayload(IntPtr handle)
    {
        return new SwiftSet<Element>(handle);
    }

    void ISwiftObject.MarshalToSwift(Span<byte> swiftDestSpan)
    {
        var metadata = SwiftObjectHelper<SwiftSet<Element>>.GetTypeMetadata();
        Debug.Assert((int)metadata.Size == swiftDestSpan.Length, $"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
        unsafe
        {
            fixed (void* swiftDest = swiftDestSpan)
            {
                // Ensure the payload is valid before making copy
                bool _success = false;
                _payload.DangerousAddRef(ref _success);
                metadata.ValueWitnessTable->InitializeWithCopy((void*)swiftDest, (void*)_payload.Handle, metadata);
                if (_success)
                    _payload.DangerousRelease();
            }
        }
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
            throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type SwiftSet and protocol {typeof(TProtocol).Name}, but no conformance was found.");
        }
        return ProtocolConformanceDescriptor.LoadFromSymbol("/usr/lib/swift/libswiftCore.dylib", symbolName);
    }

    /// <summary>
    /// Constructs a new SwiftSet from the given handle.
    /// </summary>
    unsafe SwiftSet(IntPtr handle)
    {
        IntPtr bufferPtr = (IntPtr)NativeMemory.Alloc((nuint)sizeof(IntPtr));
        System.Buffer.MemoryCopy((void*)handle, (void*)bufferPtr, sizeof(IntPtr), sizeof(IntPtr));
        _payload = new SwiftHandle(bufferPtr, SwiftObjectHelper<SwiftSet<Element>>.GetTypeMetadata());
    }

    /// <summary>
    /// Constructs a new empty SwiftSet.
    /// </summary>
    public unsafe SwiftSet()
    {
        var witnessTable = ProtocolWitnessTable.GetOrThrow<Element, ISwiftHashable>();
        var result = SwiftSetPInvokes.Init(ElementTypeMetadata, witnessTable);

        IntPtr bufferPtr = (IntPtr)NativeMemory.Alloc((nuint)sizeof(IntPtr));
        System.Buffer.MemoryCopy((void*)&result, (void*)bufferPtr, sizeof(IntPtr), sizeof(IntPtr));
        _payload = new SwiftHandle(bufferPtr, SwiftObjectHelper<SwiftSet<Element>>.GetTypeMetadata());
    }

    /// <summary>
    /// Gets the number of elements in the set.
    /// </summary>
    public unsafe int Count
    {
        get
        {
            bool _success = false;
            _payload.DangerousAddRef(ref _success);
            var witnessTable = ProtocolWitnessTable.GetOrThrow<Element, ISwiftHashable>();
            int result = (int)SwiftSetPInvokes.Count(this.Buffer, ElementTypeMetadata, witnessTable);
            if (_success)
                _payload.DangerousRelease();
            return result;
        }
    }
}

internal static class SwiftSetPInvokes
{
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sShMa")]
    public static extern TypeMetadata PInvoke_getMetadata(TypeMetadataRequest request, TypeMetadata typeMetadata, ProtocolWitnessTable witnessTable);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sS2hyxGycfC")]
    public static extern IntPtr Init(TypeMetadata elementTypeMetadata, ProtocolWitnessTable witnessTable);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSh5countSivg")]
    public static extern nint Count(IntPtr handle, TypeMetadata elementMetadata, ProtocolWitnessTable witnessTable);
}
