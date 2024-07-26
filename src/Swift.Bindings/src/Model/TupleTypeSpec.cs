// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace BindingsGeneration;

/// <summary>
/// Represents a tuple of arbitrary length
/// </summary>
public class TupleTypeSpec : TypeSpec
{
	/// <summary>
	/// Constructs an empty tuple
	/// </summary>
    public TupleTypeSpec()
        : base(TypeSpecKind.Tuple)
    {
    }

	/// <summary>
	/// Constructs a shallow copy of the the other type spec
	/// </summary>
    public TupleTypeSpec(TupleTypeSpec other)
        : base(TypeSpecKind.Tuple)
    {
        Elements.AddRange(other.Elements);
        if (other.HasAttributes)
            Attributes.AddRange(other.Attributes);
        if (other.ContainsGenericParameters)
            GenericParameters.AddRange(other.GenericParameters);
        IsInOut = other.IsInOut;
    }

	/// <summary>
	/// Constructs a tuple with the provided elements
	/// </summary>
    public TupleTypeSpec(IEnumerable<TypeSpec> elements)
        : this()
    {
        Elements.AddRange(elements);
    }

	/// <summary>
	/// Constructs a single type spec
	/// </summary>
    public TupleTypeSpec(TypeSpec single)
        : this()
    {
        Elements.Add(single);
    }

	/// <summary>
	/// Returns the elements of the tuple
	/// </summary>
    public List<TypeSpec> Elements { get; private set; } = new List<TypeSpec>();

    protected override string LLToString(bool useFullName)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append('(');
        for (int i = 0; i < Elements.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");
            builder.Append(Elements[i].ToString(useFullName));
        }
        builder.Append(')');
        return builder.ToString();
    }

    protected override bool LLEquals(TypeSpec? other, bool partialNameMatch)
    {
        if (other is TupleTypeSpec spec)
            return ListEqual(Elements, spec.Elements, partialNameMatch);
        return false;
    }

	/// <summary>
	/// Returns true if this is an empty tuple
	/// </summary>
    public override bool IsEmptyTuple
    {
        get
        {
            return Elements.Count == 0;
        }
    }

    /// <summary>
    /// Returns true if any of the types in the list have dynamic self
    /// </summary>
    public override bool HasDynamicSelf => TypeSpec.AnyHasDynamicSelf(Elements);

    static TupleTypeSpec empty = new TupleTypeSpec();

	/// <summary>
	/// Returns a singleton empty tuple
	/// </summary>
    public static TupleTypeSpec Empty { get { return empty; } }
}