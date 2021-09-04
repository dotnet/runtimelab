// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Provides functionality to convert .NET regex patterns to corresponding symbolic regexes
    /// </summary>
    internal sealed class RegexToAutomatonConverter<T> where T : class
    {
        private readonly ICharAlgebra<T> _solver;

        internal readonly Unicode.UnicodeCategoryTheory<T> _categorizer;

        internal readonly SymbolicRegexBuilder<T> _srBuilder;

        private readonly CultureInfo _culture;

        private readonly Dictionary<(bool, string), T> _createConditionFromSet_Cache = new();

        /// <summary>
        /// The character solver associated with the regex converter
        /// </summary>
        public ICharAlgebra<T> Solver => _solver;

        /// <summary>
        /// Constructs a regex to symbolic finite automata converter
        /// </summary>
        public RegexToAutomatonConverter(Unicode.UnicodeCategoryTheory<T> categorizer, CultureInfo culture)
        {
            _culture = culture;
            _solver = categorizer._solver;
            _categorizer = categorizer;
            _srBuilder = new SymbolicRegexBuilder<T>(_solver);
        }

        #region Character sequences

        internal T CreateConditionFromSet(bool ignoreCase, string set)
        {
            (bool ignoreCase, string set) key = (ignoreCase, set);
            if (!_createConditionFromSet_Cache.TryGetValue(key, out T result))
            {
                _createConditionFromSet_Cache[key] = result = CreateConditionFromSet_compute(ignoreCase, set);
            }

            return result;
        }

        private T CreateConditionFromSet_compute(bool ignoreCase, string set)
        {
            //char at position 0 is 1 iff the set is negated
            //bool negate = ((int)set[0] == 1);
            bool negate = RegexCharClass.IsNegated(set);

            //following are conditions over characters in the set
            //these will become disjuncts of a single disjunction
            //or conjuncts of a conjunction in case negate is true
            //negation is pushed in when the conditions are created
            List<T> conditions = new List<T>();

            #region ranges
            List<Tuple<char, char>> ranges = ComputeRanges(set);

            foreach (Tuple<char, char> range in ranges)
            {
                T cond = _solver.RangeConstraint(range.Item1, range.Item2, ignoreCase, _culture.Name);
                conditions.Add(negate ? _solver.Not(cond) : cond);
            }
            #endregion

            #region categories
            int setLength = set[RegexCharClass.SetLengthIndex];
            int catLength = set[RegexCharClass.CategoryLengthIndex];
            //int myEndPosition = SETSTART + setLength + catLength;

            int catStart = setLength + RegexCharClass.SetStartIndex;
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
                    T cond = MapCategoryCodeToCondition(Math.Abs(catCode) - 1);
                    conditions.Add(catCode < 0 ^ negate ? _solver.Not(cond) : cond);
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
                    bool negGroup = catCode < 0;

                    while (catCode != 0)
                    {
                        catCodes.Add(Math.Abs(catCode) - 1);
                        catCode = (short)set[j++];
                    }

                    // C1 | C2 | ... | Cn
                    T catCondDisj = MapCategoryCodeSetToCondition(catCodes);

                    T catGroupCond = negate ^ negGroup ? _solver.Not(catCondDisj) : catCondDisj;
                    conditions.Add(catGroupCond);
                }
            }
            #endregion

            #region Subtractor
            T subtractorCond = default;
            if (set.Length > j)
            {
                //the set has a subtractor-set at the end
                //all characters in the subtractor-set are excluded from the set
                //note that the subtractor sets may be nested, e.g. in r=[a-z-[b-g-[cd]]]
                //the subtractor set [b-g-[cd]] has itself a subtractor set [cd]
                //thus r is the set of characters between a..z except b,e,f,g
                string subtractor = set.Substring(j);
                subtractorCond = CreateConditionFromSet(ignoreCase, subtractor);
            }

            #endregion

            //if there are no ranges and no groups then there are no conditions
            //this situation arises for SingleLine regegex option and .
            //and means that all characters are accepted
            T moveCond = conditions.Count == 0 ?
                (negate ? _solver.False : _solver.True) :
                (negate ? _solver.And(conditions) : _solver.Or(conditions));

            //Subtelty of regex sematics:
            //note that the subtractor is not within the scope of the negation (if there is a negation)
            //thus the negated subtractor is conjuncted with moveCond after the negation has been
            //performed above
            if (subtractorCond is not null)
            {
                moveCond = _solver.And(moveCond, _solver.Not(subtractorCond));
            }

            return moveCond;
        }

        private static List<Tuple<char, char>> ComputeRanges(string set)
        {
            int setLength = set[RegexCharClass.SetLengthIndex];

            var ranges = new List<Tuple<char, char>>(setLength);
            int i = RegexCharClass.SetStartIndex;
            int end = i + setLength;
            while (i < end)
            {
                char first = set[i];
                i++;

                char last = i < end ?
                    (char)(set[i] - 1) :
                    RegexCharClass.LastChar;

                i++;
                ranges.Add(new Tuple<char, char>(first, last));
            }
            return ranges;
        }

        private T MapCategoryCodeSetToCondition(HashSet<int> catCodes)
        {
            //TBD: perhaps other common cases should be specialized similarly
            //check first if all word character category combinations are covered
            //which is the most common case, then use the combined predicate \w
            //rather than a disjunction of the component category predicates
            //the word character class \w covers categories 0,1,2,3,4,8,18
            T catCond = default;
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
                catCond = _categorizer.WordLetterCondition;
            }
            foreach (int cat in catCodes)
            {
                T cond = MapCategoryCodeToCondition(cat);
                catCond = catCond is null ? cond : _solver.Or(catCond, cond);
            }
            return catCond;
        }

        private T MapCategoryCodeToCondition(int code) =>
            code switch
            {
                99 => _categorizer.WhiteSpaceCondition, //whitespace has special code 99
                < 0 or > 29 => throw new ArgumentOutOfRangeException(nameof(code), "Must be in the range 0..29 or equal to 99"),
                _ => _categorizer.CategoryCondition(code)
            };

        #endregion

        #region Symbolic regex conversion

        internal SymbolicRegexNode<T> ConvertNodeToSymbolicRegex(RegexNode node, bool topLevel)
        {
            switch (node.Type)
            {
                case RegexNode.Alternate:
                    return _srBuilder.MkOr(Array.ConvertAll(node.ChildrenToArray(), x => ConvertNodeToSymbolicRegex(x, topLevel)));
                case RegexNode.Beginning:
                    return _srBuilder._startAnchor;
                case RegexNode.Bol:
                    // update the \n predicate in the builder if it has not been updated already
                    if (_srBuilder._newLinePredicate.Equals(_srBuilder._solver.False))
                        _srBuilder._newLinePredicate = _srBuilder._solver.CharConstraint('\n');
                    return _srBuilder._bolAnchor;
                case RegexNode.Capture:  //treat as non-capturing group (...)
                    return ConvertNodeToSymbolicRegex(node.Child(0), topLevel);
                case RegexNode.Concatenate:
                    return _srBuilder.MkConcat(Array.ConvertAll(FlattenNestedConcatenations(node), x => ConvertNodeToSymbolicRegex(x, false)), topLevel);
                case RegexNode.Empty:
                case RegexNode.UpdateBumpalong: // optional directive that behaves the same as Empty
                    return _srBuilder._epsilon;
                case RegexNode.End:  // \z anchor
                    return _srBuilder._endAnchor;
                case RegexNode.EndZ: // \Z anchor
                    // update the \n predicate in the builder if it has not been updated already
                    if (_srBuilder._newLinePredicate.Equals(_srBuilder._solver.False))
                        _srBuilder._newLinePredicate = _srBuilder._solver.CharConstraint('\n');
                    return _srBuilder._endAnchorZ;
                case RegexNode.Eol:
                    // update the \n predicate in the builder if it has not been updated already
                    if (_srBuilder._newLinePredicate.Equals(_srBuilder._solver.False))
                        _srBuilder._newLinePredicate = _srBuilder._solver.CharConstraint('\n');
                    return _srBuilder._eolAnchor;
                case RegexNode.Loop:
                    return _srBuilder.MkLoop(ConvertNodeToSymbolicRegex(node.Child(0), false), false, node.M, node.N, topLevel);
                case RegexNode.Lazyloop:
                    return _srBuilder.MkLoop(ConvertNodeToSymbolicRegex(node.Child(0), false), true, node.M, node.N, topLevel);
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
                    if (_srBuilder._wordLetterPredicate.Equals(_srBuilder._solver.False))
                        _srBuilder._wordLetterPredicate = _categorizer.WordLetterCondition;
                    return _srBuilder._wbAnchor;
                // TBD: ECMA case intersect predicate with ascii range ?
                case RegexNode.NonECMABoundary:
                case RegexNode.NonBoundary:
                    // update the word letter predicate based on the Unicode definition of it if it was not updated already
                    if (_srBuilder._wordLetterPredicate.Equals(_srBuilder._solver.False))
                        _srBuilder._wordLetterPredicate = _categorizer.WordLetterCondition;
                    return _srBuilder._nwbAnchor;
                case RegexNode.Nothing:
                    return _srBuilder._nothing;
                default:
                    throw new NotSupportedException(SR.Format(SR.NotSupported_NonBacktrackingConflictingExpression, node.Type switch
                    {
                        RegexNode.Ref => SR.ExpressionDescription_Backreference,
                        RegexNode.Testref => SR.ExpressionDescription_Conditional,
                        RegexNode.Require => SR.ExpressionDescription_PositiveLookaround,
                        RegexNode.Prevent => SR.ExpressionDescription_NegativeLookaround,
                        RegexNode.Start => SR.ExpressionDescription_ContiguousMatches,
                        RegexNode.Atomic or
                        RegexNode.Setloopatomic or
                        RegexNode.Oneloopatomic or
                        RegexNode.Notoneloopatomic => SR.ExpressionDescription_AtomicSubexpressions,
                        _ => UnexpectedNodeType(node)
                    }));

                    static string UnexpectedNodeType(RegexNode node)
                    {
                        // The default should never arise, since other node types are either supported
                        // or have been removed (e.g. Group) from the final parse tree.
                        string description = $"unexpected node type ({nameof(RegexNode)}:{node.Type})";
                        Debug.Fail(description);
                        return description;
                    }
            }
        }

        private RegexNode[] FlattenNestedConcatenations(RegexNode concat)
        {
            List<RegexNode> result = new();
            Stack<RegexNode> todo = new();
            todo.Push(concat);
            while (todo.Count > 0)
            {
                RegexNode node = todo.Pop();
                if (node.Type == RegexNode.Concatenate)
                {
                    // flatten nested concatenations
                    for (int i = node.ChildCount() - 1; i >= 0; i--)
                        todo.Push(node.Child(i));
                }
                else if (node.Type == RegexNode.Capture)
                {
                    // unwrap captures
                    todo.Push(node.Child(0));
                }
                else
                {
                    result.Add(node);
                }
            }
            return result.ToArray();
        }

        public static string Escape(char c)
        {
            int code = c;
            return
                code > 126 ? $"\\u{code:X4}" :
                code < 32 ? $"\\x{code:X}" :
                c switch
                {
                    '\0' => @"\0",
                    '\a' => @"\a",
                    '\b' => @"\b",
                    '\t' => @"\t",
                    '\r' => @"\r",
                    '\v' => @"\v",
                    '\f' => @"\f",
                    '\n' => @"\n",
                    '\u001B' => @"\e",
                    '\"' => "\\\"",
                    '\'' => "\\\'",
                    ' ' => " ",
                    _ => c.ToString(),
                };
        }

        #region Character sequences to symbolic regexes
        /// <summary>
        /// Sequence of characters in node._str
        /// </summary>
        private SymbolicRegexNode<T> ConvertNodeMultiToSymbolicRegex(RegexNode node, bool topLevel)
        {
            //sequence of characters
            string sequence = node.Str;
            bool ignoreCase = (node.Options & RegexOptions.IgnoreCase) != 0;

            T[] conds = Array.ConvertAll(sequence.ToCharArray(), c => _solver.CharConstraint(c, ignoreCase, _culture.Name));
            SymbolicRegexNode<T> seq = _srBuilder.MkSequence(conds, topLevel);
            return seq;
        }

        /// <summary>
        /// Matches chacter any character except node._ch
        /// </summary>
        private SymbolicRegexNode<T> ConvertNodeNotoneToSymbolicRegex(RegexNode node)
        {
            bool ignoreCase = (node.Options & RegexOptions.IgnoreCase) != 0;

            T cond = _solver.Not(_solver.CharConstraint(node.Ch, ignoreCase, _culture.Name));

            return _srBuilder.MkSingleton(cond);
        }

        /// <summary>
        /// Matches only node._ch
        /// </summary>
        private SymbolicRegexNode<T> ConvertNodeOneToSymbolicRegex(RegexNode node)
        {
            bool ignoreCase = (node.Options & RegexOptions.IgnoreCase) != 0;

            T cond = _solver.CharConstraint(node.Ch, ignoreCase, _culture.Name);

            return _srBuilder.MkSingleton(cond);
        }

        #endregion

        #region special loops
        private SymbolicRegexNode<T> ConvertNodeSetToSymbolicRegex(RegexNode node)
        {
            //ranges and categories are encoded in set
            string set = node.Str;

            T moveCond = CreateConditionFromSet((node.Options & RegexOptions.IgnoreCase) != 0, set);

            return _srBuilder.MkSingleton(moveCond);
        }

        private SymbolicRegexNode<T> ConvertNodeNotoneloopToSymbolicRegex(RegexNode node, bool isLazy)
        {
            bool ignoreCase = (node.Options & RegexOptions.IgnoreCase) != 0;
            T cond = _solver.Not(_solver.CharConstraint(node.Ch, ignoreCase, _culture.Name));

            SymbolicRegexNode<T> body = _srBuilder.MkSingleton(cond);
            SymbolicRegexNode<T> loop = _srBuilder.MkLoop(body, isLazy, node.M, node.N);
            return loop;
        }

        private SymbolicRegexNode<T> ConvertNodeOneloopToSymbolicRegex(RegexNode node, bool isLazy)
        {
            bool ignoreCase = (node.Options & RegexOptions.IgnoreCase) != 0;
            T cond = _solver.CharConstraint(node.Ch, ignoreCase, _culture.Name);

            SymbolicRegexNode<T> body = _srBuilder.MkSingleton(cond);
            SymbolicRegexNode<T> loop = _srBuilder.MkLoop(body, isLazy, node.M, node.N);
            return loop;
        }

        private SymbolicRegexNode<T> ConvertNodeSetloopToSymbolicRegex(RegexNode node, bool isLazy)
        {
            //ranges and categories are encoded in set
            string set = node.Str;

            T moveCond = CreateConditionFromSet((node.Options & RegexOptions.IgnoreCase) != 0, set);

            SymbolicRegexNode<T> body = _srBuilder.MkSingleton(moveCond);
            SymbolicRegexNode<T> loop = _srBuilder.MkLoop(body, isLazy, node.M, node.N);
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
        public SymbolicRegexNode<T> MkIfThenElse(SymbolicRegexNode<T> cond, SymbolicRegexNode<T> left, SymbolicRegexNode<T> right) => _srBuilder.MkIfThenElse(cond, left, right);

        /// <summary>
        /// Make a singleton sequence regex
        /// </summary>
        public SymbolicRegexNode<T> MkSingleton(T set) => _srBuilder.MkSingleton(set);

        public SymbolicRegexNode<T> MkOr(params SymbolicRegexNode<T>[] regexes) => _srBuilder.MkOr(regexes);

        public SymbolicRegexNode<T> MkConcat(params SymbolicRegexNode<T>[] regexes) => _srBuilder.MkConcat(regexes, false);

        public SymbolicRegexNode<T> MkEpsilon() => _srBuilder._epsilon;

        public SymbolicRegexNode<T> MkLoop(SymbolicRegexNode<T> regex, int lower = 0, int upper = int.MaxValue, bool isLazy = false) => _srBuilder.MkLoop(regex, isLazy, lower, upper);

        #endregion
    }
}
