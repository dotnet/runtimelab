// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace System.Text.RegularExpressions.SRM.DGML
{
    internal class DgmlWriter
    {
        private int _maxDgmlTransitionLabelLength;
        private TextWriter _tw;
        private bool _hideDerivatives;

        internal DgmlWriter(TextWriter tw, bool hideDerivatives, int maxDgmlTransitionLabelLength = 500)
        {
            _maxDgmlTransitionLabelLength = maxDgmlTransitionLabelLength;
            _tw = tw;
            _hideDerivatives = hideDerivatives;
        }

        /// <summary>
        /// Write the automaton in dgml format into the textwriter.
        /// </summary>
        public void Write<S>(IAutomaton<S> fa)
        {
            var nonEpsilonMoves = new Dictionary<Tuple<int, int>, List<S>>();
            var epsilonmoves = new List<Move<S>>();

            var nonEpsilonStates = new HashSet<int>();
            Func<int, bool> IsEpsilonState = (s => !nonEpsilonStates.Contains(s));

            foreach (var move in fa.GetMoves())
            {
                if (move.IsEpsilon)
                    epsilonmoves.Add(move);

                else
                {
                    nonEpsilonStates.Add(move.SourceState);
                    List<S> rules;
                    var p = new Tuple<int, int>(move.SourceState, move.TargetState);
                    if (!nonEpsilonMoves.TryGetValue(p, out rules))
                    {
                        rules = new List<S>();
                        nonEpsilonMoves[p] = rules;
                    }
                    rules.Add(move.Label);
                }
            }

            _tw.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            _tw.WriteLine("<DirectedGraph xmlns=\"http://schemas.microsoft.com/vs/2009/dgml\" ZoomLevel=\"1.5\" GraphDirection=\"TopToBottom\" >");
            _tw.WriteLine("<Nodes>");
            _tw.WriteLine("<Node Id=\"init\" Label=\" \" />");
            foreach (int state in fa.GetStates())
            {
                _tw.WriteLine("<Node Id=\"{0}\" Label=\"q{1}\" Category=\"State\" Group=\"{2}\">", state,
                    state == fa.InitialState ? string.Format("{0} (|Q|={1})", state, fa.StateCount) : state.ToString(),
                    _hideDerivatives ? "Collapsed" : "Expanded");
                if (state == fa.InitialState)
                    _tw.WriteLine("<Category Ref=\"InitialState\" />");
                if (fa.IsFinalState(state))
                    _tw.WriteLine("<Category Ref=\"FinalState\" />");
                _tw.WriteLine("</Node>");
                _tw.WriteLine("<Node Id=\"{0}info\" Label=\"{1}\" Category=\"StateInfo\"/>", state, fa.DescribeState(state));
            }
            _tw.WriteLine("</Nodes>");
            _tw.WriteLine("<Links>");
            _tw.WriteLine("<Link Source=\"init\" Target=\"{0}\" Label=\"{1}\" Category=\"StartTransition\" />", fa.InitialState, fa.DescribeStartLabel());
            foreach (var move in epsilonmoves)
                _tw.WriteLine("<Link Source=\"{0}\" Target=\"{1}\" Category=\"EpsilonTransition\" />", move.SourceState, move.TargetState);

            foreach (var move in nonEpsilonMoves)
                _tw.WriteLine(GetNonFinalRuleInfo(fa, move.Key.Item1, move.Key.Item2, move.Value));

            foreach (int state in fa.GetStates())
                _tw.WriteLine("<Link Source=\"{0}\" Target=\"{0}info\" Category=\"Contains\" />", state);
            _tw.WriteLine("</Links>");
            WriteCategoriesAndStyles();
            _tw.WriteLine("</DirectedGraph>");
        }

        private string GetNonFinalRuleInfo<S>(IAutomaton<S> aut, int source, int target, List<S> rules)
        {
            string lab = "";
            string info = "";
            for (int i = 0; i < rules.Count; i++)
            {
                lab += (lab == "" ? "" : ",\n ") + aut.DescribeLabel(rules[i]);
            }
            var lab_length = lab.Length;
            if (_maxDgmlTransitionLabelLength >= 0 && lab_length > _maxDgmlTransitionLabelLength)
            {
                info += string.Format(" FullLabel = \"{0}\"", lab);
                lab = lab.Substring(0, _maxDgmlTransitionLabelLength) + "..";
            }
            return string.Format("<Link Source=\"{0}\" Target=\"{1}\" Label=\"{2}\" Category=\"NonepsilonTransition\" {3}/>", source, target, lab, info);
        }

        private void WriteCategoriesAndStyles()
        {
            _tw.WriteLine("<Categories>");
            _tw.WriteLine("<Category Id=\"EpsilonTransition\" Label=\"Epsilon transition\" IsTag=\"True\" />");
            _tw.WriteLine("<Category Id=\"StartTransition\" Label=\"Initial transition\" IsTag=\"True\" />");
            _tw.WriteLine("<Category Id=\"FinalLabel\" Label=\"Final transition\" IsTag=\"True\" />");
            _tw.WriteLine("<Category Id=\"FinalState\" Label=\"Final\" IsTag=\"True\" />");
            _tw.WriteLine("<Category Id=\"SinkState\" Label=\"Sink state\" IsTag=\"True\" />");
            _tw.WriteLine("<Category Id=\"EpsilonState\" Label=\"Epsilon state\" IsTag=\"True\" />");
            _tw.WriteLine("<Category Id=\"InitialState\" Label=\"Initial\" IsTag=\"True\" />");
            _tw.WriteLine("<Category Id=\"NonepsilonTransition\" Label=\"Nonepsilon transition\" IsTag=\"True\" />");
            _tw.WriteLine("<Category Id=\"State\" Label=\"State\" IsTag=\"True\" />");
            _tw.WriteLine("</Categories>");
            _tw.WriteLine("<Styles>");
            _tw.WriteLine("<Style TargetType=\"Node\" GroupLabel=\"InitialState\" ValueLabel=\"True\">");
            _tw.WriteLine("<Condition Expression=\"HasCategory('InitialState')\" />");
            _tw.WriteLine("<Setter Property=\"Background\" Value=\"lightgray\" />");
            _tw.WriteLine("<Setter Property=\"MinWidth\" Value=\"0\" />");
            _tw.WriteLine("</Style>");
            _tw.WriteLine("<Style TargetType=\"Node\" GroupLabel=\"FinalState\" ValueLabel=\"True\">");
            _tw.WriteLine("<Condition Expression=\"HasCategory('FinalState')\" />");
            _tw.WriteLine("<Setter Property=\"Background\" Value=\"lightgreen\" />");
            _tw.WriteLine("<Setter Property=\"StrokeThickness\" Value=\"4\" />");
            //tw.WriteLine("<Setter Property=\"Background\" Value=\"white\" />");
            //tw.WriteLine("<Setter Property=\"MinWidth\" Value=\"0\" />");
            _tw.WriteLine("</Style>");
            //_tw.WriteLine("<Style TargetType=\"Node\" GroupLabel=\"SinkState\" ValueLabel=\"True\">");
            //_tw.WriteLine("<Condition Expression=\"HasCategory('SinkState')\" />");
            //_tw.WriteLine("<Setter Property=\"NodeRadius\" Value=\"0\" />");
            //_tw.WriteLine("</Style>");
            //_tw.WriteLine("<Style TargetType=\"Node\" GroupLabel=\"EpsilonState\" ValueLabel=\"True\">");
            //_tw.WriteLine("<Condition Expression=\"HasCategory('EpsilonState')\" />");
            //_tw.WriteLine("<Setter Property=\"Background\" Value=\"tomato\" />");
            //_tw.WriteLine("</Style>");
            _tw.WriteLine("<Style TargetType=\"Node\" GroupLabel=\"State\" ValueLabel=\"True\">");
            _tw.WriteLine("<Condition Expression=\"HasCategory('State')\" />");
            _tw.WriteLine("<Setter Property=\"Stroke\" Value=\"black\" />");
            _tw.WriteLine("<Setter Property=\"Background\" Value=\"white\" />");
            _tw.WriteLine("<Setter Property=\"MinWidth\" Value=\"0\" />");
            _tw.WriteLine("<Setter Property=\"FontSize\" Value=\"12\" />");
            _tw.WriteLine("<Setter Property=\"FontFamily\" Value=\"Arial\" />");
            _tw.WriteLine("</Style>");
            _tw.WriteLine("<Style TargetType=\"Link\" GroupLabel=\"NonepsilonTransition\" ValueLabel=\"True\">");
            _tw.WriteLine("<Condition Expression=\"HasCategory('NonepsilonTransition')\" />");
            _tw.WriteLine("<Setter Property=\"Stroke\" Value=\"black\" />");
            _tw.WriteLine("<Setter Property=\"FontSize\" Value=\"18\" />");
            _tw.WriteLine("<Setter Property=\"FontFamily\" Value=\"Arial\" />");
            _tw.WriteLine("</Style>");
            _tw.WriteLine("<Style TargetType=\"Link\" GroupLabel=\"StartTransition\" ValueLabel=\"True\">");
            _tw.WriteLine("<Condition Expression=\"HasCategory('StartTransition')\" />");
            _tw.WriteLine("<Setter Property=\"Stroke\" Value=\"black\" />");
            _tw.WriteLine("</Style>");
            _tw.WriteLine("<Style TargetType=\"Link\" GroupLabel=\"EpsilonTransition\" ValueLabel=\"True\">");
            _tw.WriteLine("<Condition Expression=\"HasCategory('EpsilonTransition')\" />");
            _tw.WriteLine("<Setter Property=\"Stroke\" Value=\"black\" />");
            _tw.WriteLine("<Setter Property=\"StrokeDashArray\" Value=\"8 8\" />");
            _tw.WriteLine("</Style>");
            _tw.WriteLine("<Style TargetType=\"Link\" GroupLabel=\"FinalLabel\" ValueLabel=\"False\">");
            _tw.WriteLine("<Condition Expression=\"HasCategory('FinalLabel')\" />");
            _tw.WriteLine("<Setter Property=\"Stroke\" Value=\"black\" />");
            _tw.WriteLine("<Setter Property=\"StrokeDashArray\" Value=\"8 8\" />");
            _tw.WriteLine("</Style>");
            _tw.WriteLine("<Style TargetType=\"Node\" GroupLabel=\"StateInfo\" ValueLabel=\"True\">");
            _tw.WriteLine("<Setter Property=\"Stroke\" Value=\"white\" />");
            _tw.WriteLine("<Setter Property=\"FontSize\" Value=\"18\" />");
            _tw.WriteLine("<Setter Property=\"FontFamily\" Value=\"Arial\" />");
            _tw.WriteLine("</Style>");
            _tw.WriteLine("</Styles>");
        }
    }
}
