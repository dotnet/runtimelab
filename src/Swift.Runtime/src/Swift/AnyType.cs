// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Swift.Runtime;

#nullable enable

namespace Swift;

/// <summary>
/// Represents placeholder for Swift type
/// </summary>
public struct AnyType : ISwiftObject
{
    private SwiftHandle<AnyType> _payload = SwiftHandle<AnyType>.Zero;
    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        throw new InvalidOperationException("Cannot get type metadata for AnyType");
    }
    public AnyType(IntPtr payload)
    {
        _payload = new SwiftHandle<AnyType>(payload);
    }
    public SwiftHandle<AnyType> Payload => _payload;

    /// <summary>
    /// Creates a new SwiftOptional from a Swift payload
    /// </summary>
    static ISwiftObject ISwiftObject.NewFromPayload(IntPtr payload)
    {
        return new AnyType(payload);
    }

    /// <summary>
    /// Marshals this object to a Swift destination
    /// </summary>
    /// <param name="swiftDestSpan"></param>
    /// <returns></returns>
    void ISwiftObject.MarshalToSwift(Span<byte> swiftDestSpan)
    {
        var metadata = SwiftObjectHelper<AnyType>.GetTypeMetadata();
        Debug.Assert((int)metadata.Size == swiftDestSpan.Length, $"Span size does not match type size, Expected: {(int)metadata.Size}, Actual: {swiftDestSpan.Length}");
        if (!metadata.IsValid)
        {
            throw new InvalidOperationException("Cannot marshal AnyType to Swift without metadata");
        }
        if (_payload == SwiftHandle<AnyType>.Zero)
        {
            throw new InvalidOperationException("Cannot marshal AnyType to Swift without payload");
        }
        unsafe
        {
            fixed (byte* swiftDest = swiftDestSpan)
            {
                metadata.ValueWitnessTable->InitializeWithCopy((void*)swiftDest, (void*)_payload.Handle, metadata);
            }
        }
    }

    /// <summary>
    /// Gets the protocol conformance descriptor for the given type
    /// </summary>
    /// <typeparam name="TProtocol"></typeparam>
    /// <returns></returns>
    static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>()
        where TProtocol : class
    {
        return ProtocolConformanceDescriptor.Zero;
    }
}
