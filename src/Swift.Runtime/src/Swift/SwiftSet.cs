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

    private static Dictionary<Type, string> _protocolConformanceSymbols;

    private readonly object _syncLock = new object();

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
                var metadata = SwiftObjectHelper<SwiftSet<Element>>.GetTypeMetadata();
                // Don't set the metadata, and let Dispose call Destroy when the handle is closed
                // _payload.SetMetadata(SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata());

                _payload.Dispose();
                if (_payload.IsClosed && !_payload.IsInvalid)
                {
                    lock(_syncLock)
                    {
                        var handle = _payload.Handle;
                        metadata.ValueWitnessTable->Destroy(&handle, metadata);
                        _payload.Handle = IntPtr.Zero;
                    }
                }
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
                lock(_syncLock)
                {
                    var handle = _payload.Handle;
                    metadata.ValueWitnessTable->InitializeWithCopy((void*)swiftDest, &handle, metadata);
                }
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
        _payload = new SwiftHandle(handle);
    }

    /// <summary>
    /// Constructs a new empty SwiftSet.
    /// </summary>
    public unsafe SwiftSet()
    {
        var witnessTable = ProtocolWitnessTable.GetOrThrow<Element, ISwiftHashable>();
        var handle = SwiftSetPInvokes.Init(ElementTypeMetadata, witnessTable);
        _payload = new SwiftHandle(handle);
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
            int result = (int)SwiftSetPInvokes.Count(_payload.Handle, ElementTypeMetadata, witnessTable);
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
