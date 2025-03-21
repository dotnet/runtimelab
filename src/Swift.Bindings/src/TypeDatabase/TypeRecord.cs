// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Swift.Runtime;

namespace BindingsGeneration;

/// <summary>
/// Represents type's flags.
/// </summary>
[Flags]
public enum TypeRecordFlags
{
    None = 0,
    // This flag is used in tooling to determine whether the type should be enregistered.
    // A type marked as "frozen" on the Swift side doesn't change its layout.
    // However, if it contains a non-frozen struct as a property, it is considered an opaque at compile-time;
    // otherwise, the layout is considered as known at compile-time and enregistration if possible.
    Frozen = 1 << 0,
    // This flag is used in tooling to determine whether a type requires memory management,
    // ensuring that the finalizer can handle memory if needed.
    // The 'RequiresMemoryManagement' flag indicates that the type is allocated on the heap (as in the case of classes)
    // or that it contains a heap-allocated property (for example, a struct with a reference property).
    RequiresMemoryManagement = 1 << 1,
}

/// <summary>
/// Represents a type kind.
/// </summary>
public enum TypeRecordKind
{
    Struct,
    Enum,
    Class,
    Protocol,
}

/// <summary>
/// Represents a type within a module, including metadata for interfacing with Swift.
/// </summary>
public record TypeRecord
{
    /// <summary>
    /// The C# type information.
    /// </summary>
    public required CSharpTypeName CSharpTypeName { get; init; }

    /// <summary>
    /// The Swift type identifier.
    /// </summary>
    public required SwiftTypeName SwiftTypeName { get; init; }

    /// <summary>
    /// The Swift metadata accessor.
    /// </summary>
    public required string MetadataAccessor { get; init; }

    /// <summary>
    /// The Swift runtime type information.
    /// </summary>
    public SwiftTypeInfo? SwiftTypeInfo { get; init; }

    /// <summary>
    /// Type flags.
    /// </summary>
    public required TypeRecordFlags Flags { get; init; }

    /// <summary>
    /// The kind of type.
    /// </summary>
    public required TypeRecordKind Kind { get; init; }
}
