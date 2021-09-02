// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Exeption thrown by the automata constructions
    /// </summary>
    internal sealed class AutomataException : Exception
    {
        /// <summary>
        /// the kind of exception
        /// </summary>
        public readonly AutomataExceptionKind kind;

        /// <summary>
        /// construct an exception with given kind
        /// </summary>
        public AutomataException(AutomataExceptionKind kind)
            : base(GetMessage(kind)) => this.kind = kind;

        private static string GetMessage(AutomataExceptionKind kind) => kind switch
        {
            AutomataExceptionKind.CharacterEncodingIsUnspecified => CharacterEncodingIsUnspecified,
            AutomataExceptionKind.CharSetMustBeNonempty => CharSetMustBeNonempty,
            AutomataExceptionKind.UnrecognizedRegex => UnrecognizedRegex,
            AutomataExceptionKind.InternalError => InternalError,
            _ => kind.ToString(),
        };

        public const string UnrecognizedRegex = "Unrecognized regex construct";
        public const string CharSetMustBeNonempty = "Set must be nonempty";
        public const string CharacterEncodingIsUnspecified = "Character encoding is unspecified";
        public const string InternalError = "Internal error";
    }


    /// <summary>
    /// Kinds of exceptions that may be thrown by the Automata library operations.
    /// </summary>
    internal enum AutomataExceptionKind
    {
        UnrecognizedRegex,
        CharSetMustBeNonempty,
        CharacterEncodingIsUnspecified,
        InternalError,
        Unspecified,
        InvalidArguments,
        CharSetMustBeNontrivial,
        CompactSerializationNodeLimitViolation,
        CompactSerializationBitLimitViolation,
        CompactDeserializationError,
        SetIsEmpty,
        InvalidArgument,
        IncompatibleAlgebras,
        NotSupported,
        BooleanAlgebraIsNotAtomic,
        OrdinalIsTooLarge,
        UnexpectedMTBDDTerminal,
        AlgebraMustBeCharSetSolver,
        MTBDDsNotSupportedForThisOperation,
        BDDSerializationNodeLimitViolation,
        BDDSerializationBitLimitViolation,
        BDDDeserializationError,
        BitOutOfRange,
        InternalError_SymbolicRegex,
        MustNotAcceptEmptyString,
        NrOfMintermsCanBeAtMost64,
        SerializationError,
    }
}
