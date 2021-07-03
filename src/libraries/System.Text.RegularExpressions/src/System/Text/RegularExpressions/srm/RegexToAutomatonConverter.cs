// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Provides functionality to convert .NET regex patterns to corresponding symbolic regexes
    /// </summary>
    internal class RegexToAutomatonConverter<S>
    {
        private ICharAlgebra<S> solver;

        internal Unicode.UnicodeCategoryTheory<S> categorizer;

        internal SymbolicRegexBuilder<S> srBuilder;

        private CultureInfo _culture;

        /// <summary>
        /// The character solver associated with the regex converter
        /// </summary>
        public ICharAlgebra<S> Solver
        {
            get
            {
                return solver;
            }
        }

        /// <summary>
        /// Constructs a regex to symbolic finite automata converter
        /// </summary>
        public RegexToAutomatonConverter(Unicode.UnicodeCategoryTheory<S> categorizer, CultureInfo culture)
        {
            _culture = culture;
            this.solver = categorizer.solver;
            this.categorizer = categorizer;
            this.srBuilder = new SymbolicRegexBuilder<S>(solver);
        }

        #region Character sequences

        private const int SETLENGTH = 1;
        private const int CATEGORYLENGTH = 2;
        private const int SETSTART = 3;
        private const char Lastchar = '\uFFFF';

        internal S CreateConditionFromSet(bool ignoreCase, string set)
        {
            //char at position 0 is 1 iff the set is negated
            //bool negate = ((int)set[0] == 1);
            bool negate = RegexCharClass.IsNegated(set);

            //following are conditions over characters in the set
            //these will become disjuncts of a single disjunction
            //or conjuncts of a conjunction in case negate is true
            //negation is pushed in when the conditions are created
            List<S> conditions = new List<S>();

            #region ranges
            var ranges = ComputeRanges(set);

            foreach (var range in ranges)
            {
                S cond = solver.MkRangeConstraint(range.Item1, range.Item2, ignoreCase, _culture.Name);
                conditions.Add(negate ? solver.MkNot(cond) : cond);
            }
            #endregion

            #region categories
            int setLength = set[SETLENGTH];
            int catLength = set[CATEGORYLENGTH];
            //int myEndPosition = SETSTART + setLength + catLength;

            int catStart = setLength + SETSTART;
            int j = catStart;
            while (j < catStart + catLength)
            {
                //singleton categories are stored as unicode characters whose code is
                //1 + the unicode category code as a short
                //thus - 1 is applied to exctarct the actual code of the category
                //the category itself may be negated e.g. \D instead of \d
                short catCode = (short)set[j++];
                if (catCode != 0)
                {
                    //note that double negation cancels out the negation of the category
                    S cond = MapCategoryCodeToCondition(Math.Abs(catCode) - 1);
                    conditions.Add(catCode < 0 ^ negate ? solver.MkNot(cond) : cond);
                }
                else
                {
                    //special case for a whole group G of categories surrounded by 0's
                    //essentially 0 C1 C2 ... Cn 0 ==> G = (C1 | C2 | ... | Cn)
                    catCode = (short)set[j++];
                    if (catCode == 0)
                        continue; //empty set of categories

                    //collect individual category codes into this set
                    var catCodes = new HashSet<int>();
                    //if the first catCode is negated, the group as a whole is negated
                    bool negGroup = (catCode < 0);

                    while (catCode != 0)
                    {
                        catCodes.Add(Math.Abs(catCode) - 1);
                        catCode = (short)set[j++];
                    }

                    // C1 | C2 | ... | Cn
                    S catCondDisj = MapCategoryCodeSetToCondition(catCodes);

                    S catGroupCond = (negate ^ negGroup ? solver.MkNot(catCondDisj) : catCondDisj);
                    conditions.Add(catGroupCond);
                }
            }
            #endregion

            #region Subtractor
            S subtractorCond = default(S);
            if (set.Length > j)
            {
                //the set has a subtractor-set at the end
                //all characters in the subtractor-set are excluded from the set
                //note that the subtractor sets may be nested, e.g. in r=[a-z-[b-g-[cd]]]
                //the subtractor set [b-g-[cd]] has itself a subtractor set [cd]
                //thus r is the set of characters between a..z except b,e,f,g
                var subtractor = set.Substring(j);
                subtractorCond = CreateConditionFromSet(ignoreCase, subtractor);
            }

            #endregion

            S moveCond;
            //if there are no ranges and no groups then there are no conditions
            //this situation arises for SingleLine regegex option and .
            //and means that all characters are accepted
            if (conditions.Count == 0)
                moveCond = (negate ? solver.False : solver.True);
            else
                moveCond = (negate ? solver.MkAnd(conditions) : solver.MkOr(conditions));

            //Subtelty of regex sematics:
            //note that the subtractor is not within the scope of the negation (if there is a negation)
            //thus the negated subtractor is conjuncted with moveCond after the negation has been
            //performed above
            if (!object.Equals(subtractorCond, default(S)))
            {
                moveCond = solver.MkAnd(moveCond, solver.MkNot(subtractorCond));
            }

            return moveCond;
        }

        private static List<Tuple<char, char>> ComputeRanges(string set)
        {
            int setLength = set[SETLENGTH];

            var ranges = new List<Tuple<char, char>>(setLength);
            int i = SETSTART;
            int end = i + setLength;
            while (i < end)
            {
                char first = set[i];
                i++;

                char last;
                if (i < end)
                    last = (char)(set[i] - 1);
                else
                    last = Lastchar;
                i++;
                ranges.Add(new Tuple<char, char>(first, last));
            }
            return ranges;
        }

        private S MapCategoryCodeSetToCondition(HashSet<int> catCodes)
        {
            //TBD: perhaps other common cases should be specialized similarly
            //check first if all word character category combinations are covered
            //which is the most common case, then use the combined predicate \w
            //rather than a disjunction of the component category predicates
            //the word character class \w covers categories 0,1,2,3,4,8,18
            S catCond = default(S);
            if (catCodes.Contains(0) && catCodes.Contains(1) && catCodes.Contains(2) && catCodes.Contains(3) &&
                catCodes.Contains(4) && catCodes.Contains(8) && catCodes.Contains(18))
            {
                catCodes.Remove(0);
                catCodes.Remove(1);
                catCodes.Remove(2);
                catCodes.Remove(3);
                catCodes.Remove(4);
                catCodes.Remove(8);
                catCodes.Remove(18);
                catCond = categorizer.WordLetterCondition;
            }
            foreach (var cat in catCodes)
            {
                S cond = MapCategoryCodeToCondition(cat);
                catCond = (object.Equals(catCond, default(S)) ? cond : solver.MkOr(catCond, cond));
            }
            return catCond;
        }

        private S MapCategoryCodeToCondition(int code)
        {
            //whitespace has special code 99
            if (code == 99)
                return categorizer.WhiteSpaceCondition;

            //other codes must be valid UnicodeCategory codes
            if (code < 0 || code > 29)
                throw new ArgumentOutOfRangeException(nameof(code), "Must be in the range 0..29 or equal to 99");

            return categorizer.CategoryCondition(code);
        }

        #endregion

        #region Symbolic regex conversion

        internal SymbolicRegexNode<S> ConvertNodeToSymbolicRegex(RegexNode node, bool topLevel)
        {
            switch (node.Type)
            {
                case RegexNode.Alternate:
                    return this.srBuilder.MkOr(Array.ConvertAll(node.ChildrenToArray(), x => ConvertNodeToSymbolicRegex(x, topLevel)));
                case RegexNode.Beginning:
                    return this.srBuilder.startAnchor;
                case RegexNode.Bol:
                    // update the \n predicate in the builder if it has not been updated already
                    if (srBuilder.newLinePredicate.Equals(srBuilder.solver.False))
                        srBuilder.newLinePredicate = srBuilder.solver.MkCharConstraint('\n');
                    return this.srBuilder.bolAnchor;
                case RegexNode.Capture:  //treat as non-capturing group (...)
                    return ConvertNodeToSymbolicRegex(node.Child(0), topLevel);
                case RegexNode.Concatenate:
                    return this.srBuilder.MkConcat(Array.ConvertAll(node.ChildrenToArray(), x => ConvertNodeToSymbolicRegex(x, false)), topLevel);
                case RegexNode.Empty:
                case RegexNode.UpdateBumpalong: // optional directive that behaves the same as Empty
                    return this.srBuilder.epsilon;
                case RegexNode.End:  // \z anchor
                    return this.srBuilder.endAnchor;
                case RegexNode.EndZ: // \Z anchor
                    // update the \n predicate in the builder if it has not been updated already
                    if (srBuilder.newLinePredicate.Equals(srBuilder.solver.False))
                        srBuilder.newLinePredicate = srBuilder.solver.MkCharConstraint('\n');
                    return this.srBuilder.endAnchorZ;
                case RegexNode.Eol:
                    // update the \n predicate in the builder if it has not been updated already
                    if (srBuilder.newLinePredicate.Equals(srBuilder.solver.False))
                        srBuilder.newLinePredicate = srBuilder.solver.MkCharConstraint('\n');
                    return this.srBuilder.eolAnchor;
                case RegexNode.Loop:
                    return this.srBuilder.MkLoop(ConvertNodeToSymbolicRegex(node.Child(0), false), false, node.M, node.N, topLevel);
                case RegexNode.Lazyloop:
                    return this.srBuilder.MkLoop(ConvertNodeToSymbolicRegex(node.Child(0), false), true, node.M, node.N, topLevel);
                case RegexNode.Multi:
                    return ConvertNodeMultiToSymbolicRegex(node, topLevel);
                case RegexNode.Notone:
                    return ConvertNodeNotoneToSymbolicRegex(node);
                case RegexNode.Notoneloop:
                    return ConvertNodeNotoneloopToSymbolicRegex(node, false);
                case RegexNode.Notonelazy:
                    return ConvertNodeNotoneloopToSymbolicRegex(node, true);
                case RegexNode.One:
                    return ConvertNodeOneToSymbolicRegex(node);
                case RegexNode.Oneloop:
                    return ConvertNodeOneloopToSymbolicRegex(node, false);
                case RegexNode.Onelazy:
                    return ConvertNodeOneloopToSymbolicRegex(node, true);
                case RegexNode.Set:
                    return ConvertNodeSetToSymbolicRegex(node);
                case RegexNode.Setloop:
                    return ConvertNodeSetloopToSymbolicRegex(node, false);
                case RegexNode.Setlazy:
                    return ConvertNodeSetloopToSymbolicRegex(node, true);
                case RegexNode.Testgroup:
                    return MkIfThenElse(ConvertNodeToSymbolicRegex(node.Child(0), false), ConvertNodeToSymbolicRegex(node.Child(1), false), ConvertNodeToSymbolicRegex(node.Child(2), false));
                // TBD: ECMA case intersect predicate with ascii range ?
                case RegexNode.ECMABoundary:
                case RegexNode.Boundary:
                    // update the word letter predicate based on the Unicode definition of it if it was not updated already
                    if (srBuilder.wordLetterPredicate.Equals(srBuilder.solver.False))
                        srBuilder.wordLetterPredicate = categorizer.WordLetterCondition;
                    return this.srBuilder.wbAnchor;
                // TBD: ECMA case intersect predicate with ascii range ?
                case RegexNode.NonECMABoundary:
                case RegexNode.NonBoundary:
                    // update the word letter predicate based on the Unicode definition of it if it was not updated already
                    if (srBuilder.wordLetterPredicate.Equals(srBuilder.solver.False))
                        srBuilder.wordLetterPredicate = categorizer.WordLetterCondition;
                    return this.srBuilder.nwbAnchor;
                case RegexNode.Nothing:
                    return this.srBuilder.nothing;
                default:
                    throw new NotSupportedException(SRM.Regex._DFA_incompatible_with + DescribeRegexNodeType(node.Type));
            }
        }

        /// <summary>
        /// Used in exception messages for nonsupported node types
        /// </summary>
        private static string DescribeRegexNodeType(int node_type)
        {
            string description = node_type switch
            {
                RegexNode.Ref => "backreference (\\ number)",
                RegexNode.Testref => "captured group conditional (?( name ) yes-pattern | no-pattern ) or (?( number ) yes-pattern| no-pattern )",
                RegexNode.Require => "positive lookahead (?= pattern) or positive lookbehind (?<= pattern)",
                RegexNode.Prevent => "negative lookahead (?! pattern) or negative lookbehind (?<! pattern)",
                RegexNode.Start => "contiguous matches (\\G)",
                RegexNode.Atomic => "atomic (nonbacktracking) subexpression (?> pattern)",
                RegexNode.Setloopatomic => "atomic (nonbacktracking) subexpression (?> pattern)",
                RegexNode.Oneloopatomic => "atomic (nonbacktracking) subexpression (?> pattern)",
                RegexNode.Notoneloopatomic => "atomic (nonbacktracking) subexpression (?> pattern)",
                // the default should never arise, since other node types are either supported
                // or have been removed (e.g. Group) from the final parse tree
                _ => $"unexpected node type ({nameof(RegexNode)}:{node_type})"
            };
            return description;
        }

        public static string Escape(char c)
        {
            int code = (int)c;
            if (code > 126)
                return ToUnicodeRepr(code);

            if (code < 32)
                return string.Format("\\x{0:X}", code);

            switch (c)
            {
                case '\0':
                    return @"\0";
                case '\a':
                    return @"\a";
                case '\b':
                    return @"\b";
                case '\t':
                    return @"\t";
                case '\r':
                    return @"\r";
                case '\v':
                    return @"\v";
                case '\f':
                    return @"\f";
                case '\n':
                    return @"\n";
                case '\u001B':
                    return @"\e";
                case '\"':
                    return "\\\"";
                case '\'':
                    return "\\\'";
                case ' ':
                    return " ";
                default:
                    if (code < 32)
                        return string.Format("\\x{0:X}", code);
                    else
                        return c.ToString();
            }
        }

        private static string ToUnicodeRepr(int i)
        {
            string s = string.Format("{0:X}", i);
            if (s.Length == 1)
                s = "\\u000" + s;
            else if (s.Length == 2)
                s = "\\u00" + s;
            else if (s.Length == 3)
                s = "\\u0" + s;
            else
                s = "\\u" + s;
            return s;
        }

        #region Character sequences to symbolic regexes
        /// <summary>
        /// Sequence of characters in node._str
        /// </summary>
        private SymbolicRegexNode<S> ConvertNodeMultiToSymbolicRegex(RegexNode node, bool topLevel)
        {
            //sequence of characters
            string sequence = node.Str;
            bool ignoreCase = ((node.Options & System.Text.RegularExpressions.RegexOptions.IgnoreCase) != 0);

            S[] conds = Array.ConvertAll(sequence.ToCharArray(), c => solver.MkCharConstraint(c, ignoreCase, _culture.Name));
            var seq = this.srBuilder.MkSequence(conds, topLevel);
            return seq;
        }

        /// <summary>
        /// Matches chacter any character except node._ch
        /// </summary>
        private SymbolicRegexNode<S> ConvertNodeNotoneToSymbolicRegex(RegexNode node)
        {
            bool ignoreCase = ((node.Options & System.Text.RegularExpressions.RegexOptions.IgnoreCase) != 0);

            S cond = solver.MkNot(solver.MkCharConstraint(node.Ch, ignoreCase, _culture.Name));

            return this.srBuilder.MkSingleton(cond);
        }

        /// <summary>
        /// Matches only node._ch
        /// </summary>
        private SymbolicRegexNode<S> ConvertNodeOneToSymbolicRegex(RegexNode node)
        {
            bool ignoreCase = ((node.Options & System.Text.RegularExpressions.RegexOptions.IgnoreCase) != 0);

            S cond = solver.MkCharConstraint(node.Ch, ignoreCase, _culture.Name);

            return this.srBuilder.MkSingleton(cond);
        }

        #endregion

        #region special loops
        private SymbolicRegexNode<S> ConvertNodeSetToSymbolicRegex(RegexNode node)
        {
            //ranges and categories are encoded in set
            string set = node.Str;

            S moveCond = CreateConditionFromSet((node.Options & System.Text.RegularExpressions.RegexOptions.IgnoreCase) != 0, set);

            return this.srBuilder.MkSingleton(moveCond);
        }

        private SymbolicRegexNode<S> ConvertNodeNotoneloopToSymbolicRegex(RegexNode node, bool isLazy)
        {
            bool ignoreCase = ((node.Options & System.Text.RegularExpressions.RegexOptions.IgnoreCase) != 0);
            S cond = solver.MkNot(solver.MkCharConstraint(node.Ch, ignoreCase, _culture.Name));

            SymbolicRegexNode<S> body = this.srBuilder.MkSingleton(cond);
            SymbolicRegexNode<S> loop = this.srBuilder.MkLoop(body, isLazy, node.M, node.N);
            return loop;
        }

        private SymbolicRegexNode<S> ConvertNodeOneloopToSymbolicRegex(RegexNode node, bool isLazy)
        {
            bool ignoreCase = ((node.Options & System.Text.RegularExpressions.RegexOptions.IgnoreCase) != 0);
            S cond = solver.MkCharConstraint(node.Ch, ignoreCase, _culture.Name);

            SymbolicRegexNode<S> body = this.srBuilder.MkSingleton(cond);
            SymbolicRegexNode<S> loop = this.srBuilder.MkLoop(body, isLazy, node.M, node.N);
            return loop;
        }

        private SymbolicRegexNode<S> ConvertNodeSetloopToSymbolicRegex(RegexNode node, bool isLazy)
        {
            //ranges and categories are encoded in set
            string set = node.Str;

            S moveCond = CreateConditionFromSet((node.Options & System.Text.RegularExpressions.RegexOptions.IgnoreCase) != 0, set);

            SymbolicRegexNode<S> body = this.srBuilder.MkSingleton(moveCond);
            SymbolicRegexNode<S> loop = this.srBuilder.MkLoop(body, isLazy, node.M, node.N);
            return loop;
        }

        #endregion

        /// <summary>
        /// Make an if-then-else regex (?(cond)left|right)
        /// </summary>
        /// <param name="cond">condition</param>
        /// <param name="left">true case</param>
        /// <param name="right">false case</param>
        /// <returns></returns>
        public SymbolicRegexNode<S> MkIfThenElse(SymbolicRegexNode<S> cond, SymbolicRegexNode<S> left, SymbolicRegexNode<S> right)
        {
            return this.srBuilder.MkIfThenElse(cond, left, right);
        }

        /// <summary>
        /// Make a singleton sequence regex
        /// </summary>
        public SymbolicRegexNode<S> MkSingleton(S set)
        {
            return this.srBuilder.MkSingleton(set);
        }

        public SymbolicRegexNode<S> MkOr(params SymbolicRegexNode<S>[] regexes)
        {
            return this.srBuilder.MkOr(regexes);
        }

        public SymbolicRegexNode<S> MkConcat(params SymbolicRegexNode<S>[] regexes)
        {
            return this.srBuilder.MkConcat(regexes, false);
        }

        public SymbolicRegexNode<S> MkEpsilon()
        {
            return this.srBuilder.epsilon;
        }

        public SymbolicRegexNode<S> MkLoop(SymbolicRegexNode<S> regex, int lower = 0, int upper = int.MaxValue, bool isLazy = false)
        {
            return this.srBuilder.MkLoop(regex, isLazy, lower, upper);
        }

        #endregion
    }
}
