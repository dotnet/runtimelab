// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;


/// <summary>
/// Represents a generic parameter name mapping.
/// </summary>
/// <param name="TypeParameter">The name of the generic type parameter e.g. T0.</param>
public record struct GenericParameterCSName(string TypeParameter);

/// <summary>
/// Provides methods for generating names.
/// <summary>
/// 
public static class NameProvider
{
    // Dictionary of Swift property names that need to be renamed in C#
    // Temporary workaround for https://github.com/dotnet/runtimelab/issues/2997 to keep StoreKit tests passing
    private static readonly Dictionary<string, string> PropertyNameMappings = new()
    {
        { "isEligibleForIntroOffer", "isEligibleForIntroOfferProperty" },
        { "status", "statusProperty"}
    };

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
    /// Provides the mangled name of the PInvoke method.
    /// </summary>
    /// <param name="methodDecl">The method declaration.</param>
    /// <returns>The mangled name of the PInvoke method.</returns>
    public static string GetMangledName(MethodDecl methodDecl)
    {
        if (methodDecl.IsAsync)
            return $"{methodDecl.MangledName}_async";

        return methodDecl.MangledName;
    }

    /// <summary>
    /// Provides the name of the interface based on a protocol name.
    /// </summary>
    /// <param name="protocolName">The protocol name.</param>
    /// <returns>The name of the interface.</returns>
    public static string GetInterfaceName(string protocolName)
    {
        return $"ISwift{protocolName}";
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
                TypeParameter: $"T{x.i}"
            ));

    public static string GetMetadataName(string typeName) => $"{typeName}Metadata";
    public static string GetPayloadName(string argumentName) => $"{argumentName}Payload";
    public static string GetProtocolWitnessTableName(string typeName, string protocolName) => $"{typeName}{protocolName}PWT";

    /// <summary>
    /// Maps visibility to C# access modifier keyword.
    /// </summary>
    public static string GetAccessModifier(Visibility visibility) => visibility switch
    {
        Visibility.Public => "public",
        Visibility.Private => "private",
        _ => throw new ArgumentException($"Unknown visibility: {visibility}")
    };

    /// <summary>
    /// Gets the C# property name for a given Swift property name, handling reserved keywords and special cases.
    /// </summary>
    /// <param name="swiftPropertyName">The original Swift property name.</param>
    /// <returns>The appropriate C# property name.</returns>
    public static string GetPropertyName(string swiftPropertyName) =>
        PropertyNameMappings.TryGetValue(swiftPropertyName, out var mappedName)
            ? mappedName
            : swiftPropertyName;

    /// <summary>
    /// Gets the C# variable name for the buffer of a bound generic type.
    /// </summary>
    public static string GetBoundGenericBufferName(string typeName) => $"{typeName}Buffer";
}
