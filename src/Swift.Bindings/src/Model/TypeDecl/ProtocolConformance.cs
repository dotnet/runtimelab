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
    SwiftTypeName Protocol
);

/// <summary>
/// Represents a generic parameter conformance.
/// </summary>
/// <param name="GenericParameter">The generic parameter</param>
/// <param name="Protocol">The protocol that the generic parameter conforms to</param>
public record GenericParameterConformance(
    string GenericParameter,
    SwiftTypeName Protocol
);
