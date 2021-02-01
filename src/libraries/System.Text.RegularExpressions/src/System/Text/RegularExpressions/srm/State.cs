// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace System.Text.RegularExpressions.SRM
{
    internal static class CharKind
    {
        /// <summary>
        /// Start of input (bit 0 is 1)
        /// </summary>
        internal const uint Start = 1;
        /// <summary>
        /// new line character (\n) (bit 1 is 1)
        /// </summary>
        internal const uint Newline = 2;
        /// <summary>
        /// word letter (bit 2 is 1)
        /// </summary>
        internal const uint WordLetter = 4;
        /// <summary>
        /// End of input (bit 3 is 1)
        /// </summary>
        internal const uint End = 8;
        /// <summary>
        /// Last Newline (both Newline and End bits are 1)
        /// </summary>
        internal const uint NewLineZ = 10;
    }
    /// <summary>
    /// Captures a state of a DFA explored during matching.
    /// </summary>
    internal class State<S>
    {
        internal uint PrevCharKind { get; private set; }
        internal SymbolicRegexNode<S> Node { get; private set; }

        /// <summary>
        /// State id is unique up to Equals.
        /// </summary>
        internal int Id { get; private set; }

        private State(SymbolicRegexNode<S> node, uint prevCharKind)
        {
            Node = node;
            PrevCharKind = prevCharKind;
        }

        /// <summary>
        /// Compute the target state for the given input atom
        /// </summary>
        /// <param name="atom">minterm corresponding to some input character</param>
        public State<S> Next(S atom)
        {
            // atom == solver.False is used to represent last \n, i.e., \Z
            uint nextCharKind = 0;
            if (Node.builder.solver.False.Equals(atom))
            {
                nextCharKind = CharKind.NewLineZ;
                atom = Node.builder.newLinePredicate;
            }
            else if (Node.builder.newLinePredicate.Equals(atom))
                nextCharKind = CharKind.Newline;
            else if (Node.builder.solver.IsSatisfiable(Node.builder.solver.MkAnd(Node.builder.wordLetterPredicate, atom)))
                nextCharKind = CharKind.WordLetter;

            // combined character context
            uint context = (nextCharKind << 4) | PrevCharKind;
            // compute the derivative of the node for the given context
            SymbolicRegexNode<S> derivative = Node.MkDerivative(atom, context);
            // nextCharKind will be the PrevCharKind of the target state
            // use an existing state instead if one exists already
            // otherwise create a new new id for it
            return MkState(derivative, nextCharKind);
        }

        /// <summary>
        /// Make a new state with given node and previous character context
        /// </summary>
        /// <param name="node">regex node</param>
        /// <param name="prevCharKind">ecodes what the previous character was, must be one of CharKind constant values</param>
        /// <returns></returns>
        public static State<S> MkState(SymbolicRegexNode<S> node, uint prevCharKind)
        {
#if DEBUG
            ValidateCharKind(prevCharKind);
#endif
            State<S> s = new State<S>(node, prevCharKind);
            State<S> state;
            if (!node.builder.stateCache.TryGetValue(s, out state))
            {
                state = s;
                node.builder.stateCache.Add(state);
                state.Id = node.builder.stateCache.Count;
            }
            return state;
        }

        private static void ValidateCharKind(uint prevCharKind)
        {
            if (prevCharKind != 0 & prevCharKind != CharKind.Start && prevCharKind != CharKind.End
                && prevCharKind != CharKind.Newline && prevCharKind != CharKind.WordLetter && prevCharKind != CharKind.NewLineZ)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
        }

        public override bool Equals(object? obj) =>
            obj is State<S> s && PrevCharKind == s.PrevCharKind && Node.Equals(s.Node);

        public override int GetHashCode() => (PrevCharKind, Node).GetHashCode();

        public override string ToString() => (DescribePrevCharKind(PrevCharKind), Node).ToString();

        private static string DescribePrevCharKind(uint i)
        {
            if (i == CharKind.WordLetter)
                return "\\w";
            else if (i == CharKind.Start)
                return "\\A";
            else if (i == CharKind.Newline)
                return "\\n";
            else if (i == CharKind.NewLineZ)
                return "\\Z";
            else if (i == CharKind.End)
                return "\\z";
            else
                return "\\W";
        }
    }
}
