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
}