// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.IO;
using System.Text;

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
        EndAnchorZ = 0x1000,
        WBAnchor = 0x2000,
        NWBAnchor = 0x4000,
    }

    /// <summary>
    /// Represents an AST node of a symbolic regex.
    /// </summary>
    internal class SymbolicRegexNode<S>
    {
        internal SymbolicRegexBuilder<S> builder;
        internal SymbolicRegexKind kind;
        internal int lower = -1;
        internal int upper = -1;
        internal S set;

        internal SymbolicRegexNode<S> left;
        internal SymbolicRegexNode<S> right;
        internal SymbolicRegexNode<S> iteCond;

        internal SymbolicRegexSet<S> alts;

        /// <summary>
        /// True if this node only involves lazy loops
        /// </summary>
        internal bool IsLazy { get { return info.IsLazy; } }
        /// <summary>
        /// True if this node accepts the empty string uncoditionally.
        /// </summary>
        internal bool IsNullable { get { return info.IsNullable; } }
        /// <summary>
        /// True if this node can potentially accept the empty string depending on anchors and immediate context.
        /// </summary>
        internal bool CanBeNullable
        {
            get
            {
#if DEBUG
                if (!info.CanBeNullable && info.IsNullable)
                    throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
                    return info.CanBeNullable;
            }

        }
        internal bool StartsWithSomeAnchor { get { return info.StartsWithSomeAnchor; } }

        internal SymbolicRegexInfo info;

        private int hashcode = -1;

        private static SymbolicRegexKind s_someanchor = SymbolicRegexKind.BOLAnchor |
            SymbolicRegexKind.EOLAnchor | SymbolicRegexKind.StartAnchor |
            SymbolicRegexKind.EndAnchor | SymbolicRegexKind.EndAnchorZ |
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
        public static void Serialize(SymbolicRegexNode<S> node, System.Text.StringBuilder sb)
        {
            var solver = node.builder.solver;
            SymbolicRegexNode<S> next = node;
            while (next != null)
            {
                node = next;
                next = null;
                switch (node.kind)
                {
                    case SymbolicRegexKind.Singleton:
                        {
                            if (node.set.Equals(solver.True))
                                sb.Append('.');
                            else
                            {
                                sb.Append('[');
                                sb.Append(solver.SerializePredicate(node.set));
                                sb.Append(']');
                            }
                            return;
                        }
                    case SymbolicRegexKind.Loop:
                        {
                            if (node.IsLazy)
                                sb.Append("l(");
                            else
                                sb.Append("L(");
                            sb.Append(node.lower);
                            sb.Append(',');
                            sb.Append(node.upper == int.MaxValue ? "*" : node.upper.ToString());
                            sb.Append(',');
                            Serialize(node.left, sb);
                            sb.Append(')');
                            return;
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            var elems = node.ToArray();
                            var elems_str = Array.ConvertAll(elems, x => x.Serialize());
                            var str = string.Join(",", elems_str);
                            sb.Append("S(");
                            sb.Append(str);
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
                            node.alts.Serialize(sb);
                            sb.Append(')');
                            return;
                        }
                    case SymbolicRegexKind.And:
                        {
                            sb.Append("C(");
                            node.alts.Serialize(sb);
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
                            sb.Append("#(" + node.lower + ")");
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
                            Serialize(node.iteCond, sb);
                            sb.Append(',');
                            Serialize(node.left, sb);
                            sb.Append(',');
                            Serialize(node.right, sb);
                            sb.Append(')');
                            return;
                        }
                    default:
                        {
                            throw new NotImplementedException($"{nameof(Serialize)}:{node.kind}");
                        }
                }
            }
        }

        /// <summary>
        /// Converts a concatenation into an array,
        /// returns a non-concatenation in a singleton array.
        /// </summary>
        public SymbolicRegexNode<S>[] ToArray()
        {
            var list = new List<SymbolicRegexNode<S>>();
            AppendToList(this, list);
            return list.ToArray();
        }

        /// <summary>
        /// should only be used only if this is a concatenation node
        /// </summary>
        private static void AppendToList(SymbolicRegexNode<S> concat, List<SymbolicRegexNode<S>> list)
        {
            var node = concat;
            while (node.kind == SymbolicRegexKind.Concat)
            {
                if (node.left.kind == SymbolicRegexKind.Concat)
                    AppendToList(node.left, list);
                else
                    list.Add(node.left);
                node = node.right;
            }
            list.Add(node);
        }


        #endregion

        /// <summary>
        /// Relative nullability that takes into account the immediate character context
        /// in order to resolve nullability of anchors
        /// </summary>
        /// <param name="context">kind info for previous and next character, previous char kind is in lower 4 bits, and
        /// next char kind has been shifted 4 bits left into bits 4..7 in the context</param>
        internal bool IsNullableFor(uint context)
        {
            if (!info.StartsWithSomeAnchor)
                return IsNullable;
            else
            {
                switch (kind)
                {
                    case SymbolicRegexKind.Loop:
                        return lower == 0 || left.IsNullableFor(context);
                    case SymbolicRegexKind.Concat:
                        return (left.IsNullableFor(context) && right.IsNullableFor(context));
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        return alts.IsNullableFor(context);
                    case SymbolicRegexKind.IfThenElse:
                        return (iteCond.IsNullableFor(context) ? left.IsNullableFor(context) : right.IsNullableFor(context));
                    case SymbolicRegexKind.StartAnchor:
                        {
                            if ((context & CharKind.Reverse) == 0)
                                return (context & CharKind.Start) != 0;
                            else
                                // the roles of previous and next character info are switched
                                return ((context >> 4) & CharKind.Start) != 0;
                        }
                    case SymbolicRegexKind.BOLAnchor:
                        {
                            if ((context & CharKind.Reverse) == 0)
                                return (context & (CharKind.Start | CharKind.Newline)) != 0;
                            else
                                // the roles of previous and next character info are switched
                                return ((context >> 4) & (CharKind.Start | CharKind.Newline)) != 0;
                        }
                    case SymbolicRegexKind.WBAnchor:
                        // test that prev char is word letter iff next is not not word letter
                        return ((context & CharKind.WordLetter) ^ ((context >> 4) & CharKind.WordLetter)) != 0;
                    case SymbolicRegexKind.NWBAnchor:
                        // test that prev char is word letter iff next is word letter
                        return ((context & CharKind.WordLetter) ^ ((context >> 4) & CharKind.WordLetter)) == 0;
                    case SymbolicRegexKind.EOLAnchor:
                        {
                            // End-Of-Line anchor is nullable when the next character is Newline or End
                            // note: at least one of the bits must be 1, but both could also be 1 in case of \Z
                            if ((context & CharKind.Reverse) == 0)
                                return ((context >> 4) & (CharKind.Newline | CharKind.End)) != 0;
                            else
                                // the roles of previous and next character info are switched in reverse mode
                                return (context & (CharKind.Newline | CharKind.End)) != 0;
                        }
                    case SymbolicRegexKind.EndAnchorZ:
                        {
                            // \Z anchor is nullable when the next character is either the last Newline or End
                            // note: CharKind.NewLineZ == CharKind.Newline|CharKind.End
                            if ((context & CharKind.Reverse) == 0)
                                return ((context >> 4) & CharKind.End) != 0;
                            else
                                // the roles of previous and next character info are switched in reverse mode
                                return (context & CharKind.End) != 0;
                        }
                    default:
                        {
#if DEBUG
                            if (kind != SymbolicRegexKind.EndAnchor)
                                throw new Exception($"Unexpected {nameof(SymbolicRegexKind)}.{kind}");
#endif
                            // \z anchor is nullable when the next character is End
                            if ((context & CharKind.Reverse) == 0)
                                return (context >> 4) == CharKind.End;
                            else
                                // the roles of previous and next character info are switched in reverse mode
                                return (context & 0xF) == CharKind.End;
                        }
                }
            }
        }

        #region various properties
        /// <summary>
        /// Returns true if this is equivalent to .*
        /// </summary>
        public bool IsDotStar
        {
            get
            {
                return this.IsStar && this.left.kind == SymbolicRegexKind.Singleton &&
                    this.builder.solver.AreEquivalent(this.builder.solver.True, this.left.set);
            }
        }

        /// <summary>
        /// Returns true if this is equivalent to [0-[0]]
        /// </summary>
        public bool IsNothing
        {
            get
            {
                return this.kind == SymbolicRegexKind.Singleton &&
                    !this.builder.solver.IsSatisfiable(this.set);
            }
        }

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 0 and upper bound is max
        /// </summary>
        public bool IsStar
        {
            get
            {
                return lower == 0 && upper == int.MaxValue;
            }
        }

        /// <summary>
        /// Returns true iff this loop has an upper bound
        /// </summary>
        public bool HasUpperBound
        {
            get
            {
                return upper < int.MaxValue;
            }
        }

        /// <summary>
        /// Returns true iff this loop has a lower bound
        /// </summary>
        public bool HasLowerBound
        {
            get
            {
                return lower > 0;
            }
        }

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 0 and upper bound is 1
        /// </summary>
        public bool IsMaybe
        {
            get
            {
                return lower == 0 && upper == 1;
            }
        }

        /// <summary>
        /// Returns true if this is Epsilon
        /// </summary>
        public bool IsEpsilon
        {
            get
            {
                return this.kind == SymbolicRegexKind.Epsilon;
            }
        }

        /// <summary>
        /// Returns true iff this is either a start-anchor or an end-anchor or EOLAnchor or BOLAnchor
        /// </summary>
        public bool IsAnchor
        {
            get { return (kind & s_someanchor) != 0; }
        }
        #endregion

        /// <summary>
        /// Alternatives of an OR
        /// </summary>
        public IEnumerable<SymbolicRegexNode<S>> Alts
        {
            get { return alts; }
        }

        /// <summary>
        /// Gets the kind of the regex
        /// </summary>
        internal SymbolicRegexKind Kind
        {
            get { return kind; }
        }

        /// <summary>
        /// Number of alternative branches if this is an or-node.
        /// If this is not an or-node then the value is 1.
        /// </summary>
        public int OrCount
        {
            get
            {
                if (kind == SymbolicRegexKind.Or)
                    return alts.Count;
                else
                    return 1;
            }
        }

        /// <summary>
        /// Left child of a binary node (the child of a unary node, the true-branch of an Ite-node)
        /// </summary>
        public SymbolicRegexNode<S> Left
        {
            get { return left; }
        }

        /// <summary>
        /// Right child of a binary node (the false-branch of an Ite-node)
        /// </summary>
        public SymbolicRegexNode<S> Right
        {
            get { return right; }
        }

        /// <summary>
        /// The lower bound of a loop
        /// </summary>
        public int LowerBound
        {
            get
            {
                return lower;
            }
        }

        /// <summary>
        /// The upper bound of a loop
        /// </summary>
        public int UpperBound
        {
            get
            {
                return upper;
            }
        }

        /// <summary>
        /// The set of a singleton
        /// </summary>
        public S Set
        {
            get
            {
                return set;
            }
        }

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
                    if (this.kind == SymbolicRegexKind.Concat)
                        _ConcatCount = left.ConcatCount + right.ConcatCount + 1;
                    else
                        _ConcatCount = 0;
                }
                return _ConcatCount;
            }
        }

        /// <summary>
        /// IfThenElse condition
        /// </summary>
        public SymbolicRegexNode<S> IteCond
        {
            get { return iteCond; }
        }

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 1 and upper bound is max
        /// </summary>
        public bool IsPlus
        {
            get
            {
                return lower == 1 && upper == int.MaxValue;
            }
        }

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
            this.builder = builder;
            this.kind = kind;
            this.left = left;
            this.right = right;
            this.lower = lower;
            this.upper = upper;
            this.set = set;
            this.iteCond = iteCond;
            this.alts = alts;
        }

        internal SymbolicRegexNode<S> ConcatWithoutNormalizing(SymbolicRegexNode<S> next)
        {
            var concat = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, this, next, -1, -1, default(S), null, null);
            concat.info = SymbolicRegexInfo.Concat(this.info, next.info);
            return concat;
        }

        #region called only once, in the constructor of SymbolicRegexBuilder

        internal static SymbolicRegexNode<S> MkFalse(SymbolicRegexBuilder<S> builder)
        {
            var f = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, builder.solver.False, null, null);
            f.info = SymbolicRegexInfo.Mk();
            return f;
        }

        internal static SymbolicRegexNode<S> MkTrue(SymbolicRegexBuilder<S> builder)
        {
            var t = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, builder.solver.True, null, null);
            t.info = SymbolicRegexInfo.Mk(containsSomeCharacter: true);
            return t;
        }

        internal static SymbolicRegexNode<S> MkNewline(SymbolicRegexBuilder<S> builder, S nl)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, nl, null, null);
            node.info = SymbolicRegexInfo.Mk();
            return node;
        }

        internal static SymbolicRegexNode<S> MkWatchDog(SymbolicRegexBuilder<S> builder, int length)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.WatchDog, null, null, length, -1, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(isAlwaysNullable: true);
            return node;
        }

        internal static SymbolicRegexNode<S> MkEpsilon(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Epsilon, null, null, -1, -1, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(isAlwaysNullable: true);
            return node;
        }

        internal static SymbolicRegexNode<S> MkEagerEmptyLoop(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Loop, body, null, 0, 0, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(isAlwaysNullable: true, isLazy: false);
            return node;
        }

        internal static SymbolicRegexNode<S> MkStartAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.StartAnchor, null, null, -1, -1, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true);
            return node;
        }

        internal static SymbolicRegexNode<S> MkEndAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.EndAnchor, null, null, -1, -1, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true);
            return node;
        }

        internal static SymbolicRegexNode<S> MkEndAnchorZ(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.EndAnchorZ, null, null, -1, -1, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true);
            return node;
        }

        internal static SymbolicRegexNode<S> MkEolAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.EOLAnchor, null, null, -1, -1, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true);
            return node;
        }

        internal static SymbolicRegexNode<S> MkBolAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.BOLAnchor, null, null, -1, -1, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true);
            return node;
        }

        internal static SymbolicRegexNode<S> MkWBAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.WBAnchor, null, null, -1, -1, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(startsWithBoundaryAnchor: true, canBeNullable: true);
            return node;
        }

        internal static SymbolicRegexNode<S> MkNWBAnchor(SymbolicRegexBuilder<S> builder)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.NWBAnchor, null, null, -1, -1, default(S), null, null);
            node.info = SymbolicRegexInfo.Mk(startsWithBoundaryAnchor: true, canBeNullable: true);
            return node;
        }

        internal static SymbolicRegexNode<S> MkDotStar(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Loop, body, null, 0, int.MaxValue, default(S), null, null);
            node.info = SymbolicRegexInfo.Loop(body.info, 0, true);
            return node;
        }

        #endregion

        internal static SymbolicRegexNode<S> MkSingleton(SymbolicRegexBuilder<S> builder, S set)
        {
            var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, set, null, null);
            node.info = SymbolicRegexInfo.Mk(containsSomeCharacter : !set.Equals(builder.solver.False));
            return node;
        }

        internal static SymbolicRegexNode<S> MkLoop(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body, int lower, int upper, bool isLazy)
        {
            if (lower < 0 || upper < lower)
                throw new AutomataException(AutomataExceptionKind.InvalidArgument);

            var loop = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Loop, body, null, lower, upper, default(S), null, null);
            loop.info = SymbolicRegexInfo.Loop(body.info, lower, isLazy);
            return loop;
        }

        internal static SymbolicRegexNode<S> MkOr(SymbolicRegexBuilder<S> builder, params SymbolicRegexNode<S>[] choices)
        {
            var node = MkOr(builder, SymbolicRegexSet<S>.CreateDisjunction(builder, choices));
            node.info = SymbolicRegexInfo.Or(Array.ConvertAll(choices, c => c.info));
            return node;
        }

        internal static SymbolicRegexNode<S> MkAnd(SymbolicRegexBuilder<S> builder, params SymbolicRegexNode<S>[] conjuncts)
        {
            var node = MkAnd(builder, SymbolicRegexSet<S>.CreateConjunction(builder, conjuncts));
            node.info = SymbolicRegexInfo.And(Array.ConvertAll(conjuncts, c => c.info));
            return node;
        }

        internal static SymbolicRegexNode<S> MkOr(SymbolicRegexBuilder<S> builder, SymbolicRegexSet<S> alts)
        {
            if (alts.IsNothing)
                return builder.nothing;
            else if (alts.IsEverything)
                return builder.dotStar;
            else if (alts.IsSigleton)
                return alts.GetTheElement();
            else
            {
                var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Or, null, null, -1, -1, default(S), null, alts);
                node.info = SymbolicRegexInfo.Or(Array.ConvertAll(alts.ToArray(), c => c.info));
                return node;
            }
        }

        internal static SymbolicRegexNode<S> MkAnd(SymbolicRegexBuilder<S> builder, SymbolicRegexSet<S> alts)
        {
            if (alts.IsNothing)
                return builder.nothing;
            else if (alts.IsEverything)
                return builder.dotStar;
            else if (alts.IsSigleton)
                return alts.GetTheElement();
            else
            {
                var node = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.And, null, null, -1, -1, default(S), null, alts);
                node.info = SymbolicRegexInfo.And(Array.ConvertAll(alts.ToArray(), c => c.info));
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
            if (left == builder.nothing || right == builder.nothing)
                return builder.nothing;
            else if (left.IsEpsilon)
                return right;
            else if (right.IsEpsilon)
                return left;
            else if (left.kind != SymbolicRegexKind.Concat)
            {
                concat = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, left, right, -1, -1, default(S), null, null);
                concat.info = SymbolicRegexInfo.Concat(left.info, right.info);
                return concat;
            }
            else
            {
                concat = right;
                var left_elems = left.ToArray();
                for (int i = left_elems.Length - 1; i >= 0; i = i - 1)
                {
                    var tmp = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, left_elems[i], concat, -1, -1, default(S), null, null);
                    tmp.info = SymbolicRegexInfo.Concat(left_elems[i].info, concat.info);
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
            switch (this.kind)
            {
                case SymbolicRegexKind.Concat:
                    foreach (var elem in right.EnumerateConcatElementsBackwards())
                        yield return elem;
                    yield return left;
                    yield break;
                default:
                    yield return this;
                    yield break;
            }
        }

        internal static SymbolicRegexNode<S> MkIfThenElse(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> cond, SymbolicRegexNode<S> left, SymbolicRegexNode<S> right)
        {
            if (right == builder.nothing)
            {
                var node = SymbolicRegexNode<S>.MkAnd(builder, cond, left);
                node.info = SymbolicRegexInfo.And(cond.info, left.info);
                return node;
            }
            else
            {
                var ite = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.IfThenElse, left, right, -1, -1, default(S), cond, null);
                ite.info = SymbolicRegexInfo.ITE(cond.info, left.info, right.info);
                return ite;
            }
        }

        /// <summary>
        /// Transform the symbolic regex so that all singletons have been intersected with the given predicate pred.
        /// </summary>
        public SymbolicRegexNode<S> Restrict(S pred)
        {
            switch (kind)
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
                    return this;
                case SymbolicRegexKind.Singleton:
                    {
                        var newset = builder.solver.MkAnd(this.set, pred);
                        if (this.set.Equals(newset))
                            return this;
                        else
                            return builder.MkSingleton(newset);
                    }
                case SymbolicRegexKind.Loop:
                    {
                        var body = this.left.Restrict(pred);
                        if (body == this.left)
                            return this;
                        else
                            return builder.MkLoop(body, IsLazy, this.lower, this.upper);
                    }
                case SymbolicRegexKind.Concat:
                    {
                        var first = this.left.Restrict(pred);
                        var second = this.right.Restrict(pred);
                        if (first == this.left && second == this.right)
                            return this;
                        else
                            return builder.MkConcat(first, second);
                    }
                case SymbolicRegexKind.Or:
                    {
                        var choices = alts.Restrict(pred);
                        return builder.MkOr(choices);
                    }
                case SymbolicRegexKind.And:
                    {
                        var conjuncts = alts.Restrict(pred);
                        return builder.MkAnd(conjuncts);
                    }
                case SymbolicRegexKind.IfThenElse:
                    {
                        var truecase = this.left.Restrict(pred);
                        var falsecase = this.right.Restrict(pred);
                        var cond = this.iteCond.Restrict(pred);
                        if (truecase == this.left && falsecase == this.right && cond == this.iteCond)
                            return this;
                        else
                            return builder.MkIfThenElse(cond, truecase, falsecase);
                    }
                default:
                    {
                        throw new NotImplementedException($"{nameof(Restrict)}:{kind}");
                    }
            }
        }

        /// <summary>
        /// Returns the fixed matching length of the regex or -1 if the regex does not have a fixed matching length.
        /// </summary>
        public int GetFixedLength()
        {
            switch (kind)
            {
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchorZ:
                case SymbolicRegexKind.WBAnchor:
                case SymbolicRegexKind.NWBAnchor:
                    return 0;
                case SymbolicRegexKind.Singleton:
                    return 1;
                case SymbolicRegexKind.Loop:
                    {
                        if (this.lower == this.upper)
                        {
                            var body_length = this.left.GetFixedLength();
                            if (body_length >= 0)
                                return this.lower * body_length;
                            else
                                return -1;
                        }
                        else
                            return -1;
                    }
                case SymbolicRegexKind.Concat:
                    {
                        var left_length = this.left.GetFixedLength();
                        if (left_length >= 0)
                        {
                            var right_length = this.right.GetFixedLength();
                            if (right_length >= 0)
                                return left_length + right_length;
                        }
                        return -1;
                    }
                case SymbolicRegexKind.Or:
                    {
                        return alts.GetFixedLength();
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
            if (this == builder.dotStar || this == builder.nothing)
                return this;
            else
                switch (kind)
                {
                    case SymbolicRegexKind.Singleton:
                        {
                            if (builder.solver.IsSatisfiable(builder.solver.MkAnd(elem, set)))
                                return builder.epsilon;
                            else
                                return builder.nothing;
                        }
                    case SymbolicRegexKind.Loop:
                        {
                            #region d(a, R*) = d(a,R)R*
                            var step = left.MkDerivative(elem, context);
                            if (step == builder.nothing || upper == 0)
                            {
                                return builder.nothing;
                            }
                            if (IsStar)
                            {
                                var deriv = builder.MkConcat(step, this);
                                return deriv;
                            }
                            else if (IsPlus)
                            {
                                var star = builder.MkLoop(left, IsLazy);
                                var deriv = builder.MkConcat(step, star);
                                return deriv;
                            }
                            //else if (IsMaybe)
                            //{
                            //    return step;
                            //}
                            else
                            {
                                int newupper = (upper == int.MaxValue ? int.MaxValue : upper - 1);
                                int newlower = (lower == 0 ? 0 : lower - 1);
                                var rest = builder.MkLoop(left, IsLazy, newlower, newupper);
                                var deriv = builder.MkConcat(step, rest);
                                return deriv;
                            }
                            #endregion
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            #region d(a, AB) = d(a,A)B | (if A nullable then d(a,B))
                            var first = builder.MkConcat(left.MkDerivative(elem, context), right);
                            if (left.IsNullableFor(context))
                            {
                                var second = right.MkDerivative(elem, context);
                                var deriv = builder.MkOr2(first, second);
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
                            var alts_deriv = alts.MkDerivative(elem, context);
                            return builder.MkOr(alts_deriv);
                            #endregion
                        }
                    case SymbolicRegexKind.And:
                        {
                            #region d(a,A & B) = d(a,A) & d(a,B)
                            var derivs = alts.MkDerivative(elem, context);
                            return builder.MkAnd(derivs);
                            #endregion
                        }
                    case SymbolicRegexKind.IfThenElse:
                        {
                            #region d(a,Ite(A,B,C)) = Ite(d(a,A),d(a,B),d(a,C))
                            var condD = iteCond.MkDerivative(elem, context);
                            if (condD == builder.nothing)
                            {
                                var rightD = right.MkDerivative(elem, context);
                                return rightD;
                            }
                            else if (condD == builder.dotStar)
                            {
                                var leftD = left.MkDerivative(elem, context);
                                return leftD;
                            }
                            else
                            {
                                var leftD = left.MkDerivative(elem, context);
                                var rightD = right.MkDerivative(elem, context);
                                var ite = builder.MkIfThenElse(condD, leftD, rightD);
                                return ite;
                            }
                            #endregion
                        }
                    default:
                        return builder.nothing;
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
            if (hashcode == -1)
            {
                switch (kind)
                {
                    case SymbolicRegexKind.EndAnchor:
                    case SymbolicRegexKind.StartAnchor:
                    case SymbolicRegexKind.BOLAnchor:
                    case SymbolicRegexKind.EOLAnchor:
                    case SymbolicRegexKind.Epsilon:
                    case SymbolicRegexKind.WBAnchor:
                    case SymbolicRegexKind.NWBAnchor:
                    case SymbolicRegexKind.EndAnchorZ:
                        hashcode = (kind, info).GetHashCode();
                        break;
                    case SymbolicRegexKind.WatchDog:
                        hashcode = (kind, lower).GetHashCode();
                        break;
                    case SymbolicRegexKind.Loop:
                        hashcode = (kind, left, lower, upper, info).GetHashCode();
                        break;
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        hashcode = (kind, alts, info).GetHashCode();
                        break;
                    case SymbolicRegexKind.Concat:
                        hashcode = (left, right, info).GetHashCode();
                        break;
                    case SymbolicRegexKind.Singleton:
                        hashcode = (kind, set).GetHashCode();
                        break;
                    case SymbolicRegexKind.IfThenElse:
                        hashcode = (kind, iteCond, left, right).GetHashCode();
                        break;
                    default:
                        throw new NotImplementedException($"{nameof(GetHashCode)}:{kind}");
                }
            }
            return hashcode;
        }

        public override bool Equals(object obj)
        {
            SymbolicRegexNode<S> that = obj as SymbolicRegexNode<S>;
            if (that == null)
            {
                return false;
            }
            else if (this == that)
            {
                return true;
            }
            else
            {
                if (this.kind != that.kind || !this.info.Equals(that.info))
                    return false;
                switch (this.kind)
                {
                    case SymbolicRegexKind.Concat:
                        return this.left.Equals(that.left) && this.right.Equals(that.right);
                    case SymbolicRegexKind.Singleton:
                        return object.Equals(this.set, that.set);
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        return this.alts.Equals(that.alts);
                    case SymbolicRegexKind.Loop:
                        return this.lower == that.lower && this.upper == that.upper && this.left.Equals(that.left);
                    case SymbolicRegexKind.IfThenElse:
                        return this.iteCond.Equals(that.iteCond) && this.left.Equals(that.left) && this.right.Equals(that.right);
                    default: //otherwsie this.kind == that.kind implies they must be the same
                        return true;
                }
            }
        }

        private string ToStringForLoop()
        {
            switch (kind)
            {
                case SymbolicRegexKind.Singleton:
                    return ToString();
                default:
                    return "(" + ToString() + ")";
            }
        }

        internal string ToStringForAlts()
        {
            switch (kind)
            {
                case SymbolicRegexKind.Concat:
                case SymbolicRegexKind.Singleton:
                case SymbolicRegexKind.Loop:
                    return ToString();
                default:
                    return "(" + ToString() + ")";
            }
        }

        public override string ToString()
        {
            switch (kind)
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
                case SymbolicRegexKind.Loop:
                    {
                        if (IsDotStar)
                            return ".*";
                        else if (IsMaybe)
                            return left.ToStringForLoop() + "?";
                        else if (IsStar)
                            return left.ToStringForLoop() + "*" + (IsLazy ? "?" : "");
                        else if (IsPlus)
                            return left.ToStringForLoop() + "+" + (IsLazy ? "?" : "");
                        else if (lower == 0 && upper == 0)
                            return "()";
                        else if (IsBoundedLoop)
                        {
                            if (lower == upper)
                                return left.ToStringForLoop() + "{" + lower + "}" + (IsLazy ? "?" : "");
                            else
                                return left.ToStringForLoop() + "{" + lower + "," + upper + "}" + (IsLazy ? "?" : "");
                        }
                        else
                            return left.ToStringForLoop() + "{" + lower + ",}" + (IsLazy ? "?" : "");
                    }
                case SymbolicRegexKind.Or:
                    return alts.ToString();
                case SymbolicRegexKind.And:
                    return alts.ToString();
                case SymbolicRegexKind.Concat:
                    return left.ToString() + right.ToString();
                case SymbolicRegexKind.Singleton:
                    return builder.solver.PrettyPrint(set);
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
                predicates.Add(builder.solver.True);
            return predicates;
        }

        /// <summary>
        /// Collects all predicates that occur in the regex into the given set predicates
        /// </summary>
        private void CollectPredicates_helper(HashSet<S> predicates)
        {
            switch (kind)
            {
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.EndAnchorZ:
                    {
                        predicates.Add(builder.newLinePredicate);
                        return;
                    }
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                    return;
                case SymbolicRegexKind.Singleton:
                    {
                        predicates.Add(this.set);
                        return;
                    }
                case SymbolicRegexKind.Loop:
                    {
                        this.left.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.Or:
                case SymbolicRegexKind.And:
                    {
                        foreach (SymbolicRegexNode<S> sr in this.alts)
                            sr.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.Concat:
                    {
                        // avoid deep nested recursion over long concat nodes
                        SymbolicRegexNode<S> conc = this;
                        while (conc.kind == SymbolicRegexKind.Concat)
                        {
                            conc.left.CollectPredicates_helper(predicates);
                            conc = conc.right;
                        }
                        conc.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.IfThenElse:
                    {
                        this.iteCond.CollectPredicates_helper(predicates);
                        this.left.CollectPredicates_helper(predicates);
                        this.right.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.NWBAnchor:
                case SymbolicRegexKind.WBAnchor:
                    {
                        predicates.Add(builder.wordLetterPredicate);
                        return;
                    }
                default:
                    {
                        throw new NotImplementedException($"{nameof(CollectPredicates_helper)}:{kind}");
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
            var minterms = mt.ToArray();
            return minterms;
        }

        private IEnumerable<S> EnumerateMinterms(S[] preds)
        {
            foreach (var pair in builder.solver.GenerateMinterms(preds))
                yield return pair.Item2;
        }

        /// <summary>
        /// Create the reverse of this regex
        /// </summary>
        public SymbolicRegexNode<S> Reverse()
        {
            switch (kind)
            {
                case SymbolicRegexKind.WatchDog:
                    return builder.epsilon;
                case SymbolicRegexKind.Loop:
                    return builder.MkLoop(this.left.Reverse(), this.IsLazy, this.lower, this.upper);
                case SymbolicRegexKind.Concat:
                    {
                        var rev = left.Reverse();
                        var rest = this.right;
                        while (rest.kind == SymbolicRegexKind.Concat)
                        {
                            var rev1 = rest.left.Reverse();
                            rev = builder.MkConcat(rev1, rev);
                            rest = rest.right;
                        }
                        var restr = rest.Reverse();
                        rev = builder.MkConcat(restr, rev);
                        return rev;
                    }
                case SymbolicRegexKind.Or:
                    {
                        var rev = builder.MkOr(alts.Reverse());
                        return rev;
                    }
                case SymbolicRegexKind.And:
                    {
                        var rev = builder.MkAnd(alts.Reverse());
                        return rev;
                    }
                case SymbolicRegexKind.IfThenElse:
                    {
                        return builder.MkIfThenElse(iteCond.Reverse(), left.Reverse(), right.Reverse());
                    }
                default:
                    return this;
            }
        }

        internal bool StartsWithLoop(int upperBoundLowestValue = 1)
        {
            switch (kind)
            {
                case SymbolicRegexKind.Loop:
                    return (this.upper < int.MaxValue) && (this.upper > upperBoundLowestValue);
                case SymbolicRegexKind.Concat:
                    return (this.left.StartsWithLoop(upperBoundLowestValue) ||
                        (this.left.IsNullable && this.right.StartsWithLoop(upperBoundLowestValue)));
                case SymbolicRegexKind.Or:
                    return alts.StartsWithLoop(upperBoundLowestValue);
                default:
                    return false;
            }
        }

        private int enabledBoundedLoopCount = -1;

        internal int EnabledBoundedLoopCount
        {
            get
            {
                if (enabledBoundedLoopCount == -1)
                {
                    switch (kind)
                    {
                        case SymbolicRegexKind.EndAnchor:
                        case SymbolicRegexKind.StartAnchor:
                        case SymbolicRegexKind.EOLAnchor:
                        case SymbolicRegexKind.BOLAnchor:
                        case SymbolicRegexKind.Singleton:
                        case SymbolicRegexKind.WatchDog:
                        case SymbolicRegexKind.Epsilon:
                            {
                                enabledBoundedLoopCount = 0;
                                break;
                            }
                        case SymbolicRegexKind.Loop:
                            {
                                //nr of loops in the body
                                int n = this.left.EnabledBoundedLoopCount;
                                if ((this.upper < int.MaxValue) && (this.upper > 0))
                                    n += 1;
                                enabledBoundedLoopCount = n;
                                break;
                            }
                        case SymbolicRegexKind.Concat:
                            {
                                int n = this.left.EnabledBoundedLoopCount;
                                //if (this.left.IsNullable())
                                //    n += this.right.EnabledBoundedLoopCount;
                                enabledBoundedLoopCount = n;
                                break;
                            }
                        case SymbolicRegexKind.Or:
                            {
                                enabledBoundedLoopCount = alts.EnabledBoundedLoopCount;
                                break;
                            }
                        default:
                            throw new NotImplementedException(kind.ToString());
                    }
                }
                return enabledBoundedLoopCount;
            }
        }

        internal int EnabledBoundedLoopValue()
        {

            switch (kind)
            {
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.Singleton:
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.Epsilon:
                    {
                        return 0;
                    }
                case SymbolicRegexKind.Loop:
                    {
                        if (this.upper < int.MaxValue)
                            return this.upper;
                        else
                            return 0;
                    }
                case SymbolicRegexKind.Concat:
                    {
                        return this.left.EnabledBoundedLoopValue();
                    }
                case SymbolicRegexKind.Or:
                    {
                        foreach (var alt in this.alts)
                        {
                            var k = alt.EnabledBoundedLoopValue();
                            if (k > 0)
                                return k;
                        }
                        return 0;
                    }
                default:
                    throw new NotImplementedException(kind.ToString());
            }
        }

        /// <summary>
        /// Only valid to call if there is a single bounded loop
        /// </summary>
        internal SymbolicRegexNode<S> DecrementBoundedLoopCount(bool makeZero = false)
        {
            if (EnabledBoundedLoopCount != 1)
                return this;
            else
            {
                switch (kind)
                {
                    case SymbolicRegexKind.EndAnchor:
                    case SymbolicRegexKind.StartAnchor:
                    case SymbolicRegexKind.EOLAnchor:
                    case SymbolicRegexKind.BOLAnchor:
                    case SymbolicRegexKind.Singleton:
                    case SymbolicRegexKind.WatchDog:
                    case SymbolicRegexKind.Epsilon:
                        {
                            return this;
                        }
                    case SymbolicRegexKind.Loop:
                        {
                            if ((lower == 0) && (upper > 0) && (upper < int.MaxValue))
                            {
                                //must be this loop
                                if (makeZero)
                                    return builder.epsilon;
                                else
                                {
                                    int upper1 = upper - 1;
                                    return builder.MkLoop(this.left, this.IsLazy, 0, upper1);
                                }
                            }
                            else
                            {
                                return this;
                            }
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            return builder.MkConcat(left.DecrementBoundedLoopCount(makeZero), right);
                        }
                    case SymbolicRegexKind.Or:
                        {
                            return builder.MkOr(alts.DecrementBoundedLoopCount(makeZero));
                        }
                    default:
                        throw new NotImplementedException(kind.ToString());
                }
            }
        }

        /// <summary>
        /// Gets the string prefix that the regex must match or the empty string if such a prefix does not exist.
        /// </summary>
        internal string GetFixedPrefix(CharSetSolver css, out bool ignoreCase)
        {
            var pref = GetFixedPrefix_(css, out ignoreCase);
            int i = pref.IndexOf('I');
            int k = pref.IndexOf('K');
            if (ignoreCase && (i != -1 || k != -1))
            {
                //eliminate I and K to avoid possible semantic discrepancy with later search
                //due to \u0130 (capital I with dot above, İ,  in regex same as i modulo ignore case)
                //due to \u212A (Kelvin sign, in regex same as k under ignore case)
                //but these do not match with string.IndexOf modulo ignore case
                if (k == -1)
                    return pref.Substring(0, i);
                else if (i == -1)
                    return pref.Substring(0, k);
                else
                    return pref.Substring(0, (i < k ? i : k));
            }
            else
            {
                return pref;
            }
        }

        private string GetFixedPrefix_(CharSetSolver css, out bool ignoreCase)
        {
            #region compute fixedPrefix
            S[] prefix = GetPrefix();
            if (prefix.Length == 0)
            {
                ignoreCase = false;
                return string.Empty;
            }
            else
            {
                BDD[] bdds = Array.ConvertAll(prefix, p => builder.solver.ConvertToCharSet(css, p));
                if (Array.TrueForAll(bdds, x => css.IsSingleton(x)))
                {
                    //all elements are singletons
                    char[] chars = Array.ConvertAll(bdds, x => (char)x.GetMin());
                    ignoreCase = false;
                    return new string(chars);
                }
                else
                {
                    //maps x to itself if x is invariant under ignoring case
                    //maps x to False otherwise
                    Func<BDD, BDD> F = x =>
                    {
                        char c = (char)x.GetMin();
                        var y = css.MkCharConstraint(c, true);
                        if (x == y)
                            return x;
                        else
                            return css.False;
                    };
                    BDD[] bdds1 = Array.ConvertAll(bdds, x => F(x));
                    if (Array.TrueForAll(bdds1, x => !x.IsEmpty))
                    {
                        //all elements are singletons up-to-ignoring-case
                        //choose representatives
                        char[] chars = Array.ConvertAll(bdds, x => (char)x.GetMin());
                        ignoreCase = true;
                        return new string(chars);
                    }
                    else
                    {
                        List<char> elems = new List<char>();
                        //extract prefix of singletons
                        for (int i = 0; i < bdds.Length; i++)
                        {
                            if (css.IsSingleton(bdds[i]))
                                elems.Add((char)bdds[i].GetMin());
                            else
                                break;
                        }
                        List<char> elemsI = new List<char>();
                        //extract prefix up-to-ignoring-case
                        for (int i = 0; i < bdds1.Length; i++)
                        {
                            if (bdds1[i].IsEmpty)
                                break;
                            else
                                elemsI.Add((char)bdds1[i].GetMin());
                        }
                        //TBD: these heuristics should be evaluated more
                        #region different cases of fixed prefix
                        if (elemsI.Count > elems.Count)
                        {
                            ignoreCase = true;
                            return new string(elemsI.ToArray());
                        }
                        else if (elems.Count > 0)
                        {
                            ignoreCase = false;
                            return new string(elems.ToArray());
                        }
                        else if (elemsI.Count > 0)
                        {
                            ignoreCase = true;
                            return new string(elemsI.ToArray());
                        }
                        else
                        {
                            ignoreCase = false;
                            return string.Empty;
                        }
                        #endregion
                    }
                }
            }
            #endregion
        }

        internal const int maxPrefixLength = 5;
        internal S[] GetPrefix()
        {
            return GetPrefixSequence(ImmutableList<S>.Empty, maxPrefixLength).ToArray();
        }

        private ImmutableList<S> GetPrefixSequence(ImmutableList<S> pref, int lengthBound)
        {
            if (lengthBound == 0)
            {
                return pref;
            }
            else
            {
                switch (this.kind)
                {
                    case SymbolicRegexKind.Singleton:
                        {
                            return pref.Add(this.set);
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            if (this.left.kind == SymbolicRegexKind.Singleton)
                                return this.right.GetPrefixSequence(pref.Add(this.left.set), lengthBound - 1);
                            else
                                return pref;
                        }
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        {
                            var enumerator = alts.GetEnumerator();
                            enumerator.MoveNext();
                            var alts_prefix = enumerator.Current.GetPrefixSequence(ImmutableList<S>.Empty, lengthBound);
                            while (!alts_prefix.IsEmpty && enumerator.MoveNext())
                            {
                                var p = enumerator.Current.GetPrefixSequence(ImmutableList<S>.Empty, lengthBound);
                                var prefix_length = alts_prefix.TakeWhile((x, i) => i < p.Count && x.Equals(p[i])).Count();
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

        ///// <summary>
        ///// If this node starts with a loop other than star or plus then
        ///// returns the nonnegative id of the associated counter else returns -1
        ///// </summary>
        //public int CounterId
        //{
        //    get
        //    {
        //        if (this.kind == SymbolicRegexKind.Loop && !this.IsStar && !this.IsPlus)
        //            return this.builder.GetCounterId(this);
        //        else if (this.kind == SymbolicRegexKind.Concat)
        //            return left.CounterId;
        //        else
        //            return -1;
        //    }
        //}

        //0 means value is not computed,
        //-1 means this is not a sequence of singletons
        //1 means it is a sequence of singletons
        internal int sequenceOfSingletons_count;

        internal bool IsSequenceOfSingletons
        {
            get
            {
                if (sequenceOfSingletons_count == 0)
                {
                    var node = this;
                    int k = 1;
                    while (node.kind == SymbolicRegexKind.Concat && node.left.kind == SymbolicRegexKind.Singleton)
                    {
                        node = node.right;
                        k += 1;
                    }
                    if (node.kind == SymbolicRegexKind.Singleton)
                    {
                        node.sequenceOfSingletons_count = 1;
                        node = this;
                        while (node.kind == SymbolicRegexKind.Concat)
                        {
                            node.sequenceOfSingletons_count = k;
                            node = node.right;
                            k = k - 1;
                        }
                    }
                    else
                    {
                        node.sequenceOfSingletons_count = -1;
                        node = this;
                        while (node.kind == SymbolicRegexKind.Concat && node.left.kind == SymbolicRegexKind.Singleton)
                        {
                            node.sequenceOfSingletons_count = -1;
                            node = node.right;
                        }
                    }
                }
                return sequenceOfSingletons_count > 0;
            }
        }

        /// <summary>
        /// Gets the predicate that covers all elements that make some progress.
        /// </summary>
        internal S GetStartSet()
        {
            switch (kind)
            {
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.WBAnchor:
                case SymbolicRegexKind.NWBAnchor:
                    return builder.solver.False;
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.EndAnchorZ:
                case SymbolicRegexKind.BOLAnchor:
                    return builder.newLinePredicate;
                case SymbolicRegexKind.Singleton:
                    return this.set;
                case SymbolicRegexKind.Loop:
                    return this.left.GetStartSet();
                case SymbolicRegexKind.Concat:
                    {
                        var startSet = this.left.GetStartSet();
                        if (left.CanBeNullable)
                        {
                            var set2 = this.right.GetStartSet();
                            startSet = builder.solver.MkOr(startSet, set2);
                        }
                        return startSet;
                    }
                case SymbolicRegexKind.Or:
                    {
                        S startSet = builder.solver.False;
                        foreach (var alt in alts)
                            startSet = builder.solver.MkOr(startSet, alt.GetStartSet());
                        return startSet;
                    }
                case SymbolicRegexKind.And:
                    {
                        S startSet = builder.solver.True;
                        foreach (var alt in alts)
                            startSet = builder.solver.MkAnd(startSet, alt.GetStartSet());
                        return startSet;
                    }
                default: //if-then-else
                    {
                        S startSet = builder.solver.MkOr(iteCond.GetStartSet(), builder.solver.MkOr(left.GetStartSet(), right.GetStartSet()));
                        return startSet;
                    }
            }
        }

        /// <summary>
        /// Returns true iff there exists a node that satisfies the predicate
        /// </summary>
        public bool ExistsNode(Predicate<SymbolicRegexNode<S>> pred)
        {
            if (pred(this))
                return true;
            else
                switch (kind)
                {
                    case SymbolicRegexKind.Concat:
                        return left.ExistsNode(pred) || right.ExistsNode(pred);
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        foreach (var node in this.alts)
                            if (node.ExistsNode(pred))
                                return true;
                        return false;
                    case SymbolicRegexKind.Loop:
                        return left.ExistsNode(pred);
                    default:
                        return false;
                }
        }

        public int CounterId
        {
            get
            {
                return builder.GetCounterId(this);
            }
        }

        /// <summary>
        /// Returns true if this is a loop with an upper bound
        /// </summary>
        public bool IsBoundedLoop
        {
            get
            {
                return (this.kind == SymbolicRegexKind.Loop && this.upper < int.MaxValue);
            }
        }

        /// <summary>
        /// Returns true if the match-end of this regex can be determined with a
        /// single pass from the start.
        /// </summary>
        public bool IsSinglePass
        {
            get
            {
                if (this.IsSequenceOfSingletons)
                    return true;
                else
                {
                    switch (kind)
                    {
                        case SymbolicRegexKind.Or:
                            {
                                foreach (var member in alts)
                                    if (!member.IsSinglePass)
                                        return false;
                                return true;
                            }
                        case SymbolicRegexKind.Concat:
                            {
                                return left.IsSinglePass && right.IsSinglePass;
                            }
                        default:
                            return false;
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if there is a loop
        /// </summary>
        public bool CheckIfLoopExists()
        {
            bool existsLoop = this.ExistsNode(node => (node.kind == SymbolicRegexKind.Loop));
            return existsLoop;
        }

        internal SymbolicRegexNode<S> ReplaceStartAnchorByBottom()
        {
            if (!info.ContainsLineAnchor)
                return this;

            switch (kind)
            {
                case SymbolicRegexKind.StartAnchor:
                    return this.builder.nothing;

                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.WBAnchor:
                case SymbolicRegexKind.NWBAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.EndAnchorZ:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.Singleton:
                    return this;
                case SymbolicRegexKind.Loop:
                    return MkLoop(builder, left.ReplaceStartAnchorByBottom(), lower, upper, IsLazy);

                case SymbolicRegexKind.Concat:
                    return MkConcat(builder, left.ReplaceStartAnchorByBottom(), right.ReplaceStartAnchorByBottom());
                case SymbolicRegexKind.Or:
                    {
                        List<SymbolicRegexNode<S>> elems = new();
                        foreach (var alt in alts)
                            elems.Add(alt.ReplaceStartAnchorByBottom());
                        return MkOr(builder, elems.ToArray());
                    }
                case SymbolicRegexKind.And:
                    {
                        List<SymbolicRegexNode<S>> elems = new();
                        foreach (var alt in alts)
                            elems.Add(alt.ReplaceStartAnchorByBottom());
                        return MkAnd(builder, elems.ToArray());
                    }
                default: //if-then-else
                        return MkIfThenElse(builder, iteCond.ReplaceStartAnchorByBottom(), left.ReplaceStartAnchorByBottom(), right.ReplaceStartAnchorByBottom());
            }
        }
    }

    /// <summary>
    /// The kind of a symbolic regex set
    /// </summary>
    internal enum SymbolicRegexSetKind { Conjunction, Disjunction };

    /// <summary>
    /// Represents a set of symbolic regexes that is either a disjunction or a conjunction
    /// </summary>
    internal class SymbolicRegexSet<S> : IEnumerable<SymbolicRegexNode<S>>
    {
        internal SymbolicRegexBuilder<S> builder;

        private HashSet<SymbolicRegexNode<S>> set;
        //if the set kind is disjunction then
        //symbolic regex A{0,k}B is stored as (A,B) -> k
        //symbolic regex A{0,k} is stored as (A,()) -> k
        private Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int> loops;

        internal SymbolicRegexSetKind kind;

        private int hashCode;

        //#region serialization
        ///// <summary>
        ///// Serialize
        ///// </summary>
        //public void GetObjectData(SerializationInfo info, StreamingContext context)
        //{
        //    //var ctx = context.Context as SymbolicRegexBuilder<S>;
        //    //if (ctx == null || ctx != builder)
        //    //    throw new AutomataException(AutomataExceptionKind.InvalidSerializationContext);
        //    info.AddValue("loops", loops);
        //    info.AddValue("set", set);
        //    info.AddValue("kind", kind);
        //}

        ///// <summary>
        ///// Deserialize
        ///// </summary>
        //public SymbolicRegexSet(SerializationInfo info, StreamingContext context)
        //{
        //    builder = context.Context as SymbolicRegexBuilder<S>;
        //    if (builder == null)
        //        throw new AutomataException(AutomataExceptionKind.SerializationNotSupported);

        //    kind = (SymbolicRegexSetKind)info.GetValue("kind", typeof(SymbolicRegexSetKind));
        //    set = (HashSet<SymbolicRegexNode<S>>)info.GetValue("set", typeof(HashSet<SymbolicRegexNode<S>>));
        //    loops = (Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>)info.GetValue("loops", typeof(Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>));
        //}
        //#endregion

        internal SymbolicRegexSetKind Kind
        {
            get { return kind; }
        }

        /// <summary>
        /// if >= 0 then the maximal length of a watchdog in the set
        /// </summary>
        internal int watchdog = -1;

        /// <summary>
        /// Denotes the empty conjunction
        /// </summary>
        public bool IsEverything
        {
            get { return this.kind == SymbolicRegexSetKind.Conjunction && this.set.Count == 0 && this.loops.Count == 0; }
        }

        /// <summary>
        /// Denotes the empty disjunction
        /// </summary>
        public bool IsNothing
        {
            get { return this.kind == SymbolicRegexSetKind.Disjunction && this.set.Count == 0 && this.loops.Count == 0; }
        }

        private SymbolicRegexSet(SymbolicRegexBuilder<S> builder, SymbolicRegexSetKind kind)
        {
            this.builder = builder;
            this.kind = kind;
            this.set = new HashSet<SymbolicRegexNode<S>>();
            this.loops = new Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>();
        }

        private SymbolicRegexSet(SymbolicRegexBuilder<S> builder, SymbolicRegexSetKind kind, HashSet<SymbolicRegexNode<S>> set, Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int> loops)
        {
            this.builder = builder;
            this.kind = kind;
            this.set = set;
            this.loops = loops;
        }

        internal static SymbolicRegexSet<S> MkFullSet(SymbolicRegexBuilder<S> builder)
        {
            return new SymbolicRegexSet<S>(builder, SymbolicRegexSetKind.Conjunction);
        }

        internal static SymbolicRegexSet<S> MkEmptySet(SymbolicRegexBuilder<S> builder)
        {
            return new SymbolicRegexSet<S>(builder, SymbolicRegexSetKind.Disjunction);
        }

        internal static SymbolicRegexSet<S> CreateDisjunction(SymbolicRegexBuilder<S> builder, IEnumerable<SymbolicRegexNode<S>> elems)
        {
            var loops = new Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>();
            var other = new HashSet<SymbolicRegexNode<S>>();
            int watchdog = -1;

            foreach (var elem in elems)
            {
                //keep track of maximal watchdog in the set
                if (elem.kind == SymbolicRegexKind.WatchDog && elem.lower > watchdog)
                    watchdog = elem.lower;

                #region start foreach
                if (elem == builder.dotStar)
                    return builder.fullSet;
                else if (elem != builder.nothing)
                {
                    switch (elem.kind)
                    {
                        case SymbolicRegexKind.Or:
                            {
                                foreach (var alt in elem.alts)
                                {
                                    if (alt.kind == SymbolicRegexKind.Loop && alt.lower == 0)
                                    {
                                        var pair = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(alt.left, builder.epsilon);
                                        //map to the maximal of the upper bounds
                                        int cnt;
                                        if (loops.TryGetValue(pair, out cnt))
                                        {
                                            if (cnt < alt.upper)
                                                loops[pair] = alt.upper;
                                        }
                                        else
                                        {
                                            loops[pair] = alt.upper;
                                        }
                                    }
                                    else if (alt.kind == SymbolicRegexKind.Concat && alt.left.kind == SymbolicRegexKind.Loop && alt.left.lower == 0)
                                    {
                                        var pair = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(alt.left.left, alt.right);
                                        //map to the maximal of the upper bounds
                                        int cnt;
                                        if (loops.TryGetValue(pair, out cnt))
                                        {
                                            if (cnt < alt.left.upper)
                                                loops[pair] = alt.left.upper;
                                        }
                                        else
                                        {
                                            loops[pair] = alt.left.upper;
                                        }
                                    }
                                    else
                                    {
                                        other.Add(alt);
                                    }
                                }
                                break;
                            }
                        case SymbolicRegexKind.Loop:
                            {
                                if (elem.kind == SymbolicRegexKind.Loop && elem.lower == 0)
                                {
                                    var pair = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(elem.left, builder.epsilon);
                                    //map the body of the loop (elem.left) to the maximal of the upper bounds
                                    int cnt;
                                    if (loops.TryGetValue(pair, out cnt))
                                    {
                                        if (cnt < elem.upper)
                                            loops[pair] = elem.upper;
                                    }
                                    else
                                    {
                                        loops[pair] = elem.upper;
                                    }
                                }
                                else
                                {
                                    other.Add(elem);
                                }
                                break;
                            }
                        case SymbolicRegexKind.Concat:
                            {
                                if (elem.kind == SymbolicRegexKind.Concat && elem.left.kind == SymbolicRegexKind.Loop && elem.left.lower == 0)
                                {
                                    var pair = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(elem.left.left, elem.right);
                                    //map to the maximal of the upper bounds
                                    int cnt;
                                    if (loops.TryGetValue(pair, out cnt))
                                    {
                                        if (cnt < elem.left.upper)
                                            loops[pair] = elem.left.upper;
                                    }
                                    else
                                    {
                                        loops[pair] = elem.left.upper;
                                    }
                                }
                                else
                                {
                                    other.Add(elem);
                                }
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
            //if any element of other is covered in loops then omit it
            var others1 = new HashSet<SymbolicRegexNode<S>>();
            foreach (var sr in other)
            {
                var key = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(sr, builder.epsilon);
                if (loops.ContainsKey(key))
                    others1.Add(sr);
            }
            foreach (var pair in loops)
            {
                if (other.Contains(pair.Key.Item2))
                    others1.Add(pair.Key.Item2);
            }
            other.ExceptWith(others1);

            if (other.Count == 0 && loops.Count == 0)
                return builder.emptySet;
            else
            {
                var disj = new SymbolicRegexSet<S>(builder, SymbolicRegexSetKind.Disjunction, other, loops);
                disj.watchdog = watchdog;
                return disj;
            }
        }

        internal static SymbolicRegexSet<S> CreateConjunction(SymbolicRegexBuilder<S> builder, IEnumerable<SymbolicRegexNode<S>> elems)
        {
            var loops = new Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>();
            var conjuncts = new HashSet<SymbolicRegexNode<S>>();
            foreach (var elem in elems)
            {
                if (elem == builder.nothing)
                    return builder.emptySet;
                if (elem == builder.dotStar)
                    continue;
                if (elem.kind == SymbolicRegexKind.And)
                {
                    conjuncts.UnionWith(elem.alts);
                }
                else
                {
                    conjuncts.Add(elem);
                }
            }
            return new SymbolicRegexSet<S>(builder, SymbolicRegexSetKind.Conjunction, conjuncts, loops);
        }

        private IEnumerable<SymbolicRegexNode<S>> RestrictElems(S pred)
        {
            foreach (var elem in this)
                yield return elem.Restrict(pred);
        }

        public SymbolicRegexSet<S> Restrict(S pred)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, RestrictElems(pred));
            else
                return CreateConjunction(builder, RestrictElems(pred));
        }

        /// <summary>
        /// How many elements are there in this set
        /// </summary>
        public int Count
        {
            get
            {
                return set.Count + loops.Count;
            }
        }

        /// <summary>
        /// True iff the set is a singleton
        /// </summary>
        public bool IsSigleton
        {
            get
            {
                return this.Count == 1;
            }
        }

        public bool IsNullable(bool isFirst = false, bool isLast = false)
        {
            var e = this.GetEnumerator();
            if (kind == SymbolicRegexSetKind.Disjunction)
            {
                #region some element must be nullable
                while (e.MoveNext())
                {
                    if (e.Current.IsNullable)
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
                    if (!e.Current.IsNullable)
                        return false;
                }
                return true;
                #endregion
            }
        }

        internal bool IsNullableFor(uint context)
        {
            var e = this.GetEnumerator();
            if (kind == SymbolicRegexSetKind.Disjunction)
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
            if (hashCode == 0)
            {
                hashCode = this.kind.GetHashCode();
                var e = set.GetEnumerator();
                while (e.MoveNext())
                {
                    hashCode = hashCode ^ e.Current.GetHashCode();
                }
                e.Dispose();
                var e2 = loops.GetEnumerator();
                while (e2.MoveNext())
                {
                    hashCode = (hashCode ^ (e2.Current.Key.GetHashCode() + e2.Current.Value));
                }
            }
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            var that = obj as SymbolicRegexSet<S>;
            if (that == null)
                return false;
            if (this.kind != that.kind)
                return false;
            if (this.set.Count != that.set.Count)
                return false;
            if (this.loops.Count != that.loops.Count)
                return false;
            if (this.set.Count > 0 && !this.set.SetEquals(that.set))
                return false;
            var e1 = this.loops.GetEnumerator();
            while (e1.MoveNext())
            {
                int cnt;
                if (!that.loops.TryGetValue(e1.Current.Key, out cnt))
                    return false;
                if (cnt != e1.Current.Value)
                    return false;
            }
            e1.Dispose();
            return true;
        }

        public override string ToString()
        {
            string res = "";
            var e = this.GetEnumerator();
            var R = new List<string>();
            while (e.MoveNext())
                R.Add(e.Current.ToStringForAlts());
            if (R.Count == 0)
                return res;
            if (kind == SymbolicRegexSetKind.Disjunction)
            {
                #region display as R[0]|R[1]|...
                for (int i = 0; i < R.Count; i++)
                {
                    if (res != "")
                        res += "|";
                    res += R[i].ToString();
                }
                // parentheses are needed in some cases in concatenations
                res = "(" + res + ")";
                #endregion
            }
            else
            {
                #region display using if-then-else construct: (?(A)(B)|[0-[0]]) to represent intersect(A,B)
                res = R[R.Count - 1].ToString();
                for (int i = R.Count - 2; i >= 0; i--)
                {
                    //unfortunately [] is an invalid character class expression, using [0-[0]] instead
                    res = string.Format("(?({0})({1})|{2})", R[i].ToString(), res, "[0-[0]]");
                }
                #endregion
            }
            //if (this.Count > 1 && kind == SymbolicRegexSetKind.Disjunction)
            //    //add extra parentesis to enclose the disjunction
            //    return "(" + res + ")";
            //else
            return res;
        }

        internal SymbolicRegexNode<S>[] ToArray(SymbolicRegexBuilder<S> builder)
        {
            List<SymbolicRegexNode<S>> elemsL = new List<SymbolicRegexNode<S>>(this);
            SymbolicRegexNode<S>[] elems = elemsL.ToArray();
            return elems;
        }

        internal SymbolicRegexSet<S> MkDerivative(S elem, uint context)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, MkDerivativesOfElems(elem, context));
            else
                return CreateConjunction(builder, MkDerivativesOfElems(elem, context));
        }

        private IEnumerable<SymbolicRegexNode<S>> MkDerivativesOfElems(S elem, uint context)
        {
            foreach (var s in this)
                yield return s.MkDerivative(elem, context);
        }

        private IEnumerable<SymbolicRegexNode<T>> TransformElems<T>(SymbolicRegexBuilder<T> builderT, Func<S, T> predicateTransformer)
        {
            foreach (var sr in this)
                yield return builder.Transform(sr, builderT, predicateTransformer);
        }

        internal SymbolicRegexSet<T> Transform<T>(SymbolicRegexBuilder<T> builderT, Func<S, T> predicateTransformer)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return SymbolicRegexSet<T>.CreateDisjunction(builderT, TransformElems(builderT, predicateTransformer));
            else
                return SymbolicRegexSet<T>.CreateConjunction(builderT, TransformElems(builderT, predicateTransformer));
        }

        internal SymbolicRegexNode<S> GetTheElement()
        {
            var en = this.GetEnumerator();
            en.MoveNext();
            var elem = en.Current;
            en.Dispose();
            return elem;
        }

        internal SymbolicRegexSet<S> Reverse()
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, ReverseElems());
            else
                return CreateConjunction(builder, ReverseElems());
        }

        private IEnumerable<SymbolicRegexNode<S>> ReverseElems()
        {
            foreach (var elem in this)
                yield return elem.Reverse();
        }

        internal bool StartsWithLoop(int upperBoundLowestValue)
        {
            bool res = false;
            var e = this.GetEnumerator();
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

        internal SymbolicRegexSet<S> DecrementBoundedLoopCount(bool makeZero = false)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, DecrementBoundedLoopCountElems(makeZero));
            else
                return CreateConjunction(builder, DecrementBoundedLoopCountElems(makeZero));
        }

        private IEnumerable<SymbolicRegexNode<S>> DecrementBoundedLoopCountElems(bool makeZero = false)
        {
            foreach (var elem in this)
                yield return elem.DecrementBoundedLoopCount(makeZero);
        }

        private int enabledBoundedLoopCount = -1;
        internal int EnabledBoundedLoopCount
        {
            get
            {
                if (enabledBoundedLoopCount == -1)
                {
                    int res = 0;
                    var en = this.GetEnumerator();
                    while (en.MoveNext())
                    {
                        res += en.Current.EnabledBoundedLoopCount;
                    }
                    en.Dispose();
                    enabledBoundedLoopCount = res;
                }
                return enabledBoundedLoopCount;
            }
        }

        public IEnumerator<SymbolicRegexNode<S>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        internal string Serialize()
        {
            var list = new List<SymbolicRegexNode<S>>(this);
            var arr = list.ToArray();
            var ser = Array.ConvertAll(arr, x => x.Serialize());
            var str = new List<string>(ser);
            str.Sort();
            return string.Join(",", str);
        }

        internal void Serialize(StringBuilder sb)
        {
            sb.Append(Serialize());
        }

        internal int GetFixedLength()
        {
            if (loops.Count > 0)
                return -1;
            else
            {
                int length = -1;
                foreach (var node in this.set)
                {
                    var node_length = node.GetFixedLength();
                    if (node_length == -1)
                        return -1;
                    else if (length == -1)
                        length = node_length;
                    else if (length != node_length)
                        return -1;
                }
                return length;
            }
        }

        /// <summary>
        /// Enumerates all symbolic regexes in the set
        /// </summary>
        internal class Enumerator : IEnumerator<SymbolicRegexNode<S>>
        {
            private SymbolicRegexSet<S> set;
            private bool set_next;
            private HashSet<SymbolicRegexNode<S>>.Enumerator set_en;
            private bool loops_next;
            private Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>.Enumerator loops_en;
            private SymbolicRegexNode<S> current;

            internal Enumerator(SymbolicRegexSet<S> symbolicRegexSet)
            {
                this.set = symbolicRegexSet;
                set_en = symbolicRegexSet.set.GetEnumerator();
                loops_en = symbolicRegexSet.loops.GetEnumerator();
                set_next = true;
                loops_next = true;
                current = null;
            }

            public SymbolicRegexNode<S> Current
            {
                get
                {
                    return current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return current;
                }
            }

            public void Dispose()
            {
                set_en.Dispose();
                loops_en.Dispose();
            }

            public bool MoveNext()
            {
                if (set_next)
                {
                    set_next = set_en.MoveNext();
                    if (set_next)
                    {
                        current = set_en.Current;
                        return true;
                    }
                    else
                    {
                        loops_next = loops_en.MoveNext();
                        if (loops_next)
                        {
                            var body = loops_en.Current.Key.Item1;
                            var rest = loops_en.Current.Key.Item2;
                            var upper = loops_en.Current.Value;
                            //recreate the symbolic regex from (body,rest)->k to body{0,k}rest
                            //TBD:lazy
                            current = set.builder.MkConcat(set.builder.MkLoop(body, false, 0, upper), rest);
                            return true;
                        }
                        else
                        {
                            current = null;
                            return false;
                        }
                    }
                }
                else if (loops_next)
                {
                    loops_next = loops_en.MoveNext();
                    if (loops_next)
                    {
                        var body = loops_en.Current.Key.Item1;
                        var rest = loops_en.Current.Key.Item2;
                        var upper = loops_en.Current.Value;
                        //recreate the symbolic regex from (body,rest)->k to body{0,k}rest
                        current = set.builder.MkConcat(set.builder.MkLoop(body, false, 0, upper), rest);
                        return true;
                    }
                    else
                    {
                        current = null;
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}
