// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace BindingsGeneration;

public class GenericSignatureParser
{
    /// <summary>
    /// Parses a generic signature and its sugared signature into a list of GenericArgumentDecl.
    /// </summary>
    /// <param name="genericSignature">The generic signature to parse. </param>
    /// <param name="sugaredSignature">The sugared signature to parse. </param>
    /// <returns>A list of GenericArgumentDecl.</returns>
    public static List<GenericArgumentDecl> ParseGenericSignature(string? genericSignature, string? sugaredSignature)
    {
        if (string.IsNullOrWhiteSpace(genericSignature))
            return [];

        if (string.IsNullOrWhiteSpace(sugaredSignature))
            throw new NotImplementedException($"Generic method without sugared signature is not supported.");

        genericSignature = genericSignature[1..^1];
        sugaredSignature = sugaredSignature[1..^1];

        var genericParams = ExtractGenericParams(genericSignature);
        var sugaredParams = ExtractGenericParams(sugaredSignature);

        if (genericParams.Count != sugaredParams.Count)
            throw new InvalidOperationException("Generic and sugared parameter counts do not match.");

        var paramMap = genericParams.Zip(sugaredParams, (gen, sug) => (gen, sug)).ToDictionary(x => x.gen, x => x.sug);

        var constraints = ExtractConstraints(genericSignature);

        return genericParams.Select(typeName =>
            new GenericArgumentDecl(
                typeName,
                paramMap[typeName],
                constraints.Where(c => c.Path[0] == typeName && c.Path.Length == 1).ToList(),
                constraints.Where(c => c.Path[0] == typeName && c.Path.Length > 1).ToList()
            )
        ).ToList();
    }

    /// <summary>
    /// Extracts the generic parameters from a generic signature.
    /// </summary>
    /// <param name="signature">The generic signature to extract parameters from.</param>
    /// <returns>A list of generic parameters.</returns>
    private static List<string> ExtractGenericParams(string signature)
    {
        var whereIndex = signature.IndexOf("where", StringComparison.OrdinalIgnoreCase);
        if (whereIndex >= 0)
            signature = signature[..whereIndex];

        return signature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>
    /// Extracts the constraints from a generic signature.
    /// </summary>
    /// <param name="signature">The generic signature to extract constraints from.</param>
    /// <returns>A list of constraints.</returns>
    private static List<GenericParameterConformance> ExtractConstraints(string signature)
    {
        var whereIndex = signature.IndexOf("where", StringComparison.OrdinalIgnoreCase);
        if (whereIndex == -1)
            return new List<GenericParameterConformance>();

        var constraintsSection = signature[(whereIndex + "where".Length)..];
        var constraints = constraintsSection.Split(',');

        var parsedConstraints = constraints
            .Select(ParseConstraint)
            .Cast<GenericParameterConformance>();

        return [.. parsedConstraints];
    }

    /// <summary>
    /// Parses a constraint clause into a Conformance object.
    /// </summary>
    /// <param name="clause">The constraint clause to parse.</param>
    /// <returns>A Conformance object. Null if the constraint refers to the associated type.</returns>
    private static GenericParameterConformance? ParseConstraint(string clause)
    {
        var parts = clause.Split(new[] { ":", "==" }, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Invalid constraint clause: {clause}");
        }

        var target = parts[0].Split('.');
        var conformanceTarget = parts[1];

        ConformanceKind kind = clause.Contains(":") ? ConformanceKind.Protocol : ConformanceKind.ConcreteType;
        return new GenericParameterConformance(target, SwiftTypeName.FromModuleQualifiedName(conformanceTarget), kind);
    }
}
