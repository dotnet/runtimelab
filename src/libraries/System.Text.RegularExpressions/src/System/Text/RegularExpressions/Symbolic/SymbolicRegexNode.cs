// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace System.Text.RegularExpressions.Symbolic
{
    /// <summary>Represents an AST node of a symbolic regex.</summary>
    internal sealed class SymbolicRegexNode<S> where S : notnull
    {
        internal const string EmptyCharClass = "[]";

        // Limit the maximum prefix length in the NonBacktracking case to 1000
        // TODO: alternative is to rewrite SymbolicRegexNode.GetPrefixSequence
        // to avoid deep recursion
        internal const int MaxPrefixLength = 1000; // RegexBoyerMoore.MaxLimit;

        internal readonly SymbolicRegexBuilder<S> _builder;
        internal readonly SymbolicRegexKind _kind;
        internal readonly int _lower;
        internal readonly int _upper;
        internal readonly S? _set;
        internal readonly SymbolicRegexNode<S>? _left;
        internal readonly SymbolicRegexNode<S>? _right;
        internal readonly SymbolicRegexSet<S>? _alts;

        private Dictionary<uint, bool>? _nullabilityCache;

        // Caching the computation of _startSet
        private bool _startSetIsComputed;
        private S? _startSet;

        /// <summary>AST node of a symbolic regex</summary>
        /// <param name="builder">the builder</param>
        /// <param name="kind">what kind of node</param>
        /// <param name="left">left child</param>
        /// <param name="right">right child</param>
        /// <param name="lower">lower bound of a loop</param>
        /// <param name="upper">upper boubd of a loop</param>
        /// <param name="set">singelton set</param>
        /// <param name="alts">alternatives set of a disjunction or conjunction</param>
        /// <param name="info">misc flags including laziness</param>
        private SymbolicRegexNode(SymbolicRegexBuilder<S> builder, SymbolicRegexKind kind, SymbolicRegexNode<S>? left, SymbolicRegexNode<S>? right, int lower, int upper, S? set, SymbolicRegexSet<S>? alts, SymbolicRegexInfo info)
        {
            _builder = builder;
            _kind = kind;
            _left = left;
            _right = right;
            _lower = lower;
            _upper = upper;
            _set = set;
            _alts = alts;
            _info = info;
            _hashcode = ComputeHashCode();
        }

        private bool _isInternalizedOrNode;

        /// <summary> Create a new node or retrieve one from the builder _nodeCache</summary>
        private static SymbolicRegexNode<S> Create(SymbolicRegexBuilder<S> builder, SymbolicRegexKind kind, SymbolicRegexNode<S>? left, SymbolicRegexNode<S>? right, int lower, int upper, S? set, SymbolicRegexSet<S>? alts, SymbolicRegexInfo info)
        {
            // Internalize an Or-node that is not yet internalized
            SymbolicRegexNode<S> Internalize(SymbolicRegexNode<S> node)
            {
                (SymbolicRegexKind, SymbolicRegexNode<S>?, SymbolicRegexNode<S>?, int, int, S?, SymbolicRegexSet<S>?, SymbolicRegexInfo) node_key =
                    (SymbolicRegexKind.Or, null, null, -1, -1, default(S), node._alts, node._info);
                SymbolicRegexNode<S>? node1;
                if (builder._nodeCache.TryGetValue(node_key, out node1))
                {
                    Debug.Assert(node1 is not null && node1._isInternalizedOrNode);
                    return node1;
                }
                else
                {
                    node._isInternalizedOrNode = true;
                    builder._nodeCache[node_key] = node;
                    return node;
                }
            }

            SymbolicRegexNode<S>? node;
            var key = (kind, left, right, lower, upper, set, alts, info);
            if (!builder._nodeCache.TryGetValue(key, out node))
            {
                // Do not internalize top level Or-nodes or else Antimirov mode will become ineffective
                if (kind == SymbolicRegexKind.Or)
                {
                    node = new(builder, kind, left, right, lower, upper, set, alts, info);
                    return node;
                }

                left = left == null || left._kind != SymbolicRegexKind.Or || left._isInternalizedOrNode ? left : Internalize(left);
                right = right == null || right._kind != SymbolicRegexKind.Or || right._isInternalizedOrNode ? right : Internalize(right);

                node = new(builder, kind, left, right, lower, upper, set, alts, info);
                builder._nodeCache[key] = node;
            }

            Debug.Assert(node is not null);
            return node;
        }

        /// <summary>True if this node only involves lazy loops</summary>
        internal bool IsLazy => _info.IsLazy;

        /// <summary>True if this node accepts the empty string unconditionally.</summary>
        internal bool IsNullable => _info.IsNullable;

        /// <summary>True if this node can potentially accept the empty string depending on anchors and immediate context.</summary>
        internal bool CanBeNullable
        {
            get
            {
                Debug.Assert(_info.CanBeNullable || !_info.IsNullable);
                return _info.CanBeNullable;
            }
        }

        internal SymbolicRegexInfo _info;

        private readonly int _hashcode;

        #region Serialization

        /// <summary>Produce the serialized format of this symbolic regex node.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            Serialize(this, sb);
            return sb.ToString();
        }

        /// <summary>Append the serialized from of this symbolic regex node into the StringBuilder.</summary>
        public void Serialize(StringBuilder sb) => Serialize(this, sb);

        /// <summary>Append the serialized form of this symbolic regex node to the StringBuilder.</summary>
        public static void Serialize(SymbolicRegexNode<S> node, StringBuilder sb)
        {
            ICharAlgebra<S> solver = node._builder._solver;
            SymbolicRegexNode<S>? next = node;
            while (next != null)
            {
                node = next;
                next = null;
                switch (node._kind)
                {
                    case SymbolicRegexKind.Singleton:
                        Debug.Assert(node._set is not null);
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

                    case SymbolicRegexKind.Loop:
                        Debug.Assert(node._left is not null);
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

                    case SymbolicRegexKind.Concat:
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

                    case SymbolicRegexKind.Epsilon:
                        sb.Append('E');
                        return;

                    case SymbolicRegexKind.Or:
                        Debug.Assert(node._alts is not null);
                        sb.Append("D(");
                        node._alts.Serialize(sb);
                        sb.Append(')');
                        return;

                    case SymbolicRegexKind.And:
                        Debug.Assert(node._alts is not null);
                        sb.Append("C(");
                        node._alts.Serialize(sb);
                        sb.Append(')');
                        return;

                    case SymbolicRegexKind.EndAnchor:
                        sb.Append('z');
                        return;

                    case SymbolicRegexKind.EndAnchorZ:
                        sb.Append('Z');
                        return;

                    case SymbolicRegexKind.EndAnchorZRev:
                        sb.Append('a');
                        return;

                    case SymbolicRegexKind.StartAnchor:
                        sb.Append('A');
                        return;

                    case SymbolicRegexKind.EOLAnchor:
                        sb.Append('$');
                        return;

                    case SymbolicRegexKind.BOLAnchor:
                        sb.Append('^');
                        return;

                    case SymbolicRegexKind.WatchDog:
                        sb.Append($"W({node._lower})");
                        return;

                    case SymbolicRegexKind.WBAnchor:
                        sb.Append('b');
                        return;

                    case SymbolicRegexKind.NWBAnchor:
                        sb.Append('B');
                        return;

                    case SymbolicRegexKind.Not:
                        {
                            Debug.Assert(node._left is not null);
                            sb.Append('N');
                            Serialize(node._left, sb);
                            return;
                        }

                    default:
                        Debug.Fail($"{nameof(Serialize)}:{node._kind}");
                        return;
                }
            }
        }

        /// <summary>Converts a concatenation into an array, returns a non-concatenation in a singleton array.</summary>
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
                    Debug.Assert(node._left is not null && node._right is not null);
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

            // Initialize the nullability cache for this node.
            _nullabilityCache ??= new Dictionary<uint, bool>();

            if (!_nullabilityCache.TryGetValue(context, out bool is_nullable))
            {
                switch (_kind)
                {
                    case SymbolicRegexKind.Loop:
                        Debug.Assert(_left is not null);
                        is_nullable = _lower == 0 || _left.IsNullableFor(context);
                        break;

                    case SymbolicRegexKind.Concat:
                        Debug.Assert(_left is not null && _right is not null);
                        is_nullable = _left.IsNullableFor(context) && _right.IsNullableFor(context);
                        break;

                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        Debug.Assert(_alts is not null);
                        is_nullable = _alts.IsNullableFor(context);
                        break;

                    case SymbolicRegexKind.Not:
                        Debug.Assert(_left is not null);
                        is_nullable = !_left.IsNullableFor(context);
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
                        // EndAnchorZRev (rev(\Z)) anchor is nullable when the prev character is either the first Newline or Start
                        // note: CharKind.NewLineS == CharKind.Newline|CharKind.StartStop
                        Debug.Assert(_kind == SymbolicRegexKind.EndAnchorZRev);
                        is_nullable = (CharKind.Prev(context) & CharKind.StartStop) != 0;
                        break;
                }

                _nullabilityCache[context] = is_nullable;
            }

            return is_nullable;
        }

        /// <summary>Returns true if this is equivalent to .* (the node must be eager also)</summary>
        public bool IsDotStar
        {
            get
            {
                if (IsStar)
                {
                    Debug.Assert(_left is not null);
                    if (_left._kind == SymbolicRegexKind.Singleton)
                    {
                        Debug.Assert(_left._set is not null);
                        return !IsLazy && _builder._solver.AreEquivalent(_builder._solver.True, _left._set);
                    }
                }

                return false;
            }
        }

        /// <summary>Returns true if this is equivalent to [0-[0]]</summary>
        public bool IsNothing
        {
            get
            {
                if (_kind == SymbolicRegexKind.Singleton)
                {
                    Debug.Assert(_set is not null);
                    return !_builder._solver.IsSatisfiable(_set);
                }

                return false;
            }
        }

        /// <summary>Returns true iff this is a loop whose lower bound is 0 and upper bound is max</summary>
        public bool IsStar => _lower == 0 && _upper == int.MaxValue;

        /// <summary>Returns true iff this is a loop whose lower bound is 0 and upper bound is 1</summary>
        public bool IsMaybe => _lower == 0 && _upper == 1;

        /// <summary>Returns true if this is Epsilon</summary>
        public bool IsEpsilon => _kind == SymbolicRegexKind.Epsilon;

        /// <summary>Gets the kind of the regex</summary>
        internal SymbolicRegexKind Kind => _kind;

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 1 and upper bound is max
        /// </summary>
        public bool IsPlus => _lower == 1 && _upper == int.MaxValue;

        #region called only once, in the constructor of SymbolicRegexBuilder

        internal static SymbolicRegexNode<S> MkFalse(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, builder._solver.False, null, SymbolicRegexInfo.Mk());

        internal static SymbolicRegexNode<S> MkTrue(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, builder._solver.True, null, SymbolicRegexInfo.Mk(containsSomeCharacter: true));

        internal static SymbolicRegexNode<S> MkWatchDog(SymbolicRegexBuilder<S> builder, int length) =>
            Create(builder, SymbolicRegexKind.WatchDog, null, null, length, -1, default, null, SymbolicRegexInfo.Mk(isAlwaysNullable: true));

        internal static SymbolicRegexNode<S> MkEpsilon(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.Epsilon, null, null, -1, -1, default, null, SymbolicRegexInfo.Mk(isAlwaysNullable: true));

        internal static SymbolicRegexNode<S> MkEagerEmptyLoop(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body) =>
            Create(builder, SymbolicRegexKind.Loop, body, null, 0, 0, default, null, SymbolicRegexInfo.Mk(isAlwaysNullable: true, isLazy: false));

        internal static SymbolicRegexNode<S> MkStartAnchor(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.StartAnchor, null, null, -1, -1, default, null, SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true));

        internal static SymbolicRegexNode<S> MkEndAnchor(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.EndAnchor, null, null, -1, -1, default, null, SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true));

        internal static SymbolicRegexNode<S> MkEndAnchorZ(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.EndAnchorZ, null, null, -1, -1, default, null, SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true));

        internal static SymbolicRegexNode<S> MkEndAnchorZRev(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.EndAnchorZRev, null, null, -1, -1, default, null, SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true));

        internal static SymbolicRegexNode<S> MkEolAnchor(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.EOLAnchor, null, null, -1, -1, default, null, SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true));

        internal static SymbolicRegexNode<S> MkBolAnchor(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.BOLAnchor, null, null, -1, -1, default, null, SymbolicRegexInfo.Mk(startsWithLineAnchor: true, canBeNullable: true));

        internal static SymbolicRegexNode<S> MkWBAnchor(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.WBAnchor, null, null, -1, -1, default, null, SymbolicRegexInfo.Mk(startsWithBoundaryAnchor: true, canBeNullable: true));

        internal static SymbolicRegexNode<S> MkNWBAnchor(SymbolicRegexBuilder<S> builder) =>
            Create(builder, SymbolicRegexKind.NWBAnchor, null, null, -1, -1, default, null, SymbolicRegexInfo.Mk(startsWithBoundaryAnchor: true, canBeNullable: true));

        internal static SymbolicRegexNode<S> MkDotStar(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body) =>
            Create(builder, SymbolicRegexKind.Loop, body, null, 0, int.MaxValue, default, null, SymbolicRegexInfo.Loop(body._info, 0, false));

        #endregion

        internal static SymbolicRegexNode<S> MkSingleton(SymbolicRegexBuilder<S> builder, S set) =>
            Create(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, set, null, SymbolicRegexInfo.Mk(containsSomeCharacter: !set.Equals(builder._solver.False)));

        internal static SymbolicRegexNode<S> MkLoop(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body, int lower, int upper, bool isLazy)
        {
            Debug.Assert(lower >= 0 && lower <= upper);
            return Create(builder, SymbolicRegexKind.Loop, body, null, lower, upper, default, null, SymbolicRegexInfo.Loop(body._info, lower, isLazy));
        }

        internal static SymbolicRegexNode<S> MkOr(SymbolicRegexBuilder<S> builder, params SymbolicRegexNode<S>[] disjuncts) =>
            MkCollection(builder, SymbolicRegexKind.Or, SymbolicRegexSet<S>.CreateMulti(builder, disjuncts, SymbolicRegexKind.Or), SymbolicRegexInfo.Or(GetInfos(disjuncts)));

        internal static SymbolicRegexNode<S> MkOr(SymbolicRegexBuilder<S> builder, SymbolicRegexSet<S> disjuncts)
        {
            Debug.Assert(disjuncts._kind == SymbolicRegexKind.Or || disjuncts.IsEverything);
            return MkCollection(builder, SymbolicRegexKind.Or, disjuncts, SymbolicRegexInfo.Or(GetInfos(disjuncts)));
        }

        internal static SymbolicRegexNode<S> MkAnd(SymbolicRegexBuilder<S> builder, params SymbolicRegexNode<S>[] conjuncts) =>
            MkCollection(builder, SymbolicRegexKind.And, SymbolicRegexSet<S>.CreateMulti(builder, conjuncts, SymbolicRegexKind.And), SymbolicRegexInfo.And(GetInfos(conjuncts)));

        internal static SymbolicRegexNode<S> MkAnd(SymbolicRegexBuilder<S> builder, SymbolicRegexSet<S> conjuncts)
        {
            Debug.Assert(conjuncts.IsNothing || conjuncts._kind == SymbolicRegexKind.And);
            return MkCollection(builder, SymbolicRegexKind.And, conjuncts, SymbolicRegexInfo.And(GetInfos(conjuncts)));
        }

        private static SymbolicRegexNode<S> MkCollection(SymbolicRegexBuilder<S> builder, SymbolicRegexKind kind, SymbolicRegexSet<S> alts, SymbolicRegexInfo info) =>
            alts.IsNothing ? builder._nothing :
            alts.IsEverything ? builder._dotStar :
            alts.IsSingleton ? alts.GetSingletonElement() :
            Create(builder, kind, null, null, -1, -1, default, alts, info);

        private static SymbolicRegexInfo[] GetInfos(SymbolicRegexNode<S>[] nodes)
        {
            var infos = new SymbolicRegexInfo[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                infos[i] = nodes[i]._info;
            }
            return infos;
        }

        private static SymbolicRegexInfo[] GetInfos(SymbolicRegexSet<S> nodes)
        {
            var infos = new SymbolicRegexInfo[nodes.Count];
            int i = 0;
            foreach (SymbolicRegexNode<S> node in nodes)
            {
                Debug.Assert(i < nodes.Count);
                infos[i++] = node._info;
            }
            Debug.Assert(i == nodes.Count);
            return infos;
        }

        /// <summary>
        /// Make a concatenation of given regexes, if any regex is nothing then return nothing, eliminate
        /// intermediate epsilons. Keep the concatenation flat, assuming both right and left are flat.
        /// </summary>
        internal static SymbolicRegexNode<S> MkConcat(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> left, SymbolicRegexNode<S> right)
        {
            if (left == builder._nothing || right == builder._nothing)
                return builder._nothing;

            if (left.IsEpsilon)
                return right;

            if (right.IsEpsilon)
                return left;

            if (left._kind != SymbolicRegexKind.Concat)
            {
                return Create(builder, SymbolicRegexKind.Concat, left, right, -1, -1, default, null, SymbolicRegexInfo.Concat(left._info, right._info));
            }

            SymbolicRegexNode<S> concat = right;
            List<SymbolicRegexNode<S>> left_elems = left.ToList();
            for (int i = left_elems.Count - 1; i >= 0; i--)
            {
                concat = Create(builder, SymbolicRegexKind.Concat, left_elems[i], concat, -1, -1, default, null, SymbolicRegexInfo.Concat(left_elems[i]._info, concat._info));
            }
            return concat;
        }

        internal static SymbolicRegexNode<S> MkNot(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> node) =>
            Create(builder, SymbolicRegexKind.Not, node, null, -1, -1, default, null, SymbolicRegexInfo.Not(node._info));

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
                        Debug.Assert(_set is not null);
                        S newset = _builder._solver.And(_set, pred);
                        return _set.Equals(newset) ? this : _builder.MkSingleton(newset);
                    }

                case SymbolicRegexKind.Loop:
                    {
                        Debug.Assert(_left is not null);
                        SymbolicRegexNode<S> body = _left.Restrict(pred);
                        return body == _left ? this : _builder.MkLoop(body, IsLazy, _lower, _upper);
                    }

                case SymbolicRegexKind.Concat:
                    {
                        Debug.Assert(_left is not null && _right is not null);
                        SymbolicRegexNode<S> first = _left.Restrict(pred);
                        SymbolicRegexNode<S> second = _right.Restrict(pred);
                        return first == _left && second == _right ? this : _builder.MkConcat(first, second);
                    }

                case SymbolicRegexKind.Or:
                    {
                        Debug.Assert(_alts is not null);
                        SymbolicRegexSet<S> choices = _alts.Restrict(pred);
                        return _builder.MkOr(choices);
                    }

                case SymbolicRegexKind.And:
                    {
                        Debug.Assert(_alts is not null);
                        SymbolicRegexSet<S> conjuncts = _alts.Restrict(pred);
                        return _builder.MkAnd(conjuncts);
                    }

                default:
                    {
                        Debug.Assert(_kind == SymbolicRegexKind.Not, $"{nameof(Restrict)}:{_kind}");
                        Debug.Assert(_left is not null);
                        SymbolicRegexNode<S> restricted = _left.Restrict(pred);
                        return _builder.MkNot(restricted);
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
                        Debug.Assert(_left is not null);
                        if (_lower == _upper)
                        {
                            int body_length = _left.GetFixedLength();
                            if (body_length >= 0)
                            {
                                return _lower * body_length;
                            }
                        }

                        return -1;
                    }

                case SymbolicRegexKind.Concat:
                    {
                        Debug.Assert(_left is not null && _right is not null);
                        int left_length = _left.GetFixedLength();
                        if (left_length >= 0)
                        {
                            int right_length = _right.GetFixedLength();
                            if (right_length >= 0)
                            {
                                return left_length + right_length;
                            }
                        }
                        return -1;
                    }

                case SymbolicRegexKind.Or:
                    Debug.Assert(_alts is not null);
                    return _alts.GetFixedLength();

                default:
                    return -1;
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
            {
                return this;
            }

            switch (_kind)
            {
                case SymbolicRegexKind.Singleton:
                    Debug.Assert(_set is not null);
                    return _builder._solver.IsSatisfiable(_builder._solver.And(elem, _set)) ?
                        _builder._epsilon :
                        _builder._nothing;

                case SymbolicRegexKind.Loop:
                    {
                        #region d(a, R*) = d(a,R)R*
                        Debug.Assert(_left is not null);
                        SymbolicRegexNode<S> step = _left.MkDerivative(elem, context);

                        if (step == _builder._nothing || _upper == 0)
                        {
                            return _builder._nothing;
                        }

                        if (IsStar)
                        {
                            return _builder.MkConcat(step, this);
                        }

                        if (IsPlus)
                        {
                            SymbolicRegexNode<S> star = _builder.MkLoop(_left, IsLazy);
                            return _builder.MkConcat(step, star);
                        }

                        int newupper = _upper == int.MaxValue ? int.MaxValue : _upper - 1;
                        int newlower = _lower == 0 ? 0 : _lower - 1;
                        SymbolicRegexNode<S> rest = _builder.MkLoop(_left, IsLazy, newlower, newupper);
                        return _builder.MkConcat(step, rest);
                        #endregion
                    }

                case SymbolicRegexKind.Concat:
                    {
                        #region d(a, AB) = d(a,A)B | (if A nullable then d(a,B))
                        Debug.Assert(_left is not null && _right is not null);
                        SymbolicRegexNode<S> leftd = _left.MkDerivative(elem, context);
                        SymbolicRegexNode<S> first = _builder._nothing;
                        if (_builder._antimirov && leftd._kind == SymbolicRegexKind.Or)
                        {
                            // push concatenations into the union
                            Debug.Assert(leftd._alts is not null);
                            foreach (SymbolicRegexNode<S> d in leftd._alts)
                            {
                                first = _builder.MkOr(first, _builder.MkConcat(d, _right));
                            }
                        }
                        else
                        {
                            first = _builder.MkConcat(leftd, _right);
                        }

                        if (_left.IsNullableFor(context))
                        {
                            SymbolicRegexNode<S> second = _right.MkDerivative(elem, context);
                            SymbolicRegexNode<S> deriv = _builder.MkOr2(first, second);
                            return deriv;
                        }

                        return first;
                        #endregion
                    }

                case SymbolicRegexKind.Or:
                    {
                        #region d(a,A|B) = d(a,A)|d(a,B)
                        Debug.Assert(_alts is not null && _alts._kind == SymbolicRegexKind.Or);
                        SymbolicRegexSet<S> alts_deriv = _alts.CreateDerivative(elem, context);
                        // At this point alts_deriv can be the empty conjunction denoting .*
                        return _builder.MkOr(alts_deriv);
                        #endregion
                    }

                case SymbolicRegexKind.And:
                    {
                        #region d(a,A & B) = d(a,A) & d(a,B)
                        Debug.Assert(_alts is not null && _alts._kind == SymbolicRegexKind.And);
                        SymbolicRegexSet<S> alts_deriv = _alts.CreateDerivative(elem, context);
                        // At this point alts_deriv can be the empty disjunction denoting nothing
                        return _builder.MkAnd(alts_deriv);
                        #endregion
                    }

                case SymbolicRegexKind.Not:
                    {
                        #region d(a,~(A)) = ~(d(a,A))
                        Debug.Assert(_left is not null);
                        SymbolicRegexNode<S> leftD = _left.MkDerivative(elem, context);
                        return _builder.MkNot(leftD);
                        #endregion
                    }

                default:
                    return _builder._nothing;
            }
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        private int ComputeHashCode()
        {
            switch (_kind)
            {
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WBAnchor:
                case SymbolicRegexKind.NWBAnchor:
                case SymbolicRegexKind.EndAnchorZ:
                case SymbolicRegexKind.EndAnchorZRev:
                    return HashCode.Combine(_kind, _info);

                case SymbolicRegexKind.WatchDog:
                    return HashCode.Combine(_kind, _lower);

                case SymbolicRegexKind.Loop:
                    return HashCode.Combine(_kind, _left, _lower, _upper, _info);

                case SymbolicRegexKind.Or or SymbolicRegexKind.And:
                    return HashCode.Combine(_kind, _alts, _info);

                case SymbolicRegexKind.Concat:
                    return HashCode.Combine(_left, _right, _info);

                case SymbolicRegexKind.Singleton:
                    return HashCode.Combine(_kind, _set);

                default:
                    Debug.Assert(_kind == SymbolicRegexKind.Not);
                    return HashCode.Combine(_kind, _left, _info);
            };
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is not SymbolicRegexNode<S> that)
            {
                return false;
            }

            if (this == that)
            {
                return true;
            }

            if (_kind != that._kind)
            {
                return false;
            }

            if (_kind == SymbolicRegexKind.Or)
            {
                if (_isInternalizedOrNode && that._isInternalizedOrNode)
                {
                    // Internalized nodes that are not identical are not equal
                    return false;
                }

                // Check equality of the sets of regexes
                Debug.Assert(_alts is not null && that._alts is not null);
                return _alts.Equals(that._alts);
            }

            return false;
        }

        private void ToStringForLoop(StringBuilder sb)
        {
            if (_kind == SymbolicRegexKind.Singleton)
            {
                ToString(sb);
            }
            else
            {
                sb.Append('(');
                ToString(sb);
                sb.Append(')');
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            ToString(sb);
            return sb.ToString();
        }

        internal void ToString(StringBuilder sb)
        {
            switch (_kind)
            {
                case SymbolicRegexKind.EndAnchor:
                    sb.Append("\\z");
                    return;

                case SymbolicRegexKind.StartAnchor:
                    sb.Append("\\A");
                    return;

                case SymbolicRegexKind.BOLAnchor:
                    sb.Append('^');
                    return;

                case SymbolicRegexKind.EOLAnchor:
                    sb.Append('$');
                    return;

                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                    return;

                case SymbolicRegexKind.WBAnchor:
                    sb.Append("\\b");
                    return;

                case SymbolicRegexKind.NWBAnchor:
                   sb.Append("\\B");
                    return;

                case SymbolicRegexKind.EndAnchorZ:
                    sb.Append("\\Z");
                    return;

                case SymbolicRegexKind.EndAnchorZRev:
                    sb.Append("\\a");
                    return;

                case SymbolicRegexKind.Or:
                case SymbolicRegexKind.And:
                    Debug.Assert(_alts is not null);
                    _alts.ToString(sb);
                    return;

                case SymbolicRegexKind.Concat:
                    Debug.Assert(_left is not null && _right is not null);
                    _left.ToString(sb);
                    _right.ToString(sb);
                    return;

                case SymbolicRegexKind.Singleton:
                    Debug.Assert(_set is not null);
                    sb.Append(_builder._solver.PrettyPrint(_set));
                    return;

                case SymbolicRegexKind.Loop:
                    Debug.Assert(_left is not null);
                    if (IsDotStar)
                    {
                        sb.Append(".*");
                    }
                    else if (IsMaybe)
                    {
                        _left.ToStringForLoop(sb);
                        sb.Append('?');
                    }
                    else if (IsStar)
                    {
                        _left.ToStringForLoop(sb);
                        sb.Append('*');
                        if (IsLazy)
                        {
                            sb.Append('?');
                        }
                    }
                    else if (IsPlus)
                    {
                        _left.ToStringForLoop(sb);
                        sb.Append('+');
                        if (IsLazy)
                        {
                            sb.Append('?');
                        }
                    }
                    else if (_lower == 0 && _upper == 0)
                    {
                        sb.Append("()");
                    }
                    else if (!IsBoundedLoop)
                    {
                        _left.ToStringForLoop(sb);
                        sb.Append('{');
                        sb.Append(_lower);
                        sb.Append(",}");
                        if (IsLazy)
                            sb.Append('?');
                    }
                    else if (_lower == _upper)
                    {
                        _left.ToStringForLoop(sb);
                        sb.Append('{');
                        sb.Append(_lower);
                        sb.Append('}');
                        if (IsLazy)
                            sb.Append('?');
                    }
                    else
                    {
                        _left.ToStringForLoop(sb);
                        sb.Append('{');
                        sb.Append(_lower);
                        sb.Append(',');
                        sb.Append(_upper);
                        sb.Append('}');
                        if (IsLazy)
                            sb.Append('?');
                    }
                    return;

                default:
                    Debug.Assert(_kind == SymbolicRegexKind.Not);
                    Debug.Assert(_left is not null);
                    sb.Append("(?(");
                    _left.ToString(sb);
                    sb.Append($"){EmptyCharClass}|.*)");
                    return;
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
            {
                predicates.Add(_builder._solver.True);
            }
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
                    predicates.Add(_builder._newLinePredicate);
                    return;

                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                    return;

                case SymbolicRegexKind.Singleton:
                    Debug.Assert(_set is not null);
                    predicates.Add(_set);
                    return;

                case SymbolicRegexKind.Loop:
                    Debug.Assert(_left is not null);
                    _left.CollectPredicates_helper(predicates);
                    return;

                case SymbolicRegexKind.Or:
                case SymbolicRegexKind.And:
                    Debug.Assert(_alts is not null);
                    foreach (SymbolicRegexNode<S> sr in _alts)
                    {
                        sr.CollectPredicates_helper(predicates);
                    }
                    return;

                case SymbolicRegexKind.Concat:
                    // avoid deep nested recursion over long concat nodes
                    SymbolicRegexNode<S> conc = this;
                    while (conc._kind == SymbolicRegexKind.Concat)
                    {
                        Debug.Assert(conc._left is not null && conc._right is not null);
                        conc._left.CollectPredicates_helper(predicates);
                        conc = conc._right;
                    }
                    conc.CollectPredicates_helper(predicates);
                    return;

                case SymbolicRegexKind.Not:
                    Debug.Assert(_left is not null);
                    _left.CollectPredicates_helper(predicates);
                    return;

                case SymbolicRegexKind.NWBAnchor:
                case SymbolicRegexKind.WBAnchor:
                    predicates.Add(_builder._wordLetterPredicate);
                    return;

                default:
                    Debug.Fail($"{nameof(CollectPredicates_helper)}:{_kind}");
                    break;
            }
        }

        /// <summary>
        /// Compute all the minterms from the predicates in this regex.
        /// If S implements IComparable then sort the result in increasing order.
        /// </summary>
        public S[] ComputeMinterms()
        {
            Debug.Assert(typeof(S).IsAssignableTo(typeof(IComparable)));

            HashSet<S> predicates = GetPredicates();
            Debug.Assert(predicates.Count != 0);

            S[] predicatesArray = new S[predicates.Count];
            int i = 0;
            foreach (S s in predicates)
            {
                predicatesArray[i++] = s;
            }
            Debug.Assert(i == predicatesArray.Length);

            List<S> mt = _builder._solver.GenerateMinterms(predicatesArray);
            mt.Sort();
            return mt.ToArray();
        }

        /// <summary>
        /// Create the reverse of this regex
        /// </summary>
        public SymbolicRegexNode<S> Reverse()
        {
            switch (_kind)
            {
                case SymbolicRegexKind.Loop:
                    Debug.Assert(_left is not null);
                    return _builder.MkLoop(_left.Reverse(), IsLazy, _lower, _upper);

                case SymbolicRegexKind.Concat:
                    {
                        Debug.Assert(_left is not null && _right is not null);
                        SymbolicRegexNode<S> rev = _left.Reverse();
                        SymbolicRegexNode<S> rest = _right;
                        while (rest._kind == SymbolicRegexKind.Concat)
                        {
                            Debug.Assert(rest._left is not null && rest._right is not null);
                            SymbolicRegexNode<S> rev1 = rest._left.Reverse();
                            rev = _builder.MkConcat(rev1, rev);
                            rest = rest._right;
                        }
                        SymbolicRegexNode<S> restr = rest.Reverse();
                        rev = _builder.MkConcat(restr, rev);
                        return rev;
                    }

                case SymbolicRegexKind.Or:
                    Debug.Assert(_alts is not null);
                    return _builder.MkOr(_alts.Reverse());

                case SymbolicRegexKind.And:
                    Debug.Assert(_alts is not null);
                    return _builder.MkAnd(_alts.Reverse());

                case SymbolicRegexKind.Not:
                    Debug.Assert(_left is not null);
                    return _builder.MkNot(_left.Reverse());

                case SymbolicRegexKind.WatchDog:
                    // Watchdogs are omitted in reverse
                    return _builder._epsilon;

                case SymbolicRegexKind.StartAnchor:
                    // The reverse of StartAnchor is EndAnchor
                    return _builder._endAnchor;

                case SymbolicRegexKind.EndAnchor:
                    return _builder._startAnchor;

                case SymbolicRegexKind.BOLAnchor:
                    // The reverse of BOLanchor is EOLanchor
                    return _builder._eolAnchor;

                case SymbolicRegexKind.EOLAnchor:
                    return _builder._bolAnchor;

                case SymbolicRegexKind.EndAnchorZ:
                    // The reversal of the \Z anchor
                    return _builder._endAnchorZRev;

                case SymbolicRegexKind.EndAnchorZRev:
                    // This can potentially only happen if a reversed regex is reversed again.
                    // Thus, this case is unreachable here, but included for completeness.
                    return _builder._endAnchorZ;

                default:
                    // Remaining cases map to themselves:
                    // SymbolicRegexKind.Epsilon
                    // SymbolicRegexKind.Singleton
                    // SymbolicRegexKind.WBAnchor
                    // SymbolicRegexKind.NWBAnchor
                    return this;
            }
        }

        internal bool StartsWithLoop(int upperBoundLowestValue = 1)
        {
            switch (_kind)
            {
                case SymbolicRegexKind.Loop:
                    return (_upper < int.MaxValue) && (_upper > upperBoundLowestValue);

                case SymbolicRegexKind.Concat:
                    Debug.Assert(_left is not null && _right is not null);
                    return _left.StartsWithLoop(upperBoundLowestValue) || (_left.IsNullable && _right.StartsWithLoop(upperBoundLowestValue));

                case SymbolicRegexKind.Or:
                    Debug.Assert(_alts is not null);
                    return _alts.StartsWithLoop(upperBoundLowestValue);

                default:
                    return false;
            };
        }

        /// <summary>
        /// Gets the string prefix that the regex must match or the empty string if such a prefix does not exist.
        /// Sets ignoreCase = true when the prefix works under case-insensitivity.
        /// For example if the input prefix is "---" it sets ignoreCase=false,
        /// if the prefix is "---[aA][bB]" it returns "---AB" and sets ignoreCase=true
        /// </summary>
        internal string GetFixedPrefix(CharSetSolver css, string culture, out bool ignoreCase)
        {
            StringBuilder singletonsPrefix = new();
            StringBuilder ignorecasePrefix = new();

            BDD[] bdds = Array.ConvertAll(GetPrefix(), p => _builder._solver.ConvertToCharSet(css, p));

            for (int i = 0; i < bdds.Length && css.IsSingleton(bdds[i]); i++)
            {
                singletonsPrefix.Append((char)bdds[i].GetMin());
            }

            for (int i = 0; i < bdds.Length && css.ApplyIgnoreCase(css.CharConstraint((char)bdds[i].GetMin()), culture).Equals(bdds[i]); i++)
            {
                ignorecasePrefix.Append((char)bdds[i].GetMin());
            }

            // Return the longer of the two prefixes, prefer the case-sensitive setting
            if (singletonsPrefix.Length >= ignorecasePrefix.Length)
            {
                ignoreCase = false;
                return singletonsPrefix.ToString();
            }
            else
            {
                ignoreCase = true;
                return ignorecasePrefix.ToString();
            }
        }

        internal S[] GetPrefix() => GetPrefixSequence(ImmutableList<S>.Empty, MaxPrefixLength).ToArray();

        // TODO: nonrecusrive to avoid DEEP RECURSION, in particular with Concat, and smarter in not doing unnecessary work
        // stop computing a candidate list when encountering a predicate that is neither a singleton nor closed under ignore-case
        private ImmutableList<S> GetPrefixSequence(ImmutableList<S> pref, int lengthBound)
        {
            if (lengthBound == 0)
            {
                return pref;
            }

            switch (_kind)
            {
                case SymbolicRegexKind.Singleton:
                    Debug.Assert(_set is not null);
                    return pref.Add(_set);

                case SymbolicRegexKind.Concat:
                    Debug.Assert(_left is not null && _right is not null);
                    if (_left._kind == SymbolicRegexKind.Singleton)
                    {
                        Debug.Assert(_left._set is not null);
                        return _right.GetPrefixSequence(pref.Add(_left._set), lengthBound - 1);
                    }
                    return pref;

                case SymbolicRegexKind.Or:
                case SymbolicRegexKind.And:
                    {
                        Debug.Assert(_alts is not null);

                        SymbolicRegexSet<S>.Enumerator enumerator = _alts.GetEnumerator();
                        bool movedNext = enumerator.MoveNext();
                        Debug.Assert(movedNext, "Expected a minimum of one element");
                        ImmutableList<S> altsPrefix = enumerator.Current.GetPrefixSequence(ImmutableList<S>.Empty, lengthBound);

                        while (!altsPrefix.IsEmpty && enumerator.MoveNext())
                        {
                            ImmutableList<S> p = enumerator.Current.GetPrefixSequence(ImmutableList<S>.Empty, lengthBound);
                            int prefix_length = altsPrefix.TakeWhile((x, i) => i < p.Count && x.Equals(p[i])).Count();
                            altsPrefix = altsPrefix.RemoveRange(prefix_length, altsPrefix.Count - prefix_length);
                        }

                        return pref.AddRange(altsPrefix);
                    }

                default:
                    return pref;
            }
        }

        /// <summary>Get the predicate that covers all elements that make some progress.</summary>
        internal S GetStartSet()
        {
            if (!_startSetIsComputed)
            {
                _startSet = ComputeStartSet();
                _startSetIsComputed = true;
            }

            Debug.Assert(_startSet is not null);
            return _startSet;

            S ComputeStartSet()
            {
                switch (_kind)
                {
                    // Anchors and () do not contribute to the startset
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
                        Debug.Assert(_set is not null);
                        return _set;

                    case SymbolicRegexKind.Loop:
                        Debug.Assert(_left is not null);
                        return _left.GetStartSet();

                    case SymbolicRegexKind.Concat:
                        {
                            Debug.Assert(_left is not null && _right is not null);
                            S startSet = _left.GetStartSet();
                            // To avoid deep recursion of trying to get the startset of right
                            // just return True when left can be nullable
                            // It is always correct to return True as the startset
                            if (_left.CanBeNullable)
                            {
                                startSet = _builder._solver.True;
                            }
                            return startSet;
                        }

                    case SymbolicRegexKind.Or:
                        {
                            Debug.Assert(_alts is not null);
                            S startSet = _builder._solver.False;
                            foreach (SymbolicRegexNode<S> alt in _alts)
                            {
                                startSet = _builder._solver.Or(startSet, alt.GetStartSet());
                            }
                            return startSet;
                        }

                    case SymbolicRegexKind.And:
                        {
                            Debug.Assert(_alts is not null);
                            S startSet = _builder._solver.True;
                            foreach (SymbolicRegexNode<S> alt in _alts)
                            {
                                startSet = _builder._solver.And(startSet, alt.GetStartSet());
                            }
                            return startSet;
                        }

                    default:
                        Debug.Assert(_kind == SymbolicRegexKind.Not);
                        return _builder._solver.True;
                }
            }
        }

        /// <summary>
        /// Returns true if this is a loop with an upper bound
        /// </summary>
        public bool IsBoundedLoop => _kind == SymbolicRegexKind.Loop && _upper < int.MaxValue;

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
                    Debug.Assert(_left is not null);
                    SymbolicRegexNode<S> body = _left.PruneAnchors(prevKind, contWithWL, contWithNWL);
                    return body == _left ?
                        this :
                        MkLoop(_builder, body, _lower, _upper, IsLazy);

                case SymbolicRegexKind.Concat:
                    Debug.Assert(_left is not null && _right is not null);
                    SymbolicRegexNode<S> left1 = _left.PruneAnchors(prevKind, contWithWL, contWithNWL);
                    SymbolicRegexNode<S> right1 = _left.IsNullable ? _right.PruneAnchors(prevKind, contWithWL, contWithNWL) : _right;

                    Debug.Assert(left1 is not null && right1 is not null);
                    return left1 == _left && right1 == _right ?
                        this :
                        MkConcat(_builder, left1, right1);

                case SymbolicRegexKind.Or:
                    {
                        Debug.Assert(_alts != null);
                        var elements = new SymbolicRegexNode<S>[_alts.Count];
                        int i = 0;
                        foreach (SymbolicRegexNode<S> alt in _alts)
                        {
                            elements[i++] = alt.PruneAnchors(prevKind, contWithWL, contWithNWL);
                        }
                        Debug.Assert(i == elements.Length);
                        return MkOr(_builder, elements);
                    }

                default:
                    return this;
            }
        }
    }
}
