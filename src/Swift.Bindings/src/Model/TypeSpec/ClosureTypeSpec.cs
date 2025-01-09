// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;

namespace BindingsGeneration;

/// <summary>
/// Represents a swift closure type
/// </summary>
public class ClosureTypeSpec : TypeSpec
{
	/// <summary>
	/// Constructs a new empty closure equivalent to ()->()
	/// </summary>
    public ClosureTypeSpec()
        : base(TypeSpecKind.Closure)
    {
    }

	/// <summary>
	/// Returns a closure type spec with the given arguments and return type. If either is null, it will be
    /// replaced with an empty tuple
	/// </summary>
    public ClosureTypeSpec(TypeSpec? arguments, TypeSpec? returnType)
        : this()
    {
        Arguments = arguments is null ? TupleTypeSpec.Empty : arguments;
        ReturnType = returnType is null ? TupleTypeSpec.Empty : returnType;
    }

    static ClosureTypeSpec voidVoid = new ClosureTypeSpec(TupleTypeSpec.Empty, TupleTypeSpec.Empty);

	/// <summary>
	/// Returns a singleton closure of the form ()->()
	/// </summary>
    public static ClosureTypeSpec VoidVoid { get { return voidVoid; } }

	/// <summary>
	/// Gets or sets the arguments of the tuple
	/// </summary>
    public TypeSpec Arguments { get; set; } = TupleTypeSpec.Empty;

	/// <summary>
	/// Gets or sets the return type of the tuple
	/// </summary>
    public TypeSpec ReturnType { get; set; } = TupleTypeSpec.Empty;

    /// <summary>
	/// Returns true if the closure might throw
	/// </summary>
    public bool Throws { get; set; }

	/// <summary>
	/// Returns true if the closure is asynchronous
	/// </summary>
    public bool IsAsync { get; set; }

	/// <summary>
	/// Returns true if the return type is <B>not</B> an empty tuple
	/// </summary>
    public bool HasReturn()
    {
        return !ReturnType.IsEmptyTuple;
    }

	/// <summary>
	/// Returns true if the tuple has arguments
	/// </summary>
    public bool HasArguments()
    {
        return !Arguments.IsEmptyTuple;
    }

	/// <summary>
	/// Returns the arguments as is if they're a tuple, otherwise returns a tuple of arity 1 with the single argument
	/// </summary>
    public TupleTypeSpec ArgumentsAsTuple
    {
        get
        {
            if (Arguments is TupleTypeSpec tuple)
                return tuple;
            return new TupleTypeSpec(Arguments);
        }
    }

	/// <summary>
	/// Returns the argument count
	/// </summary>
    public int ArgumentCount()
    {
        if (Arguments is TupleTypeSpec tupe)
        {
            return tupe.Elements.Count;
        }
        return 1;
    }

	/// <summary>
	/// Returns an enumeration of the arguments
	/// </summary>
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

	/// <summary>
	/// Range checked argument accessor
	/// </summary>
    public TypeSpec GetArgument(int index)
    {
        if (index < 0 || index >= ArgumentCount())
            throw new ArgumentOutOfRangeException(nameof(index));
        if (Arguments is TupleTypeSpec tuple)
            return tuple.Elements[index];
        return Arguments!;
    }

	/// <summary>
	/// Returns true if the closure is escaping
	/// </summary>
    public bool IsEscaping
    {
        get
        {
            return HasAttributes && Attributes.Exists(attr => attr.Name == "escaping");
        }
    }

	/// <summary>
	/// Returns true if the closure is an auto closure
	/// </summary>
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

    /// <summary>
    /// Returns true if the closure contains dynamic self
    /// </summary>
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