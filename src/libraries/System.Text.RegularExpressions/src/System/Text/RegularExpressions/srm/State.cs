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
    internal enum CharKindId
    {
        None = 0,
        Start = 1,
        Newline = 2,
        WordLetter = 3,
        End = 4,
        NewLineZ = 5,
    }

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
        /// <summary>
        /// This bit being 1 means that the character context is used in reverse mode.
        /// </summary>
        internal const uint Reverse = 0x80000000;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint From(CharKindId id)
        {
            switch (id)
            {
                case CharKindId.Start: return Start;
                case CharKindId.End: return End;
                case CharKindId.WordLetter: return WordLetter;
                case CharKindId.Newline: return Newline;
                case CharKindId.NewLineZ: return NewLineZ;
                default: return 0;
            }
        }
    }
    /// <summary>
    /// Captures a state of a DFA explored during matching.
    /// </summary>
    internal class State<S>
    {
        internal CharKindId PrevCharKindId { get; private set; }
        internal bool IsReverse { get { return (PrevCharKind & CharKind.Reverse) != 0; } }
        internal uint PrevCharKind { get; private set; }
        internal SymbolicRegexNode<S> Node { get; private set; }

        /// <summary>
        /// State id is unique up to Equals.
        /// </summary>
        internal int Id { get; private set; }

        /// <summary>
        /// If true then the state is a dead-end, rejects all inputs.
        /// </summary>
        public bool IsNothing { get { return Node.IsNothing; } }

        /// <summary>
        /// used to track is this state is a (PrevCharKind variant of) A1
        /// </summary>
        internal bool isInitialState;

        private State(SymbolicRegexNode<S> node, CharKindId prevCharKindId, bool reverse)
        {
            Node = node;
            PrevCharKind = (reverse ? CharKind.Reverse : 0) | CharKind.From(prevCharKindId);
            PrevCharKindId = prevCharKindId;
        }

        /// <summary>
        /// Compute the target state for the given input atom.
        /// If atom is False this means that this is \n and it is the last character of the input.
        /// </summary>
        /// <param name="atom">minterm corresponding to some input character or False corresponding to last \n</param>
        public State<S> Next(S atom)
        {
            // atom == solver.False is used to represent last \n, i.e., \Z
            CharKindId nextCharKindId = 0;
            if (Node.builder.solver.False.Equals(atom))
            {
                nextCharKindId = CharKindId.NewLineZ;
                atom = Node.builder.newLinePredicate;
            }
            else if (Node.builder.newLinePredicate.Equals(atom))
                nextCharKindId = CharKindId.Newline;
            else if (Node.builder.solver.IsSatisfiable(Node.builder.solver.MkAnd(Node.builder.wordLetterPredicate, atom)))
                nextCharKindId = CharKindId.WordLetter;

            // combined character context
            uint context = (CharKind.From(nextCharKindId) << 4) | PrevCharKind;
            // compute the derivative of the node for the given context
            SymbolicRegexNode<S> derivative = Node.MkDerivative(atom, context);
            // nextCharKind will be the PrevCharKind of the target state
            // use an existing state instead if one exists already
            // otherwise create a new new id for it --- keep the reverse bit set
            return MkState(derivative, nextCharKindId, (PrevCharKind & CharKind.Reverse) != 0);
        }

        /// <summary>
        /// Make a new state with given node and previous character context
        /// </summary>
        public static State<S> MkState(SymbolicRegexNode<S> node, CharKindId prevCharKindId, bool reverse = false)
        {
            State<S> s = new State<S>(node, prevCharKindId, reverse);
            State<S> state;
            if (!node.builder.stateCache.TryGetValue(s, out state))
            {
                state = s;
                state.Id = node.builder.stateCache.Count;
                node.builder.stateCache.Add(state);
#if DEBUG
                if (state.Id > node.builder.statearray.Length)
                    throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
                if (state.Id == node.builder.statearray.Length)
                    // extend the state lookup array with 1k new entries
                    Array.Resize(ref node.builder.statearray, node.builder.statearray.Length + 1024);
                node.builder.statearray[state.Id] = state;
            }
            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNullable(uint nextCharKind)
        {
#if DEBUG
            ValidateCharKind(nextCharKind);
#endif
            uint context = (nextCharKind << 4) | PrevCharKind;
            return Node.IsNullableFor(context);
        }

        private static void ValidateCharKind(uint prevCharKind)
        {
            // ignore the Reverse flag
            uint x = prevCharKind & 0xF;
            uint y = (prevCharKind >> 4) & 0xF;
            if (x != 0 & x != CharKind.Start && x != CharKind.End && x != CharKind.Newline && x != CharKind.WordLetter && x != CharKind.NewLineZ)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
            if (y != 0 & y != CharKind.Start && y != CharKind.End && y != CharKind.Newline && y != CharKind.WordLetter && y != CharKind.NewLineZ)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
        }

        public override bool Equals(object? obj) =>
            obj is State<S> s && PrevCharKind == s.PrevCharKind && Node.Equals(s.Node);

        public override int GetHashCode() => (PrevCharKind, Node).GetHashCode();

        public override string ToString() =>
            PrevCharKindId == (uint)CharKindId.None ? Node.ToString() :
             ("Last char:" + DescribeCharKind(PrevCharKind) + ", " + Node.ToString());

        private static string DescribeCharKind(uint i)
        {
            string rev = ((i & CharKind.Reverse) == 0 ? "" : "r");
            string prev = DescribeCharKind1(i & 0xF);
            string next = DescribeCharKind1((i >> 4) & 0xF);
            return rev + (next == string.Empty ? prev : prev + "/" + next);
        }

        private static string DescribeCharKind1(uint i)
        {
            string res;
            if (i == CharKind.WordLetter)
                res = @"\w";
            else if (i == CharKind.Start)
                res = @"\A";
            else if (i == CharKind.Newline)
                res = @"\n";
            else if (i == CharKind.NewLineZ)
                res = @"\Z";
            else if (i == CharKind.End)
                res = @"\z";
            else
                res = "";
            return res;
        }

        public string Serialize()
        {
            string regex = Node.Serialize();
            string prev = DescribeCharKind(PrevCharKind);
            return prev + "," + regex;
        }

        internal static CharKindId GetCharKindIdFromEncoding(char c)
        {
            switch (c)
            {
                case 'w': return CharKindId.WordLetter;
                case 'A': return CharKindId.Start;
                case 'n': return CharKindId.Newline;
                case 'Z': return CharKindId.NewLineZ;
                case 'z': return CharKindId.End;
                default: return CharKindId.None;
            }
        }
    }
}
