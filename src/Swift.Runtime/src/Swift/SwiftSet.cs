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
/// Represents a Swift hashable protocol.
/// </summary>
public interface ISwiftHashable { }

[StructLayout(LayoutKind.Sequential)]
public struct SetBuffer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Variant
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct VariantObject
        {
            public IntPtr _rawValue;
        }
        public VariantObject _object;
    }
    public Variant _buffer;
}

/// <summary>
/// Represents a Swift set.
/// </summary>
/// <typeparam name="Element">The element type contained in the set.</typeparam>
public class SwiftSet<Element> : IDisposable, ISwiftObject
{
    static nuint _payloadSize = SwiftObjectHelper<SwiftSet<Element>>.GetTypeMetadata().Size;

    static nuint _elementSize = ElementTypeMetadata.Size;

    private SetBuffer _variant;

    private SwiftHandle _refPayload;

    public SwiftHandle RefPayload => _refPayload;

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
        if (!_refPayload.IsInvalid)
        {
            unsafe
            {
                fixed (void* payload = &_variant)
                {
                    // Debug.Assert(_refPayload.Handle == (IntPtr)payload, "RefPayload should be the same as &_buffer");
                }

                _refPayload.SetMetadata(SwiftObjectHelper<SwiftArray<Element>>.GetTypeMetadata());

                // Pin the payload to prevent it from being moved by the GC
                int size = Marshal.SizeOf<ArrayBuffer>();
                IntPtr pPinned = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(_variant, pPinned, false);
                    _refPayload.Handle = pPinned;
                    _refPayload.Dispose();
                }
                finally
                {
                    Marshal.FreeHGlobal(pPinned);
                }
            }
        }
    }

    ~SwiftSet()
    {
        Dispose(disposing: false);
    }

    public static nuint PayloadSize => _payloadSize;

    public SetBuffer Payload => _variant;

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

    IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
    {
        var metadata = SwiftObjectHelper<SwiftSet<Element>>.GetTypeMetadata();

        unsafe
        {
            // Use localPayload to pin the payload and prevent it from being moved by the GC
            SetBuffer localPayload = _variant;
            metadata.ValueWitnessTable->InitializeWithCopy((void*)swiftDest, &localPayload, metadata);
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
            throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type SwiftSet and protocol {typeof(TProtocol).Name}, but no conformance was found.");
        }
        return ProtocolConformanceDescriptor.LoadFromSymbol("/usr/lib/swift/libswiftCore.dylib", symbolName);
    }

    /// <summary>
    /// Constructs a new SwiftSet from the given handle.
    /// </summary>
    unsafe SwiftSet(IntPtr handle)
    {
        _variant = *(SetBuffer*)handle;
        fixed (void* _payloadPtr = &_variant)
        {
            _refPayload = new SwiftHandle((IntPtr)_payloadPtr);
        }
    }

    /// <summary>
    /// Constructs a new empty SwiftSet.
    /// </summary>
    public unsafe SwiftSet()
    {
        var witnessTable = ProtocolWitnessTable.GetOrThrow<Element, ISwiftHashable>();
        _variant = SwiftSetPInvokes.Init(ElementTypeMetadata, witnessTable);
        fixed (void* _payloadPtr = &_variant)
        {
            _refPayload = new SwiftHandle((IntPtr)_payloadPtr);
        }
    }

    /// <summary>
    /// Gets the number of elements in the set.
    /// </summary>
    public unsafe int Count
    {
        get
        {
            var witnessTable = ProtocolWitnessTable.GetOrThrow<Element, ISwiftHashable>();
            return (int)SwiftSetPInvokes.Count(_variant, ElementTypeMetadata, witnessTable);
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
    public static extern SetBuffer Init(TypeMetadata elementTypeMetadata, ProtocolWitnessTable witnessTable);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSh5countSivg")]
    public static extern nint Count(SetBuffer handle, TypeMetadata elementMetadata, ProtocolWitnessTable witnessTable);
}
