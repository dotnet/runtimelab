// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Tests;
using Xunit;

namespace System.Text.RegularExpressions.Tests
{
    public class RegexCultureTests
    {
        public static IEnumerable<object[]> CharactersComparedOneByOne_AnchoredPattern_TestData()
        {
            foreach (RegexOptions options in RegexHelpers.RegexOptionsExtended())
            {
                yield return new object[] { "^aa$", "aA", "da-DK", options, false };
                yield return new object[] { "^aA$", "aA", "da-DK", options, true };
                yield return new object[] { "^aa$", "aA", "da-DK", options | RegexOptions.IgnoreCase, true };
                yield return new object[] { "^aA$", "aA", "da-DK", options | RegexOptions.IgnoreCase, true };
            }
        }

        [Theory]
        [MemberData(nameof(CharactersComparedOneByOne_AnchoredPattern_TestData))]
        public void CharactersComparedOneByOne_AnchoredPattern(string pattern, string input, string culture, RegexOptions options, bool expected)
        {
            // Regex compares characters one by one.  If that changes, it could impact the behavior of
            // a case like this, where these characters are not the same, but the strings compare
            // as equal with the invariant culture (and some other cultures as well).
            using (new ThreadCultureChange(culture))
            {
                foreach (RegexOptions compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
                {
                    Assert.Equal(expected, new Regex(pattern, options | compiled).IsMatch(input));
                }
            }
        }


        public static IEnumerable<object[]> CharactersComparedOneByOne_Invariant_TestData()
        {
            foreach (RegexOptions options in RegexHelpers.RegexOptionsExtended())
            {
                yield return new object[] { options };
                yield return new object[] { options | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant };
            }
        }

        [Theory]
        [MemberData(nameof(CharactersComparedOneByOne_Invariant_TestData))]
        public void CharactersComparedOneByOne_Invariant(RegexOptions options)
        {
            // Regex compares characters one by one.  If that changes, it could impact the behavior of
            // a case like this, where these characters are not the same, but the strings compare
            // as equal with the invariant culture (and some other cultures as well).
            const string S1 = "\u00D6\u200D";
            const string S2 = "\u004F\u0308";

            // Validate the chosen strings to make sure they compare the way we want to test via Regex
            Assert.False(S1[0] == S2[0]);
            Assert.False(S1[1] == S2[1]);
            Assert.StartsWith(S1, S2, StringComparison.InvariantCulture);
            Assert.True(S1.Equals(S2, StringComparison.InvariantCulture));

            // Test varying lengths of strings to validate codegen changes that kick in at longer lengths
            foreach (int multiple in new[] { 1, 10, 100 })
            {
                string pattern = string.Concat(Enumerable.Repeat(S1, multiple));
                string input = string.Concat(Enumerable.Repeat(S2, multiple));
                Regex r;

                // Validate when the string is at the beginning of the pattern, as it impacts Boyer-Moore prefix matching.
                r = new Regex(pattern, options);
                Assert.False(r.IsMatch(input));
                Assert.True(r.IsMatch(pattern));

                // Validate when it's not at the beginning of the pattern, as it impacts "multi" matching.
                r = new Regex("[abc]" + pattern, options);
                Assert.False(r.IsMatch("a" + input));
                Assert.True(r.IsMatch("a" + pattern));
            }
        }

        public static IEnumerable<object[]> TurkishI_Is_Differently_LowerUpperCased_In_Turkish_Culture_TestData()
        {
            // this test fails for NonBacktracking, see next test
            yield return new object[] { 2, RegexOptions.None };
            yield return new object[] { 256, RegexOptions.None };
        }

        /// <summary>
        /// See https://en.wikipedia.org/wiki/Dotted_and_dotless_I
        /// </summary>
        [Theory]
        [MemberData(nameof(TurkishI_Is_Differently_LowerUpperCased_In_Turkish_Culture_TestData))]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/56407", TestPlatforms.Android)]
        public void TurkishI_Is_Differently_LowerUpperCased_In_Turkish_Culture(int length, RegexOptions options)
        {
            var turkish = new CultureInfo("tr-TR");
            string input = string.Concat(Enumerable.Repeat("I\u0131\u0130i", length / 2));

            Regex[] cultInvariantRegex = Create(input, CultureInfo.InvariantCulture, options | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            Regex[] turkishRegex = Create(input, turkish, options | RegexOptions.IgnoreCase);

            // same input and regex does match so far so good
            Assert.All(cultInvariantRegex, rex => Assert.True(rex.IsMatch(input)));

            // when the Regex was created with a turkish locale the lower cased turkish version will
            // no longer match the input string which contains upper and lower case iiiis hence even the input string
            // will no longer match
            Assert.All(turkishRegex, rex => Assert.False(rex.IsMatch(input)));

            // Now comes the tricky part depending on the use locale in ToUpper the results differ
            // Hence the regular expression will not match if different locales were used
            Assert.All(cultInvariantRegex, rex => Assert.True(rex.IsMatch(input.ToLowerInvariant())));
            Assert.All(cultInvariantRegex, rex => Assert.False(rex.IsMatch(input.ToLower(turkish))));

            Assert.All(turkishRegex, rex => Assert.False(rex.IsMatch(input.ToLowerInvariant())));
            Assert.All(turkishRegex, rex => Assert.True(rex.IsMatch(input.ToLower(turkish))));
        }

        /// <summary>
        /// Create regular expression once compiled and once interpreted to check if both behave the same
        /// </summary>
        /// <param name="input">Input regex string</param>
        /// <param name="info">thread culture to use when creating the regex</param>
        /// <param name="additional">Additional regex options</param>
        /// <returns></returns>
        Regex[] Create(string input, CultureInfo info, RegexOptions additional)
        {
            using (new ThreadCultureChange(info))
            {
                // When RegexOptions.IgnoreCase is supplied the current thread culture is used to lowercase the input string.
                // Except if RegexOptions.CultureInvariant is additionally added locale dependent effects on the generated code or state machine may happen.
                return new Regex[]
                {
                    new Regex(input, additional),
                    new Regex(input, RegexOptions.Compiled | additional)
                };
            }
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework, "Doesn't support NonBacktracking")]
        [Fact]
        public void TurkishI_Is_Differently_LowerUpperCased_In_Turkish_Culture_NonBacktracking()
        {
            var turkish = new CultureInfo("tr-TR");
            string input = "I\u0131\u0130i";

            // Use the input as the regex also
            // Ignore the Compiled option here because it is a noop in combination with NonBacktracking 
            Regex cultInvariantRegex = Create(input, CultureInfo.InvariantCulture, RegexHelpers.RegexOptionNonBacktracking | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)[0];
            Regex turkishRegex = Create(input, turkish, RegexHelpers.RegexOptionNonBacktracking | RegexOptions.IgnoreCase)[0];

            Assert.True(cultInvariantRegex.IsMatch(input));
            Assert.True(turkishRegex.IsMatch(input));    // <---------- This result differs from the result in the previous test!!!

            // As above and no surprises here
            // The regexes recognize different lowercase variants of different versions of i differently
            Assert.True(cultInvariantRegex.IsMatch(input.ToLowerInvariant()));
            Assert.False(cultInvariantRegex.IsMatch(input.ToLower(turkish)));

            Assert.False(turkishRegex.IsMatch(input.ToLowerInvariant()));
            Assert.True(turkishRegex.IsMatch(input.ToLower(turkish)));

            // The same holds symmetrically for ToUpper
            Assert.True(cultInvariantRegex.IsMatch(input.ToUpperInvariant()));
            Assert.False(cultInvariantRegex.IsMatch(input.ToUpper(turkish)));

            Assert.False(turkishRegex.IsMatch(input.ToUpperInvariant()));
            Assert.True(turkishRegex.IsMatch(input.ToUpper(turkish)));
        }

        [ActiveIssue("Incorrect handling of IgnoreCase over intervals in Turkish Culture")]
        [Fact]
        public void TurkishCulture_Handling_Of_IgnoreCase()
        {
            var turkish = new CultureInfo("tr-TR");
            string input = "I\u0131\u0130i";
            string pattern = "[H-J][\u0131-\u0140][\u0120-\u0130][h-j]";

            Regex regex = Create(pattern, turkish, RegexOptions.IgnoreCase)[0];

            // The pattern must trivially match the input because all of the letters fall in the given intervals
            // Ignoring case can only add more letters here -- not REMOVE letters
            Assert.True(regex.IsMatch(input));
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework, "Doesn't support NonBacktracking")]
        [Fact]
        public void TurkishCulture_Handling_Of_IgnoreCase_NonBacktracking()
        {
            var turkish = new CultureInfo("tr-TR");
            string input = "I\u0131\u0130i";
            string pattern = "[H-J][\u0131-\u0140][\u0120-\u0130][h-j]";

            Regex regex = Create(pattern, turkish, RegexOptions.IgnoreCase | RegexHelpers.RegexOptionNonBacktracking)[0];

            // The pattern must trivially match the input because all of the letters fall in the given intervals
            // Ignoring case can only add more letters here -- not REMOVE letters
            Assert.True(regex.IsMatch(input));
        }
    }
}
