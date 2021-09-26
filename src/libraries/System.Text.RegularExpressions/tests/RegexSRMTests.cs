// Licensed to the .NET Foundation under one or more agreements.if #DEBUG
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Globalization;

namespace System.Text.RegularExpressions.Tests
{
    public class RegexSRMTests
    {
        private readonly ITestOutputHelper _output;

        public RegexSRMTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Causes symbolic matcher to switch to Antimirov mode internally.
        /// Antimirov mode is otherwise never triggered by typical cases.
        /// </summary>
        [Theory]
        [InlineData("a.{20}$", "a01234567890123456789", 21)]
        [InlineData("(a.{20}|a.{10})bc$", "a01234567890123456789bc", 23)]
        public void TestAntimirovMode(string pattern, string input_suffix, int matchlength)
        {
            Random random = new Random(0);
            byte[] buffer = new byte[50000];
            random.NextBytes(buffer);
            var input = new string(Array.ConvertAll(buffer, b => (b <= 0x7F ? 'a' : 'b')));
            input += input_suffix;
            Regex re = new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.Singleline);
            Match m = re.Match(input);
            Assert.True(m.Success);
            Assert.Equal(buffer.Length, m.Index);
            Assert.Equal(matchlength, m.Length);
        }

        [Theory]
        [InlineData("(a|ab|c|bcd){0,}d*", "ababcd", "0,6", "0,1")]    //different results
        [InlineData("(ab|a|bcd|c){0,}d*", "ababcd", "0,6", "0,6")]    //same result with different order of choices
        [InlineData("(a|ab|c|bcd){0,10}d*", "ababcd", "0,6", "0,1")]  //different results
        [InlineData("(ab|a|bcd|c){0,10}d*", "ababcd", "0,6", "0,6")]  //same result with different order of choices
        [InlineData("(a|ab|c|bcd)*d*", "ababcd", "0,6", "0,1")]       //different results
        [InlineData("(ab|a|bcd|c)*d*", "ababcd", "0,6", "0,6")]       //same result with different order of choices
        public void TestAlternationOrderIndependenceInEagerLoop(string pattern, string input, string dfamatch, string nondfamatch)
        {
            Regex r1 = new Regex(pattern, RegexOptions.NonBacktracking);
            Regex r1_ = new Regex(pattern);
            var m1 = r1.Match(input);
            var m1_ = r1_.Match(input);
            int[] m1exp = Array.ConvertAll(dfamatch.Split(','), int.Parse);
            int[] m1_exp = Array.ConvertAll(nondfamatch.Split(','), int.Parse);
            Assert.Equal(m1exp[0], m1.Index);
            Assert.Equal(m1exp[1], m1.Length);
            Assert.Equal(m1_exp[0], m1_.Index);
            Assert.Equal(m1_exp[1], m1_.Length);
        }

        [Fact]
        public void TestAltOrderIndependence()
        {
            string rawregex = @"(the)\s*(0?[1-9]|[12][0-9]|3[01])";
            var re = new Regex(rawregex, RegexOptions.NonBacktracking);
            var reC = new Regex(rawregex, RegexOptions.Compiled);
            var input = "it is the 10:00 time";
            var re_match = re.Match(input);
            var reC_match = reC.Match(input);
            Assert.Equal(reC_match.Index, re_match.Index);
            Assert.Equal(reC_match.Value, re_match.Value);
            Assert.Equal("the 1", re_match.Value);
            //----
            //equivalent regex in NonBacktracking mode
            string rawregex_alt = @"(the)\s*([12][0-9]|3[01]|0?[1-9])";
            var re_alt = new Regex(rawregex_alt, RegexOptions.NonBacktracking);
            var re_alt_match = re_alt.Match(input);
            Assert.Equal(re_match.Index, re_alt_match.Index);
            Assert.Equal(re_match.Value, re_alt_match.Value);
            //not equivalent as non-NonBacktracking mode because the match will be "the 10"
            var re_altC = new Regex(rawregex_alt, RegexOptions.Compiled);
            var re_altC_match = re_altC.Match(input);
            Assert.Equal("the 10", re_altC_match.Value);
        }

