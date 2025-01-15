// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dynamo;

namespace Dynamo.CSLang;

/// <summary>
/// This represents a single 'using` statement in C#.
/// </summary>
public class CSUsing : SimpleLineElement {
    /// <summary>
    /// Creates a new using statement for the specified package.
    /// </summary>
    /// <param name="package">The namespace of the package</param>
    public CSUsing (string package)
        : base ($"using {package};", indent: false, prependIndents: false, allowSplit: false)
    {
        Package = package;
    }

    /// <summary>
    /// Gets the namespace of the package.
    /// </summary>
    public string Package { get; private set; }
}

/// <summary>
/// Represents a collection of using statements in C#.
/// </summary>
public class CSUsingPackages : CodeElementCollection<CSUsing> {
    /// <summary>
    /// Constructs a new empty collection of using statements.
    /// </summary>
    public CSUsingPackages () : base () { }

    /// <summary>
    /// Constructs a new collection of using statements with the specified using statements.
    /// </summary>
    /// <param name="use">the CSUsing statements to add</param>
    public CSUsingPackages (params CSUsing [] use)
        : this ()
    {
        AddRange (use);
    }

    /// <summary>
    /// Constructs a new collection of using statements with the specified namespaces
    /// </summary>
    /// <param name="use">The namespaces to add</param>
    public CSUsingPackages (params string [] use)
        : this ()
    {
        AddRange (use.Select (s => new CSUsing (s)));
    }

    /// <summary>
    /// A fluent interface to add a using package. This can be used in the
    /// form of: packages.And(someUsing0).And(someUsing1)...
    /// </summary>
    /// <param name="use">A new package to add</param>
    /// <returns>The modified collection</returns>
    public CSUsingPackages And (CSUsing use)
    {
        Add (use);
        return this;
    }

    /// <summary>
    /// A fluent interface to add a using package. This can be used in the
    /// form of: packages.And("System").And("System.Runtime")...
    /// </summary>
    /// <param name="namespace">the namespace to add</param>
    /// <returns>The modified collection</returns>
    public CSUsingPackages And (string @namespace) { return And (new CSUsing (@namespace)); }

    /// <summary>
    /// Adds a using statement if it is not already present in the collection.
    /// </summary>
    /// <param name="">the namespace to add</param>
    public void AddIfNotPresent (string @namespace)
    {
        var target = new CSUsing (@namespace);
        if (!this.Exists (use => use.Contents == target.Contents)) {
            Add (target);
        }
    }

    /// <summary>
    /// Adds a using statement using the provided Type if it is not already present in the collection.
    /// </summary>
    /// <param name="type">A type whose namespace will be used in a using statement</param>
    public void AddIfNotPresent (Type type)
    {
        AddIfNotPresent (type.Namespace!);
    }
}

