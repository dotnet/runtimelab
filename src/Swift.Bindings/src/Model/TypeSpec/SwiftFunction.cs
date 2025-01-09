// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;

namespace BindingsGeneration;

/// <summary>
/// Represents a Swift function signature and its provenance.
/// </summary>
[DebuggerDisplay("{ToString()}")]
public class SwiftFunction {
    /// <summary>
    /// Gets the name of the function
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the provenance of the function
    /// </summary>
    public required Provenance Provenance { get; init; }

    /// <summary>
    /// Gets the parameter list of the function as a tuple
    /// </summary>
    public required TupleTypeSpec ParameterList { get; init; }

    /// <summary>
    /// Gets the generic parameters for this functions
    /// </summary>
    public List<TypeSpec> GenericParameters { get; private set; } = new List<TypeSpec> ();

    /// <summary>
    /// Gets the return type of the function
    /// </summary>
    public required TypeSpec Return { get; init; }

    /// <summary>
    /// Returns true if the give object is a SwiftFunction and matches this
    /// </summary>
    /// <param name="o"></param>
    /// <returns>true if this equals the supplied object</returns>
    public override bool Equals(object? o)
    {
        if (o is SwiftFunction other) {
            return Name == other.Name && Provenance.Equals (other.Provenance) &&
                ParameterList.Equals (other.ParameterList) && Return.Equals (other.Return) && GenericsMatch (other.GenericParameters);
        } else {
            return false;
        }
    }

    /// <summary>
    /// Returns true if and only if the GenericParameters match the given list
    /// </summary>
    /// <param name="otherGenerics"></param>
    /// <returns>true if the generic parameters match</returns>
    bool GenericsMatch (List<TypeSpec> otherGenerics)
    {
        if (GenericParameters.Count != otherGenerics.Count)
            return false;
        for (var i = 0; i < GenericParameters.Count; i++) {
            if (!GenericParameters [i].Equals (otherGenerics [i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns a hashcode for the function
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() => ToString ().GetHashCode ();

    /// <summary>
    /// Returns a string representation of the function
    /// </summary>
    /// <returns>a string representation of the function</returns>
    public override string ToString () => $"{Provenance}.{Name}{GenericParameterString ()}{ParameterList} -> {Return}";
    string GenericParameterString ()
    {
        if (GenericParameters.Count == 0) return "";
        var sb = new StringBuilder ();
        sb.Append("<").AppendJoin (", ", GenericParameters).Append (">");
        return sb.ToString ();
    }
}