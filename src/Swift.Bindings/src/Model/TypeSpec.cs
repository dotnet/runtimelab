// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace BindingsGeneration;

/// <summary>
/// Represents a reference to a swift type which will be used (at least) in the following places:
/// - field declarations
/// - argument declarations
/// - property declarations
/// - subscript declarations
/// - bound generic types
/// - generic where clauses
/// - return types
/// - type aliases
/// </summary>
public abstract class TypeSpec
{
	protected TypeSpec(TypeSpecKind kind)
	{
		Kind = kind;
		GenericParameters = new List<TypeSpec>();
		Attributes = new List<TypeSpecAttribute>();
	}

	/// <summary>
	/// Used to identify what kind of TypeSpec we're looking at
	/// </summary>
	public TypeSpecKind Kind { get; private set; }

	/// <summary>
	/// A collection of generic parameters for this type. Elements may represent
	/// bound generic parameters or unbound. The binding state for NamedTypeSpec isn't known without
	/// the context in which the TypeSpec is used.
	/// </summary>
	public List<TypeSpec> GenericParameters { get; private set; }

	/// <summary>
	/// Returns true if there are generic parameters
	/// </summary>
	public bool ContainsGenericParameters { get { return GenericParameters.Count != 0; } }

	/// <summary>
	/// Returns a list of attributes for the TypeSpec
	/// </summary>
	public List<TypeSpecAttribute> Attributes { get; private set; }

	/// <summary>
	/// Returns true if the TypeSpec has attributes
	/// </summary>
	public bool HasAttributes { get { return Attributes.Count != 0; } }

	/// <summary>
	/// Returns true if the type is an in/out paraemter. This will typically only appear in 
	/// TupleTypeSpec that are used in closures
	/// </summary>
	public bool IsInOut { get; set; }

	/// <summary>
	/// Returns true if the TypeSpec is marked with "any" as in public func Foo(a: any SomeProtocol)
	/// </summary>
	public bool IsAny { get; set; }

	/// <summary>
	/// Returns true if the TypeSpec is an empty tuple
	/// </summary>
	public virtual bool IsEmptyTuple { get { return false; } }

	/// <summary>
	/// Abstract method to generate the String representation of the TypeSpec. useFullName
	/// will determine if the and NamedTypeSpec objects contained within with be printed with their
	/// full names.
	/// </summary>
	protected abstract string LLToString(bool useFullName);

	/// <summary>
	/// Returns any final string to add to the end of the string representation
	/// </summary>
	protected virtual string LLFinalStringParts() { return ""; }

	/// <summary>
	/// Returns compare to another TypeSpec for equality with an option partially match the name
	/// </summary>
	protected abstract bool LLEquals(TypeSpec? other, bool partialNameMatch);

	/// <summary>
	/// Returns an optional label on the type. This is commonly used in tuple elements in
	/// closures.
	/// </summary>
	public string? TypeLabel { get; set; }

	/// <summary>
	/// Returns true if the type is an array
	/// </summary>
	public bool IsArray
	{
		get
		{
			return this is NamedTypeSpec ns && ns.Name == "Swift.Array";
		}
	}

	/// <summary>
	/// Returns true if this TypeSpec equals the other. This is deep equality.
	/// </summary>
	public override bool Equals(object? obj)
	{
		if (obj is TypeSpec spec)
		{
			if (Kind != spec.Kind)
				return false;
			if (!ListEqual(GenericParameters, spec.GenericParameters, false))
				return false;
			if (IsInOut != spec.IsInOut)
				return false;
			// Don't compare IsAny - it's really not important (yet)
			return LLEquals(spec, false);
		}
		return false;
	}

	/// <summary>
	/// Returns true if this TypeSpec matches spec, but do partial matches on the name
	/// </summary>
	public bool EqualsPartialMatch(TypeSpec spec)
	{
		if (spec == null)
			return false;
		if (Kind != spec.Kind)
			return false;
		if (!ListEqual(GenericParameters, spec.GenericParameters, true))
			return false;
		if (IsInOut != spec.IsInOut)
			return false;
		// Don't compare IsAny - it's really not important (yet)
		return LLEquals(spec, true);
	}

