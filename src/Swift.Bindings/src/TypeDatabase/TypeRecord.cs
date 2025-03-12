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
    Frozen = 1 << 0,
    HeapAllocated = 1 << 1,
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
    Tuple
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
