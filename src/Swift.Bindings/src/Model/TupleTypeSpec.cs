// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace BindingsGeneration;

public class TupleTypeSpec : TypeSpec
{
    public TupleTypeSpec()
        : base(TypeSpecKind.Tuple)
    {
        Elements = new List<TypeSpec>();
    }

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

    public TupleTypeSpec(IEnumerable<TypeSpec> elements)
        : this()
    {
        Elements.AddRange(elements);
    }

    public TupleTypeSpec(TypeSpec single)
        : this()
    {
        Elements.Add(single);
    }

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

    public override bool IsEmptyTuple
    {
        get
        {
            return Elements.Count == 0;
        }
    }

    public override bool HasDynamicSelf => TypeSpec.AnyHasDynamicSelf(Elements);

    static TupleTypeSpec empty = new TupleTypeSpec();
    public static TupleTypeSpec Empty { get { return empty; } }
}