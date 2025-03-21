// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
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
public class SwiftArray<Element> : IDisposable, ISwiftObject
{
    static nuint _payloadSize = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata().Size;

    static nuint _elementSize = ElementTypeMetadata.Size;

    private SwiftHandle _buffer;

    private bool _disposed = false;

    private static Dictionary<Type, string> _protocolConformanceSymbols;

    static SwiftArray()
    {
        _protocolConformanceSymbols = new Dictionary<Type, string>
        {
            { typeof(ISwiftCollection), "$sSayxGSlsMc" }
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();

            unsafe
            {
                var handle = _buffer.Handle;
                metadata.ValueWitnessTable->Destroy(&handle, metadata);
            }
            _disposed = true;
        }
    }

    ~SwiftArray()
    {
        Dispose(disposing: false);
    }

    public static nuint PayloadSize => _payloadSize;

    public SwiftHandle Payload => _buffer;

    public static nuint ElementSize => _elementSize;

    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Cache.GetOrAdd(typeof(SwiftArray<Element>), _ => SwiftArrayPInvokes.PInvoke_getMetadata(TypeMetadataRequest.Complete, ElementTypeMetadata));
    }

    static TypeMetadata ElementTypeMetadata
    {
        get => TypeMetadata.GetTypeMetadataOrThrow<Element>();
    }

    static ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle handle)
    {
        return new SwiftArray<Element>(handle);
    }

    IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
    {
        var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
        unsafe
        {
            IntPtr handle = _buffer.Handle;
            metadata.ValueWitnessTable->InitializeWithCopy((void*)swiftDest, &handle, metadata);
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
            throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type SwiftArray and protocol {typeof(TProtocol).Name}, but no conformance was found.");
        }
        return ProtocolConformanceDescriptor.LoadFromSymbol("/usr/lib/swift/libswiftCore.dylib", symbolName);
    }

    /// <summary>
    /// Constructs a new SwiftArray from the given handle.
    /// </summary>
    unsafe SwiftArray(SwiftHandle handle)
    {
        _buffer = handle;
    }

    /// <summary>
    /// Constructs a new empty SwiftArray.
    /// </summary>
    public SwiftArray()
    {
        IntPtr handle = SwiftArrayPInvokes.Init(ElementTypeMetadata);
        _buffer = new SwiftHandle(handle);
    }

    /// <summary>
    /// Gets the number of elements in the array.
    /// </summary>
    public unsafe int Count
    {
        get
        {
            return (int)SwiftArrayPInvokes.Count(_buffer.Handle, ElementTypeMetadata);
        }
    }

    /// <summary>
    /// Appends the given element to the array.
    /// </summary>
    public unsafe void Append(Element item)
    {
        IntPtr payload = IntPtr.Zero;
        var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
        try
        {
            payload = (IntPtr)NativeMemory.Alloc(ElementSize);
            SwiftMarshal.MarshalToSwift(item, payload);
            IntPtr handle = _buffer.Handle;
            SwiftArrayPInvokes.Append(payload, metadata, new SwiftSelf((void*)&handle));
            _buffer.Handle = handle;
        }
        finally
        {
            NativeMemory.Free((void*)payload);
        }
    }

    /// <summary>
    /// Inserts the given element at the given index.
    /// </summary>
    public unsafe void Insert(int index, Element item)
    {
        var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
        byte* payload = stackalloc byte[(int)ElementSize];
        SwiftMarshal.MarshalToSwift(item, (IntPtr)payload);
        IntPtr handle = _buffer.Handle;
        SwiftArrayPInvokes.Insert((IntPtr)payload, index, metadata, new SwiftSelf(&handle));
        _buffer.Handle = handle;
    }

    /// <summary>
    /// Removes the element at the given index.
    /// </summary>
    public unsafe void Remove(int index)
    {
        var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
        byte* payload = stackalloc byte[(int)ElementSize];
        SwiftMarshal.MarshalToSwift(index, (IntPtr)payload);

        IntPtr handle = _buffer.Handle;
        SwiftArrayPInvokes.Remove(new SwiftIndirectResult(payload), index, metadata, new SwiftSelf(&handle));
        _buffer.Handle = handle;
    }

    /// <summary>
    /// Removes all elements from the array.
    /// </summary>
    public unsafe void RemoveAll()
    {
        var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
        IntPtr handle = _buffer.Handle;
        SwiftArrayPInvokes.RemoveAll(1, metadata, new SwiftSelf(&handle));
        _buffer.Handle = handle;
    }

    /// <summary>
    /// Gets or sets the element at the given index.
    /// </summary>
    public unsafe Element this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();

            IntPtr payload = (IntPtr)NativeMemory.Alloc(ElementSize);
            SwiftArrayPInvokes.Get(new SwiftIndirectResult((void*)payload), (nint)index, _buffer.Handle, ElementTypeMetadata);
            return SwiftMarshal.MarshalFromSwift<Element>(payload);
        }
        set
        {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();

            var metadata = SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata();
            IntPtr payload = (IntPtr)NativeMemory.Alloc(ElementSize);
            SwiftMarshal.MarshalToSwift(value, payload);

            IntPtr handle = _buffer.Handle;
            SwiftArrayPInvokes.Set(payload, index, metadata, new SwiftSelf(&handle));
            _buffer.Handle = handle;
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
    public static unsafe extern void Get(SwiftIndirectResult result, nint index, IntPtr handle, TypeMetadata elementMetadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSayxSicis")]
    public static unsafe extern void Set(IntPtr value, nint index, TypeMetadata elementMetadata, SwiftSelf self);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa5countSivg")]
    public static extern nint Count(IntPtr handle, TypeMetadata elementMetadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa6appendyyxnF")]
    public static unsafe extern void Append(IntPtr value, TypeMetadata metadata, SwiftSelf self);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa9removeAll15keepingCapacityySb_tF")]
    public static unsafe extern void RemoveAll(byte keepCapacity, TypeMetadata metadata, SwiftSelf self);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa6remove2atxSi_tF")]
    public static unsafe extern void Remove(SwiftIndirectResult result, nint index, TypeMetadata metadata, SwiftSelf self);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa6insert_2atyxn_SitF")]
    public static unsafe extern void Insert(IntPtr value, nint index, TypeMetadata metadata, SwiftSelf self);
}
