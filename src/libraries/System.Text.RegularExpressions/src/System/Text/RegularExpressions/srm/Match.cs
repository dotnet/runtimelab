namespace Microsoft.SRM
{
    internal struct Match
    {
        public int Index { get; private set; }
        public int Length { get; private set; }

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
