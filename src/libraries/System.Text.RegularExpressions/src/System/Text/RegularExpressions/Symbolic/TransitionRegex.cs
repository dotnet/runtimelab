// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Symbolic
{
    /// <summary>Represents a symbolic derivative created from a symbolic regex without using minterms</summary>
    internal class TransitionRegex<S> : IEnumerable<(S, SymbolicRegexNode<S>)> where S : notnull
    {
        public readonly SymbolicRegexBuilder<S> _builder;
        public readonly TransitionRegexKind _kind;
        public readonly S? _test;
        public readonly TransitionRegex<S>? _first;
        public readonly TransitionRegex<S>? _second;
        public readonly SymbolicRegexNode<S>? _leaf;

        private readonly int _hashCode;

        public bool IsNothing
        {
            get
            {
                if (_kind == TransitionRegexKind.Leaf)
                {
                    Debug.Assert(_leaf != null);
                    return _leaf.IsNothing;
                }
                return false;
            }
        }

        public bool IsAnyStar
        {
            get
            {
                if (_kind == TransitionRegexKind.Leaf)
                {
                    Debug.Assert(_leaf != null);
                    return _leaf.IsAnyStar;
                }
                return false;
            }
        }

        private TransitionRegex(SymbolicRegexBuilder<S> builder, TransitionRegexKind kind, S? test, TransitionRegex<S>? first, TransitionRegex<S>? second, SymbolicRegexNode<S>? leaf)
        {
            Debug.Assert(builder is not null);
            Debug.Assert(
                kind is TransitionRegexKind.Leaf && leaf is not null && Equals(test, default(S)) && first is null && second is null ||
                kind is TransitionRegexKind.Conditional && test is not null && first is not null && second is not null && leaf is null ||
                kind is TransitionRegexKind.Union && Equals(test, default(S)) && first is not null && second is not null && leaf is null);
            _builder = builder;
            _kind = kind;
            _test = test;
            _first = first;
            _second = second;
            _leaf = leaf;
            _hashCode = HashCode.Combine(kind, test, first, second, leaf);
        }

        private static TransitionRegex<S> Create(SymbolicRegexBuilder<S> builder, TransitionRegexKind kind, S? test, TransitionRegex<S>? one, TransitionRegex<S>? two, SymbolicRegexNode<S>? leaf)
        {
            // Keep transition regexes internalized
            (TransitionRegexKind, S?, TransitionRegex<S>?, TransitionRegex<S>?, SymbolicRegexNode<S>?) key = (kind, test, one, two, leaf);
            TransitionRegex<S>? tr;
            if (!builder._trCache.TryGetValue(key, out tr))
            {
                tr = new TransitionRegex<S>(builder, kind, test, one, two, leaf);
                builder._trCache[key] = tr;
            }
            return tr;
        }

        public TransitionRegex<S> Complement()
        {
            switch (_kind)
            {
                case TransitionRegexKind.Conditional:
                    Debug.Assert(_test is not null && _first is not null && _second is not null);
                    return new TransitionRegex<S>(_builder, TransitionRegexKind.Conditional, _test, _first.Complement(), _second.Complement(), null);
                case TransitionRegexKind.Leaf:
                    Debug.Assert(_leaf is not null);
                    return new TransitionRegex<S>(_builder, TransitionRegexKind.Leaf, default(S), null, null, _leaf._builder.MkNot(_leaf));
                default:
                    Debug.Assert(_kind == TransitionRegexKind.Union && _first is not null && _second is not null);
                    return Intersect(_first.Complement(), _second.Complement());
            }
        }

        /// <summary>Concatenate a node at the end of this transition regex</summary>
        public TransitionRegex<S> Concat(SymbolicRegexNode<S> node)
        {
            switch (_kind)
            {
                case TransitionRegexKind.Leaf:
                    Debug.Assert(_leaf is not null);
                    return new TransitionRegex<S>(_builder, TransitionRegexKind.Leaf, default(S), null, null, _leaf._builder.MkConcat(_leaf, node));
                default:
                    Debug.Assert(_first is not null && _second is not null);
                    return new TransitionRegex<S>(_builder, _kind, _test, _first.Concat(node), _second.Concat(node), null);
            }
        }

        private static TransitionRegex<S> Intersect(TransitionRegex<S> one, TransitionRegex<S> two)
        {
            // Apply standard simplifications
            // [] & t = [], t & .* = t
            if (one.IsNothing || two.IsAnyStar || one == two)
            {
                return one;
            }

            // t & [] = [], .* & t = t
            if (two.IsNothing || one.IsAnyStar)
            {
                return two;
            }

            return one.IntersectWith(two, one._builder._solver.True);
        }

        private TransitionRegex<S> IntersectWith(TransitionRegex<S> that, S context)
        {
            Debug.Assert(!_builder._solver.IsSatisfiable(context));
            // Intersect when this is a Conditional
            if (_kind == TransitionRegexKind.Conditional)
            {
                Debug.Assert(_test is not null && _first is not null && _second is not null);
                S thenPath = _builder._solver.And(context, _test);
                S elsePath = _builder._solver.And(context, _builder._solver.Not(_test));
                if (!_builder._solver.IsSatisfiable(thenPath))
                {
                    // then case being infeasible implies that elsePath must be satisfiable
                    return _second.IntersectWith(that, elsePath);
                }
                if (!_builder._solver.IsSatisfiable(elsePath))
                {
                    // else case is infeasible
                    return _first.IntersectWith(that, thenPath);
                }
                TransitionRegex<S> thencase = _first.IntersectWith(that, thenPath);
                TransitionRegex<S> elsecase = _second.IntersectWith(that, elsePath);
                if (thencase == elsecase)
                {
                    // Both branches result in the same thing, so the test can be omitted
                    return thencase;
                }
                return new TransitionRegex<S>(_builder, TransitionRegexKind.Conditional, _test, thencase, elsecase, null);
            }

            // Swap the order of this and that if that is a Conditional
            if (that._kind == TransitionRegexKind.Conditional)
            {
                return that.IntersectWith(this, context);
            }

            // Intersect when this is a Union
            // Use the following law of distributivity: (A|B)&C = A&C|B&C
            if (_kind == TransitionRegexKind.Union)
            {
                Debug.Assert(_first is not null && _second is not null);
                return new TransitionRegex<S>(_builder, TransitionRegexKind.Union, default(S), _first.IntersectWith(that, context), _second.IntersectWith(that, context), null);
            }

            // Swap the order of this and that if that is a Union
            if (that._kind == TransitionRegexKind.Union)
            {
                return that.IntersectWith(this, context);
            }

            // Propagate intersection to the leaves
            Debug.Assert(_kind is TransitionRegexKind.Leaf && that._kind is TransitionRegexKind.Leaf && _leaf is not null && that._leaf is not null);
            return new TransitionRegex<S>(_builder, TransitionRegexKind.Leaf, default(S), null, null, _builder.MkAnd(_leaf, that._leaf));
        }

        private static TransitionRegex<S> Union(TransitionRegex<S> one, TransitionRegex<S> two)
        {
            // Apply common simplifications, always trying to push the operations into the leaves or to eliminate redundant branches
            if (one.IsNothing || two.IsAnyStar || one.Equals(two))
            {
                return two;
            }

            if (two.IsNothing || one.IsAnyStar)
            {
                return one;
            }

            if (one._kind == TransitionRegexKind.Conditional && two._kind == TransitionRegexKind.Conditional)
            {
                Debug.Assert(one._test is not null && one._first is not null && one._second is not null);
                Debug.Assert(two._test is not null && two._first is not null && two._second is not null);

                // if(psi, t1, t2) | if(psi, s1, s2) = if(psi, t1|s1, t2|s2)
                if (one._test.Equals(two._test))
                {
                    return IfThenElse(one._test, Union(one._first, two._first), Union(one._second, two._second));
                }

                // if(psi, t, []) | if(phi, t, []) = if(psi or phi, t, [])
                if (one._second.IsNothing && two._second.IsNothing && one._first.Equals(two._first))
                {
                    return IfThenElse(one._builder._solver.Or(one._test, two._test), one._first, one._second);
                }
            }
            // TODO: keep the representation of Union in right-associative form ordered by hashcode "as a list"
            // so that in a Union, _first is never a union and _first._hashcode is less than _second._hashcode (if _second is not a Union)
            // and if _second is a union then _first._hashcode is less than _second._first._hashcode, etc.
            // This will help to maintain a canonical representation of two equivalent unions and avoid equivalent unions being nonequal
            return Create(one._builder, TransitionRegexKind.Union, default(S), one, two, null);
        }

        private static TransitionRegex<S> IfThenElse(S test, TransitionRegex<S> thencase, TransitionRegex<S> elsecase) =>
            (thencase == elsecase || thencase._builder._solver.True.Equals(test)) ? thencase :
            thencase._builder._solver.False.Equals(test) ? elsecase :
            Create(thencase._builder, TransitionRegexKind.Conditional, test, thencase, elsecase, null);

        /// <summary>Intersection of transition regexes</summary>
        public static TransitionRegex<S> operator &(TransitionRegex<S> one, TransitionRegex<S> two) => Intersect(one, two);

        /// <summary>Union of transition regexes</summary>
        public static TransitionRegex<S> operator |(TransitionRegex<S> one, TransitionRegex<S> two) => Union(one, two);

        /// <summary>Complement of transition regex</summary>
        public static TransitionRegex<S> operator ~(TransitionRegex<S> tr) => tr.Complement();

        public override int GetHashCode() => _hashCode;

        /// <summary>Equality is object identity due to internalization</summary>
        public override bool Equals(object? obj) => this == obj;

        public override string ToString()
        {
            switch (_kind)
            {
                case TransitionRegexKind.Conditional:
                    return $"if({_test},{_first},{_second})";
                case TransitionRegexKind.Leaf:
                    return $"{_leaf}";
                default:
                    return $"{_first}|{_second}";
            }
        }

        public IEnumerator<(S, SymbolicRegexNode<S>)> GetEnumerator() => EnumeratePaths(_builder._solver.True).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => EnumeratePaths(_builder._solver.True).GetEnumerator();

        /// <summary>Enumerates the paths as outgoing transitions in an NFA</summary>
        private IEnumerable<(S, SymbolicRegexNode<S>)> EnumeratePaths(S pathCondition)
        {
            switch (_kind)
            {
                case TransitionRegexKind.Leaf:
                    Debug.Assert(_leaf is not null);
                    // Omit any path that leads to a deadend
                    if (!_leaf.IsNothing)
                    {
                        yield return (pathCondition, _leaf);
                    }
                    yield break;
                case TransitionRegexKind.Conditional:
                    Debug.Assert(_test is not null && _first is not null && _second is not null);
                    foreach ((S, SymbolicRegexNode<S>) path in _first.EnumeratePaths(_builder._solver.And(pathCondition, _test)))
                    {
                        yield return path;
                    }
                    foreach ((S, SymbolicRegexNode<S>) path in _second.EnumeratePaths(_builder._solver.And(pathCondition, _builder._solver.Not(_test))))
                    {
                        yield return path;
                    }
                    yield break;
                default:
                    Debug.Assert(_kind is TransitionRegexKind.Union && _first is not null && _second is not null);
                    foreach ((S, SymbolicRegexNode<S>) path in _first.EnumeratePaths(pathCondition))
                    {
                        yield return path;
                    }
                    foreach ((S, SymbolicRegexNode<S>) path in _second.EnumeratePaths(pathCondition))
                    {
                        yield return path;
                    }
                    yield break;
            }
        }
    }
}
