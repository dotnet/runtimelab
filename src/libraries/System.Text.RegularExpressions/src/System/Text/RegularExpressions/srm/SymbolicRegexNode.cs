// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;


namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Kinds of symbolic regexes
    /// </summary>
    internal enum SymbolicRegexKind
    {
        StartAnchor = 1,
        EndAnchor = 2,
        Epsilon = 4,
        Singleton = 8,
        Or = 0x10,
        Concat = 0x20,
        Loop = 0x40,
        IfThenElse = 0x80,
        And = 0x100,
        WatchDog = 0x200,
        BOLAnchor = 0x400,
        EOLAnchor = 0x800,
        WBAnchor = 0x1000,
        NWBAnchor = 0x2000,
        EndAnchorZ = 0x4000,
        //anchor for very first line or start-line after very first \n
        //arises as the reverse of EndAnchorZ
        EndAnchorZRev = 0x8000,
    }

    /// <summary>
    /// Represents an AST node of a symbolic regex.
    /// </summary>
    internal sealed class SymbolicRegexNode<S>
    {
        internal SymbolicRegexBuilder<S> _builder;
        internal SymbolicRegexKind _kind;
        internal int _lower = -1;
        internal int _upper = -1;
        internal S _set;

        internal SymbolicRegexNode<S> _left;
        internal SymbolicRegexNode<S> _right;
        internal SymbolicRegexNode<S> _iteCond;

        internal SymbolicRegexSet<S> _alts;

        /// <summary>
        /// True if this node only involves lazy loops
        /// </summary>
        internal bool IsLazy => _info.IsLazy;

        /// <summary>
        /// True if this node accepts the empty string unconditionally.
        /// </summary>
        internal bool IsNullable => _info.IsNullable;

        /// <summary>
        /// True if this node can potentially accept the empty string depending on anchors and immediate context.
        /// </summary>
        internal bool CanBeNullable
        {
            get
            {
                Debug.Assert(_info.CanBeNullable || !_info.IsNullable);
                return _info.CanBeNullable;
            }
        }

        internal bool StartsWithSomeAnchor => _info.StartsWithSomeAnchor;

        internal SymbolicRegexInfo _info;

        private int _hashcode = -1;

        private const SymbolicRegexKind SomeAnchor = SymbolicRegexKind.BOLAnchor |
            SymbolicRegexKind.EOLAnchor | SymbolicRegexKind.StartAnchor |
            SymbolicRegexKind.EndAnchor | SymbolicRegexKind.EndAnchorZ | SymbolicRegexKind.EndAnchorZRev |
            SymbolicRegexKind.WBAnchor | SymbolicRegexKind.NWBAnchor;

        #region serialization

        /// <summary>
        /// Produce the serialized format of this symbolic regex node.
        /// </summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            Serialize(this, sb);
            return sb.ToString();
        }

        /// <summary>
        /// Append the serialized from of this symbolic regex node into sb.
        /// </summary>
        public void Serialize(StringBuilder sb) => Serialize(this, sb);

        /// <summary>
        /// Append the serialized form of this symbolic regex node to the stringbuilder
        /// </summary>
        public static void Serialize(SymbolicRegexNode<S> node, StringBuilder sb)
        {
            ICharAlgebra<S> solver = node._builder._solver;
            SymbolicRegexNode<S> next = node;
            while (next != null)
            {
                node = next;
                next = null;
                switch (node._kind)
                {
                    case SymbolicRegexKind.Singleton:
                        {
                            if (node._set.Equals(solver.True))
                            {
                                sb.Append('.');
                            }
                            else
                            {
                                sb.Append('[');
                                solver.SerializePredicate(node._set, sb);
                                sb.Append(']');
                            }
                            return;
                        }
                    case SymbolicRegexKind.Loop:
                        {
                            if (node.IsLazy)
                            {
                                sb.Append("l(");
                            }
                            else
                            {
                                sb.Append("L(");
                            }
                            sb.Append(node._lower);
                            sb.Append(',');
                            if (node._upper == int.MaxValue)
                            {
                                sb.Append('*');
                            }
                            else
                            {
                                sb.Append(node._upper);
                            }
                            sb.Append(',');
                            Serialize(node._left, sb);
                            sb.Append(')');
                            return;
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            sb.Append("S(");
                            string separator = "";
                            foreach (SymbolicRegexNode<S> elem in node.ToList())
                            {
                                sb.Append(separator);
                                elem.Serialize(sb);
                                separator = ",";
                            }
                            sb.Append(')');
                            return;
                        }
                    case SymbolicRegexKind.Epsilon:
                        {
                            sb.Append('E');
                            return;
                        }
                    case SymbolicRegexKind.Or:
                        {
                            sb.Append("D(");
                            node._alts.Serialize(sb);
                            sb.Append(')');
                            return;
                        }
                    case SymbolicRegexKind.And:
                        {
                            sb.Append("C(");
                            node._alts.Serialize(sb);
                            sb.Append(')');
                            return;
                        }
                    case SymbolicRegexKind.EndAnchor:
                        {
                            sb.Append('z');
                            return;
                        }
                    case SymbolicRegexKind.EndAnchorZ:
                        {
                            sb.Append('Z');
                            return;
                        }
                    case SymbolicRegexKind.EndAnchorZRev:
                        {
                            sb.Append('a');
                            return;
                        }
                    case SymbolicRegexKind.StartAnchor:
                        {
                            sb.Append('A');
                            return;
                        }
                    case SymbolicRegexKind.EOLAnchor:
                        {
                            sb.Append('$');
                            return;
                        }
                    case SymbolicRegexKind.BOLAnchor:
                        {
                            sb.Append('^');
                            return;
                        }
                    case SymbolicRegexKind.WatchDog:
                        {
                            sb.Append($"W({node._lower})");
                            return;
                        }
                    case SymbolicRegexKind.WBAnchor:
                        {
                            sb.Append('b');
                            return;
                        }
                    case SymbolicRegexKind.NWBAnchor:
                        {
                            sb.Append('B');
                            return;
                        }
                    case SymbolicRegexKind.IfThenElse:
                        {
                            sb.Append("I(");
                            Serialize(node._iteCond, sb);
                            sb.Append(',');
                            Serialize(node._left, sb);
                            sb.Append(',');
                            Serialize(node._right, sb);
                            sb.Append(')');
                            return;
                        }
                    default:
                        {
                            throw new NotImplementedException($"{nameof(Serialize)}:{node._kind}");
                        }
                }
            }
        }

        /// <summary>
        /// Converts a concatenation into an array,
        /// returns a non-concatenation in a singleton array.
        /// </summary>
        public List<SymbolicRegexNode<S>> ToList()
        {
            var list = new List<SymbolicRegexNode<S>>();
            AppendToList(this, list);
            return list;

            static void AppendToList(SymbolicRegexNode<S> concat, List<SymbolicRegexNode<S>> list)
            {
                SymbolicRegexNode<S> node = concat;
                while (node._kind == SymbolicRegexKind.Concat)
                {
                    if (node._left._kind == SymbolicRegexKind.Concat)
                    {
                        AppendToList(node._left, list);
                    }
                    else
                    {
                        list.Add(node._left);
                    }
                    node = node._right;
                }

                list.Add(node);
            }
        }


        #endregion


        private Dictionary<uint, bool> _nullability_cache;
        /// <summary>
        /// Relative nullability that takes into account the immediate character context
        /// in order to resolve nullability of anchors
        /// </summary>
        /// <param name="context">kind info for previous and next characters</param>
        internal bool IsNullableFor(uint context)
        {
            if (!_info.StartsWithSomeAnchor)
                return IsNullable;
            if (!_info.CanBeNullable)
                return false;

            //initialize the nullability cache for this node
            if (_nullability_cache == null)
                _nullability_cache = new Dictionary<uint, bool>();

            if (!_nullability_cache.TryGetValue(context, out bool is_nullable))
            {
                switch (_kind)
                {
                    case SymbolicRegexKind.Loop:
                        is_nullable = _lower == 0 || _left.IsNullableFor(context);
                        break;
                    case SymbolicRegexKind.Concat:
                        is_nullable = _left.IsNullableFor(context) && _right.IsNullableFor(context);
                        break;
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        is_nullable = _alts.IsNullableFor(context);
                        break;
                    case SymbolicRegexKind.IfThenElse:
                        is_nullable = _iteCond.IsNullableFor(context) ? _left.IsNullableFor(context) : _right.IsNullableFor(context);
                        break;
                    case SymbolicRegexKind.StartAnchor:
                        is_nullable = CharKind.Prev(context) == CharKind.StartStop;
                        break;
                    case SymbolicRegexKind.EndAnchor:
                        is_nullable = CharKind.Next(context) == CharKind.StartStop;
                        break;
                    case SymbolicRegexKind.BOLAnchor:
                        // Beg-Of-Line anchor is nullable when the previous character is Newline or Start
                        // note: at least one of the bits must be 1, but both could also be 1 in case of very first newline
                        is_nullable = (CharKind.Prev(context) & CharKind.NewLineS) != 0;
                        break;
                    case SymbolicRegexKind.EOLAnchor:
                        // End-Of-Line anchor is nullable when the next character is Newline or Stop
                        // note: at least one of the bits must be 1, but both could also be 1 in case of \Z
                        is_nullable = (CharKind.Next(context) & CharKind.NewLineS) != 0;
                        break;
                    case SymbolicRegexKind.WBAnchor:
                        // test that prev char is word letter iff next is not not word letter
                        is_nullable = ((CharKind.Prev(context) & CharKind.WordLetter) ^ (CharKind.Next(context) & CharKind.WordLetter)) != 0;
                        break;
                    case SymbolicRegexKind.NWBAnchor:
                        // test that prev char is word letter iff next is word letter
                        is_nullable = ((CharKind.Prev(context) & CharKind.WordLetter) ^ (CharKind.Next(context) & CharKind.WordLetter)) == 0;
                        break;
                    case SymbolicRegexKind.EndAnchorZ:
                        // \Z anchor is nullable when the next character is either the last Newline or Stop
                        // note: CharKind.NewLineS == CharKind.Newline|CharKind.StartStop
                        is_nullable = (CharKind.Next(context) & CharKind.StartStop) != 0;
                        break;
                    default: //SymbolicRegexKind.EndAnchorZRev:
                        {
                            Debug.Assert(_kind == SymbolicRegexKind.EndAnchorZRev);

                            // EndAnchorZRev (rev(\Z)) anchor is nullable when the prev character is either the first Newline or Start
                            // note: CharKind.NewLineS == CharKind.Newline|CharKind.StartStop
                            is_nullable = (CharKind.Prev(context) & CharKind.StartStop) != 0;
                            break;
                        }
                }
                _nullability_cache[context] = is_nullable;
            }
            return is_nullable;
        }

        #region various properties
        /// <summary>
        /// Returns true if this is equivalent to .* (the node must be eager also)
        /// </summary>
        public bool IsDotStar => IsStar && _left._kind == SymbolicRegexKind.Singleton && !IsLazy &&
                    _builder._solver.AreEquivalent(_builder._solver.True, _left._set);

        /// <summary>
        /// Returns true if this is equivalent to [0-[0]]
        /// </summary>
        public bool IsNothing => _kind == SymbolicRegexKind.Singleton &&
                    !_builder._solver.IsSatisfiable(_set);

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 0 and upper bound is max
        /// </summary>
        public bool IsStar => _lower == 0 && _upper == int.MaxValue;

        /// <summary>
        /// Returns true iff this loop has an upper bound
        /// </summary>
        public bool HasUpperBound => _upper < int.MaxValue;

        /// <summary>
        /// Returns true iff this loop has a lower bound
        /// </summary>
        public bool HasLowerBound => _lower > 0;

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 0 and upper bound is 1
        /// </summary>
        public bool IsMaybe => _lower == 0 && _upper == 1;

        /// <summary>
        /// Returns true if this is Epsilon
        /// </summary>
        public bool IsEpsilon => _kind == SymbolicRegexKind.Epsilon;

        /// <summary>
        /// Returns true iff this is either a start-anchor or an end-anchor or EOLAnchor or BOLAnchor
        /// </summary>
        public bool IsAnchor => (_kind & SomeAnchor) != 0;
        #endregion

        /// <summary>
        /// Gets the kind of the regex
        /// </summary>
        internal SymbolicRegexKind Kind => _kind;

        /// <summary>
        /// Left child of a binary node (the child of a unary node, the true-branch of an Ite-node)
        /// </summary>
        public SymbolicRegexNode<S> Left => _left;

        /// <summary>
        /// Right child of a binary node (the false-branch of an Ite-node)
        /// </summary>
        public SymbolicRegexNode<S> Right => _right;

        /// <summary>
        /// The lower bound of a loop
        /// </summary>
        public int LowerBound => _lower;

        /// <summary>
        /// The upper bound of a loop
        /// </summary>
        public int UpperBound => _upper;

        /// <summary>
        /// The set of a singleton
        /// </summary>
        public S Set => _set;

        /// <summary>
        /// Returns the number of top-level concatenation nodes.
        /// </summary>
        private int _ConcatCount = -1;
        public int ConcatCount
        {
            get
            {
                if (_ConcatCount == -1)
                {
                    if (_kind == SymbolicRegexKind.Concat)
                        _ConcatCount = _left.ConcatCount + _right.ConcatCount + 1;
                    else
                        _ConcatCount = 0;
                }
                return _ConcatCount;
            }
        }

        /// <summary>
        /// IfThenElse condition
        /// </summary>
        public SymbolicRegexNode<S> IteCond => _iteCond;

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 1 and upper bound is max
        /// </summary>
        public bool IsPlus => _lower == 1 && _upper == int.MaxValue;

        /// <summary>
        /// AST node of a symbolic regex
        /// </summary>
        /// <param name="builder">the builder</param>
        /// <param name="kind">what kind of node</param>
        /// <param name="left">left child</param>
        /// <param name="right">right child</param>
        /// <param name="lower">lower bound of a loop</param>
        /// <param name="upper">upper boubd of a loop</param>
        /// <param name="set">singelton set</param>
        /// <param name="iteCond">if-then-else condition</param>
        /// <param name="alts">alternatives set of a disjunction</param>
        private SymbolicRegexNode(SymbolicRegexBuilder<S> builder, SymbolicRegexKind kind, SymbolicRegexNode<S> left, SymbolicRegexNode<S> right, int lower, int upper, S set, SymbolicRegexNode<S> iteCond, SymbolicRegexSet<S> alts)
        {
            _builder = builder;
            _kind = kind;
            _left = left;
            _right = right;
            _lower = lower;
            _upper = upper;
            _set = set;
            _iteCond = iteCond;
            _alts = alts;
        }

        internal SymbolicRegexNode<S> ConcatWithoutNormalizing(SymbolicRegexNode<S> next)
        {
            var concat = new SymbolicRegexNode<S>(_builder, SymbolicRegexKind.Concat, this, next, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Concat(_info, next._info)
            };
            return concat;
        }

        #region called only once, in the constructor of SymbolicRegexBuilder

        internal static SymbolicRegexNode<S> MkFalse(SymbolicRegexBuilder<S> builder)
        {
            var f = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, builder._solver.False, null, null)
            {
                _info = SymbolicRegexInfo.Mk()
            };
            return f;
        }

        internal static SymbolicRegexNode<S> MkTrue(SymbolicRegexBuilder<S> builder)
        {
            var t = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, builder._solver.True, null, null)
            {
                _info = SymbolicRegexInfo.Mk(containsSomeCharacter: true)
            };
            return t;
        }

        internal static SymbolicRegexNode<S> MkNewline(SymbolicRegexBuilder<S> builder, S nl)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, nl, null, null)
            {
                _info = SymbolicRegexInfo.Mk()
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkWatchDog(SymbolicRegexBuilder<S> builder, int length)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.WatchDog, null, null, length, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(isAlwaysNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkEpsilon(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Epsilon, null, null, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(isAlwaysNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkEagerEmptyLoop(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Loop, body, null, 0, 0, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(isAlwaysNullable: true, isLazy: false)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkStartAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.StartAnchor, null, null, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkEndAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.EndAnchor, null, null, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkEndAnchorZ(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.EndAnchorZ, null, null, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkEndAnchorZRev(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.EndAnchorZRev, null, null, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkEolAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.EOLAnchor, null, null, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkBolAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.BOLAnchor, null, null, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkWBAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.WBAnchor, null, null, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(startsWithBoundaryAnchor: true, canBeNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkNWBAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.NWBAnchor, null, null, -1, -1, default, null, null)
            {
                _info = SymbolicRegexInfo.Mk(startsWithBoundaryAnchor: true, canBeNullable: true)
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkDotStar(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Loop, body, null, 0, int.MaxValue, default, null, null)
            {
                _info = SymbolicRegexInfo.Loop(body._info, 0, false)
            };
            return node;
        }

        #endregion

        internal static SymbolicRegexNode<S> MkSingleton(SymbolicRegexBuilder<S> builder, S set)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, set, null, null)
            {
                _info = SymbolicRegexInfo.Mk(containsSomeCharacter: !set.Equals(builder._solver.False))
            };
            return node;
        }

        internal static SymbolicRegexNode<S> MkLoop(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body, int lower, int upper, bool isLazy)
        {
            if (lower < 0 || upper < lower)
                throw new AutomataException(AutomataExceptionKind.InvalidArgument);

            var loop = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Loop, body, null, lower, upper, default, null, null)
            {
                _info = SymbolicRegexInfo.Loop(body._info, lower, isLazy)
            };
            return loop;
        }

        internal static SymbolicRegexNode<S> MkOr(SymbolicRegexBuilder<S> builder, params SymbolicRegexNode<S>[] choices)
        {
            SymbolicRegexNode<S> node = MkOr(builder, SymbolicRegexSet<S>.CreateMultiset(builder, choices, SymbolicRegexKind.Or));
            node._info = SymbolicRegexInfo.Or(Array.ConvertAll(choices, c => c._info));
            return node;
        }

        internal static SymbolicRegexNode<S> MkAnd(SymbolicRegexBuilder<S> builder, params SymbolicRegexNode<S>[] conjuncts)
        {
            SymbolicRegexNode<S> node = MkAnd(builder, SymbolicRegexSet<S>.CreateMultiset(builder, conjuncts, SymbolicRegexKind.And));
            node._info = SymbolicRegexInfo.And(Array.ConvertAll(conjuncts, c => c._info));
            return node;
        }

        internal static SymbolicRegexNode<S> MkOr(SymbolicRegexBuilder<S> builder, SymbolicRegexSet<S> alts)
        {
            if (alts.IsNothing)
                return builder._nothing;
            else if (alts.IsEverything)
                return builder._dotStar;
            else if (alts.IsSigleton)
                return alts.GetTheElement();
            else
            {
                var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Or, null, null, -1, -1, default, null, alts)
                {
                    _info = SymbolicRegexInfo.Or(Array.ConvertAll(alts.ToArray(), c => c._info))
                };
                return node;
            }
        }

        internal static SymbolicRegexNode<S> MkAnd(SymbolicRegexBuilder<S> builder, SymbolicRegexSet<S> alts)
        {
            if (alts.IsNothing)
                return builder._nothing;
            else if (alts.IsEverything)
                return builder._dotStar;
            else if (alts.IsSigleton)
                return alts.GetTheElement();
            else
            {
                var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.And, null, null, -1, -1, default, null, alts)
                {
                    _info = SymbolicRegexInfo.And(Array.ConvertAll(alts.ToArray(), c => c._info))
                };
                return node;
            }
        }

        /// <summary>
        /// Make a concatenation of given regexes, if any regex is nothing then return nothing, eliminate
        /// intermediate epsilons. Keep the concatenation flat, assuming both right and left are flat.
        /// </summary>
        internal static SymbolicRegexNode<S> MkConcat(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> left, SymbolicRegexNode<S> right)
        {
            SymbolicRegexNode<S> concat;
            if (left == builder._nothing || right == builder._nothing)
                return builder._nothing;
            else if (left.IsEpsilon)
                return right;
            else if (right.IsEpsilon)
                return left;
            else if (left._kind != SymbolicRegexKind.Concat)
            {
                concat = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, left, right, -1, -1, default, null, null)
                {
                    _info = SymbolicRegexInfo.Concat(left._info, right._info)
                };
                return concat;
            }
            else
            {
                concat = right;
                List<SymbolicRegexNode<S>> left_elems = left.ToList();
                for (int i = left_elems.Count - 1; i >= 0; i--)
                {
                    var tmp = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, left_elems[i], concat, -1, -1, default, null, null)
                    {
                        _info = SymbolicRegexInfo.Concat(left_elems[i]._info, concat._info)
                    };
                    concat = tmp;
                }
            }
            return concat;
        }

        //internal SymbolicRegexNode<S> MkConcatWith(SymbolicRegexNode<S> that)
        //{
        //    switch (this.kind)
        //    {
        //        case SymbolicRegexKind.Concat:
        //            {
        //                var concat = that;
        //                foreach (var node in this.EnumerateConcatElementsBackwards())
        //                {
        //                    var tmp = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, node, concat, -1, -1, default(S), null, null);
        //                    tmp.isNullable = node.isNullable && concat.isNullable;
        //                    tmp.containsAnchors = node.containsAnchors || concat.containsAnchors;
        //                    concat = tmp;
        //                }
        //                return concat;
        //            }
        //        case SymbolicRegexKind.Singleton:
        //            {
        //                if (that.kind == SymbolicRegexKind.Singleton)
        //                {
        //                    var seq = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Sequence, null, null, -1, -1, default(S), null, null, new ImmutableList<S>(this.set, that.set));
        //                    seq.isNullable = false;
        //                    seq.containsAnchors = false;
        //                    return seq;
        //                }
        //                else if (that.kind == SymbolicRegexKind.Sequence)
        //                {
        //                    var seq = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Sequence, null, null, -1, -1, default(S), null, null, new ImmutableList<S>(this.set).Append(that.sequence));
        //                    seq.isNullable = false;
        //                    seq.containsAnchors = false;
        //                    return seq;
        //                }
        //                else if (that.kind == SymbolicRegexKind.Concat)
        //                {
        //                    if (that.left.kind == SymbolicRegexKind.Singleton)
        //                    {
        //                        var seq = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Sequence, null, null, -1, -1, default(S), null, null, new ImmutableList<S>(this.set, that.left.set));
        //                        seq.isNullable = false;
        //                        seq.containsAnchors = false;
        //                        var concat = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, seq, that.right, -1, -1, default(S), null, null);
        //                    }
        //                }
        //            }
        //        default:
        //            {
        //                var concat = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, this, that, -1, -1, default(S), null, null);
        //                concat.isNullable = this.isNullable && that.isNullable;
        //                concat.containsAnchors = this.containsAnchors || that.containsAnchors;
        //                return concat;
        //            }
        //    }
        //}

        private IEnumerable<SymbolicRegexNode<S>> EnumerateConcatElementsBackwards()
        {
            switch (_kind)
            {
                case SymbolicRegexKind.Concat:
                    foreach (SymbolicRegexNode<S> elem in _right.EnumerateConcatElementsBackwards())
                    {
                        yield return elem;
                    }
                    yield return _left;
                    break;

                default:
                    yield return this;
                    break;
            }
        }

        internal static SymbolicRegexNode<S> MkIfThenElse(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> cond, SymbolicRegexNode<S> left, SymbolicRegexNode<S> right)
        {
            if (right == builder._nothing)
            {
                var node = SymbolicRegexNode<S>.MkAnd(builder, cond, left);
                node._info = SymbolicRegexInfo.And(cond._info, left._info);
                return node;
            }
            else
            {
                var ite = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.IfThenElse, left, right, -1, -1, default, cond, null)
                {
                    _info = SymbolicRegexInfo.ITE(cond._info, left._info, right._info)
                };
                return ite;
            }
        }

        /// <summary>
        /// Transform the symbolic regex so that all singletons have been intersected with the given predicate pred.
        /// </summary>
        public SymbolicRegexNode<S> Restrict(S pred)
        {
            switch (_kind)
            {
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.WBAnchor:
                case SymbolicRegexKind.NWBAnchor:
                case SymbolicRegexKind.EndAnchorZ:
                case SymbolicRegexKind.EndAnchorZRev:
                    return this;
                case SymbolicRegexKind.Singleton:
                    {
                        S newset = _builder._solver.MkAnd(_set, pred);
                        if (_set.Equals(newset))
                            return this;
                        else
                            return _builder.MkSingleton(newset);
                    }
                case SymbolicRegexKind.Loop:
                    {
                        SymbolicRegexNode<S> body = _left.Restrict(pred);
                        if (body == _left)
                            return this;
                        else
                            return _builder.MkLoop(body, IsLazy, _lower, _upper);
                    }
                case SymbolicRegexKind.Concat:
                    {
                        SymbolicRegexNode<S> first = _left.Restrict(pred);
                        SymbolicRegexNode<S> second = _right.Restrict(pred);
                        if (first == _left && second == _right)
                            return this;
                        else
                            return _builder.MkConcat(first, second);
                    }
                case SymbolicRegexKind.Or:
                    {
                        SymbolicRegexSet<S> choices = _alts.Restrict(pred);
                        return _builder.MkOr(choices);
                    }
                case SymbolicRegexKind.And:
                    {
                        SymbolicRegexSet<S> conjuncts = _alts.Restrict(pred);
                        return _builder.MkAnd(conjuncts);
                    }
                case SymbolicRegexKind.IfThenElse:
                    {
                        SymbolicRegexNode<S> truecase = _left.Restrict(pred);
                        SymbolicRegexNode<S> falsecase = _right.Restrict(pred);
                        SymbolicRegexNode<S> cond = _iteCond.Restrict(pred);
                        if (truecase == _left && falsecase == _right && cond == _iteCond)
                            return this;
                        else
                            return _builder.MkIfThenElse(cond, truecase, falsecase);
                    }
                default:
                    {
                        throw new NotImplementedException($"{nameof(Restrict)}:{_kind}");
                    }
            }
        }

        /// <summary>
        /// Returns the fixed matching length of the regex or -1 if the regex does not have a fixed matching length.
        /// </summary>
        public int GetFixedLength()
        {
            switch (_kind)
            {
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchorZ:
                case SymbolicRegexKind.EndAnchorZRev:
                case SymbolicRegexKind.WBAnchor:
                case SymbolicRegexKind.NWBAnchor:
                    return 0;
                case SymbolicRegexKind.Singleton:
                    return 1;
                case SymbolicRegexKind.Loop:
                    {
                        if (_lower == _upper)
                        {
                            int body_length = _left.GetFixedLength();
                            if (body_length >= 0)
                                return _lower * body_length;
                            else
                                return -1;
                        }
                        else
                            return -1;
                    }
                case SymbolicRegexKind.Concat:
                    {
                        int left_length = _left.GetFixedLength();
                        if (left_length >= 0)
                        {
                            int right_length = _right.GetFixedLength();
                            if (right_length >= 0)
                                return left_length + right_length;
                        }
                        return -1;
                    }
                case SymbolicRegexKind.Or:
                    {
                        return _alts.GetFixedLength();
                    }
                default:
                    {
                        return -1;
                    }
            }
        }

        /// <summary>
        /// Takes the derivative of the symbolic regex wrt elem.
        /// Assumes that elem is either a minterm wrt the predicates of the whole regex or a singleton set.
        /// </summary>
        /// <param name="elem">given element wrt which the derivative is taken</param>
        /// <param name="context">immediately surrounding character context that affects nullability of anchors</param>
        /// <returns></returns>
        internal SymbolicRegexNode<S> MkDerivative(S elem, uint context)
        {
            if (this == _builder._dotStar || this == _builder._nothing)
                return this;
            else
                switch (_kind)
                {
                    case SymbolicRegexKind.Singleton:
                        {
                            if (_builder._solver.IsSatisfiable(_builder._solver.MkAnd(elem, _set)))
                                return _builder._epsilon;
                            else
                                return _builder._nothing;
                        }
                    case SymbolicRegexKind.Loop:
                        {
                            #region d(a, R*) = d(a,R)R*
                            SymbolicRegexNode<S> step = _left.MkDerivative(elem, context);
                            if (step == _builder._nothing || _upper == 0)
                            {
                                return _builder._nothing;
                            }
                            if (IsStar)
                            {
                                SymbolicRegexNode<S> deriv = _builder.MkConcat(step, this);
                                return deriv;
                            }
                            else if (IsPlus)
                            {
                                SymbolicRegexNode<S> star = _builder.MkLoop(_left, IsLazy);
                                SymbolicRegexNode<S> deriv = _builder.MkConcat(step, star);
                                return deriv;
                            }
                            else
                            {
                                int newupper = _upper == int.MaxValue ? int.MaxValue : _upper - 1;
                                int newlower = _lower == 0 ? 0 : _lower - 1;
                                SymbolicRegexNode<S> rest = _builder.MkLoop(_left, IsLazy, newlower, newupper);
                                SymbolicRegexNode<S> deriv = _builder.MkConcat(step, rest);
                                return deriv;
                            }
                            #endregion
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            #region d(a, AB) = d(a,A)B | (if A nullable then d(a,B))
                            SymbolicRegexNode<S> leftd = _left.MkDerivative(elem, context);
                            SymbolicRegexNode<S> first = _builder._nothing;
                            if (_builder._antimirov && leftd._kind == SymbolicRegexKind.Or)
                                // push concatenations into the union
                                foreach (SymbolicRegexNode<S> d in leftd._alts)
                                    first = _builder.MkOr(first, _builder.MkConcat(d, _right));
                            else
                                first = _builder.MkConcat(leftd, _right);
                            if (_left.IsNullableFor(context))
                            {
                                SymbolicRegexNode<S> second = _right.MkDerivative(elem, context);
                                SymbolicRegexNode<S> deriv = _builder.MkOr2(first, second);
                                return deriv;
                            }
                            else
                            {
                                return first;
                            }
                            #endregion
                        }
                    case SymbolicRegexKind.Or:
                        {
                            #region d(a,A|B) = d(a,A)|d(a,B)
                            SymbolicRegexSet<S> alts_deriv = _alts.MkDerivative(elem, context);
                            return _builder.MkOr(alts_deriv);
                            #endregion
                        }
                    case SymbolicRegexKind.And:
                        {
                            #region d(a,A & B) = d(a,A) & d(a,B)
                            SymbolicRegexSet<S> derivs = _alts.MkDerivative(elem, context);
                            return _builder.MkAnd(derivs);
                            #endregion
                        }
                    case SymbolicRegexKind.IfThenElse:
                        {
                            #region d(a,Ite(A,B,C)) = Ite(d(a,A),d(a,B),d(a,C))
                            SymbolicRegexNode<S> condD = _iteCond.MkDerivative(elem, context);
                            if (condD == _builder._nothing)
                            {
                                SymbolicRegexNode<S> rightD = _right.MkDerivative(elem, context);
                                return rightD;
                            }
                            else if (condD == _builder._dotStar)
                            {
                                SymbolicRegexNode<S> leftD = _left.MkDerivative(elem, context);
                                return leftD;
                            }
                            else
                            {
                                SymbolicRegexNode<S> leftD = _left.MkDerivative(elem, context);
                                SymbolicRegexNode<S> rightD = _right.MkDerivative(elem, context);
                                SymbolicRegexNode<S> ite = _builder.MkIfThenElse(condD, leftD, rightD);
                                return ite;
                            }
                            #endregion
                        }
                    default:
                        return _builder._nothing;
                }
        }

        ///// <summary>
        ///// Temporary counter automaton exploration untility
        ///// </summary>
        ///// <returns></returns>
        //internal Automaton<Tuple<Maybe<S>,ImmutableList<CounterOperation>>> Explore()
        //{
        //    var this_normalized = this.builder.NormalizeGeneralLoops(this);
        //    var stateLookup = new Dictionary<SymbolicRegexNode<S>, int>();
        //    var regexLookup = new Dictionary<int,SymbolicRegexNode<S>>();
        //    stateLookup[this_normalized] = 0;
        //    int stateid = 2;
        //    regexLookup[0] = this_normalized;
        //    SimpleStack<int> frontier = new SimpleStack<int>();
        //    var moves = new List<Move<Tuple<Maybe<S>, ImmutableList<CounterOperation>>>>();
        //    var finalStates = new HashSet<int>();
        //    frontier.Push(0);
        //    var reset0 = this_normalized.GetNullabilityCondition(false, true);
        //    if (reset0 != null)
        //    {
        //        if (reset0.TrueForAll(x => x.Counter.LowerBound == 0))
        //            reset0 = ImmutableList<CounterOperation>.Empty;
        //        moves.Add(Move<Tuple<Maybe<S>, ImmutableList<CounterOperation>>>.Create(0, 1,
        //            new Tuple<Maybe<S>, ImmutableList<CounterOperation>>(Maybe<S>.Nothing, reset0)));
        //        finalStates.Add(1);
        //    }
        //    while (frontier.IsNonempty)
        //    {
        //        var q = frontier.Pop();
        //        var regex = regexLookup[q];
        //        //partition corresponds to the alphabet
        //        foreach (S a in builder.solver.GetPartition())
        //        {
        //            foreach (var cd in builder.EnumerateConditionalDerivatives(a, regex, false))
        //            {
        //                int p;
        //                if (!stateLookup.TryGetValue(cd.PartialDerivative, out p))
        //                {
        //                    p = stateid++;
        //                    stateLookup[cd.PartialDerivative] = p;
        //                    regexLookup[p] = cd.PartialDerivative;

        //                    var reset = cd.PartialDerivative.GetNullabilityCondition(false, true);
        //                    if (reset != null)
        //                    {
        //                        if (reset.TrueForAll(x => x.Counter.LowerBound == 0))
        //                            reset = ImmutableList<CounterOperation>.Empty;
        //                        moves.Add(Move<Tuple<Maybe<S>, ImmutableList<CounterOperation>>>.Create(p, 1,
        //                            new Tuple<Maybe<S>, ImmutableList<CounterOperation>>(Maybe<S>.Nothing, reset)));
        //                        finalStates.Add(1);
        //                    }
        //                    frontier.Push(p);
        //                }
        //                moves.Add(Move<Tuple<Maybe<S>, ImmutableList<CounterOperation>>>.Create(q, p,
        //                    new Tuple<Maybe<S>, ImmutableList<CounterOperation>>(Maybe<S>.Something(a), cd.Condition)));
        //            }
        //        }
        //    }
        //    var aut = Automaton<Tuple<Maybe<S>, ImmutableList<CounterOperation>>>.Create(new CABA<S>(builder),
        //        0, finalStates, moves);
        //    return aut;
        //}

        public override int GetHashCode()
        {
            if (_hashcode == -1)
            {
                _hashcode = _kind switch
                {
                    SymbolicRegexKind.EndAnchor or
                    SymbolicRegexKind.StartAnchor or
                    SymbolicRegexKind.BOLAnchor or
                    SymbolicRegexKind.EOLAnchor or
                    SymbolicRegexKind.Epsilon or
                    SymbolicRegexKind.WBAnchor or
                    SymbolicRegexKind.NWBAnchor or
                    SymbolicRegexKind.EndAnchorZ or
                    SymbolicRegexKind.EndAnchorZRev => (_kind, _info).GetHashCode(),
                    SymbolicRegexKind.WatchDog => (_kind, _lower).GetHashCode(),
                    SymbolicRegexKind.Loop => (_kind, _left, _lower, _upper, _info).GetHashCode(),
                    SymbolicRegexKind.Or or
                    SymbolicRegexKind.And => (_kind, _alts, _info).GetHashCode(),
                    SymbolicRegexKind.Concat => (_left, _right, _info).GetHashCode(),
                    SymbolicRegexKind.Singleton => (_kind, _set).GetHashCode(),
                    SymbolicRegexKind.IfThenElse => (_kind, _iteCond, _left, _right).GetHashCode(),
                    _ => throw new NotImplementedException($"{nameof(GetHashCode)}:{_kind}"),
                };
            }
            return _hashcode;
        }

        public override bool Equals(object obj)
        {
            if (obj is not SymbolicRegexNode<S> that)
            {
                return false;
            }
            else if (this == that)
            {
                return true;
            }
            else
            {
                if (_kind != that._kind || !_info.Equals(that._info))
                    return false;

                return _kind switch
                {
                    SymbolicRegexKind.Concat => _left.Equals(that._left) && _right.Equals(that._right),
                    SymbolicRegexKind.Singleton => Equals(_set, that._set),
                    SymbolicRegexKind.Or or SymbolicRegexKind.And => _alts.Equals(that._alts),
                    SymbolicRegexKind.Loop => _lower == that._lower && _upper == that._upper && _left.Equals(that._left),
                    SymbolicRegexKind.IfThenElse => _iteCond.Equals(that._iteCond) && _left.Equals(that._left) && _right.Equals(that._right),
                    //otherwsie this.kind == that.kind implies they must be the same
                    _ => true,
                };
            }
        }

        private string ToStringForLoop() => _kind switch
        {
            SymbolicRegexKind.Singleton => ToString(),
            _ => $"({ToString()})",
        };

        internal string ToStringForAlts() => _kind switch
        {
            SymbolicRegexKind.Concat or SymbolicRegexKind.Singleton or SymbolicRegexKind.Loop => ToString(),
            _ => "(" + ToString() + ")",
        };

        public override string ToString()
        {
            switch (_kind)
            {
                case SymbolicRegexKind.EndAnchor:
                    return "\\z";
                case SymbolicRegexKind.StartAnchor:
                    return "\\A";
                case SymbolicRegexKind.BOLAnchor:
                    return "^";
                case SymbolicRegexKind.EOLAnchor:
                    return "$";
                case SymbolicRegexKind.Epsilon:
                    return "";
                case SymbolicRegexKind.WatchDog:
                    return "";
                case SymbolicRegexKind.WBAnchor:
                    return "\\b";
                case SymbolicRegexKind.NWBAnchor:
                    return "\\B";
                case SymbolicRegexKind.EndAnchorZ:
                    return "\\Z";
                case SymbolicRegexKind.EndAnchorZRev:
                    return "\\a";
                case SymbolicRegexKind.Loop:
                    {
                        if (IsDotStar)
                            return ".*";
                        else if (IsMaybe)
                            return _left.ToStringForLoop() + "?";
                        else if (IsStar)
                            return _left.ToStringForLoop() + "*" + (IsLazy ? "?" : "");
                        else if (IsPlus)
                            return _left.ToStringForLoop() + "+" + (IsLazy ? "?" : "");
                        else if (_lower == 0 && _upper == 0)
                            return "()";
                        else if (IsBoundedLoop)
                        {
                            if (_lower == _upper)
                                return _left.ToStringForLoop() + "{" + _lower + "}" + (IsLazy ? "?" : "");
                            else
                                return _left.ToStringForLoop() + "{" + _lower + "," + _upper + "}" + (IsLazy ? "?" : "");
                        }
                        else
                            return _left.ToStringForLoop() + "{" + _lower + ",}" + (IsLazy ? "?" : "");
                    }
                case SymbolicRegexKind.Or:
                    return _alts.ToString();
                case SymbolicRegexKind.And:
                    return _alts.ToString();
                case SymbolicRegexKind.Concat:
                    return _left.ToString() + _right.ToString();
                case SymbolicRegexKind.Singleton:
                    return _builder._solver.PrettyPrint(_set);
                default:
                    return "(TBD:if-then-else)";
            }
        }

        /// <summary>
        /// Returns the set of all predicates that occur in the regex or
        /// the set containing True if there are no precidates in the regex, e.g., if the regex is "^"
        /// </summary>
        public HashSet<S> GetPredicates()
        {
            var predicates = new HashSet<S>();
            CollectPredicates_helper(predicates);
            if (predicates.Count == 0)
                predicates.Add(_builder._solver.True);
            return predicates;
        }

        /// <summary>
        /// Collects all predicates that occur in the regex into the given set predicates
        /// </summary>
        private void CollectPredicates_helper(HashSet<S> predicates)
        {
            switch (_kind)
            {
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.EndAnchorZ:
                case SymbolicRegexKind.EndAnchorZRev:
                    {
                        predicates.Add(_builder._newLinePredicate);
                        return;
                    }
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                    return;
                case SymbolicRegexKind.Singleton:
                    {
                        predicates.Add(_set);
                        return;
                    }
                case SymbolicRegexKind.Loop:
                    {
                        _left.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.Or:
                case SymbolicRegexKind.And:
                    {
                        foreach (SymbolicRegexNode<S> sr in _alts)
                            sr.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.Concat:
                    {
                        // avoid deep nested recursion over long concat nodes
                        SymbolicRegexNode<S> conc = this;
                        while (conc._kind == SymbolicRegexKind.Concat)
                        {
                            conc._left.CollectPredicates_helper(predicates);
                            conc = conc._right;
                        }
                        conc.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.IfThenElse:
                    {
                        _iteCond.CollectPredicates_helper(predicates);
                        _left.CollectPredicates_helper(predicates);
                        _right.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.NWBAnchor:
                case SymbolicRegexKind.WBAnchor:
                    {
                        predicates.Add(_builder._wordLetterPredicate);
                        return;
                    }
                default:
                    {
                        throw new NotImplementedException($"{nameof(CollectPredicates_helper)}:{_kind}");
                    }
            }
        }

        /// <summary>
        /// Compute all the minterms from the predicates in this regex.
        /// If S implements IComparable then sort the result in increasing order.
        /// </summary>
        public S[] ComputeMinterms()
        {
            var predicates = new List<S>(GetPredicates());
            var mt = new List<S>(EnumerateMinterms(predicates.ToArray()));
            if (mt[0] is IComparable)
                mt.Sort();
            S[] minterms = mt.ToArray();
            return minterms;
        }

        private IEnumerable<S> EnumerateMinterms(S[] preds)
        {
            foreach (Tuple<bool[], S> pair in _builder._solver.GenerateMinterms(preds))
                yield return pair.Item2;
        }

        /// <summary>
        /// Create the reverse of this regex
        /// </summary>
        public SymbolicRegexNode<S> Reverse()
        {
            switch (_kind)
            {
                case SymbolicRegexKind.Loop:
                    return _builder.MkLoop(_left.Reverse(), IsLazy, _lower, _upper);
                case SymbolicRegexKind.Concat:
                    {
                        SymbolicRegexNode<S> rev = _left.Reverse();
                        SymbolicRegexNode<S> rest = _right;
                        while (rest._kind == SymbolicRegexKind.Concat)
                        {
                            SymbolicRegexNode<S> rev1 = rest._left.Reverse();
                            rev = _builder.MkConcat(rev1, rev);
                            rest = rest._right;
                        }
                        SymbolicRegexNode<S> restr = rest.Reverse();
                        rev = _builder.MkConcat(restr, rev);
                        return rev;
                    }
                case SymbolicRegexKind.Or:
                    {
                        SymbolicRegexNode<S> rev = _builder.MkOr(_alts.Reverse());
                        return rev;
                    }
                case SymbolicRegexKind.And:
                    {
                        SymbolicRegexNode<S> rev = _builder.MkAnd(_alts.Reverse());
                        return rev;
                    }
                case SymbolicRegexKind.IfThenElse:
                    {
                        return _builder.MkIfThenElse(_iteCond.Reverse(), _left.Reverse(), _right.Reverse());
                    }
                case SymbolicRegexKind.WatchDog:
                    //watchdogs are omitted in reverse
                    return _builder._epsilon;
                case SymbolicRegexKind.StartAnchor:
                    // the reverse of StartAnchor is EndAnchor
                    return _builder._endAnchor;
                case SymbolicRegexKind.EndAnchor:
                    return _builder._startAnchor;
                case SymbolicRegexKind.BOLAnchor:
                    // the reverse of BOLanchor is EOLanchor
                    return _builder._eolAnchor;
                case SymbolicRegexKind.EOLAnchor:
                    return _builder._bolAnchor;
                case SymbolicRegexKind.EndAnchorZ:
                    // the reversal of the \Z anchor
                    return _builder._endAnchorZRev;
                case SymbolicRegexKind.EndAnchorZRev:
                    //this can potentially only happen if a reversed regex is reversed again
                    //thus, this case is unreachable here, but included for completeness
                    return _builder._endAnchorZ;
                //remaining cases map to themselves
                default:
                    /*
                     * SymbolicRegexKind.Epsilon
                     * SymbolicRegexKind.Singleton
                     * SymbolicRegexKind.WBAnchor
                     * SymbolicRegexKind.NWBAnchor
                     */
                    return this;
            }
        }

        internal bool StartsWithLoop(int upperBoundLowestValue = 1) => _kind switch
        {
            SymbolicRegexKind.Loop => (_upper < int.MaxValue) && (_upper > upperBoundLowestValue),
            SymbolicRegexKind.Concat => _left.StartsWithLoop(upperBoundLowestValue) || (_left.IsNullable && _right.StartsWithLoop(upperBoundLowestValue)),
            SymbolicRegexKind.Or => _alts.StartsWithLoop(upperBoundLowestValue),
            _ => false,
        };

        /// <summary>
        /// Gets the string prefix that the regex must match or the empty string if such a prefix does not exist.
        /// Sets ignoreCase = true when the prefix works under case-insensitivity.
        /// For example if the input prefix is "---" it sets ignoreCase=false,
        /// if the prefix is "---[aA][bB]" it returns "---AB" and sets ignoreCase=true
        /// </summary>
        internal string GetFixedPrefix(CharSetSolver css, string culture, out bool ignoreCase)
        {
            S[] prefix = GetPrefix();
            BDD[] bdds = Array.ConvertAll(prefix, p => _builder._solver.ConvertToCharSet(css, p));
            //singletons prefix
            string sing_pref = string.Empty;
            //ignore-case prefix
            string ic_pref = string.Empty;
            for (int i = 0; i < bdds.Length; i++)
            {
                if (!css.IsSingleton(bdds[i]))
                    break;

                sing_pref += ((char)bdds[i].GetMin()).ToString();
            }
            for (int i = 0; i < bdds.Length; i++)
            {
                if (!css.ApplyIgnoreCase(css.MkCharConstraint((char)bdds[i].GetMin()), culture).Equals(bdds[i]))
                    break;

                ic_pref += ((char)bdds[i].GetMin()).ToString();
            }
            //return the longer of the two prefixes, prefer the case-sensitive setting
            if (sing_pref.Length >= ic_pref.Length)
            {
                ignoreCase = false;
                return sing_pref;
            }
            else
            {
                ignoreCase = true;
                return ic_pref;
            }
        }

        internal const int MaxPrefixLength = RegexBoyerMoore.MaxLimit;

        internal S[] GetPrefix() => GetPrefixSequence(ImmutableList<S>.Empty, MaxPrefixLength).ToArray();

        private ImmutableList<S> GetPrefixSequence(ImmutableList<S> pref, int lengthBound)
        {
            if (lengthBound == 0)
            {
                return pref;
            }
            else
            {
                switch (_kind)
                {
                    case SymbolicRegexKind.Singleton:
                        {
                            return pref.Add(_set);
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            return _left._kind == SymbolicRegexKind.Singleton ?
                                _right.GetPrefixSequence(pref.Add(_left._set), lengthBound - 1) :
                                pref;
                        }
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        {
                            IEnumerator<SymbolicRegexNode<S>> enumerator = _alts.GetEnumerator();
                            enumerator.MoveNext();
                            ImmutableList<S> alts_prefix = enumerator.Current.GetPrefixSequence(ImmutableList<S>.Empty, lengthBound);
                            while (!alts_prefix.IsEmpty && enumerator.MoveNext())
                            {
                                ImmutableList<S> p = enumerator.Current.GetPrefixSequence(ImmutableList<S>.Empty, lengthBound);
                                int prefix_length = alts_prefix.TakeWhile((x, i) => i < p.Count && x.Equals(p[i])).Count();
                                alts_prefix = alts_prefix.RemoveRange(prefix_length, alts_prefix.Count - prefix_length);
                            }
                            return pref.AddRange(alts_prefix);
                        }
                    default:
                        {
                            return pref;
                        }
                }
            }
        }

        //caching the computation of startset
        private bool _startSetIsComputed;
        private S _startSet;
        /// <summary>
        /// Get the predicate that covers all elements that make some progress.
        /// </summary>
        internal S GetStartSet()
        {
            if (_startSetIsComputed)
                return _startSet;

            _startSet = GetStartSet_();
            _startSetIsComputed = true;
            return _startSet;
        }

        /// <summary>
        /// Compute the startset
        /// </summary>
        private S GetStartSet_()
        {
            switch (_kind)
            {
                //anchors and () do not contribute to the startset
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.WBAnchor:
                case SymbolicRegexKind.NWBAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.EndAnchorZ:
                case SymbolicRegexKind.EndAnchorZRev:
                case SymbolicRegexKind.BOLAnchor:
                    return _builder._solver.False;
                case SymbolicRegexKind.Singleton:
                    return _set;
                case SymbolicRegexKind.Loop:
                    return _left.GetStartSet();
                case SymbolicRegexKind.Concat:
                    {
                        S startSet = _left.GetStartSet();
                        if (_left.CanBeNullable)
                        {
                            S set2 = _right.GetStartSet();
                            startSet = _builder._solver.MkOr(startSet, set2);
                        }
                        return startSet;
                    }
                case SymbolicRegexKind.Or:
                    {
                        S startSet = _builder._solver.False;
                        foreach (SymbolicRegexNode<S> alt in _alts)
                        {
                            startSet = _builder._solver.MkOr(startSet, alt.GetStartSet());
                        }
                        return startSet;
                    }
                case SymbolicRegexKind.And:
                    {
                        S startSet = _builder._solver.True;
                        foreach (SymbolicRegexNode<S> alt in _alts)
                        {
                            startSet = _builder._solver.MkAnd(startSet, alt.GetStartSet());
                        }
                        return startSet;
                    }
                default: //if-then-else
                    {
                        return _builder._solver.MkOr(
                            _builder._solver.MkAnd(_iteCond.GetStartSet(), _left.GetStartSet()),
                            _builder._solver.MkAnd(_builder._solver.MkNot(_iteCond.GetStartSet()), _right.GetStartSet()));
                    }
            }
        }

        /// <summary>
        /// Returns true iff there exists a node that satisfies the predicate
        /// </summary>
        public bool ExistsNode(Predicate<SymbolicRegexNode<S>> pred)
        {
            if (pred(this))
            {
                return true;
            }

            switch (_kind)
            {
                case SymbolicRegexKind.Concat:
                    return _left.ExistsNode(pred) || _right.ExistsNode(pred);

                case SymbolicRegexKind.Or:
                case SymbolicRegexKind.And:
                    foreach (SymbolicRegexNode<S> node in _alts)
                    {
                        if (node.ExistsNode(pred))
                            return true;
                    }
                    return false;

                case SymbolicRegexKind.Loop:
                    return _left.ExistsNode(pred);

                default:
                    return false;
            }
        }

        public int CounterId => _builder.GetCounterId(this);

        /// <summary>
        /// Returns true if this is a loop with an upper bound
        /// </summary>
        public bool IsBoundedLoop => _kind == SymbolicRegexKind.Loop && _upper < int.MaxValue;

        /// <summary>
        /// Returns true if there is a loop
        /// </summary>
        public bool CheckIfLoopExists()
        {
            bool existsLoop = ExistsNode(node => node._kind == SymbolicRegexKind.Loop);
            return existsLoop;
        }

        /// <summary>
        /// Replace anchors that are infeasible by [] wrt the given previous character kind and what continuation is possible.
        /// </summary>
        /// <param name="prevKind">previous character kind</param>
        /// <param name="contWithWL">if true the continuation can start with wordletter or stop</param>
        /// <param name="contWithNWL">if true the continuation can start with nonwordletter or stop</param>
        internal SymbolicRegexNode<S> PruneAnchors(uint prevKind, bool contWithWL, bool contWithNWL)
        {
            if (!_info.StartsWithSomeAnchor)
                return this;

            switch (_kind)
            {
                case SymbolicRegexKind.StartAnchor:
                    return prevKind == CharKind.StartStop ?
                        this :
                        _builder._nothing; //start anchor is only nullable if the previous character is Start

                case SymbolicRegexKind.EndAnchorZRev:
                    return ((prevKind & CharKind.StartStop) != 0) ?
                        this :
                        _builder._nothing; //rev(\Z) is only nullable if the previous characters is Start or the very first \n

                case SymbolicRegexKind.WBAnchor:
                    return (prevKind == CharKind.WordLetter ? contWithNWL : contWithWL) ?
                        this :
                        // \b is impossible when the previous character is \w but no continuation matches \W
                        // or the previous character is \W but no continuation matches \w
                        _builder._nothing;

                case SymbolicRegexKind.NWBAnchor:
                    return (prevKind == CharKind.WordLetter ? contWithWL : contWithNWL) ?
                        this :
                        // \B is impossible when the previous character is \w but no continuation matches \w
                        // or the previous character is \W but no continuation matches \W
                        _builder._nothing;

                case SymbolicRegexKind.Loop:
                    SymbolicRegexNode<S> body = _left.PruneAnchors(prevKind, contWithWL, contWithNWL);
                    return body == _left ?
                        this :
                        MkLoop(_builder, body, _lower, _upper, IsLazy);

                case SymbolicRegexKind.Concat:
                    SymbolicRegexNode<S> left1 = _left.PruneAnchors(prevKind, contWithWL, contWithNWL);
                    SymbolicRegexNode<S> right1 = _left.IsNullable ? _right.PruneAnchors(prevKind, contWithWL, contWithNWL) : _right;
                    return left1 == _left && right1 == _right ?
                        this :
                        MkConcat(_builder, left1, right1);

                case SymbolicRegexKind.Or:
                    {
                        List<SymbolicRegexNode<S>> elems = new();
                        foreach (SymbolicRegexNode<S> alt in _alts)
                        {
                            elems.Add(alt.PruneAnchors(prevKind, contWithWL, contWithNWL));
                        }
                        return MkOr(_builder, elems.ToArray());
                    }

                default:
                    return this;
            }
        }
    }

    /// <summary>
    /// Represents a set of symbolic regexes that is either a disjunction or a conjunction
    /// </summary>
    internal sealed class SymbolicRegexSet<S> : IEnumerable<SymbolicRegexNode<S>>
    {
        internal SymbolicRegexBuilder<S> _builder;

        private readonly HashSet<SymbolicRegexNode<S>> _set;
        //symbolic regex A{0,k}?B is stored as (A,B,true) -> k  -- lazy
        //symbolic regex A{0,k}? is stored as (A,(),true) -> k  -- lazy
        //symbolic regex A{0,k}B is stored as (A,B,false) -> k  -- eager
        //symbolic regex A{0,k} is stored as (A,(),false) -> k  -- eager
        private readonly Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int> _loops;

        internal SymbolicRegexKind _kind;

        private int _hashCode;

        /// <summary>
        /// if >= 0 then the maximal length of a watchdog in the set
        /// </summary>
        internal int _watchdog = -1;

        /// <summary>
        /// Denotes the empty conjunction
        /// </summary>
        public bool IsEverything => _kind == SymbolicRegexKind.And && _set.Count == 0 && _loops.Count == 0;

        /// <summary>
        /// Denotes the empty disjunction
        /// </summary>
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
            //loops contains the actual multi-set part of the collection
            var loops = new Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int>();
            //other represents a normal set
            var other = new HashSet<SymbolicRegexNode<S>>();
            int watchdog = -1;

            foreach (SymbolicRegexNode<S> elem in elems)
            {
                // keep track of the maximal watchdog if this is a disjunction
                // this means for example if the regex is abc(3)|bc(2) and
                // the input is xxxabcyyy then two watchdogs will occur (3) and (2)
                // after reading c and the maximal one is taken
                // in a conjuctive setting this is undefined and the watchdog remains -1
                if (kind == SymbolicRegexKind.Or)
                    if (elem._kind == SymbolicRegexKind.WatchDog && elem._lower > watchdog)
                        watchdog = elem._lower;

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
                            {
                                if (kind == elem._kind)
                                    //flatten the inner set
                                    foreach (SymbolicRegexNode<S> alt in elem._alts)
                                    {
                                        if (alt._kind == SymbolicRegexKind.Loop && alt._lower == 0)
                                            AddLoopElem(builder, loops, other, alt, builder._epsilon, kind);
                                        else if (alt._kind == SymbolicRegexKind.Concat && alt._left._kind == SymbolicRegexKind.Loop && alt._left._lower == 0)
                                            AddLoopElem(builder, loops, other, alt._left, alt._right, kind);
                                        else
                                            other.Add(alt);
                                    }
                                else
                                    other.Add(elem);
                                break;
                            }
                        case SymbolicRegexKind.Loop:
                            {
                                if (elem._lower == 0)
                                    AddLoopElem(builder, loops, other, elem, builder._epsilon, kind);
                                else
                                    other.Add(elem);
                                break;
                            }
                        case SymbolicRegexKind.Concat:
                            {
                                if (elem._kind == SymbolicRegexKind.Concat && elem._left._kind == SymbolicRegexKind.Loop && elem._left._lower == 0)
                                    AddLoopElem(builder, loops, other, elem._left, elem._right, kind);
                                else
                                    other.Add(elem);
                                break;
                            }
                        default:
                            {
                                other.Add(elem);
                                break;
                            }
                    }
                }
                #endregion
            }

            //the following further optimizations are only valid for a disjunction
            if (kind == SymbolicRegexKind.Or)
            {
                //if any element of other is covered in loops then omit it
                var others1 = new HashSet<SymbolicRegexNode<S>>();
                foreach (SymbolicRegexNode<S> sr in other)
                {
                    //if there is an element A{0,m} then A is not needed because
                    //it is included by the loop due to the upper bound m > 0
                    var key = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>(sr, builder._epsilon, false);
                    if (loops.ContainsKey(key))
                        others1.Add(sr);
                }
                foreach (KeyValuePair<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>, int> pair in loops)
                {
                    //if there is an element A{0,m}B then B is not needed because
                    //it is included by the concatenation due to the lower bound 0
                    if (other.Contains(pair.Key.Item2))
                        others1.Add(pair.Key.Item2);
                }
                other.ExceptWith(others1);
            }

            if (other.Count == 0 && loops.Count == 0)
            {
                if (kind == SymbolicRegexKind.Or)
                    return builder._emptySet;
                else
                    return builder._fullSet;
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
                // in a set treat a loop with upper=lower=0 and no rest (no continuation after the loop)
                // as () independent of whether it is lazy or eager
                other.Add(builder._epsilon);
            }
            else
            {
                var key = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>, bool>(loop._left, rest, loop.IsLazy);
                if (loops.TryGetValue(key, out int cnt))
                {
                    // if disjunction then map to the maximum of the upper bounds else to the minimum
                    if (kind == SymbolicRegexKind.Or ? cnt < loop._upper : cnt > loop._upper)
                        loops[key] = loop._upper;
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

        /// <summary>
        /// How many elements are there in this set
        /// </summary>
        public int Count => _set.Count + _loops.Count;

        /// <summary>
        /// True iff the set is a singleton
        /// </summary>
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

        public override bool Equals(object obj)
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

        private IEnumerable<SymbolicRegexNode<T>> TransformElems<T>(SymbolicRegexBuilder<T> builderT, Func<S, T> predicateTransformer)
        {
            foreach (SymbolicRegexNode<S> sr in this)
                yield return _builder.Transform(sr, builderT, predicateTransformer);
        }

        internal SymbolicRegexSet<T> Transform<T>(SymbolicRegexBuilder<T> builderT, Func<S, T> predicateTransformer)
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
            private SymbolicRegexNode<S> _current;

            internal Enumerator(SymbolicRegexSet<S> symbolicRegexSet)
            {
                _set = symbolicRegexSet;
                _set_en = symbolicRegexSet._set.GetEnumerator();
                _loops_en = symbolicRegexSet._loops.GetEnumerator();
                _set_next = true;
                _loops_next = true;
                _current = null;
            }

            public SymbolicRegexNode<S> Current => _current;

            object IEnumerator.Current => _current;

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
                    else
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
                        else
                        {
                            _current = null;
                            return false;
                        }
                    }
                }
                else if (_loops_next)
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
                    else
                    {
                        _current = null;
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            public void Reset() => throw new NotImplementedException();
        }
    }
}
