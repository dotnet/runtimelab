// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if DEBUG
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.RegularExpressions.Symbolic.DGML
{
    /// <summary>
    /// Used by DgmlWriter to unwind a regex into a DFA up to a bound that limits the number of states
    /// </summary>
    internal sealed class RegexAutomaton<T> : IAutomaton<(SymbolicRegexNode<T>?, T)> where T : notnull
    {
        private readonly DfaMatchingState<T> _q0;
        private readonly List<int> _states = new();
        private readonly HashSet<int> _stateSet = new();
        private readonly List<Move<(SymbolicRegexNode<T>?, T)>> _moves = new();
        private readonly SymbolicRegexBuilder<T> _builder;
        private readonly List<SymbolicRegexNode<T>> _nfaStates = new();
        private readonly Dictionary<SymbolicRegexNode<T>, int> _nfaStateId = new();
        private readonly bool _asNFA;

        internal RegexAutomaton(SymbolicRegexMatcher<T> srm, int bound, bool addDotStar, bool inReverse, bool asNFA)
        {
            _asNFA = asNFA;
            _builder = srm._builder;
            uint startId = inReverse ?
                (srm._reversePattern._info.StartsWithLineAnchor ? CharKind.StartStop : 0) :
                (srm._pattern._info.StartsWithLineAnchor ? CharKind.StartStop : 0);

            //inReverse only matters if Ar contains some line anchor
            _q0 = _builder.MkState(inReverse ? srm._reversePattern : (addDotStar ? srm._dotstarredPattern : srm._pattern), startId);

            if (asNFA)
            {
                Stack<SymbolicRegexNode<T>> stack = new();
                stack.Push(_q0.Node);
                _nfaStates.Add(_q0.Node);
                _nfaStateId[_q0.Node] = 0;
                _states.Add(0);
                _stateSet.Add(0);
                while (stack.Count > 0 && (bound <= 0 || _nfaStates.Count < bound))
                {
                    SymbolicRegexNode<T> q = stack.Pop();
                    int qId = _nfaStateId[q];
                    foreach ((T, SymbolicRegexNode<T>?, SymbolicRegexNode<T>) branch in q.MkDerivative())
                    {
                        SymbolicRegexNode<T> p = branch.Item3;
                        int pId;
                        if (!_nfaStateId.TryGetValue(p, out pId))
                        {
                            pId = _nfaStates.Count;
                            _nfaStateId[p] = pId;
                            _nfaStates.Add(p);
                            _stateSet.Add(pId);
                            _states.Add(pId);
                            stack.Push(p);
                        }
                        _moves.Add(Move<(SymbolicRegexNode<T>?, T)>.Create(qId, pId, (branch.Item2, branch.Item1)));
                    }
                }
            }
            else
            {
                Dictionary<(int, int), T> normalizedmoves = new();
                Stack<DfaMatchingState<T>> stack = new();
                stack.Push(_q0);
                _states.Add(_q0.Id);
                _stateSet.Add(_q0.Id);

                T[]? partition = _builder._solver.GetPartition();
                Debug.Assert(partition is not null);
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
                    _moves.Add(Move<(SymbolicRegexNode<T>?, T)>.Create(entry.Key.Item1, entry.Key.Item2, (null, entry.Value)));
            }
        }

        public (SymbolicRegexNode<T>?, T)[] Alphabet
        {
            get
            {
                T[]? alphabet = _builder._solver.GetPartition();
                Debug.Assert(alphabet is not null);
                return Array.ConvertAll<T, (SymbolicRegexNode<T>?, T)>(alphabet, a => (null, a));
            }
        }

        public int InitialState => _asNFA ? 0 : _q0.Id;

        public int StateCount => _states.Count;

        public int TransitionCount => _moves.Count;

        public string DescribeLabel((SymbolicRegexNode<T>?, T) lab) =>
            lab.Item1 is null ? Net.WebUtility.HtmlEncode(_builder._solver.PrettyPrint(lab.Item2)) :
            // Conditional nullability based on anchors
            Net.WebUtility.HtmlEncode($"{lab.Item1}/{_builder._solver.PrettyPrint(lab.Item2)}");

        public string DescribeStartLabel() => "";

        public string DescribeState(int state)
        {
            if (_asNFA)
            {
                Debug.Assert(state < _nfaStates.Count);
                return Net.WebUtility.HtmlEncode(_nfaStates[state].ToString());
            }

            Debug.Assert(_builder._statearray is not null);
            return _builder._statearray[state].DgmlView;
        }

        public IEnumerable<int> GetStates() => _states;

        public bool IsFinalState(int state)
        {
            if (_asNFA)
            {
                Debug.Assert(state < _nfaStates.Count);
                return _nfaStates[state].CanBeNullable;
            }

            Debug.Assert(_builder._statearray is not null && state < _builder._statearray.Length);
            return _builder._statearray[state].IsNullable(CharKind.StartStop);
        }

        public IEnumerable<Move<(SymbolicRegexNode<T>?, T)>> GetMoves() => _moves;
    }
}
#endif