        #region Following tests are currently relevant only in DEBUG mode
        [Theory]
        [InlineData("[abc]{0,10}", "a[abc]{0,3}", "xxxabbbbbbbyyy", true, "abbb")]
        [InlineData("[abc]{0,10}?", "a[abc]{0,3}?", "xxxabbbbbbbyyy", true, "a")]
        public void TestConjunctionOverCounting(string conjunct1, string conjunct2, string input, bool success, string match)
        {
            try
            {
                string pattern = And(conjunct1, conjunct2);
                Regex re = new Regex(pattern, RegexOptions.NonBacktracking);
                Match m = re.Match(input);
                Assert.Equal(success, m.Success);
                Assert.Equal(match, m.Value);
            }
            catch (NotSupportedException e)
            {
                // In Release build (?( test-pattern ) yes-pattern | no-pattern ) is not supported
                Assert.Contains("conditional", e.Message);
            }
        }

        [Fact]
        public void TestDGMLGeneration()
        {
            StringWriter sw = new StringWriter();
            var re = new Regex(".*a+", RegexOptions.NonBacktracking | RegexOptions.Singleline);
            if (TrySaveDGML(re, sw))
            {
                string str = sw.ToString();
                Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", str);
                Assert.Contains("DirectedGraph", str);
                Assert.Contains(".*a+", str);
            }

            static bool TrySaveDGML(Regex regex, TextWriter writer, int bound = -1, bool hideStateInfo = false, bool addDotStar = false, bool inReverse = false, bool onlyDFAinfo = false, int maxLabelLength = -1, bool asNFA = false)
            {
                MethodInfo saveDgml = regex.GetType().GetMethod("SaveDGML", BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveDgml is not null)
                {
                    saveDgml.Invoke(regex, new object[] { writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength, asNFA });
                    return true;
                }

                return false;
            }
        }

        static string And(params string[] regexes)
        {
            string conj = "(" + regexes[regexes.Length - 1] + ")";
            for (int i = regexes.Length - 2; i >= 0; i--)
            {
                conj = $"(?({regexes[i]}){conj}|[0-[0]])";
            }
            return conj;
        }

        static string Not(string regex)
        {
            return $"(?({regex})[0-[0]]|[\\0-\\uFFFF]*)";
        }

