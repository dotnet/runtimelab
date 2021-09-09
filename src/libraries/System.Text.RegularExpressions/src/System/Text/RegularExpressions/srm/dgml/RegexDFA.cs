// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.RegularExpressions.Symbolic.DGML
{
    /// <summary>
    /// Used by DgmlWriter to unwind a regex into a DFA up to a bound that limits the number of states
    /// </summary>
    internal sealed class RegexDFA<T> : IAutomaton<T> where T : notnull
    {
        private readonly DfaMatchingState<T> _q0;
        private readonly List<int> _states = new();
        private readonly HashSet<int> _stateSet = new();
        private readonly List<Move<T>> _moves = new();
        private readonly SymbolicRegexBuilder<T> _builder;

        internal RegexDFA(SymbolicRegexMatcher<T> srm, int bound, bool addDotStar, bool inReverse)
        {
            _builder = srm._builder;
            uint startId = inReverse ?
                (srm._reversePattern._info.StartsWithLineAnchor ? CharKind.StartStop : 0) :
                (srm._pattern._info.StartsWithLineAnchor ? CharKind.StartStop : 0);

            //inReverse only matters if Ar contains some line anchor
            _q0 = _builder.MkState(inReverse ? srm._reversePattern : (addDotStar ? srm._dotstarredPattern : srm._pattern), startId);
            var stack = new Stack<DfaMatchingState<T>>();
            stack.Push(_q0);
            _states.Add(_q0.Id);
            _stateSet.Add(_q0.Id);

            T[]? partition = _builder._solver.GetPartition();
            Debug.Assert(partition is not null);

            Dictionary<(int, int), T> normalizedmoves = new();

            //unwind until the stack is empty or the bound has been reached
            while (stack.Count > 0 && (bound <= 0 || _states.Count < bound))
            {
                DfaMatchingState<T> q = stack.Pop();
                foreach (T c in partition)
                {
                    DfaMatchingState<T> p = q.Next(c);

                    // check that p is not a dead-end
                    if (!p.IsNothing)
                    {
                        if (_stateSet.Add(p.Id))
                        {
                            stack.Push(p);
                            _states.Add(p.Id);
                        }

                        var qp = (q.Id, p.Id);
                        normalizedmoves[qp] = normalizedmoves.ContainsKey(qp) ?
                            _builder._solver.Or(normalizedmoves[qp], c) :
                            c;
                    }
                }
            }

            foreach (KeyValuePair<(int, int), T> entry in normalizedmoves)
                _moves.Add(Move<T>.Create(entry.Key.Item1, entry.Key.Item2, entry.Value));
        }

        public T[] Alphabet
        {
            get
            {
                T[]? alphabet = _builder._solver.GetPartition();
                Debug.Assert(alphabet is not null);
                return alphabet;
            }
        }

        public int InitialState => _q0.Id;

        public int StateCount => _states.Count;

        public int TransitionCount => _moves.Count;

        public string DescribeLabel(T lab) => HTMLEncodeChars(_builder._solver.PrettyPrint(lab));

        public string DescribeStartLabel() => "";

        public string DescribeState(int state)
        {
            Debug.Assert(_builder._statearray is not null);
            return _builder._statearray[state].DgmlView;
        }

        public IEnumerable<int> GetStates() => _states;

        public bool IsFinalState(int state)
        {
            Debug.Assert(_builder._statearray is not null);
            return _builder._statearray[state].IsNullable(CharKind.StartStop);
        }

        public IEnumerable<Move<T>> GetMoves() => _moves;

        private static string HTMLEncodeChars(string s) => s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
