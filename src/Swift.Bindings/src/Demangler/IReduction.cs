// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration.Demangling;

/// <summary>
/// Represents the result of reducing a Node or tree of nodes
/// </summary>
public interface IReduction {
    string Symbol { get; init; }
}

/// <summary>
/// Represents an error in an attempted reduction
/// </summary>
public class ReductionError : IReduction {
    public required string Symbol { get; init; }
    /// <summary>
    /// Returns an error message describing the error
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Represents a reduction that reduces to a single type
/// </summary>
public class TypeSpecReduction : IReduction {
    public required string Symbol { get; init; }
    /// <summary>
    /// Returns a TypeSpec for the type that this node represents
    /// </summary>
    public required TypeSpec TypeSpec { get; init; }
}

/// <summary>
/// Represents a reduction that reduces to a Swift function
/// </summary>
public class FunctionReduction : IReduction {
    public required string Symbol { get; init; }
    /// <summary>
    /// Returns a function that this type represents
    /// </summary>
    public required SwiftFunction Function { get; init; }
}

public class ProtocolWitnessTableReduction : IReduction {
    public required string Symbol { get; init; }
    public required NamedTypeSpec ImplementingType { get; init; }
    public required NamedTypeSpec ProtocolType { get; init;}
}