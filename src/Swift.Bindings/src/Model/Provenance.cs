// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace BindingsGeneration;

/// <summary>
/// Represents the origin of a particular function
/// </summary>
public class Provenance {
    private Provenance() { }

    /// <summary>
    /// If the provenance is in instance, return the type
    /// </summary>
    public NamedTypeSpec? InstanceType { get; private set; }

    /// <summary>
    /// If the provenance is an extension, return type it is an extension on
    /// </summary>
    public NamedTypeSpec? ExtensionOn { get; private set; }

    /// <summary>
    /// If the provenance is top level, return the module
    /// </summary>
    public string? Module {get; private set; }

    /// <summary>
    /// Construct a new instance provenance from the given NamedTypeSpec
    /// </summary>
    /// <param name="ns"></param>
    /// <returns></returns>
    public static Provenance Instance (NamedTypeSpec ns) {
        return new Provenance () { InstanceType = ns };
    }

    /// <summary>
    /// Construct a new extension provenance from the given NamedTypeSpec
    /// </summary>
    /// <param name="ns"></param>
    /// <returns></returns>
    public static Provenance Extension (NamedTypeSpec ns) {
        return new Provenance () { ExtensionOn = ns };
    }

    /// <summary>
    /// Construct a new top-level provenance from the given module name
    /// </summary>
    /// <param name="module"></param>
    /// <returns></returns>
    public static Provenance TopLevel (string module) {
        return new Provenance () { Module = module };;
    }

    /// <summary>
    /// Returns true if and only if the provenance is an instance
    /// </summary>
    [MemberNotNullWhen(true, nameof(InstanceType))]
    public bool IsInstance => InstanceType is not null;

    /// <summary>
    /// Returns true if and only if the provenance is an extension
    /// </summary>
    [MemberNotNullWhen(true, nameof(ExtensionOn))]
    public bool IsExtension => ExtensionOn is not null;

    /// <summary>
    /// Returns true if and only if the provenance is top level
    /// </summary>
    [MemberNotNullWhen(true, nameof(Module))]
    public bool IsTopLevel => Module is not null;

    /// <summary>
    /// Returns true if o is a Provenance and equals this one
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public override bool Equals(object? o)
    {
        if (o is Provenance other) {
            return (IsInstance && InstanceType.Equals (other.InstanceType)) ||
                (IsExtension && ExtensionOn.Equals (other.ExtensionOn)) ||
                (IsTopLevel && Module == other.Module);
        }
        return false;
    }

    /// <summary>
    /// Returns a hashcode for this object
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode () => ToString ().GetHashCode ();

    /// <summary>
    /// Returns a string representation of the provenance
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override string ToString()
    {
        if (IsInstance) {
            return InstanceType.ToString ();
        } else if (IsExtension) {
            return ExtensionOn.ToString ();
        } else if (IsTopLevel) {
            return Module.ToString ();
        }
        throw new NotImplementedException("Unknown provenance");
    }
}