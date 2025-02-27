// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

/// <summary>
/// Represents a generic argument declaration.
/// </summary>
/// <param name="TypeName">The name of the generic argument type</param>
/// <param name="SugaredTypeName">The sugared name of the generic argument type</param>
/// <param name="GenericConformances">The conformances of the generic argument type</param>
/// <param name="AssosiatedTypeConformances">The conformances of the associated types of the generic argument type</param>
public record GenericArgumentDecl(
    string TypeName,
    string SugaredTypeName,
    List<GenericParameterConformance> GenericConformances,
    List<GenericParameterConformance> AssosiatedTypeConformances
);
