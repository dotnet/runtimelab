// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Swift.Runtime;

namespace BindingsGeneration;

/// <summary>
/// Represents a type within a module, including metadata for interfacing with Swift.
/// </summary>
public record TypeRecord
{
    /// <summary>
    /// The C# namespace of the module.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// The C# type identifier.
    /// </summary>
    public required string CSTypeIdentifier { get; init; }

    /// <summary>
    /// The Swift module name.
    /// </summary>
    public required string ModuleName { get; init; }

    /// <summary>
    /// The Swift type identifier.
    /// </summary>
    public required string SwiftTypeIdentifier { get; init; }

    /// <summary>
    /// The Swift metadata accessor.
    /// </summary>
    public required string MetadataAccessor { get; init; }

    /// <summary>
    /// The Swift runtime type information.
    /// </summary>
    public SwiftTypeInfo? SwiftTypeInfo { get; init; }

    /// <summary>
    /// Indicates if the type is blittable.
    /// </summary>
    public required bool IsBlittable { get; init; }

    /// <summary>
    /// Indicates if the type is frozen.
    /// </summary>
    public required bool IsFrozen { get; init; }
}