	/// <summary>
	/// Returns true if the types match ignoring differences on references
	/// </summary>
	public virtual bool EqualsReferenceInvaraint(TypeSpec type)
	{
		var a = ProjectAsNonReference(this);
		var b = ProjectAsNonReference(type);

		if (b.Kind != a.Kind)
			return false;
		if (b.GetType() != a.GetType())
			return false;
		// shouldn't do Name equality except in functions
		return a.LLEquals(b, false);
	}

	/// <summary>
	/// Returns a copy of this type spec with IsInOut set to false
	/// </summary>
	public TypeSpec NonReferenceCloneOf()
	{
		if (!IsInOut)
			return this;
		var ty = MemberwiseClone() as TypeSpec;
		ty!.IsInOut = false;
		return ty!;
	}

	/// <summary>
	/// Returns a copy of this TypeSpec will **all** references removed, including
	/// removing Swift pointer types.
	/// </summary>
	static TypeSpec ProjectAsNonReference(TypeSpec a)
	{
		if (a.IsInOut)
		{
			return a.NonReferenceCloneOf();
		}
		if (a is NamedTypeSpec namedType && namedType.GenericParameters.Count == 1)
		{
			if (namedType.Name == "Swift.UnsafePointer" || namedType.Name == "Swift.UnsafeMutablePointer")
				return namedType.GenericParameters[0];
		}
		return a;
	}

	/// <summary>
	/// Returns a hash value for the type
	/// </summary>
	public override int GetHashCode()
	{
		return ToString().GetHashCode();
	}

