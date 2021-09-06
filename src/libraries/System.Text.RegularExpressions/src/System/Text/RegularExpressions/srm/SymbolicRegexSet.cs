// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>Represents a set of symbolic regexes that is either a disjunction or a conjunction</summary>
    internal sealed class SymbolicRegexSet<S> : IEnumerable<SymbolicRegexNode<S>> where S : notnull
    {
        internal readonly SymbolicRegexBuilder<S> _builder;

        private readonly HashSet<SymbolicRegexNode<S>> _set;

        // Symbolic regex A{0,k}?B is stored as (A,B,true) -> k  -- lazy
        // Symbolic regex A{0,k}? is stored as (A,(),true) -> k  -- lazy
        // Symbolic regex A{0,k}B is stored as (A,B,false) -> k  -- eager
        // Symbolic regex A{0,k} is stored as (A,(),false) -> k  -- eager
        private readonly Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int> _loops;

        internal readonly SymbolicRegexKind _kind;

        private int _hashCode;

        /// <summary>if >= 0 then the maximal length of a watchdog in the set</summary>
        internal int _watchdog = -1;

        /// <summary>
        /// Denotes the empty conjunction
        /// </summary>
        public bool IsEverything => _kind == SymbolicRegexKind.And && _set.Count == 0 && _loops.Count == 0;

        /// <summary>Denotes the empty disjunction</summary>
        public bool IsNothing => _kind == SymbolicRegexKind.Or && _set.Count == 0 && _loops.Count == 0;

        private SymbolicRegexSet(SymbolicRegexBuilder<S> builder, SymbolicRegexKind kind)
        {
            _builder = builder;
            _kind = kind;
            _set = new HashSet<SymbolicRegexNode<S>>();
            _loops = new Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int>();
        }

        private SymbolicRegexSet(SymbolicRegexBuilder<S> builder, SymbolicRegexKind kind, HashSet<SymbolicRegexNode<S>> set, Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int> loops)
        {
            _builder = builder;
            _kind = kind;
            _set = set;
            _loops = loops;
        }

        internal static SymbolicRegexSet<S> MkFullSet(SymbolicRegexBuilder<S> builder) => new SymbolicRegexSet<S>(builder, SymbolicRegexKind.And);

        internal static SymbolicRegexSet<S> MkEmptySet(SymbolicRegexBuilder<S> builder) => new SymbolicRegexSet<S>(builder, SymbolicRegexKind.Or);

        internal static SymbolicRegexSet<S> CreateMultiset(SymbolicRegexBuilder<S> builder, IEnumerable<SymbolicRegexNode<S>> elems, SymbolicRegexKind kind)
        {
            // Loops contains the actual multi-set part of the collection
            var loops = new Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int>();

            // Other represents a normal set
            var other = new HashSet<SymbolicRegexNode<S>>();
            int watchdog = -1;

            foreach (SymbolicRegexNode<S> elem in elems)
            {
                // Keep track of the maximal watchdog if this is a disjunction
                // this means for example if the regex is abc(3)|bc(2) and
                // the input is xxxabcyyy then two watchdogs will occur (3) and (2)
                // after reading c and the maximal one is taken
                // in a conjuctive setting this is undefined and the watchdog remains -1
                if (kind == SymbolicRegexKind.Or &&
                    elem._kind == SymbolicRegexKind.WatchDog && elem._lower > watchdog)
                {
                    watchdog = elem._lower;
                }

                #region start foreach
                if (elem == builder._dotStar)
                {
                    // .* is the absorbing element for disjunction
                    if (kind == SymbolicRegexKind.Or)
                        return builder._fullSet;
                }
                else if (elem == builder._nothing)
                {
                    // [] is the absorbing element for conjunction
                    if (kind == SymbolicRegexKind.And)
                        return builder._emptySet;
                }
                else
                {
                    switch (elem._kind)
                    {
                        case SymbolicRegexKind.And:
                        case SymbolicRegexKind.Or:
                            Debug.Assert(elem._alts is not null);
                            if (kind == elem._kind)
                            {
                                //flatten the inner set
                                foreach (SymbolicRegexNode<S> alt in elem._alts)
                                {
                                    if (alt._kind == SymbolicRegexKind.Loop && alt._lower == 0)
                                    {
                                        AddLoopElem(builder, loops, other, alt, builder._epsilon, kind);
                                    }
                                    else
                                    {
                                        if (alt._kind == SymbolicRegexKind.Concat && alt._left!._kind == SymbolicRegexKind.Loop && alt._left._lower == 0)
                                        {
                                            Debug.Assert(alt._right is not null);
                                            AddLoopElem(builder, loops, other, alt._left, alt._right, kind);
                                        }
                                        else
                                        {
                                            other.Add(alt);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                other.Add(elem);
                            }
                            break;

                        case SymbolicRegexKind.Loop:
                            if (elem._lower == 0)
                            {
                                AddLoopElem(builder, loops, other, elem, builder._epsilon, kind);
                            }
                            else
                            {
                                other.Add(elem);
                            }
                            break;

                        case SymbolicRegexKind.Concat:
                            Debug.Assert(elem._left is not null && elem._right is not null);
                            if (elem._kind == SymbolicRegexKind.Concat && elem._left._kind == SymbolicRegexKind.Loop && elem._left._lower == 0)
                            {
                                AddLoopElem(builder, loops, other, elem._left, elem._right, kind);
                            }
                            else
                            {
                                other.Add(elem);
                            }
                            break;

                        default:
                            other.Add(elem);
                            break;
                    }
                }
                #endregion
            }

            // The following further optimizations are only valid for a disjunction
            if (kind == SymbolicRegexKind.Or)
            {
                //if any element of other is covered in loops then omit it
                var others1 = new HashSet<SymbolicRegexNode<S>>();
                foreach (SymbolicRegexNode<S> sr in other)
                {
                    // If there is an element A{0,m} then A is not needed because
                    // it is included by the loop due to the upper bound m > 0
                    var key = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>(sr, builder._epsilon, false);
                    if (loops.ContainsKey(key))
                        others1.Add(sr);
                }

                foreach (KeyValuePair<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int> pair in loops)
                {
                    // If there is an element A{0,m}B then B is not needed because
                    // it is included by the concatenation due to the lower bound 0
                    if (other.Contains(pair.Key.Item2))
                        others1.Add(pair.Key.Item2);
                }

                other.ExceptWith(others1);
            }

            if (other.Count == 0 && loops.Count == 0)
            {
                return kind == SymbolicRegexKind.Or ?
                    builder._emptySet :
                    builder._fullSet;
            }
            else
            {
                var set = new SymbolicRegexSet<S>(builder, kind, other, loops)
                {
                    _watchdog = watchdog
                };
                return set;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddLoopElem(SymbolicRegexBuilder<S> builder, Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int> loops,
                                HashSet<SymbolicRegexNode<S>> other, SymbolicRegexNode<S> loop, SymbolicRegexNode<S> rest, SymbolicRegexKind kind)
        {
            if (loop._upper == 0 && rest.IsEpsilon)
            {
                // In a set treat a loop with upper=lower=0 and no rest (no continuation after the loop)
                // as () independent of whether it is lazy or eager
                other.Add(builder._epsilon);
            }
            else
            {
                Debug.Assert(loop._left is not null);
                var key = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>(loop._left, rest, loop.IsLazy);
                if (loops.TryGetValue(key, out int cnt))
                {
                    // If disjunction then map to the maximum of the upper bounds else to the minimum
                    if (kind == SymbolicRegexKind.Or ? cnt < loop._upper : cnt > loop._upper)
                    {
                        loops[key] = loop._upper;
                    }
                }
                else
                {
                    loops[key] = loop._upper;
                }
            }
        }

        private IEnumerable<SymbolicRegexNode<S>> RestrictElems(S pred)
        {
            foreach (SymbolicRegexNode<S> elem in this)
                yield return elem.Restrict(pred);
        }

        public SymbolicRegexSet<S> Restrict(S pred) => CreateMultiset(_builder, RestrictElems(pred), _kind);

        /// <summary>How many elements are there in this set</summary>
        public int Count => _set.Count + _loops.Count;

        /// <summary>True iff the set is a singleton</summary>
        public bool IsSigleton => Count == 1;

        internal bool IsNullableFor(uint context)
        {
            using IEnumerator<SymbolicRegexNode<S>> e = GetEnumerator();
            if (_kind == SymbolicRegexKind.Or)
            {
                #region some element must be nullable
                while (e.MoveNext())
                {
                    if (e.Current.IsNullableFor(context))
                        return true;
                }
                return false;
                #endregion
            }
            else
            {
                #region  all elements must be nullable
                while (e.MoveNext())
                {
                    if (!e.Current.IsNullableFor(context))
                        return false;
                }
                return true;
                #endregion
            }
        }

        public override int GetHashCode()
        {
            if (_hashCode == 0)
            {
                _hashCode = _kind.GetHashCode();
                HashSet<SymbolicRegexNode<S>>.Enumerator e = _set.GetEnumerator();
                while (e.MoveNext())
                {
                    _hashCode ^= e.Current.GetHashCode();
                }
                e.Dispose();
                Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int>.Enumerator e2 = _loops.GetEnumerator();
                while (e2.MoveNext())
                {
                    _hashCode ^= e2.Current.Key.GetHashCode() + e2.Current.Value.GetHashCode();
                }
            }
            return _hashCode;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is not SymbolicRegexSet<S> that)
                return false;

            if (_kind != that._kind)
                return false;

            if (_set.Count != that._set.Count)
                return false;

            if (_loops.Count != that._loops.Count)
                return false;

            if (_set.Count > 0 && !_set.SetEquals(that._set))
                return false;

            foreach (KeyValuePair<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int> c in _loops)
            {
                if (!that._loops.TryGetValue(c.Key, out int cnt) || !cnt.Equals(c.Value))
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString()
        {
            string result = string.Empty;

            var r = new List<string>();
            foreach (SymbolicRegexNode<S> s in this)
            {
                r.Add(s.ToStringForAlts());
            }

            if (r.Count == 0)
            {
                return result;
            }

            if (_kind == SymbolicRegexKind.Or)
            {
                #region display as R[0]|R[1]|...
                for (int i = 0; i < r.Count; i++)
                {
                    if (result != "")
                        result += "|";
                    result += r[i].ToString();
                }
                // parentheses are needed in some cases in concatenations
                result = "(" + result + ")";
                #endregion
            }
            else
            {
                #region display using if-then-else construct: (?(A)(B)|[0-[0]]) to represent intersect(A,B)
                result = r[r.Count - 1].ToString();
                for (int i = r.Count - 2; i >= 0; i--)
                {
                    // unfortunately [] is an invalid character class expression, using [0-[0]] instead
                    result = $"(?({r[i]})({result})|{"[0-[0]]"})";
                }
                #endregion
            }
            return result;
        }

        internal SymbolicRegexSet<S> MkDerivative(S elem, uint context)
             => CreateMultiset(_builder, MkDerivativesOfElems(elem, context), _kind);

        private IEnumerable<SymbolicRegexNode<S>> MkDerivativesOfElems(S elem, uint context)
        {
            foreach (SymbolicRegexNode<S> s in this)
                yield return s.MkDerivative(elem, context);
        }

        private IEnumerable<SymbolicRegexNode<T>> TransformElems<T>(SymbolicRegexBuilder<T> builderT, Func<S, T> predicateTransformer) where T : notnull
        {
            foreach (SymbolicRegexNode<S> sr in this)
                yield return _builder.Transform(sr, builderT, predicateTransformer);
        }

        internal SymbolicRegexSet<T> Transform<T>(SymbolicRegexBuilder<T> builderT, Func<S, T> predicateTransformer) where T : notnull
            => SymbolicRegexSet<T>.CreateMultiset(builderT, TransformElems(builderT, predicateTransformer), _kind);

        internal SymbolicRegexNode<S> GetTheElement()
        {
            using IEnumerator<SymbolicRegexNode<S>> en = GetEnumerator();
            en.MoveNext();
            return en.Current;
        }

        internal SymbolicRegexSet<S> Reverse() => CreateMultiset(_builder, ReverseElems(), _kind);

        private IEnumerable<SymbolicRegexNode<S>> ReverseElems()
        {
            foreach (SymbolicRegexNode<S> elem in this)
                yield return elem.Reverse();
        }

        internal bool StartsWithLoop(int upperBoundLowestValue)
        {
            bool res = false;
            IEnumerator<SymbolicRegexNode<S>> e = GetEnumerator();
            while (e.MoveNext())
            {
                if (e.Current.StartsWithLoop(upperBoundLowestValue))
                {
                    res = true;
                    break;
                }
            }
            e.Dispose();
            return res;
        }

        public IEnumerator<SymbolicRegexNode<S>> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        internal void Serialize(StringBuilder sb)
        {
            var list = new List<SymbolicRegexNode<S>>(this);

            var str = new List<string>(list.Count);
            foreach (SymbolicRegexNode<S> node in list)
            {
                str.Add(node.Serialize());
            }
            str.Sort();

            string separator = "";
            foreach (string s in str)
            {
                sb.Append(separator);
                sb.Append(s);
                separator = ",";
            }
        }

        internal int GetFixedLength()
        {
            if (_loops.Count > 0)
            {
                return -1;
            }

            int length = -1;
            foreach (SymbolicRegexNode<S> node in _set)
            {
                int node_length = node.GetFixedLength();
                if (node_length == -1)
                {
                    return -1;
                }
                else if (length == -1)
                {
                    length = node_length;
                }
                else if (length != node_length)
                {
                    return -1;
                }
            }
            return length;
        }

        /// <summary>
        /// Enumerates all symbolic regexes in the set
        /// </summary>
        internal sealed class Enumerator : IEnumerator<SymbolicRegexNode<S>>
        {
            private readonly SymbolicRegexSet<S> _set;
            private bool _set_next;
            private HashSet<SymbolicRegexNode<S>>.Enumerator _set_en;
            private bool _loops_next;
            private Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int>.Enumerator _loops_en;
            private SymbolicRegexNode<S>? _current;

            internal Enumerator(SymbolicRegexSet<S> symbolicRegexSet)
            {
                _set = symbolicRegexSet;
                _set_en = symbolicRegexSet._set.GetEnumerator();
                _loops_en = symbolicRegexSet._loops.GetEnumerator();
                _set_next = true;
                _loops_next = true;
                _current = null;
            }

            public SymbolicRegexNode<S> Current => _current!;

            object IEnumerator.Current => _current!;

            public void Dispose()
            {
                _set_en.Dispose();
                _loops_en.Dispose();
            }

            public bool MoveNext()
            {
                if (_set_next)
                {
                    _set_next = _set_en.MoveNext();
                    if (_set_next)
                    {
                        _current = _set_en.Current;
                        return true;
                    }

                    _loops_next = _loops_en.MoveNext();
                    if (_loops_next)
                    {
                        SymbolicRegexNode<S> body = _loops_en.Current.Key.Item1;
                        SymbolicRegexNode<S> rest = _loops_en.Current.Key.Item2;
                        bool isLazy = _loops_en.Current.Key.Item3;
                        int upper = _loops_en.Current.Value;

                        //recreate the symbolic regex from (body,rest)->k to body{0,k}rest
                        _current = _set._builder.MkConcat(_set._builder.MkLoop(body, isLazy, 0, upper), rest);
                        return true;
                    }

                    _current = null;
                    return false;
                }

                if (_loops_next)
                {
                    _loops_next = _loops_en.MoveNext();
                    if (_loops_next)
                    {
                        SymbolicRegexNode<S> body = _loops_en.Current.Key.Item1;
                        SymbolicRegexNode<S> rest = _loops_en.Current.Key.Item2;
                        bool isLazy = _loops_en.Current.Key.Item3;
                        int upper = _loops_en.Current.Value;

                        //recreate the symbolic regex from (body,rest)->k to body{0,k}rest
                        _current = _set._builder.MkConcat(_set._builder.MkLoop(body, isLazy, 0, upper), rest);
                        return true;
                    }

                    _current = null;
                    return false;
                }

                return false;
            }

            public void Reset() => throw new NotImplementedException();
        }
    }
}
