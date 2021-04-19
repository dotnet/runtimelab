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
        private Dictionary<int, State<S>> _states = new Dictionary<int, State<S>>();
        private Dictionary<Tuple<int, int>, S> _normalizedmoves = new Dictionary<Tuple<int, int>, S>();
        private ICharAlgebra<S> _solver;
        private bool _hideDerivatives;

        internal RegexDFA(SymbolicRegexMatcher<S> srm, int bound, bool hideDerivatives, bool addDotStar)
        {
            _solver = srm.builder.solver;
            _hideDerivatives = hideDerivatives;
            CharKindId startId = (srm.A.info.StartsWithSomeAnchor ? CharKindId.Start : CharKindId.None);
            _q0 = State<S>.MkState(addDotStar ? srm.A1 : srm.A, startId);
            var stack = new Stack<State<S>>();
            stack.Push(_q0);
            _states[_q0.Id] = _q0;
            var partition = _solver.GetPartition();
            //construct until the stack is empty or the bound has been reached
            while (stack.Count > 0 && (bound <= 0 || _states.Count < bound))
            {
                var q = stack.Pop();
                foreach (var c in partition)
                {
                    var p = q.Next(c);
                    //check that p is not a dead-end
                    if (!p.IsNothing)
                    {
                        if (!_states.ContainsKey(p.Id))
                        {
                            stack.Push(p);
                            _states[p.Id] = p;
                        }
                        var qp = new Tuple<int, int>(q.Id, p.Id);
                        if (_normalizedmoves.ContainsKey(qp))
                            _normalizedmoves[qp] = _solver.MkOr(_normalizedmoves[qp], c);
                        else
                            _normalizedmoves[qp] = c;
                    }
                }
            }
        }

        public int InitialState => _q0.Id;

        public string DescribeLabel(S lab) => EncodeChars(_solver.PrettyPrint(lab));

        public string DescribeStartLabel() => "";

        public string DescribeState(int state) => (_hideDerivatives ? state.ToString() : ViewState(_states[state]));

        public IEnumerable<int> GetStates() => _states.Keys;

        public bool IsFinalState(int state) => _states[state].IsNullable(0);

        public IEnumerable<Move<S>> GetMoves()
        {
            foreach (var entry in _normalizedmoves)
                yield return Move<S>.Create(entry.Key.Item1, entry.Key.Item2, entry.Value);
        }

        private static string EncodeChars(string s)
        {
            string s1 = SRM.StringUtility.Escape(s);
            string s2 = s1.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
            return s2;
        }

        private static string ViewState(State<S> state)
        {
            if (state.PrevCharKindId == CharKindId.None)
                return EncodeChars(state.Node.ToString());
            else
                return string.Format("Prev:{0}&#13;{1}", state.PrevCharKindId, EncodeChars(state.Node.ToString()));
        }
    }
}
