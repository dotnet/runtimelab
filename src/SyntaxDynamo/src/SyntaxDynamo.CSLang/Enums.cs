// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.CSLang
{
    public enum CSVisibility
    {
        None = 0,
        Public,
        Internal,
        Protected,
        Private,
    }

    public enum CSMethodKind
    {
        None = 0,
        Static,
        StaticExtern,
        StaticNew,
        Virtual,
        Override,
        Extern,
        New,
        Abstract,
        Interface,
        Unsafe,
        StaticUnsafe,
    }

    public enum CSBinaryOperator
    {
        Add = 0,
        Sub,
        Mul,
        Div,
        Mod,
        And,
        Or,
        Less,
        Greater,
        Equal,
        NotEqual,
        LessEqual,
        GreaterEqual,
        BitAnd,
        BitOr,
        BitXor,
        LeftShift,
        RightShift,
        Dot,
        Is,
        As,
        NullCoalesce,
    }

    public enum CSUnaryOperator
    {
        Neg = 0,
        Pos,
        At,
        Not,
        BitNot,
        Ref,
        Out,
        AddressOf,
        Indirection,
        Await,
        PostBang,
        Question,
    }

    public enum CSAssignmentOperator
    {
        Assign,
        AddAssign,
        SubAssign,
        MulAssign,
        DivAssign,
        ModAssign,
        AndAssign,
        OrAssign,
        XorAssign
    }

    public enum CSParameterKind
    {
        None,
        Ref,
        Out,
        Params,
        This
    }

    public enum CSShortCircuitKind
    {
        Break,
        Continue,
    }
}

