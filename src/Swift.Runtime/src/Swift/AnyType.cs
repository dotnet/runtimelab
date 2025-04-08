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
    private SwiftSafeHandle<AnyType> _payload = SwiftSafeHandle<AnyType>.Zero;
    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        throw new InvalidOperationException("Cannot get type metadata for AnyType");
    }
    public AnyType(SwiftHandle payload)
    {
        _payload = new SwiftSafeHandle<AnyType>(payload);
    }
    public SwiftSafeHandle<AnyType> Payload => _payload;

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
    int ISwiftObject.MarshalToSwift(ref Span<byte> swiftDestSpan)
    {
        throw new InvalidOperationException("Cannot marshal AnyType to Swift");
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