        [Fact]
        public void SRMTest_ConjuctionIsMatch()
        {
            try
            {
                var re = new Regex(And(".*a.*", ".*b.*"), RegexOptions.NonBacktracking | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                bool ok = re.IsMatch("xxaaxxBxaa");
                Assert.True(ok);
                bool fail = re.IsMatch("xxaaxxcxaa");
                Assert.False(fail);
            }
            catch (NotSupportedException e)
            {
                // In Release build (?( test-pattern ) yes-pattern | no-pattern ) is not supported
                Assert.Contains("conditional", e.Message);
            }
        }

        [Fact]
        public void SRMTest_ConjuctionFindMatch()
        {
            try
            {
                // contains lower, upper, and a digit, and is between 2 and 4 characters long
                var re = new Regex(And(".*[a-z].*", ".*[A-Z].*", ".*[0-9].*", ".{2,4}"), RegexOptions.NonBacktracking | RegexOptions.Singleline);
                var match = re.Match("xxaac\n5Bxaa");
                Assert.True(match.Success);
                Assert.Equal(4, match.Index);
                Assert.Equal(4, match.Length);
            }
            catch (NotSupportedException e)
            {
                // In Release build (?( test-pattern ) yes-pattern | no-pattern ) is not supported
                Assert.Contains("conditional", e.Message);
            }
        }

        [Fact]
        public void SRMTest_ComplementFindMatch()
        {
            try
            {
                // contains lower, upper, and a digit, and is between 4 and 8 characters long, does not contain 2 consequtive digits
                var re = new Regex(And(".*[a-z].*", ".*[A-Z].*", ".*[0-9].*", ".{4,8}",
                    Not(".*(01|12|23|34|45|56|67|78|89).*")), RegexOptions.NonBacktracking | RegexOptions.Singleline);
                var match = re.Match("xxaac12Bxaas3455");
                Assert.True(match.Success);
                Assert.Equal(6, match.Index);
                Assert.Equal(7, match.Length);
            }
            catch (NotSupportedException e)
            {
                // In Release build (?( test-pattern ) yes-pattern | no-pattern ) is not supported
                Assert.Contains("conditional", e.Message);
            }
        }

        [Fact]
        public void PasswordSearch()
        {
            try
            {
                string twoLower = ".*[a-z].*[a-z].*";
                string twoUpper = ".*[A-Z].*[A-Z].*";
                string threeDigits = ".*[0-9].*[0-9].*[0-9].*";
                string oneSpecial = @".*[\x21-\x2F\x3A-\x40\x5B-x60\x7B-\x7E].*";
                string Not_countUp = Not(".*(012|123|234|345|456|567|678|789).*");
                string Not_countDown = Not(".*(987|876|765|654|543|432|321|210).*");
                // Observe that the space character (immediately before '!' in ASCII) is excluded
                string length = "[!-~]{8,12}";

                // Just to make the chance that the randomly generated part actually has a match
                // be astronomically unlikely require 'P' and 'r' to be present also,
                // although this constraint is really bogus from password constraints point of view
                string contains_first_P_and_then_r = ".*P.*r.*";

                // Conjunction of all the above constraints
                string all = And(twoLower, twoUpper, threeDigits, oneSpecial, Not_countUp, Not_countDown, length, contains_first_P_and_then_r);

                // search for the password in a context surrounded by word boundaries
                Regex re = new Regex($@"\b{all}\b", RegexOptions.NonBacktracking | RegexOptions.Singleline);

                // Does not qualify because of 123 and connot end between 2 and 3 because of \b
                string almostPassw1 = "P@ssW0rd123";
                // Does not have at least two uppercase
                string almostPassw2 = "P@55w0rd";

                // These two qualify
                string password1 = "P@55W0rd";
                string password2 = "Pa5$w00rD";

                foreach (int k in new int[] { 500, 1000, 5000, 10000, 50000, 100000 })
                {
                    Random random = new(k);
                    byte[] buffer1 = new byte[k];
                    byte[] buffer2 = new byte[k];
                    byte[] buffer3 = new byte[k];
                    random.NextBytes(buffer1);
                    random.NextBytes(buffer2);
                    random.NextBytes(buffer3);
                    string part1 = new string(Array.ConvertAll(buffer1, b => (char)b));
                    string part2 = new string(Array.ConvertAll(buffer2, b => (char)b));
                    string part3 = new string(Array.ConvertAll(buffer3, b => (char)b));

                    string input = $"{part1} {almostPassw1} {part2} {password1} {part3} {password2}, finally this {almostPassw2} does not qualify either";

                    int expextedMatch1Index = (2 * k) + almostPassw1.Length + 3;
                    int expextedMatch1Length = password1.Length;

                    int expextedMatch2Index = (3 * k) + almostPassw1.Length + password1.Length + 5;
                    int expextedMatch2Length = password2.Length;

                    // Random text hiding almostPassw and password
                    int t = System.Environment.TickCount;
                    Match match1 = re.Match(input);
                    Match match2 = match1.NextMatch();
                    Match match3 = match2.NextMatch();
                    t = System.Environment.TickCount - t;

                    _output.WriteLine($@"k={k}, t={t}ms");

                    Assert.True(match1.Success);
                    Assert.Equal(expextedMatch1Index, match1.Index);
                    Assert.Equal(expextedMatch1Length, match1.Length);
                    Assert.Equal(password1, match1.Value);

                    Assert.True(match2.Success);
                    Assert.Equal(expextedMatch2Index, match2.Index);
                    Assert.Equal(expextedMatch2Length, match2.Length);
                    Assert.Equal(password2, match2.Value);

                    Assert.False(match3.Success);
                }
            }
            catch (NotSupportedException e)
            {
                // In Release build (?( test-pattern ) yes-pattern | no-pattern ) is not supported
                Assert.Contains("conditional", e.Message);
            }
        }

        [Fact]
        public void PasswordSearchDual()
        {
            try
            {
                string Not_twoLower = Not(".*[a-z].*[a-z].*");
                string Not_twoUpper = Not(".*[A-Z].*[A-Z].*");
                string Not_threeDigits = Not(".*[0-9].*[0-9].*[0-9].*");
                string Not_oneSpecial = Not(@".*[\x21-\x2F\x3A-\x40\x5B-x60\x7B-\x7E].*");
                string countUp = ".*(012|123|234|345|456|567|678|789).*";
                string countDown = ".*(987|876|765|654|543|432|321|210).*";
                // Observe that the space character (immediately before '!' in ASCII) is excluded
                string Not_length = Not("[!-~]{8,12}");

                // Just to make the chance that the randomly generated part actually has a match
                // be astronomically unlikely require 'P' and 'r' to be present also,
                // although this constraint is really bogus from password constraints point of view
                string Not_contains_first_P_and_then_r = Not(".*P.*r.*");

                // Negated disjunction of all the above constraints
                // By deMorgan's laws we know that ~(A|B|...|C) = ~A&~B&...&~C and ~~A = A
                // So Not(Not_twoLower|...) is equivalent to twoLower&~(...)
                string all = Not($"{Not_twoLower}|{Not_twoUpper}|{Not_threeDigits}|{Not_oneSpecial}|{countUp}|{countDown}|{Not_length}|{Not_contains_first_P_and_then_r}");

                // search for the password in a context surrounded by word boundaries
                Regex re = new Regex($@"\b{all}\b", RegexOptions.NonBacktracking | RegexOptions.Singleline);

                // Does not qualify because of 123 and connot end between 2 and 3 because of \b
                string almostPassw1 = "P@ssW0rd123";
                // Does not have at least two uppercase
                string almostPassw2 = "P@55w0rd";

                // These two qualify
                string password1 = "P@55W0rd";
                string password2 = "Pa5$w00rD";

                foreach (int k in new int[] { 500, 1000, 5000, 10000, 50000, 100000 })
                {
                    Random random = new(k);
                    byte[] buffer1 = new byte[k];
                    byte[] buffer2 = new byte[k];
                    byte[] buffer3 = new byte[k];
                    random.NextBytes(buffer1);
                    random.NextBytes(buffer2);
                    random.NextBytes(buffer3);
                    string part1 = new string(Array.ConvertAll(buffer1, b => (char)b));
                    string part2 = new string(Array.ConvertAll(buffer2, b => (char)b));
                    string part3 = new string(Array.ConvertAll(buffer3, b => (char)b));

                    string input = $"{part1} {almostPassw1} {part2} {password1} {part3} {password2}, finally this {almostPassw2} does not qualify either";

                    int expectedMatch1Index = (2 * k) + almostPassw1.Length + 3;
                    int expectedMatch1Length = password1.Length;

                    int expectedMatch2Index = (3 * k) + almostPassw1.Length + password1.Length + 5;
                    int expectedMatch2Length = password2.Length;

                    // Random text hiding almostPassw and password
                    int t = System.Environment.TickCount;
                    Match match1 = re.Match(input);
                    Match match2 = match1.NextMatch();
                    Match match3 = match2.NextMatch();
                    t = System.Environment.TickCount - t;

                    _output.WriteLine($@"k={k}, t={t}ms");

                    Assert.True(match1.Success);
                    Assert.Equal(expectedMatch1Index, match1.Index);
                    Assert.Equal(expectedMatch1Length, match1.Length);
                    Assert.Equal(password1, match1.Value);

                    Assert.True(match2.Success);
                    Assert.Equal(expectedMatch2Index, match2.Index);
                    Assert.Equal(expectedMatch2Length, match2.Length);
                    Assert.Equal(password2, match2.Value);

                    Assert.False(match3.Success);
                }
            }
            catch (NotSupportedException e)
            {
                // In Release build (?( test-pattern ) yes-pattern | no-pattern ) is not supported
                Assert.Contains("conditional", e.Message);
            }
        }

        #endregion
    }
}
