// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Misc information of structural properties of a Symbolic Regex Node that is computed bottom up.
    /// </summary>
    internal struct SymbolicRegexInfo
    {
        private readonly uint _info;

        private SymbolicRegexInfo(uint i) => _info = i;

        /// <summary>
        /// Optimized lookup array for most common combinations.
        /// Most common cases will be 0 (no anchors and not nullable) and 1 (no anchors and nullable)
        /// </summary>
        private static readonly SymbolicRegexInfo[] s_infos = CreateSymbolicRegexInfos();

        private static SymbolicRegexInfo[] CreateSymbolicRegexInfos()
        {
            var infos = new SymbolicRegexInfo[128];
            for (uint i = 0; i < 128; i++)
            {
                infos[i] = new SymbolicRegexInfo(i);
            }
            return infos;
        }

        private static SymbolicRegexInfo Mk(uint i)
        {
            SymbolicRegexInfo[] infos = s_infos;
            if (i < infos.Length)
                return infos[i];

            return new SymbolicRegexInfo(i);
        }

        private const uint IsAlwaysNullableMask = 1;
        private const uint StartsWithLineAnchorMask = 2;
        private const uint IsLazyMask = 4;
        private const uint CanBeNullableMask = 8;
        private const uint ContainsSomeAnchorMask = 16;
        private const uint ContainsLineAnchorMask = 32;
        private const uint ContainsSomeCharacterMask = 64;
        private const uint StartsWithBoundaryAnchorMask = 128;

        internal static SymbolicRegexInfo Mk(bool isAlwaysNullable = false, bool canBeNullable = false, bool startsWithLineAnchor = false,
            bool startsWithBoundaryAnchor = false, bool containsSomeAnchor = false,
            bool containsLineAnchor = false, bool containsSomeCharacter = false, bool isLazy = true)
        {
            uint i = (isAlwaysNullable ? IsAlwaysNullableMask : 0) |
                   (startsWithLineAnchor ? StartsWithLineAnchorMask : 0) |
                   (startsWithBoundaryAnchor ? StartsWithBoundaryAnchorMask : 0) |
                   (canBeNullable || isAlwaysNullable ? CanBeNullableMask : 0) |
                   (startsWithLineAnchor || startsWithBoundaryAnchor || containsSomeAnchor || containsLineAnchor ? ContainsSomeAnchorMask : 0) |
                   (startsWithLineAnchor || containsLineAnchor ? ContainsLineAnchorMask : 0) |
                   (containsSomeCharacter ? ContainsSomeCharacterMask : 0) |
                   (isLazy ? IsLazyMask : 0);
            return Mk(i);
        }

        public bool IsNullable => (_info & IsAlwaysNullableMask) != 0;

        public bool CanBeNullable => (_info & CanBeNullableMask) != 0;

        public bool StartsWithSomeAnchor => (_info & (StartsWithLineAnchorMask | StartsWithBoundaryAnchorMask)) != 0;

        public bool StartsWithLineAnchor => (_info & StartsWithLineAnchorMask) != 0;

        public bool StartsWithBoundaryAnchor => (_info & StartsWithBoundaryAnchorMask) != 0;

        public bool ContainsSomeAnchor => (_info & ContainsSomeAnchorMask) != 0;

        public bool ContainsLineAnchor => (_info & ContainsLineAnchorMask) != 0;

        public bool ContainsSomeCharacter => (_info & ContainsSomeCharacterMask) != 0;

        public bool IsLazy => (_info & IsLazyMask) != 0;

        public static SymbolicRegexInfo Or(IEnumerable<SymbolicRegexInfo> infos)
        {
            uint isLazy = IsLazyMask;
            uint i = 0;
            foreach (SymbolicRegexInfo info in infos)
            {
                // disjunction is lazy if ALL of its members are lazy
                isLazy &= info._info;
                i |= info._info;
            }
            i = (i & ~IsLazyMask) | isLazy;
            return Mk(i);
        }

        public static SymbolicRegexInfo Or(params SymbolicRegexInfo[] infos)
        {
            uint isLazy = IsLazyMask;
            uint i = 0;
            for (int j = 0; j < infos.Length; j++)
            {
                // disjunction is lazy if ALL of its members are lazy
                isLazy &= infos[j]._info;
                i |= infos[j]._info;
            }
            i = (i & ~IsLazyMask) | isLazy;
            return Mk(i);
        }

        public static SymbolicRegexInfo And(IEnumerable<SymbolicRegexInfo> infos)
        {
            uint isLazy = IsLazyMask;
            uint isNullable = IsAlwaysNullableMask | CanBeNullableMask;
            uint i = 0;
            foreach (SymbolicRegexInfo info in infos)
            {
                //nullability and lazyness are conjunctive while other properties are disjunctive
                isLazy &= info._info;
                isNullable &= info._info;
                i |= info._info;
            }
            i = (i & ~IsLazyMask) | isLazy;
            i = (i & ~(IsAlwaysNullableMask | CanBeNullableMask)) | isNullable;
            return Mk(i);
        }

        public static SymbolicRegexInfo And(params SymbolicRegexInfo[] infos)
        {
            uint isLazy = IsLazyMask;
            uint isNullable = IsAlwaysNullableMask | CanBeNullableMask;
            uint i = 0;
            for (int j = 0; j < infos.Length; j++)
            {
                //nullability and lazyness are conjunctive while other properties are disjunctive
                isLazy &= infos[j]._info;
                isNullable &= infos[j]._info;
                i |= infos[j]._info;
            }
            i = (i & ~IsLazyMask) | isLazy;
            i = (i & ~(IsAlwaysNullableMask | CanBeNullableMask)) | isNullable;
            return Mk(i);
        }

        public static SymbolicRegexInfo Concat(SymbolicRegexInfo left_info, SymbolicRegexInfo right_info)
        {
            bool isNullable = left_info.IsNullable && right_info.IsNullable;
            bool canBeNullable = left_info.CanBeNullable && right_info.CanBeNullable;
            bool startsWithLineAnchor = left_info.StartsWithLineAnchor || (left_info.CanBeNullable && right_info.StartsWithLineAnchor);
            bool startsWithBoundaryAnchor = left_info.StartsWithBoundaryAnchor || (left_info.CanBeNullable && right_info.StartsWithBoundaryAnchor);
            bool containsSomeAnchor = left_info.ContainsSomeAnchor || right_info.ContainsSomeAnchor;
            bool containsLineAnchor = left_info.ContainsLineAnchor || right_info.ContainsLineAnchor;
            bool containsSomeCharacter = left_info.ContainsSomeCharacter || right_info.ContainsSomeCharacter;
            //both have to be lazy for the concat to be lazy
            bool isLazy = left_info.IsLazy && right_info.IsLazy;
            return Mk(isNullable, canBeNullable, startsWithLineAnchor, startsWithBoundaryAnchor, containsSomeAnchor, containsLineAnchor, containsSomeCharacter, isLazy);
        }

        public static SymbolicRegexInfo Loop(SymbolicRegexInfo body_info, int lowerBound, bool isLazy)
        {
            // inherit anchor visibility from the loop body
            uint i = body_info._info;
            // the loop is nullable if either the body is nullable or if the lower boud is 0
            i |= lowerBound == 0 ? (IsAlwaysNullableMask | CanBeNullableMask) : 0;
            // the loop is lazy iff it is marked lazy
            if (isLazy)
            {
                i |= IsLazyMask;
            }
            else
            {
                i &= ~IsLazyMask;
            }
            return Mk(i);
        }

        public static SymbolicRegexInfo ITE(SymbolicRegexInfo cond_info, SymbolicRegexInfo then_info, SymbolicRegexInfo else_info)
        {
            uint i = (cond_info._info | then_info._info | else_info._info) & ~IsAlwaysNullableMask;

            // nullability is determined as follows
            // it is unclear exactly what the correct behavior should be of anchors in ITE and for lazy loops
            bool isAlwaysNullable = cond_info.IsNullable ? then_info.IsNullable : else_info.IsNullable;
            if (isAlwaysNullable)
            {
                i |= IsAlwaysNullableMask | CanBeNullableMask;
            }

            return Mk(i);
        }

        public override bool Equals(object? obj) => obj is SymbolicRegexInfo i && i._info == _info;

        public override int GetHashCode() => _info.GetHashCode();

        public override string ToString() => _info.ToString("X");

        public string Serialize() => _info.ToString("X");

        /// <summary>
        /// Parses from a string created with Serialize().
        /// </summary>
        public static SymbolicRegexInfo Parse(string info)
        {
            if (uint.TryParse(info, Globalization.NumberStyles.HexNumber, null, out uint i) && i < 64)
                return s_infos[i];

            throw new ArgumentException($"{nameof(Parse)} error of {nameof(SymbolicRegexInfo)}", nameof(info));
        }
    }
}
