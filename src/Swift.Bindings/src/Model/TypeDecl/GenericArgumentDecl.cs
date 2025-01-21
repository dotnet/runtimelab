// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

/// <summary>
/// Represents a generic argument declaration.
/// </summary>
/// <param name="TypeName">The name of the generic argument type</param>
/// <param name="SugaredTypeName">The sugared name of the generic argument type</param>
/// <param name="Constraints">The constraints of the generic argument type</param>
public record GenericArgumentDecl(
    string TypeName,
    string SugaredTypeName,
    List<Conformance> Constraints
);

/// <summary>
/// Represents a generic argument declaration.
/// </summary>
/// <param name="TargetType">The target type of the conformance</param>
/// <param name="ProtocolName">The protocol name of the conformance</param>
public abstract record Conformance(
    string TargetType,
    string ProtocolName
);

/// <summary>
/// Represents a protocol conformance.
/// </summary>
/// <param name="TargetType">The target type of the conformance</param>
/// <param name="ProtocolName">The protocol name of the conformance</param>
public record ProtocolConformance(
    string TargetType,
    string ProtocolName
) : Conformance(TargetType, ProtocolName);

/// <summary>
/// Represents an associated type conformance.
/// </summary>
/// <param name="TargetType">The target type of the conformance</param>
/// <param name="ProtocolName">The protocol name of the conformance</param>
/// <param name="AssociatedTypeName">The associated type name of the conformance</param>
public record AssociatedTypeConformance(
    string TargetType,
    string ProtocolName,
    string AssociatedTypeName
) : Conformance(TargetType, ProtocolName);
