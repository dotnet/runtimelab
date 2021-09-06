// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.SRM
{
    internal static class CharKind
    {
        /// <summary>
        /// Start or Stop of input (bit 0 is 1)
        /// </summary>
        internal const uint StartStop = 1;
        /// <summary>
        /// new line character (\n) (bit 1 is 1)
        /// </summary>
        internal const uint Newline = 2;
        /// <summary>
        /// Last \n or first \n in reverse mode (both Newline and StartStop bits are 1)
        /// </summary>
        internal const uint NewLineS = 3;
        /// <summary>
        /// word letter (bit 2 is 1)
        /// </summary>
        internal const uint WordLetter = 4;

        /// <summary>
        /// Prettyprints the character kind
        /// </summary>
        internal static string PrettyPrint(uint kind) => kind switch
        {
            StartStop => @"\A",
            WordLetter => @"\w",
            Newline => @"\n",
            NewLineS => @"\A\n",
            _ => "",
        };

        /// <summary>
        /// Gets the previous character kind from a context
        /// </summary>
        internal static uint Prev(uint context) => context & 0x7;

        /// <summary>
        /// Gets the next character kind from a context
        /// </summary>
        internal static uint Next(uint context) => context >> 3;

        /// <summary>
        /// Creates the context of the previous and the next character kinds.
        /// </summary>
        internal static uint Context(uint prevKind, uint nextKind) => (nextKind << 3) | prevKind;

        internal static string DescribeContext(uint context)
        {
            string prev = DescribePrev(Prev(context));
            string next = DescribeNext(Next(context));
            return next == string.Empty ?
                prev :
                $"{prev}/{next}";
        }

        internal static string DescribePrev(uint i) =>
            i == WordLetter ? @"\w" :
            i == StartStop ? @"\A" :
            i == Newline ? @"\n" :
            i == NewLineS ? @"\A\n" :
            string.Empty;

        internal static string DescribeNext(uint i) =>
            i == WordLetter ? @"\w" :
            i == StartStop ? @"\z" :
            i == Newline ? @"\n" :
            i == NewLineS ? @"\n\z" :
            string.Empty;
    }

    /// <summary>
    /// Captures a state of a DFA explored during matching.
    /// </summary>
    internal sealed class State<T> where T : notnull
    {
        internal int Id { get; set; }
        internal bool IsInitialState { get; set; }
        internal uint PrevCharKind { get; private set; }
        internal SymbolicRegexNode<T> Node { get; private set; }

        /// <summary>
        /// State is lazy
        /// </summary>
        internal bool IsLazy => Node._info.IsLazy;

        /// <summary>
        /// This is a deadend state
        /// </summary>
        internal bool IsDeadend => Node.IsNothing;

        /// <summary>
        /// The node must be nullable here
        /// </summary>
        internal int WatchDog
        {
            get
            {
                if (Node._kind == SymbolicRegexKind.WatchDog)
                {
                    return Node._lower;
                }

                if (Node._kind == SymbolicRegexKind.Or)
                {
                    Debug.Assert(Node._alts is not null);
                    return Node._alts._watchdog;
                }

                return -1;
            }
        }

        /// <summary>
        /// If true then the state is a dead-end, rejects all inputs.
        /// </summary>
        internal bool IsNothing => Node.IsNothing;

        /// <summary>
        /// If true then state starts with a ^ or $ or \A or \z or \Z
        /// </summary>
        internal bool StartsWithLineAnchor => Node._info.StartsWithLineAnchor;

        internal State(SymbolicRegexNode<T> node, uint prevCharKind) : base()
        {
            Node = node;
            PrevCharKind = prevCharKind;
        }

        /// <summary>
        /// Compute the target state for the given input atom.
        /// If atom is False this means that this is \n and it is the last character of the input.
        /// </summary>
        /// <param name="atom">minterm corresponding to some input character or False corresponding to last \n</param>
        internal State<T> Next(T atom)
        {
            ICharAlgebra<T> alg = Node._builder._solver;
            T WLpred = Node._builder._wordLetterPredicate;
            T NLpred = Node._builder._newLinePredicate;

            // atom == solver.False is used to represent the very last \n
            uint nextCharKind = 0;
            if (alg.False.Equals(atom))
            {
                nextCharKind = CharKind.NewLineS;
                atom = NLpred;
            }
            else if (NLpred.Equals(atom))
            {
                //if the previous state was the start state, mark this as the very FIRST \n
                //essentially, this looks the same as the very last \n and
                //is used to nullify rev(\Z) in the conext of a reversed automaton
                //either \Z or rev(\Z) is ever possible as an anchor
                nextCharKind = PrevCharKind == CharKind.StartStop ?
                    CharKind.NewLineS :
                    CharKind.Newline;
            }
            else if (alg.IsSatisfiable(alg.And(WLpred, atom)))
            {
                nextCharKind = CharKind.WordLetter;
            }

            // combined character context
            uint context = CharKind.Context(PrevCharKind, nextCharKind);

            // compute the derivative of the node for the given context
            SymbolicRegexNode<T> derivative = Node.MkDerivative(atom, context);

            // nextCharKind will be the PrevCharKind of the target state
            // use an existing state instead if one exists already
            // otherwise create a new new id for it
            return Node._builder.MkState(derivative, nextCharKind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsNullable(uint nextCharKind)
        {
            Debug.Assert(nextCharKind is 0 or CharKind.StartStop or CharKind.Newline or CharKind.WordLetter or CharKind.NewLineS);
            uint context = CharKind.Context(PrevCharKind, nextCharKind);
            return Node.IsNullableFor(context);
        }

        public override bool Equals(object? obj) =>
            obj is State<T> s && PrevCharKind == s.PrevCharKind && Node.Equals(s.Node);

        public override int GetHashCode() => (PrevCharKind, Node).GetHashCode();

        public override string ToString() =>
            PrevCharKind == 0 ? Node.ToString() :
             $"({CharKind.DescribePrev(PrevCharKind)},{Node})";

        internal string Description => ToString();

        internal string DgmlView
        {
            get
            {
                string info = CharKind.PrettyPrint(PrevCharKind);
                if (info != string.Empty)
                {
                    info = $"Previous: {info}&#13;";
                }

                string deriv = HTMLEncodeChars(Node.ToString());
                if (deriv == string.Empty)
                {
                    deriv = "()";
                }

                return $"{info}{deriv}";
            }
        }

        private static string HTMLEncodeChars(string s) => s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
