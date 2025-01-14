// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime;

/// <summary>
/// Represents a type that can be marshaled into Swift
/// </summary>
public interface ISwiftObject
{
    /// <summary>
    /// Returns the TypeMetadata for this object
    /// </summary>
    /// <returns>A type metadata object for the type.</returns>
    public static abstract TypeMetadata GetTypeMetadata();

    /// <summary>  
    /// Creates a new Swift object from a given payload
    /// </summary>
    public static abstract ISwiftObject NewFromPayload(SwiftHandle payload);

    /// <summary>
    /// Marshals this object to a Swift destination
    /// </summary>
    unsafe IntPtr MarshalToSwift(IntPtr swiftDest);
}

/// <summary>  
/// Helper class for Swift invoking ISwiftObject static methods
/// </summary>
public struct SwiftObjectHelper<T> where T : ISwiftObject
{
    /// <summary>
    /// Returns the TypeMetadata for T
    /// </summary>
    /// <returns>the TypeMetadata for T</returns>
    public static TypeMetadata GetTypeMetadata()
    {
        return TypeMetadata.Cache.GetOrAdd(typeof(T), _ => T.GetTypeMetadata());
    }

    /// <summary>
    /// Creates a new Swift object from a given payload
    /// </summary>
    /// <param name="payload"></param>
    /// <returns>a new ISwiftObject</returns>
    public static ISwiftObject NewFromPayload(SwiftHandle payload)
    {
        return T.NewFromPayload(payload);
    }
}
