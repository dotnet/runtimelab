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
/// Represents a Swift collection protocol.
/// </summary>
public interface ISwiftCollection { }

/// <summary>
/// Represents a Swift array.
/// </summary>
/// <typeparam name="Element">The element type contained in the array.</typeparam>
public class SwiftArray<Element> : ISwiftObject
{
    static nuint _payloadSize = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata().Size;

    static nuint _elementSize = ElementTypeMetadata.Size;

    private SwiftSafeHandle<SwiftArray<Element>> _payload;

    public SwiftSafeHandle<SwiftArray<Element>> Payload => _payload;

    public unsafe IntPtr PayloadBuffer => *(IntPtr*)_payload.Handle;

    private static Dictionary<Type, string> _protocolConformanceSymbols;

    static SwiftArray()
    {
        _protocolConformanceSymbols = new Dictionary<Type, string>
        {
            { typeof(ISwiftCollection), "$sSayxGSlsMc" }
        };
    }

    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Cache.GetOrAdd(typeof(SwiftArray<Element>), _ => SwiftArrayPInvokes.PInvoke_getMetadata(TypeMetadataRequest.Complete, ElementTypeMetadata));
    }

    static TypeMetadata ElementTypeMetadata
    {
        get => TypeMetadata.GetTypeMetadataOrThrow<Element>();
    }

    static ISwiftObject ISwiftObject.NewFromPayload(IntPtr handle)
    {
        return new SwiftArray<Element>(handle);
    }

    void ISwiftObject.MarshalToSwift(Span<byte> swiftDestSpan)
    {
        var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
        Debug.Assert((int)metadata.Size == swiftDestSpan.Length, $"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
        unsafe
        {
            fixed (void* swiftDest = swiftDestSpan)
            {
                // Ensure the payload is valid before making copy
                bool _success = false;
                _payload.DangerousAddRef(ref _success);
                metadata.ValueWitnessTable->InitializeWithCopy(swiftDest, _payload, metadata);
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
            throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type SwiftArray and protocol {typeof(TProtocol).Name}, but no conformance was found.");
        }
        return ProtocolConformanceDescriptor.LoadFromSymbol("/usr/lib/swift/libswiftCore.dylib", symbolName);
    }

    /// <summary>
    /// Constructs a new SwiftArray from the given handle.
    /// </summary>
    unsafe SwiftArray(IntPtr handle)
    {
        IntPtr bufferPtr = (IntPtr)NativeMemory.Alloc((nuint)sizeof(IntPtr));
        *(IntPtr*)bufferPtr = *(IntPtr*)handle;
        _payload = new SwiftSafeHandle<SwiftArray<Element>>(bufferPtr);
    }

    /// <summary>
    /// Constructs a new empty SwiftArray.
    /// </summary>
    public unsafe SwiftArray()
    {
        IntPtr result = SwiftArrayPInvokes.Init(ElementTypeMetadata);
        IntPtr bufferPtr = (IntPtr)NativeMemory.Alloc((nuint)sizeof(IntPtr));
        *(IntPtr*)bufferPtr = result;
        _payload = new SwiftSafeHandle<SwiftArray<Element>>(bufferPtr);
    }

    /// <summary>
    /// Gets the number of elements in the array.
    /// </summary>
    public int Count
    {
        get
        {
            bool _success = false;
            _payload.DangerousAddRef(ref _success);
            try
            {
                int result = (int)SwiftArrayPInvokes.Count(PayloadBuffer, ElementTypeMetadata);
                return result;
            }
            finally
            {
                if (_success)
                    _payload.DangerousRelease();
            }
        }
    }

    /// <summary>
    /// Appends the given element to the array.
    /// </summary>
    public unsafe void Append(Element item)
    {
        var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
        bool _success = false;
        _payload.DangerousAddRef(ref _success);
        try
        {
            byte* payload = stackalloc byte[(int)_elementSize];
            Span<byte> span = new Span<byte>(payload, (int)_elementSize);
            SwiftMarshal.MarshalToSwift(item, span);
            SwiftArrayPInvokes.Append((IntPtr)payload, metadata, new SwiftSelf(_payload));
        }
        finally
        {
            if (_success)
                _payload.DangerousRelease();
        }
    }

    /// <summary>
    /// Inserts the given element at the given index.
    /// </summary>
    public unsafe void Insert(int index, Element item)
    {
        bool _success = false;
        _payload.DangerousAddRef(ref _success);
        try
        {
            var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
            byte* payload = stackalloc byte[(int)_elementSize];
            Span<byte> span = new Span<byte>(payload, (int)_elementSize);
            SwiftMarshal.MarshalToSwift(item, span);
            SwiftArrayPInvokes.Insert((IntPtr)payload, index, metadata, new SwiftSelf(_payload));
        }
        finally
        {
            if (_success)
                _payload.DangerousRelease();
        }
    }

    /// <summary>
    /// Removes the element at the given index.
    /// </summary>
    public unsafe void Remove(int index)
    {
        bool _success = false;
        _payload.DangerousAddRef(ref _success);
        try
        {
            var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
            byte* payload = stackalloc byte[(int)_elementSize];
            SwiftArrayPInvokes.Remove(new SwiftIndirectResult(payload), index, metadata, new SwiftSelf(_payload));
        }
        finally
        {
            if (_success)
                _payload.DangerousRelease();
        }
    }

    /// <summary>
    /// Removes all elements from the array.
    /// </summary>
    public unsafe void RemoveAll()
    {
        bool _success = false;
        _payload.DangerousAddRef(ref _success);
        try
        {
            var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
            SwiftArrayPInvokes.RemoveAll(1, metadata, new SwiftSelf(_payload));
        }
        finally
        {
            if (_success)
                _payload.DangerousRelease();
        }
    }

    /// <summary>
    /// Gets or sets the element at the given index.
    /// </summary>
    public unsafe Element this[int index]
    {
        get
        {
            bool _success = false;
            _payload.DangerousAddRef(ref _success);
            try
            {
                byte* payload = stackalloc byte[(int)_elementSize];
                SwiftArrayPInvokes.Get(new SwiftIndirectResult(payload), (nint)index, PayloadBuffer, ElementTypeMetadata);
                return SwiftMarshal.MarshalFromSwift<Element>((IntPtr)payload);
            }
            finally
            {
                if (_success)
                    _payload.DangerousRelease();
            }
        }
        set
        {
            bool _success = false;
            _payload.DangerousAddRef(ref _success);
            try
            {
                var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
                byte* payload = stackalloc byte[(int)_elementSize];
                Span<byte> span = new Span<byte>(payload, (int)_elementSize);
                SwiftMarshal.MarshalToSwift(value, span);
                SwiftArrayPInvokes.Set((IntPtr)payload, index, metadata, new SwiftSelf(_payload));
            }
            finally
            {
                if (_success)
                    _payload.DangerousRelease();
            }
        }
    }
}

internal static class SwiftArrayPInvokes
{
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSaMa")]
    public static extern TypeMetadata PInvoke_getMetadata(TypeMetadataRequest request, TypeMetadata typeMetadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sS2ayxGycfC")]
    public static extern IntPtr Init(TypeMetadata typeMetadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSayxSicig")]
    public static extern void Get(SwiftIndirectResult result, nint index, IntPtr handle, TypeMetadata elementMetadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSayxSicis")]
    public static extern void Set(IntPtr value, nint index, TypeMetadata elementMetadata, SwiftSelf self);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa5countSivg")]
    public static extern nint Count(IntPtr handle, TypeMetadata elementMetadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa6appendyyxnF")]
    public static extern void Append(IntPtr value, TypeMetadata metadata, SwiftSelf self);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa9removeAll15keepingCapacityySb_tF")]
    public static extern void RemoveAll(byte keepCapacity, TypeMetadata metadata, SwiftSelf self);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa6remove2atxSi_tF")]
    public static extern void Remove(SwiftIndirectResult result, nint index, TypeMetadata metadata, SwiftSelf self);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa6insert_2atyxn_SitF")]
    public static extern void Insert(IntPtr value, nint index, TypeMetadata metadata, SwiftSelf self);
}
