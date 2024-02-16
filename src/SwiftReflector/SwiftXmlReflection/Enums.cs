// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SwiftReflector.SwiftXmlReflection
{
    public enum TypeKind
    {
        Unknown = 0,
        Class,
        Struct,
        Enum,
        Protocol
    }

    public enum Accessibility
    {
        Unknown = 0,
        Public,
        Private,
        Internal,
        Open,
    }

    public enum StorageKind
    {
        Unknown = 0,
        Addressed,
        AddressedWithObservers,
        AddressedWithTrivialAccessors,
        Computed,
        ComputedWithMutableAddress,
        Inherited,
        InheritedWithObservers,
        Stored,
        StoredWithObservers,
        StoredWithTrivialAccessors,
        Coroutine,
        MutableAddressor,
    }

    public enum TypeSpecKind
    {
        Named = 0,
        Tuple,
        Closure,
        ProtocolList,
    }

    public enum TypeTokenKind
    {
        TypeName,
        Comma,
        LeftParenthesis,
        RightParenthesis,
        LeftAngle,
        RightAngle,
        LeftBracket,
        RightBracket,
        Arrow,
        At,
        QuestionMark,
        TypeLabel,
        Colon,
        ExclamationPoint,
        Period,
        Ampersand,
        Done,
    }

    public enum InheritanceKind
    {
        Class,
        Protocol
    }

    public enum ConstraintKind
    {
        Inherits,
        Equal
    }

    public enum AttributeParameterKind
    {
        None,
        Label,
        Literal,
        Sublist,
        Unknown,
    }
}

