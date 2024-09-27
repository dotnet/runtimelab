// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

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
                ParameterList.Equals (other.ParameterList) && Return.Equals (other.Return);
        } else {
            return false;
        }
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
    public override string ToString () => $"{Provenance}.{Name}{ParameterList} -> {Return}";
}