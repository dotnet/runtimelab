// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

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
        internal static string PrettyPrint(uint kind)
        {
            switch (kind)
            {
                case StartStop: return @"\A";
                case WordLetter: return @"\w";
                case Newline: return @"\n";
                case NewLineS: return @"\A\n";
                default: return "";
            }
        }

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
            return (next == string.Empty ? prev : prev + "/" + next);
        }

        internal static string DescribePrev(uint i)
        {
            string res;
            if (i == CharKind.WordLetter)
                res = @"\w";
            else if (i == CharKind.StartStop)
                res = @"\A";
            else if (i == CharKind.Newline)
                res = @"\n";
            else if (i == CharKind.NewLineS)
                res = @"\A\n";
            else
                res = "";
            return res;
        }

        internal static string DescribeNext(uint i)
        {
            string res;
            if (i == CharKind.WordLetter)
                res = @"\w";
            else if (i == CharKind.StartStop)
                res = @"\z";
            else if (i == CharKind.Newline)
                res = @"\n";
            else if (i == CharKind.NewLineS)
                res = @"\n\z";
            else
                res = "";
            return res;
        }
    }

    /// <summary>
    /// Captures a state of a DFA explored during matching.
    /// </summary>
    internal class State<S>
    {
        internal int Id { get; set; }
        internal bool IsInitialState { get; set; }
        internal uint PrevCharKind { get; private set; }
        internal SymbolicRegexNode<S> Node { get; private set; }

        /// <summary>
        /// State is lazy
        /// </summary>
        internal bool IsLazy => Node.info.IsLazy;

        /// <summary>
        /// This is a deadend state
        /// </summary>
        internal bool IsDeadend => Node.IsNothing;

        /// <summary>
        /// The node must be nullable here
        /// </summary>
        internal int WatchDog => (Node.kind == SymbolicRegexKind.WatchDog ? Node.lower : (Node.kind == SymbolicRegexKind.Or ? Node.alts.watchdog : -1));

        /// <summary>
        /// If true then the state is a dead-end, rejects all inputs.
        /// </summary>
        internal bool IsNothing { get { return Node.IsNothing; } }

        /// <summary>
        /// If true then state starts with a ^ or $ or \A or \z or \Z
        /// </summary>
        internal bool StartsWithLineAnchor => Node.info.StartsWithLineAnchor;

        internal State(SymbolicRegexNode<S> node, uint prevCharKind) : base()
        {
            Node = node;
            PrevCharKind = prevCharKind;
        }

        /// <summary>
        /// Compute the target state for the given input atom.
        /// If atom is False this means that this is \n and it is the last character of the input.
        /// </summary>
        /// <param name="atom">minterm corresponding to some input character or False corresponding to last \n</param>
        /// <param name="antimirov">if true uses antimirov derivatives</param>
        internal State<S> Next(S atom, bool antimirov)
        {
            var alg = Node.builder.solver;
            S WLpred = Node.builder.wordLetterPredicate;
            S NLpred = Node.builder.newLinePredicate;
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
                if (PrevCharKind == CharKind.StartStop)
                    nextCharKind = CharKind.NewLineS;
                else
                    nextCharKind = CharKind.Newline;
            }
            else if (alg.IsSatisfiable(alg.MkAnd(WLpred, atom)))
                nextCharKind = CharKind.WordLetter;

            // combined character context
            uint context = CharKind.Context(PrevCharKind, nextCharKind);
            // compute the derivative of the node for the given context
            SymbolicRegexNode<S> derivative = Node.MkDerivative(atom, context, antimirov);
            // nextCharKind will be the PrevCharKind of the target state
            // use an existing state instead if one exists already
            // otherwise create a new new id for it
            return Node.builder.MkState(derivative, nextCharKind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsNullable(uint nextCharKind)
        {
#if DEBUG
            ValidateCharKind(nextCharKind);
#endif
            uint context = CharKind.Context(PrevCharKind, nextCharKind);
            return Node.IsNullableFor(context);
        }

        private static void ValidateCharKind(uint x)
        {
            if (x != 0 && x != CharKind.StartStop && x != CharKind.Newline && x != CharKind.WordLetter && x != CharKind.NewLineS)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
        }

        public override bool Equals(object? obj) =>
            obj is State<S> s && PrevCharKind == s.PrevCharKind && Node.Equals(s.Node);

        public override int GetHashCode() => (PrevCharKind, Node).GetHashCode();

        public override string ToString() =>
            PrevCharKind == 0 ? Node.ToString() :
             string.Format("({0},{1})", CharKind.DescribePrev(PrevCharKind), Node.ToString());

        internal string Description => ToString();

        internal string DgmlView
        {
            get
            {
                string deriv = HTMLEncodeChars(Node.ToString());
                string info = CharKind.PrettyPrint(PrevCharKind);
                if (info != string.Empty)
                    info = string.Format("Previous: {0}&#13;", info);
                if (deriv == string.Empty)
                    deriv = "()";
                return string.Format("{0}{1}", info, deriv);
            }
        }

        private static string HTMLEncodeChars(string s) => s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
