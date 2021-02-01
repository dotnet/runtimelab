// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Misc information of structural properties of a Symbolic Regex Node
    /// </summary>
    internal struct SymbolicRegexInfo
    {
        private readonly uint _info;

        private SymbolicRegexInfo(uint i) { _info = i; }

        /// <summary>
        /// Optimized lookup array for all possible combinations.
        /// Most common cases will be 0 (no anchors and not nullable) and 1 (no anchors and nullable)
        /// </summary>
        private static SymbolicRegexInfo[] s_infos =
            new SymbolicRegexInfo[16] { new (0), new (1), new (2), new (3), new (4), new (5), new (6), new (7),
                new (8), new (9), new (10), new (11), new (12), new (13), new (14), new (15) };

        private const uint IsNullableMask = 1;
        private const uint StartsWithLineAnchorMask = 2;
        private const uint StartsWithWordBoundaryAnchorMask = 4;
        private const uint StartsWithNonWordBoundaryAnchorMask = 8;
        private const uint SomeAnchorMask = 14;
        private const uint BoundaryMask = 12;

        internal static SymbolicRegexInfo Mk(bool isNullable = false, bool startsWithLineAnchor = false, bool startsWithWBAnchor = false, bool startsWithNWBAnchor = false)
        {
            uint i = (isNullable ? IsNullableMask : 0) |
                   (startsWithLineAnchor ? StartsWithLineAnchorMask : 0) |
                   (startsWithWBAnchor ? StartsWithWordBoundaryAnchorMask : 0) |
                   (startsWithNWBAnchor ? StartsWithNonWordBoundaryAnchorMask : 0);
            return s_infos[i];
        }

        public bool IsNullable
        {
            get { return (_info & IsNullableMask) != 0; }
        }

        public bool StartsWithSomeAnchor
        {
            get { return (_info & SomeAnchorMask) != 0; }
        }

        public bool StartsWithLineAnchor
        {
            get { return (_info & StartsWithLineAnchorMask) != 0; }
        }

        public bool StartsWithWordBoundaryAnchor
        {
            get { return (_info & StartsWithWordBoundaryAnchorMask) != 0; }
        }

        public bool StartsWithNonWordBoundaryAnchor
        {
            get { return (_info & StartsWithNonWordBoundaryAnchorMask) != 0; }
        }

        public bool StartsWithBoundaryAnchor
        {
            get { return (_info & BoundaryMask) != 0; }
        }

        public static SymbolicRegexInfo Or(IEnumerable<SymbolicRegexInfo> infos)
        {
            uint i = 0;
            foreach (SymbolicRegexInfo info in infos)
                i |= info._info;
            return s_infos[i];
        }

        public static SymbolicRegexInfo Or(params SymbolicRegexInfo[] infos)
        {
            uint i = 0;
            for (int j = 0; j < infos.Length; j++)
                i |= infos[j]._info;
            return s_infos[i];
        }

        public static SymbolicRegexInfo And(IEnumerable<SymbolicRegexInfo> infos)
        {
            uint isNullable = IsNullableMask;
            uint i = 0;
            foreach (SymbolicRegexInfo info in infos)
            {
                //nullability is conjunctive while other properties are disjunctive
                isNullable &= info._info;
                i |= info._info;
            }
            i = (i & ~IsNullableMask) | isNullable;
            return s_infos[i];
        }

        public static SymbolicRegexInfo And(params SymbolicRegexInfo[] infos)
        {
            uint isNullable = IsNullableMask;
            uint i = 0;
            for (int j = 0; j < infos.Length; j++)
            {
                //nullability is conjunctive while other properties are disjunctive
                isNullable &= infos[j]._info;
                i |= infos[j]._info;
            }
            i = (i & ~IsNullableMask) | isNullable;
            return s_infos[i];
        }

        public static SymbolicRegexInfo Concat(SymbolicRegexInfo left_info, SymbolicRegexInfo right_info)
        {
            uint i = left_info._info;
            if (left_info.IsNullable)
            {
                // if the left element is nullable then all anchors in the right are visible
                i = i | right_info._info;
            }
            // but nullability is preserved only if both left and right are nullable
            if (!right_info.IsNullable)
                i = i & ~IsNullableMask;

            return s_infos[i];
        }

        public static SymbolicRegexInfo Loop(SymbolicRegexInfo body_info, int lowerBound)
        {
            // anchors are visible from outside the loop
            uint i = body_info._info;
            // if the lower boud is 0 then the loop is also nullable else it is nullable if the body is nullable
            if (lowerBound == 0)
                i = i | IsNullableMask;
            return s_infos[i];
        }

        public static SymbolicRegexInfo ITE(SymbolicRegexInfo cond_info, SymbolicRegexInfo then_info, SymbolicRegexInfo else_info)
        {
            uint i = (cond_info._info | then_info._info | else_info._info) & ~IsNullableMask;

            // nullability is determined as follows
            bool isNull = (cond_info.IsNullable ? then_info.IsNullable : else_info.IsNullable);
            if (isNull)
                i = i | IsNullableMask;

            return s_infos[i];
        }

        public override bool Equals(object? obj) => (obj is SymbolicRegexInfo && ((SymbolicRegexInfo)obj)._info == _info);

        public override int GetHashCode() => _info.GetHashCode();

        public override string ToString() => _info.ToString("X");

        /// <summary>
        /// Parses from a string created with ToString().
        /// </summary>
        public static SymbolicRegexInfo Parse(string info)
        {
            uint i;
            if (uint.TryParse(info, Globalization.NumberStyles.HexNumber, null, out i) & i < 16)
                return s_infos[i];
            else
                throw new ArgumentException($"{nameof(Parse)} error of {nameof(SymbolicRegexInfo)}", nameof(info));
        }
    }
}
