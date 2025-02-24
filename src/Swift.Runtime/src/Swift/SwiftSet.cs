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
/// Represents a Swift set payload.
///
/// The following diagram illustrates the hierarchy of a Swift set type.
/// The actual implementation may differ:
///
///    struct Set
///    +-----------------------------------------------------------------------+
///    |   struct Variant                                                     |
///    |   +--------------------------------------------------------------+   |
///    |   |   struct Object                                              |   |
///    |   |   +------------------------------------------------------+   |   |
///    |   |   | var rawValue: IntPtr                                 |   |   |
///    |   |   +------------------------------------------------------+   |   |
///    |   +--------------------------------------------------------------+   |
///    +-----------------------------------------------------------------------+
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 8)]
public struct Variant
{
    public IntPtr rawValue;
}

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

    private Variant _variant;

    private static Dictionary<Type, string> _protocolConformanceSymbols;

    static SwiftSet()
    {
        _protocolConformanceSymbols = new Dictionary<Type, string>
        {
            { typeof(ISwiftCollection), "$sShyxGSlsMc" }
        };
    }

    public unsafe void Dispose()
    {
        if (_variant.rawValue != IntPtr.Zero)
        {
            // TODO: https://github.com/dotnet/runtimelab/issues/2851
            Arc.Release(_variant.rawValue);
            _variant.rawValue = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    unsafe ~SwiftSet()
    {
        if (_variant.rawValue != IntPtr.Zero)
        {
            // TODO: https://github.com/dotnet/runtimelab/issues/2851
            Arc.Release(_variant.rawValue);
            _variant.rawValue = IntPtr.Zero;
        }
    }

    public static nuint PayloadSize => _payloadSize;

    public Variant Payload => _variant;

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

    static ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle handle)
    {
        return new SwiftSet<Element>(handle);
    }

    IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
    {
        var metadata = SwiftObjectHelper<SwiftSet<Element>>.GetTypeMetadata();
        unsafe
        {
            fixed (void* _payloadPtr = &_variant)
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
            throw new SwiftRuntimeException($"Attempted to retrieve protocol conformance descriptor for type SwiftSet and protocol {typeof(TProtocol).Name}, but no conformance was found.");
        }
        return ProtocolConformanceDescriptor.LoadFromSymbol("/usr/lib/swift/libswiftCore.dylib", symbolName);
    }

    /// <summary>
    /// Constructs a new SwiftSet from the given handle.
    /// </summary>
    unsafe SwiftSet(SwiftHandle handle)
    {
        this._variant = *(Variant*)(handle);
    }

    /// <summary>
    /// Constructs a new empty SwiftSet.
    /// </summary>
    public SwiftSet()
    {
        var witnessTable = ProtocolWitnessTable.GetOrThrow<Element, ISwiftHashable>();
        _variant = SwiftSetPInvokes.Init(ElementTypeMetadata, witnessTable);
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
    public static extern Variant Init(TypeMetadata elementTypeMetadata, ProtocolWitnessTable witnessTable);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSh5countSivg")]
    public static extern nint Count(Variant handle, TypeMetadata elementMetadata, ProtocolWitnessTable witnessTable);
}
