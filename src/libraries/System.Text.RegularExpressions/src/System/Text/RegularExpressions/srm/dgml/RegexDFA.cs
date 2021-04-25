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
        private List<int> _states = new();
        private HashSet<int> _stateSet = new();
        private List<Move<S>> _moves = new();
        private SymbolicRegexBuilder<S> _builder;

        internal RegexDFA(SymbolicRegexMatcher<S> srm, int bound,  bool addDotStar, bool inReverse)
        {
            _builder = srm.builder;
            CharKindId startId = (inReverse ? (srm.Ar.info.StartsWithLineAnchor ? CharKindId.End : CharKindId.None)
                                            : (srm.A.info.StartsWithLineAnchor ? CharKindId.Start : CharKindId.None));
            //inReverse only matters if Ar contains some line anchor
            _q0 = State<S>.MkState(inReverse ? srm.Ar : (addDotStar ? srm.A1 : srm.A), startId, srm.A.info.ContainsLineAnchor ? inReverse : false);
            var stack = new Stack<State<S>>();
            stack.Push(_q0);
            _states.Add(_q0.Id);
            _stateSet.Add(_q0.Id);
            var partition = _builder.solver.GetPartition();
            Dictionary<Tuple<int, int>, S> normalizedmoves = new();
            //unwind until the stack is empty or the bound has been reached
            while (stack.Count > 0 && (bound <= 0 || _states.Count < bound))
            {
                var q = stack.Pop();
                foreach (var c in partition)
                {
                    var p = q.Next(c);
                    //check that p is not a dead-end
                    if (!p.IsNothing)
                    {
                        if (_stateSet.Add(p.Id))
                        {
                            stack.Push(p);
                            _states.Add(p.Id);
                        }
                        var qp = new Tuple<int, int>(q.Id, p.Id);
                        if (normalizedmoves.ContainsKey(qp))
                            normalizedmoves[qp] = _builder.solver.MkOr(normalizedmoves[qp], c);
                        else
                            normalizedmoves[qp] = c;
                    }
                }
            }
            foreach (var entry in normalizedmoves)
                _moves.Add(Move<S>.Create(entry.Key.Item1, entry.Key.Item2, entry.Value));
        }

        public S[] Alphabet => _builder.solver.GetPartition();

        public int InitialState => _q0.Id;

        public int StateCount => _states.Count;

        public int TransitionCount => _moves.Count;

        public string DescribeLabel(S lab) => HTMLEncodeChars(_builder.solver.PrettyPrint(lab));

        public string DescribeStartLabel() => "";

        public string DescribeState(int state) => ViewState(_builder.statearray[state]);

        public IEnumerable<int> GetStates() => _states;

        public bool IsFinalState(int state) => _builder.statearray[state].IsNullable(_builder.statearray[state].IsReverse ? CharKind.Start : CharKind.End);

        public IEnumerable<Move<S>> GetMoves() => _moves;

        private static string HTMLEncodeChars(string s) => s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

        private const string REVERSE_SYMBOL = "&#x1F501;";

        private string ViewState(State<S> state)
        {
            string deriv = HTMLEncodeChars(state.Node.ToString());
            string info = CharKind.PrettyPrint(state.PrevCharKindId);
            string rev = state.IsReverse ? REVERSE_SYMBOL + "&#13;" : "";
            if (info != string.Empty)
                info = string.Format("Previous: {0}&#13;", info);
            if (deriv == string.Empty)
                deriv = "()";
            return string.Format("{0}{1}{2}", rev, info, deriv);
        }
    }
}
