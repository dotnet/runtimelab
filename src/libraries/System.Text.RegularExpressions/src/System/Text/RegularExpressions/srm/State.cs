// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Net;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>Captures a state of a DFA explored during matching.</summary>
    internal sealed class State<T> where T : notnull
    {
        internal int Id { get; set; }

        internal bool IsInitialState { get; set; }

        internal uint PrevCharKind { get; private set; }

        internal SymbolicRegexNode<T> Node { get; private set; }

        /// <summary>State is lazy</summary>
        internal bool IsLazy => Node._info.IsLazy;

        /// <summary>This is a deadend state</summary>
        internal bool IsDeadend => Node.IsNothing;

        /// <summary>The node must be nullable here</summary>
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

        /// <summary>If true then the state is a dead-end, rejects all inputs.</summary>
        internal bool IsNothing => Node.IsNothing;

        /// <summary>If true then state starts with a ^ or $ or \A or \z or \Z</summary>
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
                // If the previous state was the start state, mark this as the very FIRST \n.
                // Essentially, this looks the same as the very last \n and is used to nullify
                // rev(\Z) in the conext of a reversed automaton.
                nextCharKind = PrevCharKind == CharKind.StartStop ?
                    CharKind.NewLineS :
                    CharKind.Newline;
            }
            else if (alg.IsSatisfiable(alg.And(WLpred, atom)))
            {
                nextCharKind = CharKind.WordLetter;
            }

            // Combined character context
            uint context = CharKind.Context(PrevCharKind, nextCharKind);

            // Compute the derivative of the node for the given context
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

        internal string DgmlView
        {
            get
            {
                string info = CharKind.DescribePrev(PrevCharKind);
                if (info != string.Empty)
                {
                    info = $"Previous: {info}&#13;";
                }

                string deriv = WebUtility.HtmlEncode(Node.ToString());
                if (deriv == string.Empty)
                {
                    deriv = "()";
                }

                return $"{info}{deriv}";
            }
        }
    }
}
