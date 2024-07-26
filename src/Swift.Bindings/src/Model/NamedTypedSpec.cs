// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

/// <summary>
/// Represents a TypeSpec for a nominal type. This will include classes, structs, actors, and protocols.
/// </summary>
public class NamedTypeSpec : TypeSpec
{
	/// <summary>
	/// Constructs a new type spec from the given name. It's encouraged that the name be fully qualified.
	/// </summary>
    public NamedTypeSpec(string name)
        : base(TypeSpecKind.Named)
    {
        name = UnTick(name);
        // For whatever reason, Any and AnyObject are not
        // strictly in the Swift module. But they are.
        // But they're not.
        // What do I mean by this?
        // Apple's demangler will print these as Swift.Any or
        // Swift.AnyObject if the options are set to print
        // fully qualified names, so I feel no remorse for doing
        // this.
        if (name == "Any")
            name = "Swift.Any";
        else if (name == "AnyObject")
            name = "Swift.AnyObject";
        Name = name;
    }

	/// <summary>
	/// Constructs a new type spec from the given name and generic arguments.
	/// </summary>
    public NamedTypeSpec(string name, params TypeSpec[] genericSpecialization)
        : this(name)
    {
        GenericParameters.AddRange(genericSpecialization);
    }

	/// <summary>
	/// Gets or sets an inner type for the type spec
	/// </summary>
    public NamedTypeSpec? InnerType { get; set; }

    public bool IsProtocolList { get { return Name == "protocol"; } }
    public string Name { get; private set; }

    protected override string LLToString(bool useFullName)
    {
        return useFullName ? Name : NameWithoutModule;
    }

    protected override string LLFinalStringParts()
    {
        if (InnerType is null)
            return "";
        return "." + InnerType;
    }

    protected override bool LLEquals(TypeSpec? other, bool partialNameMatch)
    {
        if (other is NamedTypeSpec spec)
        {
            var innersMatch = (InnerType is null && spec.InnerType is null) ||
                (InnerType is not null && InnerType.LLEquals(spec.InnerType, partialNameMatch));
            if (partialNameMatch)
            {
                return NameWithoutModule == spec.NameWithoutModule && innersMatch;
            }
            else
            {
                return Name == spec.Name && innersMatch;
            }
        }
        return false;
    }

	/// <summary>
	/// Returns true if the TypeSpec contains a module
	/// </summary>
    public bool HasModule()
    {
        // note that this will fail if the type is a protocol with associated types full path,
        // but that can only be determined with a larger context which we don't have (yet)
        // the extra context is the context within which this type spec is defined and will need a type mapper.
        return Name.Contains(".");
    }

	/// <summary>
	/// Returns the module component if present, empty string otherwise.
	/// </summary>
    public string Module
    {
        get
        {
            return HasModule() ? Name.Substring(0, Name.IndexOf('.')) : "";
        }
    }

	/// <summary>
	/// Returns the name of the type spec without a module (if present)
	/// </summary>
    public string NameWithoutModule
    {
        get
        {
            return Name.IndexOf('.') >= 0 ? Name.Substring(Name.IndexOf('.') + 1) : Name;
        }
    }

	/// <summary>
	/// Returns true if the name is "Self" or if any of the generic parameters contains dynamic self
	/// </summary>
    public override bool HasDynamicSelf
    {
        get
        {
            if (Name == "Self")
                return true;
            return TypeSpec.AnyHasDynamicSelf(GenericParameters);
        }
    }

    static string UnTick(string str)
    {
        // a back-ticked string will start and end with `
        // the swift grammar guarantees this.
        // Identifier :
        // Identifier_head Identifier_characters?
        // | OpBackTick Identifier_head Identifier_characters? OpBackTick
        // | ImplicitParameterName
        // There will be no starting and ending whitespace.
        //
        // There are some edge cases that we can take advantage of:
        // 1. If it starts with `, it *has* to end with back tick, so we don't need to check
        // 2. `` will never exist, so the minimum length *has* to be 3
        // In generalized string manipulation, we couldn't make these assumptions,
        // but in this case the grammar works for us.
        // first weed out the easy cases:
        // null, too short, does start and end with back tick
        // then just substring it
        if (str.Length < 3 || str[0] != '`')
            return str;
        return str.Substring(1, str.Length - 2);
    }
}