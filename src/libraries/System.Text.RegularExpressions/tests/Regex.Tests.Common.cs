// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Text.RegularExpressions.Tests
{
    public static class RegexHelpers
    {
        public const string DefaultMatchTimeout_ConfigKeyName = "REGEX_DEFAULT_MATCH_TIMEOUT";

        /// <summary>RegexOptions.NonBacktracking.</summary>
        /// <remarks>Defined here to be able to reference the value by name even on .NET Framework test builds.</remarks>
        public const RegexOptions RegexOptionNonBacktracking = (RegexOptions)0x400;

        public const RegexOptions RegexOptionDebug = (RegexOptions)0x80;

        static RegexHelpers()
        {
            if (PlatformDetection.IsNetCore)
            {
                Assert.Equal(RegexOptionNonBacktracking, Enum.Parse(typeof(RegexOptions), "NonBacktracking"));
            }
        }

        public static bool IsDefaultCount(string input, RegexOptions options, int count)
        {
            if ((options & RegexOptions.RightToLeft) != 0)
            {
                return count == input.Length || count == -1;
            }
            return count == input.Length;
        }

        public static bool IsDefaultStart(string input, RegexOptions options, int start)
        {
            if ((options & RegexOptions.RightToLeft) != 0)
            {
                return start == input.Length;
            }
            return start == 0;
        }
    }

    public class CaptureData
    {
        private CaptureData(string value, int index, int length, bool createCaptures)
        {
            Value = value;
            Index = index;
            Length = length;

            // Prevent a StackOverflow recursion in the constructor
            if (createCaptures)
            {
                Captures = new CaptureData[] { new CaptureData(value, index, length, false) };
            }
        }

        public CaptureData(string value, int index, int length) : this(value, index, length, true)
        {
        }

        public CaptureData(string value, int index, int length, CaptureData[] captures) : this(value, index, length, false)
        {
            Captures = captures;
        }

        public string Value { get; }
        public int Index { get; }
        public int Length { get; }
        public CaptureData[] Captures { get; }
    }
}