	/// <summary>
	/// Returns true if two lists of TypeSpec match
	/// </summary>
	protected static bool ListEqual(List<TypeSpec> one, List<TypeSpec> two, bool partialNameMatch)
	{
		if (one.Count != two.Count)
			return false;
		for (int i = 0; i < one.Count; i++)
		{
			if (partialNameMatch)
			{
				if (!one[i].EqualsPartialMatch(two[i]))
					return false;
			}
			else
			{
				if (!one[i].Equals(two[i]))
					return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Returns true if `spec` is null or an empty tuple
	/// </summary>
	public static bool IsNullOrEmptyTuple(TypeSpec? spec)
	{
		return spec is null || spec.IsEmptyTuple;
	}

	/// <summary>
	/// Returns true if both TypeSpecs are null or if not, if they are equal
	/// </summary>
	public static bool BothNullOrEqual(TypeSpec? one, TypeSpec? two)
	{
		if (one is null && two is null)
			return true;
		if (one is null || two is null)
			return false;
		return one.Equals(two);
	}
	
	/// <summary>
	/// Returns a string representation of the TypeSpec
	/// </summary>
	public override string ToString()
	{
		return ToString(true);
	}

	/// <summary>
	/// Returns a string representation of the type spec with an option to have full names or not
	/// </summary>
	public string ToString(bool useFullNames)
	{
		StringBuilder builder = new StringBuilder();

		foreach (var attr in Attributes)
		{
			builder.Append(attr.ToString());
			builder.Append(' ');
		}
		if (IsInOut)
			builder.Append("inout ");

		if (IsAny)
			builder.Append("any ");

		if (TypeLabel is not null)
		{
			builder.Append(TypeLabel).Append(": ");
		}
		builder.Append(LLToString(useFullNames));

		if (ContainsGenericParameters)
		{
			builder.Append('<');
			for (int i = 0; i < GenericParameters.Count; i++)
			{
				if (i > 0)
					builder.Append(", ");
				builder.Append(GenericParameters[i].ToString(useFullNames));
			}
			builder.Append('>');
		}
		builder.Append(LLFinalStringParts());

		return builder.ToString();
	}

	/// <summary>
	/// Returns true if the type is "Self" (aka, dynamic self)
	/// </summary>
	public bool IsDynamicSelf
	{
		get
		{
			return this is NamedTypeSpec ns && ns.Name == "Self";
		}
	}

	/// <summary>
	/// Returns true if the type contains a dynamic self. This is useful to determine if
	/// any element of a protocol uses dynamic self (and is therefore a protocol with associated types)
	/// </summary>
	public abstract bool HasDynamicSelf
	{
		get;
	}

	/// <summary>
	/// Returns true if any of the types in the list have dynamic self
	/// </summary>
	public static bool AnyHasDynamicSelf(List<TypeSpec> types)
	{
		return types.Any(t => t.HasDynamicSelf);
	}

	/// <summary>
	/// Replaces the name toFind anywhere in the TypeSpec with the replacement string,
	/// returning a new TypeSpec and leaving the old one unchanged. This is useful for
	/// undoing typealias declarations
	/// </summary>
	public TypeSpec ReplaceName(string toFind, string replacement)
	{
		var result = this;
		if (!String.IsNullOrEmpty(replacement))
			ReplaceName(this, toFind, replacement, ref result);
		return result;
	}

	static bool ReplaceName(TypeSpec original, string toFind, string replacement, ref TypeSpec result)
	{
		result = original;
		var changed = false;
		switch (original.Kind)
		{
			case TypeSpecKind.Named:
				changed = ReplaceName((original as NamedTypeSpec)!, toFind, replacement, ref result);
				break;
			case TypeSpecKind.ProtocolList:
				changed = ReplaceName((original as ProtocolListTypeSpec)!, toFind, replacement, ref result);
				break;
			case TypeSpecKind.Closure:
				changed = ReplaceName((original as ClosureTypeSpec)!, toFind, replacement, ref result);
				break;
			case TypeSpecKind.Tuple:
				changed = ReplaceName((original as TupleTypeSpec)!, toFind, replacement, ref result);
				break;
			default:
				throw new ArgumentOutOfRangeException($"Unknown TypeSpec kind {original.Kind}");
		}
		if (changed)
		{
			result.Attributes.AddRange(original.Attributes);
			result.TypeLabel = original.TypeLabel;
			result.IsInOut = original.IsInOut;
			result.IsAny = original.IsAny;
		}
		return changed;
	}

	static bool ReplaceName(NamedTypeSpec named, string toFind, string replacement, ref TypeSpec result)
	{
		result = named;
		var changed = false;
		if (named.Name == toFind)
		{
			changed = true;
			result = new NamedTypeSpec(replacement);
		}
		var resultGenerics = new List<TypeSpec>(named.GenericParameters.Count);
		var changedGenerics = ReplaceName(named.GenericParameters, toFind, replacement, resultGenerics);

		if (changedGenerics)
		{
			if (!changed)
			{
				result = new NamedTypeSpec(named.Name);
			}
			result.GenericParameters.AddRange(resultGenerics);
		}
		else
		{
			if (changed)
				result.GenericParameters.AddRange(named.GenericParameters);
		}
		return changed || changedGenerics;
	}

	static bool ReplaceName(List<TypeSpec> originalTypes, string toFind, string replacement, List<TypeSpec> resultTypes)
	{
		var changed = false;
		foreach (var type in originalTypes)
		{
			var result = type;
			changed = ReplaceName(type, toFind, replacement, ref result) || changed;
			resultTypes.Add(result);
		}
		return changed;
	}

	static bool ReplaceName(TupleTypeSpec tuple, string toFind, string replacement, ref TypeSpec result)
	{
		List<TypeSpec> resultTypes = new List<TypeSpec>(tuple.Elements.Count);
		if (ReplaceName(tuple.Elements, toFind, replacement, resultTypes))
		{
			result = new TupleTypeSpec(resultTypes);
			return true;
		}
		result = tuple;
		return false;
	}

	static bool ReplaceName(ProtocolListTypeSpec protolist, string toFind, string replacement, ref TypeSpec result)
	{
		var originalProtos = new List<TypeSpec>(protolist.Protocols.Count);
		var resultProtos = new List<TypeSpec>(protolist.Protocols.Count);
		originalProtos.AddRange(protolist.Protocols.Keys);
		if (ReplaceName(originalProtos, toFind, replacement, resultProtos))
		{
			result = new ProtocolListTypeSpec(resultProtos.OfType<NamedTypeSpec>());
			return true;
		}
		return false;
	}

	static bool ReplaceName(ClosureTypeSpec closure, string toFind, string replacement, ref TypeSpec result)
	{
		var resultArgs = closure.Arguments;
		var resultReturn = closure.ReturnType;

		var argsChanged = ReplaceName(closure.Arguments, toFind, replacement, ref resultArgs);
		var returnChanged = ReplaceName(closure.ReturnType, toFind, replacement, ref resultReturn);
		if (argsChanged || returnChanged)
		{
			result = new ClosureTypeSpec(resultArgs, resultReturn);
			return true;
		}
		return false;
	}
}