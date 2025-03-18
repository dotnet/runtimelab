// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;


/// <summary>
/// Represents a protocol conformance.
/// </summary>
/// <param name="ConformingType">The type that conforms to the protocol</param>
/// <param name="Protocol">The protocol that the type conforms to</param>
public record TypeConformance(
    SwiftTypeName ConformingType,
    SwiftTypeName Protocol,
    string ProtocolConformanceDescriptor
);

/// <summary>
/// Represents the kind of conformance.
/// </summary>
public enum ConformanceKind
{
    Protocol,
    ConcreteType
}

/// <summary>
/// Represents a generic parameter conformance.
/// </summary>
/// <param name="path">The path to the generic parameter</param>
/// <param name="ConformanceTarget">The type that conforms to the protocol</param>
/// <param name="Kind">The kind of conformance</param>
public record GenericParameterConformance(
    string[] Path,
    SwiftTypeName ConformanceTarget,
    ConformanceKind Kind
);
