namespace BindingsGeneration;

/// <summary>
/// Represents a generic argument declaration.
/// </summary>
/// <param name="TargetType">The target type of the conformance</param>
/// <param name="ProtocolSpec">The protocol spec of the conformance</param>
public abstract record Conformance(
    string TargetType,
    NamedTypeSpec ProtocolSpec
);

/// <summary>
/// Represents a protocol conformance.
/// </summary>
/// <param name="TargetType">The target type of the conformance</param>
/// <param name="ProtocolSpec">The protocol spec of the conformance</param>
public record ProtocolConformance(
    string TargetType,
    NamedTypeSpec ProtocolSpec
) : Conformance(TargetType, ProtocolSpec);

/// <summary>
/// Represents an associated type conformance.
/// </summary>
/// <param name="Path">The path to the associated type</param>
/// <param name="ProtocolSpec">The protocol spec of the conformance</param>
public record AssociatedTypeConformance(
    string Path,
    NamedTypeSpec ProtocolSpec
) : Conformance(Path, ProtocolSpec);
