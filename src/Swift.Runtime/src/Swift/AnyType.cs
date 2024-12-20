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
}
