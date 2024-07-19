// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace BindingsGeneration;

public abstract class TypeSpec
{
	protected TypeSpec(TypeSpecKind kind)
	{
		Kind = kind;
		GenericParameters = new List<TypeSpec>();
		Attributes = new List<TypeSpecAttribute>();
	}

	public TypeSpecKind Kind { get; private set; }
	public List<TypeSpec> GenericParameters { get; private set; }
	public bool ContainsGenericParameters { get { return GenericParameters.Count != 0; } }
	public List<TypeSpecAttribute> Attributes { get; private set; }
	public bool HasAttributes { get { return Attributes.Count != 0; } }
	public bool IsInOut { get; set; }
	public bool IsAny { get; set; }
	public virtual bool IsEmptyTuple { get { return false; } }
	protected abstract string LLToString(bool useFullName);
	protected virtual string LLFinalStringParts() { return ""; }
	protected abstract bool LLEquals(TypeSpec? other, bool partialNameMatch);
	public string? TypeLabel { get; set; }
	public bool IsArray
	{
		get
		{
			return this is NamedTypeSpec ns && ns.Name == "Swift.Array";
		}
	}

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

	public TypeSpec NonReferenceCloneOf()
	{
		if (!IsInOut)
			return this;
		var ty = MemberwiseClone() as TypeSpec;
		ty!.IsInOut = false;
		return ty!;
	}

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

	public override int GetHashCode()
	{
		return ToString().GetHashCode();
	}

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

	public static bool IsNullOrEmptyTuple(TypeSpec? spec)
	{
		return spec is null || spec.IsEmptyTuple;
	}

	public static bool BothNullOrEqual(TypeSpec? one, TypeSpec? two)
	{
		if (one is null && two is null)
			return true;
		if (one is null || two is null)
			return false;
		return one.Equals(two);
	}

	public bool ContainsBoundGenericClosure()
	{
		return ContainsBoundGenericClosure(0);
	}

	bool ContainsBoundGenericClosure(int depth)
	{
		if (this is NamedTypeSpec namedTypeSpec)
		{
			foreach (var subSpec in namedTypeSpec.GenericParameters)
			{
				if (subSpec.ContainsBoundGenericClosure(depth + 1))
					return true;
			}
		}
		else if (this is TupleTypeSpec tupleSpec)
		{
			foreach (var subSpec in tupleSpec.Elements)
			{
				if (subSpec.ContainsBoundGenericClosure(depth + 1))
					return true;
			}
		}
		else if (this is ClosureTypeSpec closureSpec)
		{
			return depth > 0;
		}
		return false;
	}

	public override string ToString()
	{
		return ToString(true);
	}

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

	static string[] intNames = {
			"Swift.Int", "Swift.UInt", "Swift.Int8", "Swift.UInt8",
			"Swift.Int16", "Swift.UInt16", "Swift.Int32", "Swift.UInt32",
			"Swift.Int64", "Swift.UInt64", "Swift.Char"
		};

	public static bool IsIntegral(TypeSpec ts)
	{
		return ts is NamedTypeSpec named && Array.IndexOf(intNames, named.Name) >= 0;
	}

	public static bool IsFloatingPoint(TypeSpec ts)
	{
		if (ts is NamedTypeSpec named)
		{
			return named.Name == "Swift.Float" || named.Name == "Swift.Double" || named.Name == "CoreGraphics.CGFloat";
		}
		return false;
	}

	public static bool IsBoolean(TypeSpec ts)
	{
		return ts is NamedTypeSpec named && named.Name == "Swift.Bool";
	}

	public static bool IsBuiltInValueType(TypeSpec ts)
	{
		return IsIntegral(ts) || IsFloatingPoint(ts) || IsBoolean(ts);
	}

	public bool IsDynamicSelf
	{
		get
		{
			return this is NamedTypeSpec ns && ns.Name == "Self";
		}
	}

	public abstract bool HasDynamicSelf
	{
		get;
	}

	public static bool AnyHasDynamicSelf(List<TypeSpec> types)
	{
		return types.Any(t => t.HasDynamicSelf);
	}

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