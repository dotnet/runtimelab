// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;


/// <summary>
/// Represents a generic parameter name mapping.
/// </summary>
/// <param name="PlaceholderName">The name of the generic type parameter e.g. T0.</param>
/// <param name="MetadataName">The name of the metadata type parameter.</param>
/// <param name="PayloadName">The name of the payload type parameter. </param>
public record struct GenericParameterCSName(string PlaceholderName, string MetadataName, string PayloadName);

/// <summary>
/// Provides methods for generating names.
/// <summary>
/// 
public static class NameProvider
{
    /// <summary>
    /// Provides the name of the PInvoke method.
    /// </summary>
    /// <param name="methodDecl">The method declaration.</param>
    /// <returns>The name of the PInvoke method.</returns>
    public static string GetPInvokeName(MethodDecl methodDecl)
    {
        return $"PInvoke_{methodDecl.Name}";
    }

    /// <summary>
    /// Provides the mapping of generic type parameters.
    /// </summary>
    /// <param name="methodDecl">The method declaration.</param>
    /// <returns>The mapping of generic type parameters.</returns>
    public static Dictionary<string, GenericParameterCSName> GetGenericTypeMapping(MethodDecl methodDecl) =>
        methodDecl.GenericParameters
            .Select((param, i) => (param, i))
            .ToDictionary(x => x.param.TypeName, x => new GenericParameterCSName(
                PlaceholderName: $"T{x.i}",
                MetadataName: $"T{x.i}Metadata",
                PayloadName: $"T{x.i}Payload"
            ));
}
