// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

/// <summary>
/// Represents a Swift type name.
/// </summary>
public record SwiftTypeName
{
    /// <summary>
    /// The module name.
    /// </summary>
    public string Module { get; }

    /// <summary>
    /// The type name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The module-qualified type name. Includes all parent types.
    /// </summary>
    public string ModuleQualifiedName { get; }

    /// <inheritdoc />
    public override string ToString() => ModuleQualifiedName;

    private SwiftTypeName(string module, string name, string moduleQualifiedName)
    {
        Module = module;
        Name = name;
        ModuleQualifiedName = moduleQualifiedName;
    }

    /// <summary>
    /// Creates a new SwiftTypeName from a module-qualified name.
    /// </summary>
    /// <param name="moduleQualifiedName">The module-qualified name.</param>
    /// <returns>The SwiftTypeName.</returns>
    public static SwiftTypeName FromModuleQualifiedName(string moduleQualifiedName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleQualifiedName, nameof(moduleQualifiedName));

        var parts = SplitModuleQualifiedName(moduleQualifiedName); // TODO: Remove this once bound generic are correctly handled
        if (parts.Count < 2)
        {
            throw new ArgumentException($"Invalid module-qualified name: {moduleQualifiedName}");
        }

        return new SwiftTypeName(parts.First(), parts.Last(), moduleQualifiedName);
    }

    /// <summary>
    /// Swift type name for void.
    /// </summary>
    public static readonly SwiftTypeName VoidType = new SwiftTypeName(string.Empty, "()", "()");

    /// <summary>
    /// Swift type name for Any.
    /// </summary>
    public static readonly SwiftTypeName AnyType = new SwiftTypeName(string.Empty, "Any", "Any");

    //TODO: Remove SplitModuleQualifiedName once we handle bound generics.

    /// <summary>
    /// Splits a module-qualified name into its parts respecting angle brackets. If the name is "Swift.Array<Swift.String>", the parts will be "Swift" and "Array<Swift.String>".
    /// </summary>
    /// <param name="qualifiedName">The module-qualified name.</param>
    /// <returns>The parts of the module-qualified name.</returns>
    private static List<string> SplitModuleQualifiedName(string qualifiedName)
    {
        var parts = new List<string>();
        int start = 0;
        int bracketLevel = 0;

        for (int i = 0; i < qualifiedName.Length; i++)
        {
            char c = qualifiedName[i];

            if (c == '<')
            {
                // Entering a generic section.
                bracketLevel++;
            }
            else if (c == '>')
            {
                // Exiting a generic section.
                bracketLevel--;
            }
            else if (c == '.' && bracketLevel == 0)
            {
                // Only split on a period if we're not inside generic brackets.
                parts.Add(qualifiedName.Substring(start, i - start));
                start = i + 1;
            }
        }

        // Add the final part.
        if (start < qualifiedName.Length)
        {
            parts.Add(qualifiedName.Substring(start));
        }

        return parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
    }
}
