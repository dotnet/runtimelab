// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.RegularExpressions.SRM
{
    internal sealed class Match
    {
        internal static readonly Match NoMatch = new(-1, -1);

        public int Index { get; private set; }
        public int Length { get; private set; }
        public bool Success => Index >= 0;

        public Match(int index, int length)
        {
            Index = index;
            Length = length;
        }

        public static bool operator ==(Match left, Match right)
            => left.Index == right.Index && left.Length == right.Length;

        public static bool operator !=(Match left, Match right) => !(left == right);

        public override bool Equals(object obj) => obj is Match other && this == other;

        public override int GetHashCode() => (Index, Length).GetHashCode();
    }
}
