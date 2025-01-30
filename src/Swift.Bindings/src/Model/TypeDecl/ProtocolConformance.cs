// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;


/// <summary>
/// Represents a protocol conformance.
/// </summary>
/// <param name="TargetType">The type spec of the target type of the conformance</param>
/// <param name="ProtocolSpec">The protocol spec of the conformance</param>
public record ProtocolConformance(
    NamedTypeSpec TargetType,
    NamedTypeSpec ProtocolSpec
);
