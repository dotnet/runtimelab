using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.SRM
{
    /// <summary>
    /// Exeption thrown by the automata constructions
    /// </summary>
    internal class AutomataException : Exception
    {
        /// <summary>
        /// the kind of exception
        /// </summary>
        public readonly AutomataExceptionKind kind;

        /// <summary>
        /// construct an exception
        /// </summary>
        public AutomataException(string message, Exception innerException)
            : base(message, innerException)
        {
            kind = AutomataExceptionKind.Unspecified;
        }

        /// <summary>
        /// construct an exception with given message
        /// </summary>
        public AutomataException(string message)
            : base(message)
        {
            kind = AutomataExceptionKind.Unspecified;
        }

        /// <summary>
        /// construct an exception with given kind
        /// </summary>
        public AutomataException(AutomataExceptionKind kind)
            : base(GetMessage(kind))
        {
            this.kind = kind;
        }

        /// <summary>
        /// construct an exception with given kind and inner exception
        /// </summary>
        public AutomataException(AutomataExceptionKind kind, Exception innerException)
            : base(GetMessage(kind), innerException)
        {
            this.kind = kind;
        }

        private static string GetMessage(AutomataExceptionKind kind)
        {
            switch (kind)
            {
                case AutomataExceptionKind.CharacterEncodingIsUnspecified:
                    return CharacterEncodingIsUnspecified;
                case AutomataExceptionKind.CharSetMustBeNonempty:
                    return CharSetMustBeNonempty;
                case AutomataExceptionKind.UnrecognizedRegex:
                    return UnrecognizedRegex;
                case AutomataExceptionKind.InternalError:
                    return InternalError;
                default:
                    return kind.ToString();
            }
        }

        public const string UnrecognizedRegex =
            "Unrecognized regex construct";
        public const string CharSetMustBeNonempty =
            "Set must be nonempty";
        public const string CharacterEncodingIsUnspecified =
            "Character encoding is unspecified";
        public const string InternalError =
            "Internal error";
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
    }
}
