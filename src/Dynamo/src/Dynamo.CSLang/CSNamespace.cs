// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dynamo;

namespace Dynamo.CSLang;

/// <summary>
/// Represents a C# namespace declaration using the non-block syntax.
/// </summary>
public class CSNamespace : SimpleLineElement
{
    /// <summary>
    /// Constructs a new namespace declaration.
    /// </summary>
    /// <param name="namespace">the namespace that will be declared</param>
    public CSNamespace(string @namespace)
        : base($"namespace {@namespace};", indent: false, prependIndents: true, allowSplit: false)
    {
        Namespace = @namespace;
    }

    /// <summary>
    /// Gets the namespace that is declared.
    /// </summary>
    public string Namespace { get; private set; }
}
