// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;

namespace BindingsGeneration;

public class ClosureTypeSpec : TypeSpec
{
    public ClosureTypeSpec()
        : base(TypeSpecKind.Closure)
    {
    }

    public ClosureTypeSpec(TypeSpec? arguments, TypeSpec? returnType)
        : this()
    {
        Arguments = arguments is null ? TupleTypeSpec.Empty : arguments;
        ReturnType = returnType is null ? TupleTypeSpec.Empty : returnType;
    }

    static ClosureTypeSpec voidVoid = new ClosureTypeSpec(TupleTypeSpec.Empty, TupleTypeSpec.Empty);

    public static ClosureTypeSpec VoidVoid { get { return voidVoid; } }

    public TypeSpec Arguments { get; set; } = TupleTypeSpec.Empty;
    public TypeSpec ReturnType { get; set; } = TupleTypeSpec.Empty;
    public bool Throws { get; set; }
    public bool IsAsync { get; set; }

    public bool HasReturn()
    {
        return !ReturnType.IsEmptyTuple;
    }

    public bool HasArguments()
    {
        return !Arguments.IsEmptyTuple;
    }

    public TupleTypeSpec ArgumentsAsTuple
    {
        get
        {
            if (Arguments is TupleTypeSpec tuple)
                return tuple;
            return new TupleTypeSpec(Arguments);
        }
    }

    public int ArgumentCount()
    {
        if (Arguments is TupleTypeSpec tupe)
        {
            return tupe.Elements.Count;
        }
        return 1;
    }

    public IEnumerable<TypeSpec> EachArgument()
    {
        if (!HasArguments())
            yield break;
        if (Arguments is TupleTypeSpec argList)
        {
            foreach (TypeSpec arg in argList.Elements)
                yield return arg;
        }
        else
        {
            yield return Arguments!;
        }
    }

    public TypeSpec GetArgument(int index)
    {
        if (index < 0 || index >= ArgumentCount())
            throw new ArgumentOutOfRangeException(nameof(index));
        if (Arguments is TupleTypeSpec tuple)
            return tuple.Elements[index];
        return Arguments!;
    }

    public bool IsEscaping
    {
        get
        {
            return HasAttributes && Attributes.Exists(attr => attr.Name == "escaping");
        }
    }

    public bool IsAutoClosure
    {
        get
        {
            return HasAttributes && Attributes.Exists(attr => attr.Name == "autoclosure");
        }
    }

    protected override string LLToString(bool useFullName)
    {
        StringBuilder builder = new StringBuilder();
        if (Arguments is TupleTypeSpec tuple)
        {
            builder.Append(Arguments.ToString(useFullName));
        }
        else
        {
            builder.Append('(').Append(Arguments.ToString()).Append(')');
        }
        if (Throws)
            builder.Append(" throws -> ");
        else
            builder.Append(" -> ");
        builder.Append(ReturnType.ToString(useFullName));
        return builder.ToString();
    }

    protected override bool LLEquals(TypeSpec? obj, bool partialNameMatch)
    {
        if (obj is ClosureTypeSpec spec)
        {
            if (partialNameMatch)
            {
                return Arguments.EqualsPartialMatch(spec.Arguments) &&
                ReturnType.EqualsPartialMatch(spec.ReturnType);

            }
            else
            {
                return BothNullOrEqual(Arguments, spec.Arguments) &&
                    BothNullOrEqual(ReturnType, spec.ReturnType);
            }
        }
        return false;
    }

    public override bool HasDynamicSelf
    {
        get
        {
            if (Arguments is null)
                return false;
            if (Arguments.HasDynamicSelf)
                return true;
            if (!IsNullOrEmptyTuple(ReturnType) && ReturnType!.HasDynamicSelf)
                return true;
            return false;
        }
    }
}