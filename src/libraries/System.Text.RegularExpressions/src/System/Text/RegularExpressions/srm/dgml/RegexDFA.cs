// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace System.Text.RegularExpressions.SRM.DGML
{
    /// <summary>
    /// Used by DgmlWriter to unwind a regex into a DFA up to a bound that limits the number of states
    /// </summary>
    internal class RegexDFA<S> : IAutomaton<S>
    {
        private State<S> _q0;
        private List<State<S>> _states = new List<State<S>>();
        private Dictionary<State<S>, int> _stateId = new Dictionary<State<S>, int>();
        private Dictionary<Tuple<int, int>, S> _normalizedmoves = new Dictionary<Tuple<int, int>, S>();
        private ICharAlgebra<S> _solver;

        internal RegexDFA(SymbolicRegexMatcher<S> srm, int bound,  bool addDotStar)
        {
            _solver = srm.builder.solver;
            CharKindId startId = (srm.A.info.StartsWithSomeAnchor ? CharKindId.Start : CharKindId.None);
            _q0 = State<S>.MkState(addDotStar ? srm.A1 : srm.A, startId);
            var stack = new Stack<State<S>>();
            stack.Push(_q0);
            _states.Add(_q0);
            _stateId[_q0] = 0;
            var partition = _solver.GetPartition();
            //construct until the stack is empty or the bound has been reached
            while (stack.Count > 0 && (bound <= 0 || _states.Count < bound))
            {
                var q = stack.Pop();
                int qId = _stateId[q];
                foreach (var c in partition)
                {
                    var p = q.Next(c);
                    //check that p is not a dead-end
                    if (!p.IsNothing)
                    {
                        int pId;
                        if (!_stateId.TryGetValue(p, out pId))
                        {
                            stack.Push(p);
                            pId = _states.Count;
                            _states.Add(p);
                            _stateId[p] = pId;
                        }
                        var qp = new Tuple<int, int>(qId, pId);
                        if (_normalizedmoves.ContainsKey(qp))
                            _normalizedmoves[qp] = _solver.MkOr(_normalizedmoves[qp], c);
                        else
                            _normalizedmoves[qp] = c;
                    }
                }
            }
        }

        public int InitialState => 0;

        public int StateCount => _states.Count;

        public string DescribeLabel(S lab) => HTMLEncodeChars(_solver.PrettyPrint(lab));

        public string DescribeStartLabel() => "";

        public string DescribeState(int state) => ViewState(_states[state]);

        public IEnumerable<int> GetStates() => Array.ConvertAll(_states.ToArray(), state => _stateId[state]);

        public bool IsFinalState(int state) => _states[state].IsNullable(0);

        public IEnumerable<Move<S>> GetMoves()
        {
            foreach (var entry in _normalizedmoves)
                yield return Move<S>.Create(entry.Key.Item1, entry.Key.Item2, entry.Value);
        }

        private static string HTMLEncodeChars(string s) => s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string ViewState(State<S> state)
        {
            if (state.PrevCharKindId == CharKindId.None)
                return HTMLEncodeChars(state.Node.ToString());
            else
                return string.Format("Last char: {0}&#13;{1}", state.PrevCharKindId, HTMLEncodeChars(state.Node.ToString()));
        }
    }
}
