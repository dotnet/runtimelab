// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Swift.Runtime;

#nullable enable

namespace Swift;

/// <summary>
/// Represents placeholder for Swift type
/// </summary>
public struct AnyType : ISwiftObject {
    private SwiftHandle _payload = SwiftHandle.Zero;
    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Zero;
    }
    public AnyType(SwiftHandle payload)
    {
        _payload = payload;
    }
    public SwiftHandle Payload => _payload;

    /// <summary>
    /// Creates a new SwiftOptional from a Swift payload
    /// </summary>
    static ISwiftObject ISwiftObject.NewFromPayload(IntPtr payload)
    {
        return new AnyType(new SwiftHandle(payload));
    }

    /// <summary>
    /// Marshals this object to a Swift destination
    /// </summary>
    /// <param name="swiftDest"></param>
    /// <returns></returns>
    IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
    {
        var metadata = SwiftObjectHelper<AnyType>.GetTypeMetadata();
        if (!metadata.IsValid)
        {
            throw new InvalidOperationException("Cannot marshal AnyType to Swift without metadata");
        }
        if (_payload == SwiftHandle.Zero)
        {
            throw new InvalidOperationException("Cannot marshal AnyType to Swift without payload");
        }
        unsafe {
            metadata.ValueWitnessTable->InitializeWithCopy((void *)swiftDest, (void *)_payload, metadata);
        }
        return swiftDest;
    }

}
