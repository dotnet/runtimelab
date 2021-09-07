// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Builder of symbolic regexes over TElement.
    /// TElement is the type of elements of an effective Boolean algebra.
    /// Used to convert .NET regexes to symbolic regexes.
    /// </summary>
    internal sealed class SymbolicRegexBuilder<TElement> where TElement : notnull
    {
        internal readonly ICharAlgebra<TElement> _solver;

        internal readonly SymbolicRegexNode<TElement> _epsilon;
        internal readonly SymbolicRegexNode<TElement> _nothing;
        internal readonly SymbolicRegexNode<TElement> _startAnchor;
        internal readonly SymbolicRegexNode<TElement> _endAnchor;
        internal readonly SymbolicRegexNode<TElement> _endAnchorZ;
        internal readonly SymbolicRegexNode<TElement> _endAnchorZRev;
        internal readonly SymbolicRegexNode<TElement> _bolAnchor;
        internal readonly SymbolicRegexNode<TElement> _eolAnchor;
        internal readonly SymbolicRegexNode<TElement> _dot;
        internal readonly SymbolicRegexNode<TElement> _dotStar;
        internal readonly SymbolicRegexNode<TElement> _wbAnchor;
        internal readonly SymbolicRegexNode<TElement> _nwbAnchor;
        internal readonly SymbolicRegexSet<TElement> _fullSet;
        internal readonly SymbolicRegexSet<TElement> _emptySet;
        internal readonly SymbolicRegexNode<TElement> _eagerEmptyLoop;

        internal TElement _wordLetterPredicate;
        internal TElement _newLinePredicate;

        /// <summary>
        /// Partition of the input space of predicates.
        /// </summary>
        internal TElement[]? _atoms;

        private readonly Dictionary<TElement, SymbolicRegexNode<TElement>> _singletonCache = new Dictionary<TElement, SymbolicRegexNode<TElement>>();
        // states that have been created
        internal HashSet<State<TElement>> _stateCache = new HashSet<State<TElement>>();
        /// <summary>
        /// Maps state ids to states, initial capacity is 1024 states.
        /// Each time more states are needed the length is increased by 1024.
        /// </summary>
        internal State<TElement>[]? _statearray;
        internal State<TElement>[]? _delta;
        private const int InitialStateLimit = 1024;

        /// <summary>
        /// K is the smallest k s.t. 2^k >= atoms.Length + 1
        /// </summary>
        internal int _K;

        /// <summary>
        /// If true then delta is used in a mode where
        /// each target state represents a set of states.
        /// </summary>
        internal bool _antimirov;

        /// <summary>Create a new symbolic regex builder.</summary>
        internal SymbolicRegexBuilder(ICharAlgebra<TElement> solver)
        {
            _epsilon = SymbolicRegexNode<TElement>.MkEpsilon(this);
            _startAnchor = SymbolicRegexNode<TElement>.MkStartAnchor(this);
            _endAnchor = SymbolicRegexNode<TElement>.MkEndAnchor(this);
            _endAnchorZ = SymbolicRegexNode<TElement>.MkEndAnchorZ(this);
            _endAnchorZRev = SymbolicRegexNode<TElement>.MkEndAnchorZRev(this);
            _eolAnchor = SymbolicRegexNode<TElement>.MkEolAnchor(this);
            _bolAnchor = SymbolicRegexNode<TElement>.MkBolAnchor(this);
            _wbAnchor = SymbolicRegexNode<TElement>.MkWBAnchor(this);
            _nwbAnchor = SymbolicRegexNode<TElement>.MkNWBAnchor(this);
            _emptySet = SymbolicRegexSet<TElement>.CreateEmpty(this);
            _fullSet = SymbolicRegexSet<TElement>.CreateFull(this);
            _eagerEmptyLoop = SymbolicRegexNode<TElement>.MkEagerEmptyLoop(this, _epsilon);

            // atoms = null if partition of the solver is undefined and returned as null
            _atoms = solver.GetPartition();
            if (_atoms == null)
            {
                _K = -1;
            }
            else
            {
                _statearray = new State<TElement>[InitialStateLimit];

                // the extra slot with id atoms.Length is reserved for \Z (last occurrence of \n)
                int k = 1;
                while (_atoms.Length >= (1 << k))
                {
                    k += 1;
                }
                _K = k;
                _delta = new State<TElement>[InitialStateLimit << _K];
            }

            _solver = solver;

            // initialized to False but updated later to the actual condition ony if \b or \B occurs anywhere in the regex
            // this implies that if a regex never uses \b or \B then the character context will never
            // update the previous character context to distinguish word and nonword letters
            _wordLetterPredicate = solver.False;

            // initialized to False but updated later to the actual condition of \n ony if a line anchor occurs anywhere in the regex
            // this implies that if a regex never uses a line anchor then the character context will never
            // update the previous character context to mark that the previous caharcter was \n
            _newLinePredicate = solver.False;
            _nothing = SymbolicRegexNode<TElement>.MkFalse(this);
            _dot = SymbolicRegexNode<TElement>.MkTrue(this);
            _dotStar = SymbolicRegexNode<TElement>.MkDotStar(this, _dot);

            // --- initialize singletonCache ---
            _singletonCache[_solver.False] = _nothing;
            _singletonCache[_solver.True] = _dot;
        }

        /// <summary>
        /// Make a disjunction of given regexes, simplify by eliminating any regex that accepts no inputs
        /// </summary>
        internal SymbolicRegexNode<TElement> MkOr(params SymbolicRegexNode<TElement>[] regexes)
        {
            Debug.Assert(regexes.Length != 0);

            SymbolicRegexNode<TElement> sr = regexes[regexes.Length - 1];

            for (int i = regexes.Length - 2; i >= 0; i--)
            {
                sr = MkOr2(regexes[i], sr);
                if (sr == _dotStar)
                    return _dotStar;
            }

            return sr;
        }

        /// <summary>
        /// Make a conjunction of given regexes, simplify by eliminating regexes that accept everything
        /// </summary>
        internal SymbolicRegexNode<TElement> MkAnd(params SymbolicRegexNode<TElement>[] regexes)
        {
            Debug.Assert(regexes.Length != 0);

            SymbolicRegexNode<TElement> sr = regexes[regexes.Length - 1];

            for (int i = regexes.Length - 2; i >= 0; i--)
            {
                sr = MkAnd2(regexes[i], sr);
                if (sr == _nothing)
                    return _nothing;
            }

            return sr;
        }

        /// <summary>
        /// Make a disjunction of given regexes, simplify by eliminating any regex that accepts no inputs
        /// </summary>
        internal SymbolicRegexNode<TElement> MkOr(SymbolicRegexSet<TElement> regexset) =>
            regexset.IsNothing ? _nothing :
            regexset.IsEverything ? _dotStar :
            regexset.IsSingleton ? regexset.GetSingletonElement() :
            SymbolicRegexNode<TElement>.MkOr(this, regexset);

        internal SymbolicRegexNode<TElement> MkOr2(SymbolicRegexNode<TElement> x, SymbolicRegexNode<TElement> y) =>
            x == _dotStar || y == _dotStar ? _dotStar :
            x == _nothing ? y :
            y == _nothing ? x :
            SymbolicRegexNode<TElement>.MkOr(this, x, y);

        private SymbolicRegexNode<TElement> MkAnd2(SymbolicRegexNode<TElement> x, SymbolicRegexNode<TElement> y) =>
            x == _nothing || y == _nothing ? _nothing :
            x == _dotStar ? y :
            y == _dotStar ? x :
            SymbolicRegexNode<TElement>.MkAnd(this, x, y);

        /// <summary>
        /// Make a conjunction of given regexes, simplify by eliminating any regex that accepts all inputs,
        /// returns the empty regex if the regex accepts nothing
        /// </summary>
        internal SymbolicRegexNode<TElement> MkAnd(SymbolicRegexSet<TElement> regexset) =>
            regexset.IsNothing ? _nothing :
            regexset.IsEverything ? _dotStar :
            regexset.IsSingleton ? regexset.GetSingletonElement() :
            SymbolicRegexNode<TElement>.MkAnd(this, regexset);

        /// <summary>
        /// Make a concatenation of given regexes, if any regex is nothing then return nothing, eliminate
        /// intermediate epsilons, if toplevel and length is fixed, add watchdog at the end
        /// </summary>
        internal SymbolicRegexNode<TElement> MkConcat(SymbolicRegexNode<TElement>[] regexes, bool topLevel)
        {
            if (regexes.Length == 0)
                return _epsilon;

            SymbolicRegexNode<TElement> sr = _epsilon;
            int length = CalculateFixedLength(regexes);
            if (topLevel && length >= 0)
                sr = MkWatchDog(length);

            //exclude epsilons from the concatenation
            for (int i = regexes.Length - 1; i >= 0; i--)
            {
                if (regexes[i] == _nothing)
                    return _nothing;

                sr = SymbolicRegexNode<TElement>.MkConcat(this, regexes[i], sr);
            }

            return sr;
        }

        internal SymbolicRegexNode<TElement> MkConcat(SymbolicRegexNode<TElement> left, SymbolicRegexNode<TElement> right) => SymbolicRegexNode<TElement>.MkConcat(this, left, right);

        private int CalculateFixedLength(SymbolicRegexNode<TElement>[] regexes)
        {
            int length = 0;
            for (int i = 0; i < regexes.Length; i++)
            {
                int k = regexes[i].GetFixedLength();
                if (k < 0)
                {
                    return -1;
                }

                length += k;
            }

            return length;
        }


        /// <summary>
        /// Make loop regex
        /// </summary>
        internal SymbolicRegexNode<TElement> MkLoop(SymbolicRegexNode<TElement> regex, bool isLazy, int lower = 0, int upper = int.MaxValue, bool toplevel = false)
        {
            if (lower == 1 && upper == 1)
            {
                return regex;
            }

            if (lower == 0 && upper == 0)
            {
                return isLazy ? _epsilon : _eagerEmptyLoop;
            }

            if (!isLazy && lower == 0 && upper == int.MaxValue && regex._kind == SymbolicRegexKind.Singleton)
            {
                Debug.Assert(regex._set is not null);
                if (_solver.AreEquivalent(_solver.True, regex._set))
                {
                    return _dotStar;
                }
            }

            return SymbolicRegexNode<TElement>.MkLoop(this, regex, lower, upper, isLazy);
        }

        /// <summary>
        /// Make a singleton sequence regex
        /// </summary>
        internal SymbolicRegexNode<TElement> MkSingleton(TElement set)
        {
            if (!_singletonCache.TryGetValue(set, out SymbolicRegexNode<TElement>? res))
            {
                _singletonCache[set] = res = SymbolicRegexNode<TElement>.MkSingleton(this, set);
            }

            return res;
        }

        /// <summary>
        /// Make end of sequence marker
        /// </summary>
        internal SymbolicRegexNode<TElement> MkWatchDog(int length) => SymbolicRegexNode<TElement>.MkWatchDog(this, length);

        /// <summary>
        /// Make a sequence regex, i.e., a concatenation of singletons, with a watchdog at the end
        /// </summary>
        internal SymbolicRegexNode<TElement> MkSequence(TElement[] seq, bool topLevel)
        {
            int k = seq.Length;
            if (k == 0)
            {
                return _epsilon;
            }
            else if (k == 1)
            {
                return topLevel ?
                    SymbolicRegexNode<TElement>.MkConcat(this, MkSingleton(seq[0]), MkWatchDog(1)) :
                    MkSingleton(seq[0]);
            }
            else
            {
                SymbolicRegexNode<TElement>[] singletons = Array.ConvertAll(seq, MkSingleton);
                return MkConcat(singletons, topLevel);
            }
        }

        /// <summary>
        /// Make an if-then-else regex (?(cond)left|right),
        /// or create it as conjuction if right is false
        /// </summary>
        /// <param name="cond">condition</param>
        /// <param name="left">true case</param>
        /// <param name="right">false case</param>
        /// <returns></returns>
        internal SymbolicRegexNode<TElement> MkIfThenElse(SymbolicRegexNode<TElement> cond, SymbolicRegexNode<TElement> left, SymbolicRegexNode<TElement> right) => SymbolicRegexNode<TElement>.MkIfThenElse(this, cond, left, right);

        internal SymbolicRegexNode<TElement> NormalizeGeneralLoops(SymbolicRegexNode<TElement> sr)
        {
            switch (sr._kind)
            {
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.Singleton:
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.EndAnchorZ:
                case SymbolicRegexKind.EndAnchorZRev:
                case SymbolicRegexKind.WBAnchor:
                case SymbolicRegexKind.NWBAnchor:
                    return sr;

                case SymbolicRegexKind.Loop:
                    {
                        Debug.Assert(sr._left is not null);
                        if (!sr.IsStar)
                        {
                            if (sr.IsMaybe)
                            {
                                return MkOr2(sr._left, _epsilon);
                            }

                            if (sr.IsPlus)
                            {
                                SymbolicRegexNode<TElement> star = MkLoop(sr._left, sr.IsLazy);
                                SymbolicRegexNode<TElement> plus = MkConcat(sr._left, star);
                                return plus;
                            }

                            if (sr._upper == int.MaxValue)
                            {
                                SymbolicRegexNode<TElement> fixed_loop = MkLoop(sr._left, false, sr._lower, sr._lower);
                                SymbolicRegexNode<TElement> star = MkLoop(sr._left, sr.IsLazy);
                                SymbolicRegexNode<TElement> concat = MkConcat(fixed_loop, star);
                                return concat;
                            }
                        }

                        return sr;
                    }

                case SymbolicRegexKind.Concat:
                    {
                        Debug.Assert(sr._left is not null && sr._right is not null);
                        SymbolicRegexNode<TElement> left = NormalizeGeneralLoops(sr._left);
                        SymbolicRegexNode<TElement> right = NormalizeGeneralLoops(sr._right);
                        SymbolicRegexNode<TElement> concat = MkConcat(left, right);
                        return concat;
                    }

                case SymbolicRegexKind.Or:
                    {
                        Debug.Assert(sr._alts is not null);
                        var alts = new SymbolicRegexNode<TElement>[sr._alts.Count];
                        int i = 0;
                        foreach (SymbolicRegexNode<TElement> e in sr._alts)
                        {
                            alts[i++] = NormalizeGeneralLoops(e);
                        }
                        Debug.Assert(i == alts.Length);
                        SymbolicRegexNode<TElement> or = MkOr(alts);
                        return or;
                    }

                default:
                    throw new NotSupportedException($"{nameof(NormalizeGeneralLoops)}:{sr._kind}");
            }
        }

        private readonly Dictionary<SymbolicRegexNode<TElement>, int> counterIdMap = new Dictionary<SymbolicRegexNode<TElement>, int>();

        internal int GetCounterId(SymbolicRegexNode<TElement> node)
        {
            if (node._kind == SymbolicRegexKind.Loop && node._upper < int.MaxValue)
            {
                if (!counterIdMap.TryGetValue(node, out int c))
                {
                    counterIdMap[node] = c = counterIdMap.Count;
                }

                return c;
            }

            return -1;
        }

        private SymbolicRegexNode<TElement> ToLeftAssocForm(SymbolicRegexNode<TElement> node)
        {
            if (node._kind != SymbolicRegexKind.Concat)
            {
                return node;
            }

            SymbolicRegexNode<TElement>? right = node._right;
            Debug.Assert(right is not null);

            if (right._kind != SymbolicRegexKind.Concat)
            {
                return node;
            }

            SymbolicRegexNode<TElement>? left = node._left;
            Debug.Assert(left is not null);

            while (right._kind == SymbolicRegexKind.Concat)
            {
                Debug.Assert(right._left is not null);
                left = left.ConcatWithoutNormalizing(right._left);
                right = right._right;
                Debug.Assert(right is not null);
            }

            return left.ConcatWithoutNormalizing(right);
        }

        private bool IsCountingLoop(SymbolicRegexNode<TElement> node) => !node.IsMaybe && !node.IsStar && !node.IsPlus;

        internal SymbolicRegexNode<T> Transform<T>(SymbolicRegexNode<TElement> sr, SymbolicRegexBuilder<T> builderT, Func<TElement, T> predicateTransformer) where T : notnull
        {
            switch (sr._kind)
            {
                case SymbolicRegexKind.StartAnchor:
                    return builderT._startAnchor;

                case SymbolicRegexKind.EndAnchor:
                    return builderT._endAnchor;

                case SymbolicRegexKind.EndAnchorZ:
                    return builderT._endAnchorZ;

                case SymbolicRegexKind.EndAnchorZRev:
                    return builderT._endAnchorZRev;

                case SymbolicRegexKind.BOLAnchor:
                    return builderT._bolAnchor;

                case SymbolicRegexKind.EOLAnchor:
                    return builderT._eolAnchor;

                case SymbolicRegexKind.WBAnchor:
                    return builderT._wbAnchor;

                case SymbolicRegexKind.NWBAnchor:
                    return builderT._nwbAnchor;

                case SymbolicRegexKind.WatchDog:
                    return builderT.MkWatchDog(sr._lower);

                case SymbolicRegexKind.Epsilon:
                    return builderT._epsilon;

                case SymbolicRegexKind.Singleton:
                    Debug.Assert(sr._set is not null);
                    return builderT.MkSingleton(predicateTransformer(sr._set));

                case SymbolicRegexKind.Loop:
                    Debug.Assert(sr._left is not null);
                    return builderT.MkLoop(Transform(sr._left, builderT, predicateTransformer), sr.IsLazy, sr._lower, sr._upper);

                case SymbolicRegexKind.Or:
                    Debug.Assert(sr._alts is not null);
                    return builderT.MkOr(sr._alts.Transform(builderT, predicateTransformer));

                case SymbolicRegexKind.And:
                    Debug.Assert(sr._alts is not null);
                    return builderT.MkAnd(sr._alts.Transform(builderT, predicateTransformer));

                case SymbolicRegexKind.Concat:
                    {
                        List<SymbolicRegexNode<TElement>> sr_elems = sr.ToList();
                        SymbolicRegexNode<T>[] sr_elems_trasformed = new SymbolicRegexNode<T>[sr_elems.Count];
                        for (int i = 0; i < sr_elems.Count; i++)
                        {
                            sr_elems_trasformed[i] = Transform(sr_elems[i], builderT, predicateTransformer);
                        }
                        return builderT.MkConcat(sr_elems_trasformed, false);
                    }

                case SymbolicRegexKind.IfThenElse:
                    Debug.Assert(sr._iteCond is not null && sr._left is not null && sr._right is not null);
                    return builderT.MkIfThenElse(
                        Transform(sr._iteCond, builderT, predicateTransformer),
                        Transform(sr._left, builderT, predicateTransformer),
                        Transform(sr._right, builderT, predicateTransformer));

                default:
                    throw new NotImplementedException($"{nameof(Transform)}:{sr._kind}");
            }
        }

        internal SymbolicRegexNode<TElement> Parse(string s, int i, out int i_next)
        {
            switch (s[i])
            {
                case '.':
                    {
                        #region .
                        i_next = i + 1;
                        return _dot;
                        #endregion
                    }
                case '[':
                    {
                        #region parse singleton
                        int j = s.IndexOf(']', i);
                        TElement p = _solver.DeserializePredicate(s.Substring(i + 1, j - (i + 1)));
                        SymbolicRegexNode<TElement> node = MkSingleton(p);
                        //SymbolicRegexNode<S> node;
                        //var seq_str = s.Substring(i + 1, j - (i + 1));
                        //var preds_str = seq_str.Split(';');
                        //var preds = Array.ConvertAll(preds_str, solver.DeserializePredicate);
                        //node = this.MkSequence(preds);
                        i_next = j + 1;
                        return node;
                        #endregion
                    }
                case 'E':
                    {
                        #region Epsilon
                        i_next = i + 1;
                        return _epsilon;
                        #endregion
                    }
                case 'L': //L(l,u,body) for body{l,u} u may be *
                    {
                        #region Loop
                        int j = s.IndexOf(',', i + 2);
                        int lower = int.Parse(s.Substring(i + 2, j - (i + 2)));
                        int upper = int.MaxValue;
                        if (s[j + 1] == '*')
                        {
                            j += 3;
                        }
                        else
                        {
                            int k = s.IndexOf(',', j + 1);
                            upper = int.Parse(s.Substring(j + 1, k - (j + 1)));
                            j = k + 1;
                        }
                        SymbolicRegexNode<TElement> body = Parse(s, j, out int n);
                        var node = SymbolicRegexNode<TElement>.MkLoop(this, body, lower, upper, false);
                        i_next = n + 1;
                        return node;
                        #endregion
                    }
                case 'l': //l(m,u,body) for body{m,u}? u may be *
                    {
                        #region Loop
                        int j = s.IndexOf(',', i + 2);
                        int lower = int.Parse(s.Substring(i + 2, j - (i + 2)));
                        int upper = int.MaxValue;
                        if (s[j + 1] == '*')
                        {
                            j += 3;
                        }
                        else
                        {
                            int k = s.IndexOf(',', j + 1);
                            upper = int.Parse(s.Substring(j + 1, k - (j + 1)));
                            j = k + 1;
                        }
                        SymbolicRegexNode<TElement> body = Parse(s, j, out int n);
                        var node = SymbolicRegexNode<TElement>.MkLoop(this, body, lower, upper, true);
                        i_next = n + 1;
                        return node;
                        #endregion
                    }
                case 'S':
                    {
                        #region concatenation
                        SymbolicRegexNode<TElement>[] nodes = ParseSequence(s, i + 2, out int n);
                        SymbolicRegexNode<TElement> concat = MkConcat(nodes, false);
                        i_next = n;
                        return concat;
                        #endregion
                    }
                case 'C': //conjunction C(R1,R2,...,Rk)
                    {
                        #region conjunction
                        SymbolicRegexNode<TElement>[] nodes = ParseSequence(s, i + 2, out int n);
                        var conj = SymbolicRegexNode<TElement>.MkAnd(this, nodes);
                        i_next = n;
                        return conj;
                        #endregion
                    }
                case 'D': //Disjunction D(R1,R2,...,Rk)
                    {
                        #region disjunction
                        SymbolicRegexNode<TElement>[] nodes = ParseSequence(s, i + 2, out int n);
                        var disj = SymbolicRegexNode<TElement>.MkOr(this, nodes);
                        i_next = n;
                        return disj;
                        #endregion
                    }
                case 'I': //if then else I(x,y,z)
                    {
                        #region ITE
                        SymbolicRegexNode<TElement> cond = Parse(s, i + 2, out int n);
                        SymbolicRegexNode<TElement> first = Parse(s, n + 1, out int m);
                        SymbolicRegexNode<TElement> second = Parse(s, m + 1, out int k);
                        var ite = SymbolicRegexNode<TElement>.MkIfThenElse(this, cond, first, second);
                        i_next = k + 1;
                        return ite;
                        #endregion
                    }
                case 'A':
                    {
                        #region start anchor
                        i_next = i + 1;
                        return _startAnchor;
                        #endregion
                    }
                case 'a':
                    {
                        #region start anchor a (the reverse of EndAnchorZ)
                        i_next = i + 1;
                        return _endAnchorZRev;
                        #endregion
                    }
                case 'z':
                    {
                        #region end anchor
                        i_next = i + 1;
                        return _endAnchor;
                        #endregion
                    }
                case 'Z':
                    {
                        #region end anchor Z
                        i_next = i + 1;
                        return _endAnchorZ;
                        #endregion
                    }
                case '^':
                    {
                        #region beginning of line anchor
                        i_next = i + 1;
                        return _bolAnchor;
                        #endregion
                    }
                case '$':
                    {
                        #region end of line anchor
                        i_next = i + 1;
                        return _eolAnchor;
                        #endregion
                    }
                case 'W':
                    {
                        #region end of sequence/watchdog anchor
                        int j = s.IndexOf(')', i + 2);
                        int length = int.Parse(s.Substring(i + 2, j - (i + 2)));
                        i_next = j + 1;
                        return SymbolicRegexNode<TElement>.MkWatchDog(this, length);
                        #endregion
                    }
                case 'b':
                    {
                        #region word border anchor
                        i_next = i + 1;
                        return _wbAnchor;
                        #endregion
                    }
                case 'B':
                    {
                        #region non word border anchor
                        i_next = i + 1;
                        return _nwbAnchor;
                        #endregion
                    }
                default:
                    throw new ArgumentException($"{nameof(Parse)}:{s[i]}");
            }
        }

        /// <summary>
        /// Deserialize a symbolic regex from its serialized representation
        /// that was produced by SymbolicRegexNode.Serialize
        /// </summary>
        internal SymbolicRegexNode<TElement> Deserialize(string s) => Parse(s, 0, out _);

        private SymbolicRegexNode<TElement>[] ParseSequence(string s, int i, out int n)
        {
            if (s[i] == ')')
            {
                n = i + 1;
                return Array.Empty<SymbolicRegexNode<TElement>>();
            }
            else
            {
                var nodes = new List<SymbolicRegexNode<TElement>>
                {
                    Parse(s, i, out int j)
                };
                while (s[j] == ',')
                {
                    i = j + 1;
                    nodes.Add(Parse(s, i, out j));
                }
                n = j + 1;
                return nodes.ToArray();
            }
        }

        /// <summary>
        /// Make a state with given node and previous character context
        /// </summary>
        public State<TElement> MkState(SymbolicRegexNode<TElement> node, uint prevCharKind, bool antimirov = false)
        {
            //first prune the anchors in the node
            TElement WLpred = _wordLetterPredicate;
            TElement startSet = node.GetStartSet();

            //true if the startset of the node overlaps with some wordletter or the node can be nullable
            bool contWithWL = node.CanBeNullable || _solver.IsSatisfiable(_solver.And(WLpred, startSet));

            //true if the startset of the node overlaps with some nonwordletter or the node can be nullable
            bool contWithNWL = node.CanBeNullable || _solver.IsSatisfiable(_solver.And(_solver.Not(WLpred), startSet));
            SymbolicRegexNode<TElement> pruned_node = node.PruneAnchors(prevCharKind, contWithWL, contWithNWL);
            var s = new State<TElement>(pruned_node, prevCharKind);
            if (!_stateCache.TryGetValue(s, out State<TElement>? state))
            {
                // do not cache set of states as states in antimirov mode
                if (antimirov && pruned_node.Kind == SymbolicRegexKind.Or)
                {
                    s.Id = -1; // mark the Id as invalid
                    state = s;
                }
                else
                {
                    state = MakeNewState(s);
                }
            }

            return state;
        }

        private State<TElement> MakeNewState(State<TElement> state)
        {
            lock (this)
            {
                state.Id = _stateCache.Count;
                _stateCache.Add(state);

                Debug.Assert(_statearray is not null);

                if (state.Id == _statearray.Length)
                {
                    int newsize = _statearray.Length + 1024;
                    Array.Resize(ref _statearray, newsize);
                    Array.Resize(ref _delta, newsize << _K);
                }
                _statearray[state.Id] = state;
                return state;
            }
        }
    }
}
