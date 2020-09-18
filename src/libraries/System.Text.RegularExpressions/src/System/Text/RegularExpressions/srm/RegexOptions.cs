using System;
using System.Diagnostics;

namespace Microsoft.SRM
{
    [Serializable]
    internal struct RegexOptions
    {
        // .NET compatible options
        public static RegexOptions None = new RegexOptions(0);
        public static RegexOptions IgnoreCase = new RegexOptions(1);
        public static RegexOptions Multiline = new RegexOptions(2);
        public static RegexOptions Singleline = new RegexOptions(4);
        public static RegexOptions IgnorePatternWhitespace = new RegexOptions(8);
        public static RegexOptions CultureInvariant = new RegexOptions(16);
        public static RegexOptions ECMAScript = new RegexOptions(32);

        // SRM specific options
        public static RegexOptions Vectorize = new RegexOptions(1024);

        private int value;

        private RegexOptions(int value)
        {
            this.value = value;
        }

        public static RegexOptions operator|(RegexOptions left, RegexOptions right)
        {
            return new RegexOptions(left.value | right.value);
        }

        public static RegexOptions operator^(RegexOptions left, RegexOptions right)
        {
            return new RegexOptions(left.value ^ right.value);
        }

        public static RegexOptions operator&(RegexOptions left, RegexOptions right)
        {
            return new RegexOptions(left.value & right.value);
        }

        public static implicit operator int(RegexOptions ourOptions)
        {
            return ourOptions.value;
        }

        public static implicit operator System.Text.RegularExpressions.RegexOptions(RegexOptions ourOptions)
        {
            var theirOptions = System.Text.RegularExpressions.RegexOptions.None;
            var handledOptions = None;
            Action<RegexOptions, System.Text.RegularExpressions.RegexOptions> handleEquivalentOption = (o, t) =>
            {
                if ((ourOptions & o) != 0)
                {
                    theirOptions |= t;
                    handledOptions |= o;
                }
            };
            Action<RegexOptions> ignoreOption = t =>
            {
                if ((ourOptions & t) != 0)
                {
                    handledOptions |= t;
                }
            };
            handleEquivalentOption(IgnoreCase, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            handleEquivalentOption(Multiline, System.Text.RegularExpressions.RegexOptions.Multiline);
            handleEquivalentOption(Singleline, System.Text.RegularExpressions.RegexOptions.Singleline);
            handleEquivalentOption(IgnorePatternWhitespace, System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);
            handleEquivalentOption(CultureInvariant, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            handleEquivalentOption(ECMAScript, System.Text.RegularExpressions.RegexOptions.ECMAScript);
            ignoreOption(Vectorize);
            Debug.Assert(handledOptions == ourOptions);
            return theirOptions;
        }

        public static implicit operator RegexOptions(System.Text.RegularExpressions.RegexOptions theirOptions)
        {
            var ourOptions = None;
            var handledOptions = System.Text.RegularExpressions.RegexOptions.None;
            Action<RegexOptions, System.Text.RegularExpressions.RegexOptions> handleEquivalentOption = (o, t) =>
            {
                if ((theirOptions & t) != 0)
                {
                    ourOptions |= o;
                    handledOptions |= t;
                }
            };
            Action<System.Text.RegularExpressions.RegexOptions> ignoreOption = t =>
            {
                if ((theirOptions & t) != 0)
                {
                    handledOptions |= t;
                }
            };
            handleEquivalentOption(IgnoreCase, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            handleEquivalentOption(Multiline, System.Text.RegularExpressions.RegexOptions.Multiline);
            handleEquivalentOption(Singleline, System.Text.RegularExpressions.RegexOptions.Singleline);
            handleEquivalentOption(IgnorePatternWhitespace, System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);
            handleEquivalentOption(CultureInvariant, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            handleEquivalentOption(ECMAScript, System.Text.RegularExpressions.RegexOptions.ECMAScript);
            ignoreOption(System.Text.RegularExpressions.RegexOptions.RightToLeft);
            ignoreOption(System.Text.RegularExpressions.RegexOptions.Compiled);
            ignoreOption(System.Text.RegularExpressions.RegexOptions.ExplicitCapture);
            Debug.Assert(handledOptions == theirOptions);
            return ourOptions;
        }
    }
}
