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

        static void WriteLine(string s)
        {
#if DEBUG
            // the string will appear in the Output window
            System.Diagnostics.Debug.WriteLine(s);
#endif
        }

        internal const RegexOptions DFA = (RegexOptions)0x400;

        private const char Turkish_I_withDot = '\u0130';
        private const char Turkish_i_withoutDot = '\u0131';
        private const char Kelvin_sign = '\u212A';

        //[Theory]
        //[InlineData(RegexOptions.None)]
        //[InlineData(DFA)]
        //public void TestBoundary(RegexOptions options)
        //{
        //    var W = new Regex(@"\W", options);
        //    var b = new Regex(@"\b", options);
        //    var ambiguous = new List<char>();
        //    for (char c = '\0'; c < '\uFFFF'; c++)
        //        if (W.IsMatch(c.ToString()) && b.IsMatch(c.ToString()))
        //            ambiguous.Add(c);
        //    Assert.Empty(ambiguous);
        //}

        //[Fact]
        //public void TestWordchar()
        //{
        //    var w1 = new Regex(@"\w", RegexOptions.None);
        //    var w2 = new Regex(@"\w", DFA);
        //    var ambiguous = new List<char>();
        //    for (char c = '\0'; c < '\uFFFF'; c++)
        //        if (w1.IsMatch(c.ToString()) != w2.IsMatch(c.ToString()))
        //            ambiguous.Add(c);
        //    Assert.Empty(ambiguous);
        //}

        [Theory]
        [InlineData("((?:0*)+?(?:.*)+?)?", "0a", 2)]
        [InlineData("(?:(?:0?)+?(?:a?)+?)?", "0a", 2)]
        [InlineData(@"(?i:(\()((?<a>\w+(\.\w+)*)(,(?<a>\w+(\.\w+)*)*)?)(\)))", "some.text(this.is,the.match)", 1)]
        private void TestDifficultCasesForBacktracking(string pattern, string input, int matchcount)
        {
            var regex = new Regex(pattern, DFA);
            List<Match> matches = new List<Match>();
            var match = regex.Match(input);
            while (match.Success)
            {
                matches.Add(match);
                match = match.NextMatch();
            }
            Assert.Equal(matchcount, matches.Count);
        }

        [Fact]
        public void TestMixedLazyEagerCounting()
        {
            string pattern = "z(a{0,5}|a{0,10}?)";
            var input = "xyzaaaaaaaaaxyz";
            Regex re = new Regex(pattern, DFA);
            Match m = re.Match(input);
            Assert.True(m.Success);
            Assert.Equal(2, m.Index);
            Assert.Equal(6, m.Length);
        }

        [Fact]
        public void TestNFAmode()
        {
            string rawregex = "a.{20}$";
            Random random = new Random(0);
            byte[] buffer = new byte[50000];
            random.NextBytes(buffer);
            var input = new string(Array.ConvertAll(buffer, b => (b <= 0x7F ? 'a' : 'b')));
            input += "a01234567890123456789";
            Regex re = new Regex(rawregex, DFA | RegexOptions.Singleline);
            Match m = re.Match(input);
            Assert.True(m.Success);
            Assert.Equal(buffer.Length, m.Index);
            Assert.Equal(21, m.Length);
        }

        [Fact]
        public void TestNFAmodeAntimirov()
        {
            string rawregex = "(a.{20}|a.{10})bc$";
            Random random = new Random(0);
            byte[] buffer = new byte[50000];
            random.NextBytes(buffer);
            var input = new string(Array.ConvertAll(buffer, b => (b <= 0x7F ? 'a' : 'b')));
            input += "a01234567890123456789bc";
            Regex re = new Regex(rawregex, DFA | RegexOptions.Singleline);
            Match m = re.Match(input);
            Assert.True(m.Success);
            Assert.Equal(buffer.Length, m.Index);
            Assert.Equal(23, m.Length);
        }

        /// <summary>
        /// Maps each character c to the set of all of its equivalent characters if case is ignored or null if c in case-insensitive
        /// </summary>
        /// <param name="culture">ignoring case wrt this culture</param>
        /// <param name="treatedAsCaseInsensitive">characters that are otherwise case-sensitive but not in a regex</param>
        private static HashSet<char>[] ComputeIgnoreCaseTable(CultureInfo culture, HashSet<char> treatedAsCaseInsensitive)
        {
            CultureInfo ci = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;
            var ignoreCase = new HashSet<char>[0x10000];
            for (uint i = 0; i <= 0xFFFF; i++)
            {
                char c = (char)i;
                char cU = char.ToUpper(c);
                char cL = char.ToLower(c);
                // Turkish i without dot is only considered case-sensitive in tr and az languages
                if (treatedAsCaseInsensitive.Contains(c) ||
                    (c == Turkish_i_withoutDot && culture.TwoLetterISOLanguageName != "tr" && culture.TwoLetterISOLanguageName != "az"))
                    continue;
                if (cU != cL)
                {
                    var set = (ignoreCase[c] == null ? (ignoreCase[cU] == null ? (ignoreCase[cL] == null ? new HashSet<char>()
                                                     : ignoreCase[cL]) : ignoreCase[cU]) : ignoreCase[c]);
                    set.Add(c);
                    set.Add(cU);
                    set.Add(cL);
                    ignoreCase[c] = set;
                    ignoreCase[cL] = set;
                    ignoreCase[cU] = set;
                }
            }
            CultureInfo.CurrentCulture = ci;
            return ignoreCase;
        }

        /// <summary>
        /// represents the difference between the two tables as a special string
        /// </summary>
        private static string GetDiff(HashSet<char>[] table1, HashSet<char>[] table2)
        {
            List<string> diffs = new();
            Func<HashSet<char>, int, string> F = (s, i) =>
             {
                 if (s == null)
                     return ((char)i).ToString();
                 List<char> elems = new List<char>(s);
                 elems.Sort();
                 string res = new string(elems.ToArray());
                 return res;
             };
            for (int i = 0; i <= 0xFFFF; i++)
            {
                var s1 = F(table1[i], i);
                var s2 = F(table2[i], i);
                if (s1 != s2)
                    diffs.Add(string.Format("{0}:{1}/{2}", (char)i, s1, s2));
            }
            return string.Join(",", diffs.ToArray());
        }

        /// <summary>
        /// This test is to make sure that the generated IgnoreCaseRelation table for DFA does not need to be updated.
        /// It would need to be updated/regenerated if this test fails.
        /// </summary>
        [OuterLoop("May take several seconds due to large number of cultures tested")]
        [Fact]
        public void TestIgnoreCaseRelation()
        {
            // these 22 characters are considered case-insensitive by regex, while they are case-sensitive outside regex
            // but they are only case-sensitive in an asymmmetrical way: tolower(c)=c, tolower(toupper(c)) != c
            HashSet<char> treatedAsCaseInsensitive =
                 new("\u00B5\u017F\u0345\u03C2\u03D0\u03D1\u03D5\u03D6\u03F0\u03F1\u03F5\u1C80\u1C81\u1C82\u1C83\u1C84\u1C85\u1C86\u1C87\u1C88\u1E9B\u1FBE");
            foreach (char c in treatedAsCaseInsensitive)
            {
                char cU = char.ToUpper(c);
                Assert.NotEqual(c, cU);
                Assert.False(Regex.IsMatch(c.ToString(), cU.ToString(), RegexOptions.IgnoreCase));
            }

            Assert.False(Regex.IsMatch(Turkish_i_withoutDot.ToString(), "i", RegexOptions.IgnoreCase));

            // as baseline it is assumed the the invariant culture does not change
            var inv_table = ComputeIgnoreCaseTable(CultureInfo.InvariantCulture, treatedAsCaseInsensitive);
            var cultures = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures);
            // expected difference between invariant and tr or az culture
            string tr_diff = string.Format("I:Ii/I{0},i:Ii/i{1},{1}:{1}/i{1},{0}:{0}/I{0}", Turkish_i_withoutDot, Turkish_I_withDot);
            // expected differnce between invariant and other cultures including the default en-US
            string default_diff = string.Format("I:Ii/Ii{0},i:Ii/Ii{0},{0}:{0}/Ii{0}", Turkish_I_withDot);
            // the expected difference between invariant culture and all other cultures is only for i,I,Turkish_I_withDot,Turkish_i_withoutDot
            // differentiate based on the TwoLetterISOLanguageName only (232 cases instead of 812)
            List<CultureInfo> testcultures = new();
            HashSet<string> done = new();
            for (int i = 0; i < cultures.Length; i++)
                if (cultures[i] != CultureInfo.InvariantCulture && done.Add(cultures[i].TwoLetterISOLanguageName))
                    testcultures.Add(cultures[i]);
            foreach (var culture in testcultures)
            {
                var table = ComputeIgnoreCaseTable(culture, treatedAsCaseInsensitive);
                string diff = GetDiff(inv_table, table);
                if (culture.TwoLetterISOLanguageName == "tr" || culture.TwoLetterISOLanguageName == "az")
                    // tr or az alphabet
                    Assert.Equal(tr_diff, diff);
                else
                    // all other alphabets are treated the same as en-US
                    Assert.Equal(default_diff, diff);
            }
        }

        [OuterLoop("May take tens of seconds")]
        [Fact]
        public void TestIgnoreCaseRelationBorderCasesInDFAmode()
        {
            // these 22 characters are considered case-insensitive by regex, while they are case-sensitive outside regex
            // but they are only case-sensitive in an asymmmetrical way: tolower(c)=c, tolower(toupper(c)) != c
            HashSet<char> treatedAsCaseInsensitive =
                 new("\u00B5\u017F\u0345\u03C2\u03D0\u03D1\u03D5\u03D6\u03F0\u03F1\u03F5\u1C80\u1C81\u1C82\u1C83\u1C84\u1C85\u1C86\u1C87\u1C88\u1E9B\u1FBE");
            foreach (char c in treatedAsCaseInsensitive)
            {
                char cU = char.ToUpper(c);
                Assert.NotEqual(c, cU);
                Assert.False(Regex.IsMatch(c.ToString(), cU.ToString(), RegexOptions.IgnoreCase | DFA));
            }

            Assert.False(Regex.IsMatch(Turkish_i_withoutDot.ToString(), "i", RegexOptions.IgnoreCase | DFA));
            Assert.True(Regex.IsMatch(Turkish_I_withDot.ToString(), "i", RegexOptions.IgnoreCase | DFA));
            Assert.True(Regex.IsMatch(Turkish_I_withDot.ToString(), "i", RegexOptions.IgnoreCase | DFA));
            Assert.False(Regex.IsMatch(Turkish_I_withDot.ToString(), "i", RegexOptions.IgnoreCase | DFA | RegexOptions.CultureInvariant));

            // Turkish i without dot is not considered case-sensitive in the default en-US culture
            treatedAsCaseInsensitive.Add(Turkish_i_withoutDot);

            List<char> caseSensitiveChars = new();
            for (char c = '\0'; c < '\uFFFF'; c++)
                if (!treatedAsCaseInsensitive.Contains(c) && char.ToUpper(c) != char.ToLower(c))
                    caseSensitiveChars.Add(c);

            // test all case-sensitive characters exhaustively in DFA mode
            foreach (char c in caseSensitiveChars)
                Assert.True(Regex.IsMatch(char.ToUpper(c).ToString() + char.ToLower(c).ToString(),
                    c.ToString() + c.ToString(), RegexOptions.IgnoreCase | DFA));
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
            Regex r1 = new Regex(pattern, DFA);
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

        [Theory]
        [InlineData("^", RegexOptions.None, "", "0")]
        [InlineData("$", RegexOptions.None, "", "0")]
        [InlineData("^$", RegexOptions.None, "", "0")]
        [InlineData("$^", RegexOptions.None, "", "0")]
        [InlineData("a*", RegexOptions.None, "bbb", "0,1,2,3")]
        [InlineData("a*", RegexOptions.None, "baaabb", "0,1,4,5,6")]
        [InlineData("\\b", RegexOptions.None, "hello--world", "0,5,7,12")]
        [InlineData("\\B", RegexOptions.None, "hello--world", "1,2,3,4,6,8,9,10,11")]
        public void TestEmptyMatches(string pattern, RegexOptions options, string input, string indices)
        {
            //testing both DFA and nonDFA to make sure they agree
            int[] positions = Array.ConvertAll(indices.Split(','), int.Parse);
            Regex re_ = new Regex(pattern, options);
            Match m_ = re_.Match(input);
            Assert.True(m_.Success);
            Assert.Equal(positions[0], m_.Index);
            Regex re = new Regex(pattern, options | DFA);
            Match m = re.Match(input);
            Assert.True(m.Success);
            Assert.Equal(positions[0], m.Index);
            for (int i = 1; i < positions.Length; i++)
            {
                m_ = m_.NextMatch();
                Assert.True(m_.Success);
                Assert.Equal(positions[i], m_.Index);
                m = m.NextMatch();
                Assert.True(m.Success);
                Assert.Equal(positions[i], m.Index);
            }
            //there should be no more matches remaining
            var m_fail = m_.NextMatch();
            Assert.False(m_fail.Success);
            var mfail = m.NextMatch();
            Assert.False(mfail.Success);
        }

        [Theory]    
        [InlineData("^abc", RegexOptions.None, "abcccc",  true, "abc", 0)]
        [InlineData("^abc", RegexOptions.None, "aabcccc", false, "", 0)]
        [InlineData("abc$", RegexOptions.None, "aabcccc", false, "", 0)]
        [InlineData("abc\\z", RegexOptions.None, "aabc\n", false, "", 0)]
        [InlineData("abc\\Z", RegexOptions.None, "aabc\n", true, "abc", 1)]
        [InlineData("abc$", RegexOptions.None, "aabc\nabc", true, "abc", 5)]
        [InlineData("abc$", RegexOptions.Multiline, "aabc\nabc", true, "abc", 1)]
        [InlineData("a\\bb", RegexOptions.None, "ab", false, "", 0)]
        [InlineData("a\\Bb", RegexOptions.None, "ab", true, "ab", 0)]
        [InlineData("(a\\Bb|a\\bb)", RegexOptions.None, "ab", true, "ab", 0)]
        public void TestAnchorPruning(string pattern, RegexOptions options, string input, bool success, string match, int index)
        {
            Regex re = new Regex(pattern, options | DFA);
            Match m = re.Match(input);
            Assert.Equal(success, m.Success);
            Assert.Equal(match, m.Value);
            if (success)
                Assert.Equal(index, m.Index);
        }

        [Theory]
        [InlineData("[abc]{0,10}", "a[abc]{0,3}", "xxxabbbbbbbyyy", true, "abbb")]
        [InlineData("[abc]{0,10}?", "a[abc]{0,3}?", "xxxabbbbbbbyyy", true, "a")]
        public void TestConjunctionOverCounting(string conjunct1, string conjunct2, string input, bool success, string match)
        {
            string pattern = And(conjunct1, conjunct2);
            Regex re = new Regex(pattern, DFA);
            Match m = re.Match(input);
            Assert.Equal(success, m.Success);
            Assert.Equal(match, m.Value);
        }

        [Theory]
        [InlineData("a[abc]{0,10}", "a[abc]{0,3}", "xxxabbbbbbbyyy", true, "abbbbbbb")]
        [InlineData("a[abc]{0,10}?", "a[abc]{0,3}?", "xxxabbbbbbbyyy", true, "a")]
        public void TestDisjunctionOverCounting(string disjunct1, string disjunct2, string input, bool success, string match)
        {
            string pattern = disjunct1 + "|" + disjunct2;
            Regex re = new Regex(pattern, DFA);
            Match m = re.Match(input);
            Assert.Equal(success, m.Success);
            Assert.Equal(match, m.Value);
        }


        [Theory]
        [InlineData("(?i:[a-dÕ]+k*)", "xyxaBõc\u212AKAyy", true, "aBõc\u212AK")]
        [InlineData("(?i:[a-d]+)", "xyxaBcyy", true, "aBc")]
        [InlineData("(?i:[^a])", "aAaA", false, "")]                             // this correponds to not{a,A}
        [InlineData("(?i:[\0-\uFFFF-[A]])", "aAaA", false, "")]                  // this correponds to not{a,A}
        [InlineData("(?i:[\0-@B-\uFFFF]+)", "xaAaAy", true, "xaAaAy")]           // this correponds to .+
        [InlineData("(?i:[\0-ac-\uFFFF])", "b", true, "b")]
        [InlineData("(?i:[^b])", "b", false, "")]
        [InlineData("(?i:[\0-PR-\uFFFF])", "Q", true, "Q")]
        [InlineData("(?i:[^Q])", "q", false, "")]
        [InlineData("(?i:[\0-pr-\uFFFF])", "q", true, "q")]
        public void TestOfCaseInsensitiveCornerCasesInSRM(string pattern, string input, bool success, string match_expected)
        {
            Regex r_ = new Regex(pattern);
            Regex r = new Regex(pattern, DFA);
            //RegexExperiment.ViewDGML(r);
            var m = r.Match(input);
            var m_ = r_.Match(input);
            Assert.Equal(success, m_.Success);
            Assert.Equal(match_expected, m_.Value);
            Assert.Equal(success, m.Success);
            Assert.Equal(match_expected, m.Value);
        }

        [Theory]
        [InlineData("(?i:I)", "xy\u0131ab", "", "", "\u0131")]
        [InlineData("(?i:iI+)", "abcIIIxyz", "III", "III", "")]
        [InlineData("(?i:iI+)", "abcIi\u0130xyz", "Ii\u0130", "Ii", "")]
        [InlineData("(?i:iI+)", "abcI\u0130ixyz", "I\u0130i", "", "")]
        [InlineData("(?i:iI+)", "abc\u0130IIxyz", "\u0130II", "II", "\u0130II")]
        [InlineData("(?i:iI+)", "abc\u0130\u0131Ixyz", "", "", "\u0130\u0131I")]
        [InlineData("(?i:iI+)", "abc\u0130Iixyz", "\u0130Ii", "Ii", "\u0130I")]
        [InlineData("(?i:[^IJKLM]I)", "ii\u0130i\u0131ab", "", "\u0130i", "i\u0131")]
        public void TestOfCulturesInSRM(string pattern, string input, string match_en_expected, string match_in_expected, string match_tr_expected)
        {
            CultureInfo tr_culture = new CultureInfo("tr");
            CultureInfo savedCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = tr_culture;
            //this will treat i=\u0130 and I=\u0131 but i!=I
            Regex r_tr = new Regex(pattern, DFA);
            Regex r_tr_ = new Regex(pattern, RegexOptions.None);
            var s = r_tr.Match(input).Value;
            var s_ = r_tr_.Match(input).Value;
            CultureInfo.CurrentCulture = savedCulture;
            Assert.Equal(s, s_);
            //this will treat i=I and i!=\u0130 and I!=\u131
            Regex r_in = new Regex(pattern, DFA | RegexOptions.CultureInvariant);
            //this is the default for e-US culture where
            //i=I=\u130 but I!=\u0131 
            Regex r_en = new Regex(pattern, DFA);

            TestOfCulturesInSRM_(r_en, r_in, r_tr, input, match_en_expected, match_in_expected, match_tr_expected);

            //validate that the correct results are maintained through serialization
            TestOfCulturesInSRM_(SerDeser(r_en), SerDeser(r_in), SerDeser(r_tr), input, match_en_expected, match_in_expected, match_tr_expected);
        }

        private static Regex SerDeser(Regex r) => Deserialize(Serialize(r));

        private void TestOfCulturesInSRM_(Regex r_en, Regex r_in, Regex r_tr, string input,
            string match_en_expected, string match_in_expected, string match_tr_expected)
        {
            var match_en = r_en.Match(input);
            var match_in = r_in.Match(input);
            var match_tr = r_tr.Match(input);

            Assert.Equal(match_en_expected != string.Empty, match_en.Success);
            Assert.Equal(match_in_expected != string.Empty, match_in.Success);
            Assert.Equal(match_tr_expected != string.Empty, match_tr.Success);

            Assert.Equal(match_en_expected, match_en.Value);
            Assert.Equal(match_in_expected, match_in.Value);
            Assert.Equal(match_tr_expected, match_tr.Value);
        }

            [Fact]
        public void TestAltOrderIndependence()
        {
            string rawregex = @"(the)\s*(0?[1-9]|[12][0-9]|3[01])";
            var re = new Regex(rawregex, DFA);
            var reC = new Regex(rawregex, RegexOptions.Compiled);
            var input = "it is the 10:00 time";
            var re_match = re.Match(input);
            var reC_match = reC.Match(input);
            Assert.Equal(reC_match.Index, re_match.Index);
            Assert.Equal(reC_match.Value, re_match.Value);
            Assert.Equal("the 1", re_match.Value);
            //----
            //equivalent regex as DFA
            string rawregex_alt = @"(the)\s*([12][0-9]|3[01]|0?[1-9])";
            var re_alt = new Regex(rawregex_alt, DFA);
            var re_alt_match = re_alt.Match(input);
            Assert.Equal(re_match.Index, re_alt_match.Index);
            Assert.Equal(re_match.Value, re_alt_match.Value);
            //not equivalent as non-DFA because the match will be "the 10"
            var re_altC = new Regex(rawregex_alt, RegexOptions.Compiled);
            var re_altC_match = re_altC.Match(input);
            Assert.Equal("the 10", re_altC_match.Value);
        }

        [Fact]
        public void TestSRMTermination()
        {
            string input = " 123456789 123456789 123456789 123456789 123456789";
            for (int i = 0; i < 12; i++)
                input = input + input;
            //the input has 2^12 * 50 = 204800 characters
            string rawregex = @"[\\/]?[^\\/]*?(heythere|hej)[^\\/]*?$";
            Regex reC = new Regex(rawregex, RegexOptions.Compiled, new TimeSpan(0, 0, 1));
            Regex re = new Regex(rawregex, DFA, new TimeSpan(0, 0, 0, 0, 100));
            //it takes over 4min with backtracking, so 1sec times out for sure
            Assert.Throws<RegexMatchTimeoutException>(() => { reC.Match(input); });
            //DFA needs less than 100ms
            Assert.False(re.Match(input).Success);
        }

        /// <summary>
        /// Test that timeout is being checked in DFA mode.
        /// </summary>
        [Fact]
        public void TestSRMTimeout() => TestSRMTimeout_(new Regex(@"a.{20}$", DFA, new TimeSpan(0, 0, 0, 0, 10)));

        /// <summary>
        /// Test that serialization preserves timeout information.
        /// </summary>
        [Fact]
        public void TestSRMTimeoutAfterDeser() => TestSRMTimeout_(Deserialize(Serialize(new Regex(@"a.{20}$", DFA, new TimeSpan(0, 0, 0, 0, 10)))));

        private void TestSRMTimeout_(Regex re)
        {
            Random rnd = new Random(0);
            byte[] buffer = new byte[1000000];
            rnd.NextBytes(buffer);
            string input = new String(Array.ConvertAll(buffer, b => b < 200 ? 'a' : (char)b));
            Assert.Throws<RegexMatchTimeoutException>(() => { re.Match(input); });
        }

        /// <summary>
        /// Save the regex as a DFA in DGML format in the textwriter.
        /// </summary>
        /// <param name="r"></param>
        private static void SaveDGML(Regex regex, TextWriter writer, int bound = -1, bool hideStateInfo = false, bool addDotStar = false, bool inReverse = false, bool onlyDFAinfo = false, int maxLabelLength = -1)
        {
            MethodInfo saveDgml = regex.GetType().GetMethod("SaveDGML", BindingFlags.NonPublic | BindingFlags.Instance);
            saveDgml.Invoke(regex, new object[] { writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength });
        }

        [Fact]
        public void TestDGMLGeneration()
        {
            StringWriter sw = new StringWriter();
            var re = new Regex(".*a+", DFA | RegexOptions.Singleline);
            SaveDGML(re, sw);
            string str = sw.ToString();
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", str);
            Assert.Contains("DirectedGraph", str);
            Assert.Contains(".*a+", str);
        }

        [Fact]
        public void TestWordBoundary()
        {
            var re = new Regex(@"\b\w+nn\b", DFA);
            var match = re.Match("both Anne and Ann are names that contain nn");
            Assert.True(match.Success);
            Assert.Equal<int>(14, match.Index);
            Assert.Equal<int>(3, match.Length);
            var match2 = match.NextMatch();
            Assert.False(match2.Success);
        }

        [Fact]
        public void TestStartSet()
        {
            var re = new Regex(@"\u221E|\u2713", DFA);
            var match = re.Match("infinity \u221E and checkmark \u2713 are contained here");
            Assert.True(match.Success);
            Assert.Equal<int>(9, match.Index);
            Assert.Equal<int>(1, match.Length);
            var match2 = match.NextMatch();
            Assert.True(match2.Success);
            Assert.Equal<int>(25, match2.Index);
            Assert.Equal<int>(1, match2.Length);
            var match3 = match2.NextMatch();
            Assert.False(match3.Success);
        }

        private static MethodInfo _Deserialize = typeof(Regex).GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static);
        private static Regex Deserialize(string s) => _Deserialize.Invoke(null, new object[] { s }) as Regex;

        private static MethodInfo _Serialize = typeof(Regex).GetMethod("Serialize", BindingFlags.NonPublic | BindingFlags.Instance);
        private static string Serialize(Regex r) => _Serialize.Invoke(r, null) as string;

        /// <summary>
        /// Test serialization/deserialization of some sample regexes.
        /// Verify correctness of all the deserialized regexes against the original ones.
        /// Test also that deserialization followed by serialization is idempotent.
        /// </summary>
        [Fact]
        public void TestRegexSerialization()
        {
            string[] rawregexes = new string[] {@"\b\d\w+nn\b", "An+e", "^this", "(?i:Jaan$)", @"\p{Sm}+" };
            int k = rawregexes.Length;
            //DFA versions
            var rs = Array.ConvertAll(rawregexes, s => new Regex(s, DFA));
            //Compiled versions
            var rsC = Array.ConvertAll(rawregexes, s => new Regex(s, RegexOptions.Compiled));
            //serialize
            var ser = Array.ConvertAll(rs, Serialize);
            //deserialize
            var drs = Array.ConvertAll(ser, Deserialize);
            //repeat serialization on  the deserialized regexes
            var ser2 = Array.ConvertAll(drs, Serialize);
            //check idempotence
            for (int i = 0; i < k; i++)
                Assert.True(ser[i] == ser2[i], string.Format("idempotence of serialization of regex {0} fails", i));

            string input = "this text contains math symbols +~<> and names like Ann and Anne and maku and jaan";
            for (int i = 0; i < k; i++)
            {
                var match1 = rs[i].Match(input);
                var match2 = drs[i].Match(input);
                var matchExpected = rsC[i].Match(input);
                Assert.Equal(matchExpected.Success, match1.Success);
                Assert.Equal(matchExpected.Success, match2.Success);
                Assert.Equal(matchExpected.Value, match1.Value);
                Assert.Equal(matchExpected.Value, match2.Value);
            }
        }

        [Fact]
        public void SRMPrefixBugFixTest()
        {
            var re1 = new Regex("(a|ba)c", DFA);
            var match1 = re1.Match("bac");
            Assert.True(match1.Success);
            Assert.Equal(0, match1.Index);
            Assert.Equal(3, match1.Length);
            //---
            var match2 = re1.Match("ac");
            Assert.True(match2.Success);
            Assert.Equal(0, match2.Index);
            Assert.Equal(2, match2.Length);
            //---
            var match3 = re1.Match("baacd");
            Assert.True(match3.Success);
            Assert.Equal(2, match3.Index);
            Assert.Equal(2, match3.Length);
        }

        [Fact]
        public void BasicSRMTestBorderAnchors1()
        {
            var re1 = new Regex(@"\B x", DFA);
            var match1 = re1.Match(" xx");
            Assert.True(match1.Success);
            Assert.Equal(0, match1.Index);
            Assert.Equal(2, match1.Length);
            //---
            var re2 = new Regex(@"\bxx\b", DFA);
            var match2 = re2.Match(" zxx:xx");
            Assert.True(match2.Success);
            Assert.Equal(5, match2.Index);
            Assert.Equal(2, match2.Length);
        }

        [Fact]
        public void BasicSRMTestBorderAnchors2()
        {
            var re3 = new Regex(@"^abc*\B", RegexOptions.Multiline | DFA);
            var match3 = re3.Match("\nabcc \nabcccd\n");
            Assert.True(match3.Success);
            Assert.Equal(1, match3.Index);
            Assert.Equal(3, match3.Length);
            var match3b = match3.NextMatch();
            Assert.True(match3b.Success);
            Assert.Equal(7, match3b.Index);
            Assert.Equal(5, match3b.Length);
        }

        [Fact]
        public void BasicSRMTestBorderAnchors3()
        {
            var re = new Regex(@"a$", RegexOptions.Multiline | DFA);
            var match = re.Match("b\na");
            Assert.True(match.Success);
            Assert.Equal(2, match.Index);
            Assert.Equal(1, match.Length);
        }

        [Fact]
        public void BasicSRMTest()
        {
            var re = new Regex(@"a+", DFA);
            var match1 = re.Match("xxxxxaaaaxxxxxxxxxxaaaaaa");
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(4, match1.Length);
            Assert.Equal("aaaa", match1.Value);
            var match2 = match1.NextMatch();
            Assert.True(match2.Success);
            Assert.Equal(19, match2.Index);
            Assert.Equal(6, match2.Length);
            Assert.Equal("aaaaaa", match2.Value);
            var match3 = match2.NextMatch();
            Assert.False(match3.Success);
        }

        [Fact]
        public void BasicSRMTestWithIgnoreCase()
        {
            var re = new Regex(@"a+", DFA | RegexOptions.IgnoreCase);
            var match1 = re.Match("xxxxxaAAaxxxxxxxxxxaaaaAa");
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(4, match1.Length);
            Assert.Equal("aAAa", match1.Value);
            var match2 = match1.NextMatch();
            Assert.True(match2.Success);
            Assert.Equal(19, match2.Index);
            Assert.Equal(6, match2.Length);
            Assert.Equal("aaaaAa", match2.Value);
            var match3 = match2.NextMatch();
            Assert.False(match3.Success);
        }

        [Fact]
        public void BasicSRMTestNonASCII()
        {
            var re = new Regex(@"\d\s\w+", DFA);
            var match1 = re.Match("=====1\v\u212A4==========1\ta\u0130Aa");
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(4, match1.Length);
            Assert.Equal("1\v\u212A4", match1.Value);
            var match2 = match1.NextMatch();
            Assert.True(match2.Success);
            Assert.Equal(19, match2.Index);
            Assert.Equal(6, match2.Length);
            Assert.Equal("1\ta\u0130Aa", match2.Value);
            var match3 = match2.NextMatch();
            Assert.False(match3.Success);
        }

        [Fact]
        public void BasicSRMTest_WhiteSpace()
        {
            var re = new Regex(@"\s+", DFA);
            var match1 = re.Match("===== \n\t\v\r ====");
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(6, match1.Length);
            Assert.Equal(" \n\t\v\r ", match1.Value);
        }

        [Fact]
        public void BasicSRMTest_FFFF()
        {
            var re = new Regex(@"(\uFFFE\uFFFF)+", DFA);
            var match1 = re.Match("=====\uFFFE\uFFFF\uFFFE\uFFFF\uFFFE====");
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(4, match1.Length);
            Assert.Equal("\uFFFE\uFFFF\uFFFE\uFFFF", match1.Value);
        }

        [Fact]
        public void BasicSRMTest_NoPartition()
        {
            var re = new Regex(@"(...)+", DFA | RegexOptions.Singleline);
            var match1 = re.Match("abcdefgh");
            Assert.True(match1.Success);
            Assert.Equal(0, match1.Index);
            Assert.Equal(6, match1.Length);
            Assert.Equal("abcdef", match1.Value);
        }

        [Fact]
        public void SRMTest_UnicodeCategories00to09()
        {
            //"Lu", 0: UppercaseLetter
            //"Ll", 1: LowercaseLetter
            //"Lt", 2: TitlecaseLetter
            //"Lm", 3: ModifierLetter
            //"Lo", 4: OtherLetter
            //"Mn", 5: NonSpacingMark
            //"Mc", 6: SpacingCombiningMark
            //"Me", 7: EnclosingMark
            //"Nd", 8: DecimalDigitNumber
            //"Nl", 9: LetterNumber
            var re = new Regex(@"\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Mn}\p{Mc}\p{Me}\p{Nd}\p{Nl}", DFA);
            //match contains the first character from each category
            string input = "=====Aa\u01C5\u02B0\u01BB\u0300\u0903\u04880\u16EE===";
            var match1 = re.Match(input);
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(10, match1.Length);
            var match2 = match1.NextMatch();
            Assert.False(match2.Success);
        }

        [Fact]
        public void SRMTest_UnicodeCategories10to19()
        {
            //"No", 10: OtherNumber
            //"Zs", 11: SpaceSeparator
            //"Zl", 12: LineSeparator
            //"Zp", 13: ParagraphSeparator
            //"Cc", 14: Control
            //"Cf", 15: Format
            //"Cs", 16: Surrogate
            //"Co", 17: PrivateUse
            //"Pc", 18: ConnectorPunctuation
            //"Pd", 19: DashPunctuation
            var re = new Regex(@"\p{No}\p{Zs}\p{Zl}\p{Zp}\p{Cc}\p{Cf}\p{Cs}\p{Co}\p{Pc}\p{Pd}", DFA);
            //match contains the first character from each category
            string input = "=====\u00B2 \u2028\u2029\0\u0600\uD800\uE000_\u002D===";
            var match1 = re.Match(input);
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(10, match1.Length);
            var match2 = match1.NextMatch();
            Assert.False(match2.Success);
        }

        [Fact]
        public void SRMTest_UnicodeCategories20to29()
        {
            //"Ps", 20: OpenPunctuation
            //"Pe", 21: ClosePunctuation
            //"Pi", 22: InitialQuotePunctuation
            //"Pf", 23: FinalQuotePunctuation
            //"Po", 24: OtherPunctuation
            //"Sm", 25: MathSymbol
            //"Sc", 26: CurrencySymbol
            //"Sk", 27: ModifierSymbol
            //"So", 28: OtherSymbol
            //"Cn", 29: OtherNotAssigned
            var re = new Regex(@"\p{Ps}\p{Pe}\p{Pi}\p{Pf}\p{Po}\p{Sm}\p{Sc}\p{Sk}\p{So}\p{Cn}", DFA);
            //match contains the first character from each category
            string input = "=====()\xAB\xBB!+$^\xA6\u0378======";
            var match1 = re.Match(input);
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(10, match1.Length);
            var match2 = match1.NextMatch();
            Assert.False(match2.Success);
        }

        [Fact]
        public void SRMTest_UnicodeCategories00to29()
        {
            var re = new Regex(@"\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Mn}\p{Mc}\p{Me}\p{Nd}\p{Nl}\p{No}\p{Zs}\p{Zl}\p{Zp}\p{Cc}\p{Cf}\p{Cs}\p{Co}\p{Pc}\p{Pd}\p{Ps}\p{Pe}\p{Pi}\p{Pf}\p{Po}\p{Sm}\p{Sc}\p{Sk}\p{So}\p{Cn}", DFA);
            //match contains the first character from each category
            string input = "=====Aa\u01C5\u02B0\u01BB\u0300\u0903\u04880\u16EE\xB2 \u2028\u2029\0\u0600\uD800\uE000_\x2D()\xAB\xBB!+$^\xA6\u0378======";
            var match1 = re.Match(input);
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index); 
            Assert.Equal(30, match1.Length);
            var match2 = match1.NextMatch();
            Assert.False(match2.Success);
        }

        [Fact]
        public void SRMTest_BV()
        {
             //this will need a total of 68 parts, thus will use the general BV algebra instead of BV64 algebra
            var re = new Regex(@"(abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789<>:;@)+", DFA);
            string input = "=====abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789<>:;@abcdefg======";
            var match1 = re.Match(input);
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(67, match1.Length);
            var match2 = match1.NextMatch();
            Assert.False(match2.Success);
        }

        [Fact]
        public void SRMTest_BV_WideLatin()
        {
            //this will need a total of 2x70 + 2 parts, thus will use the general BV algebra instead of BV64 algebra
            string pattern_orig = @"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789<>:;&@%!";
            //shift each char in the pattern to the Wide-Latin alphabet of Unicode
            //pattern_WL = "ａｂｃｄｅｆｇｈｉｊｋｌｍｎｏｐｑｒｓｔｕｖｗｘｙｚＡＢＣＤＥＦＧＨＩＪＫＬＭＮＯＰＱＲＳＴＵＶＷＸＹＺ０１２３４５６７８９＜＞：；＆＠％！"
            string pattern_WL = new String(Array.ConvertAll(pattern_orig.ToCharArray(), c => (char)((int)c + 0xFF00 - 32)));
            string pattern = "(" + pattern_orig + "===" + pattern_WL + ")+";
            var re = new Regex(pattern, DFA);
            string input = "=====" + pattern_orig + "===" + pattern_WL + pattern_orig + "===" + pattern_WL + "===" + pattern_orig + "===" + pattern_orig;
            var match1 = re.Match(input); 
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(2*(pattern_orig.Length + 3 + pattern_WL.Length), match1.Length);
            var match2 = match1.NextMatch();
            Assert.False(match2.Success);
        }

        [Fact]
        public void SRMTest_BV64_WideLatin()
        {
            string pattern_orig = @"abc";
            //shift each char in the pattern to the Wide-Latin alphabet of Unicode
            //pattern_WL = "ａｂｃ"
            string pattern_WL = new String(Array.ConvertAll(pattern_orig.ToCharArray(), c => (char)((int)c + 0xFF00 - 32)));
            string pattern = "(" + pattern_orig + "===" + pattern_WL + ")+";
            var re = new Regex(pattern, DFA | RegexOptions.IgnoreCase);
            string input = "=====" + pattern_orig.ToUpper() + "===" + pattern_WL + pattern_orig + "===" + pattern_WL.ToUpper() + "===" + pattern_orig + "===" + pattern_orig;
            var match1 = re.Match(input);
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(2 * (pattern_orig.Length + 3 + pattern_WL.Length), match1.Length);
            var match2 = match1.NextMatch();
            Assert.False(match2.Success);
        }

        static string And(params string[] regexes)
        {
            string conj = "(" + regexes[regexes.Length - 1] + ")";
            for (int i= regexes.Length - 2; i >=0; i--)
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
            var re = new Regex(And(".*a.*",".*b.*"), DFA | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            bool ok = re.IsMatch("xxaaxxBxaa");
            Assert.True(ok);
            bool fail = re.IsMatch("xxaaxxcxaa");
            Assert.False(fail);
        }

        [Fact]
        public void SRMTest_ConjuctionFindMatch()
        {
            // contains lower, upper, and a digit, and is between 2 and 4 characters long
            var re = new Regex(And(".*[a-z].*", ".*[A-Z].*", ".*[0-9].*", ".{2,4}"), DFA | RegexOptions.Singleline);
            var match = re.Match("xxaac\n5Bxaa");
            Assert.True(match.Success);
            Assert.Equal(4, match.Index);
            Assert.Equal(4, match.Length);
        }

        [Fact]
        public void SRMTest_ComplementFindMatch()
        {
            // contains lower, upper, and a digit, and is between 4 and 8 characters long, does not contain 2 consequtive digits
            var re = new Regex(And(".*[a-z].*", ".*[A-Z].*", ".*[0-9].*", ".{4,8}",
                Not(".*(01|12|23|34|45|56|67|78|89).*")), DFA | RegexOptions.Singleline);
            var match = re.Match("xxaac12Bxaas3455");
            Assert.True(match.Success);
            Assert.Equal(6, match.Index);
            Assert.Equal(7, match.Length);
        }

        [Fact]
        public void SRMTest_LazyLoopMatch()
        {
            //eager
            var re = new Regex("a[0-9]+0", DFA);
            var match = re.Match("ababca123000xyz");
            Assert.True(match.Success);
            Assert.Equal(5, match.Index);
            Assert.Equal(7, match.Length);
            //---
            //lazy
            var reL = new Regex("a[0-9]+?0", DFA);
            var matchL = reL.Match("ababca123000xyz");
            Assert.True(matchL.Success);
            Assert.Equal(5, matchL.Index);
            Assert.Equal(5, matchL.Length);
            //lazy and eager combined
            var re2 = new Regex("a[0-9]+?0|b[0-9]+0", DFA);
            var match21 = re2.Match("ababca123000xyzababcb123000xyz");
            //the first match starting with 'a' uses lazy loop
            Assert.True(match21.Success);
            Assert.Equal(5, match21.Index);
            Assert.Equal(5, match21.Length);
            //the second match starting with 'b' uses eager loop
            var match22 = match21.NextMatch();
            Assert.True(match22.Success);
            Assert.Equal(20, match22.Index);
            Assert.Equal(7, match22.Length);
        }

        [Fact]
        public void SRMTest_NestedLazyLoop()
        {
            //read lazily blocks of 3 x's at a time
            var re = new Regex("(x{3})+?", DFA);
            var match = re.Match("abcxxxxxxxxacacaca");
            Assert.True(match.Success);
            Assert.Equal(3, match.Index);
            Assert.Equal(3, match.Length);
        }

        [Fact]
        public void SRMTest_NestedEagerLoop()
        {
            //read eagerly blocks of 3 x's at a time
            var re = new Regex("(x{3})+", DFA);
            var match = re.Match("abcxxxxxxxxacacaca");
            Assert.True(match.Success);
            Assert.Equal(3, match.Index);
            Assert.Equal(6, match.Length);
        }

        [Fact]
        public void SRMTest_CountedLazyLoop()
        {
            var re = new Regex("a[bcd]{4,5}?(.)", DFA);
            var match = re.Match("acdbcdbe");
            Assert.True(match.Success);
            Assert.Equal(0, match.Index);
            // lazy loop [bcd]{4,5}? only needs to iterate 4 times
            Assert.Equal(6, match.Length);
        }

        [Fact]
        public void SRMTest_CountedLoop()
        {
            //eager matching of the loop must match 5 elements in the loop
            var re = new Regex("a[bcd]{4,5}(.)", DFA);
            var match = re.Match("acdbcdbe");
            Assert.True(match.Success);
            Assert.Equal(0, match.Index);
            Assert.Equal(7, match.Length);
        }

        [Fact]
        public void SRMTest_NewLine()
        {
            var re = new Regex("\n", DFA);
            var match = re.Match("\n");
            Assert.True(match.Success);
            Assert.Equal(0, match.Index);
            Assert.Equal(1, match.Length);
            //---
            var re2 = new Regex("[^a]", DFA);
            var match2 = re2.Match("\n");
            Assert.True(match2.Success);
            Assert.Equal(0, match2.Index);
            Assert.Equal(1, match2.Length);
        }

        [Theory]
        [MemberData(nameof(ValidateSRMRegex_NotSupportedCases_Data))]
        public void ValidateSRMRegex_NotSupportedCases(string pattern, RegexOptions options, string expected)
        {
            string actual = string.Empty;
            try
            {
                new Regex(pattern, options | DFA);
            }
            catch (Exception e)
            {
                actual = e.Message;
            }
            Assert.Contains(expected, actual);
        }

        /// <summary>
        /// Nonsupported cases for the DFA option that are detected at Regex construction time
        /// </summary>
        public static IEnumerable<object[]> ValidateSRMRegex_NotSupportedCases_Data()
        {
            yield return new object[] { @"abc", RegexOptions.RightToLeft, "RightToLeft" };
            yield return new object[] { @"abc", RegexOptions.ECMAScript, "ECMAScript" };
            yield return new object[] { @"^(a)?(?(1)a|b)+$", RegexOptions.None, "captured group conditional" };
            yield return new object[] { @"(abc)\1", RegexOptions.None, "backreference" };
            yield return new object[] { @"a(?=d).", RegexOptions.None, "positive lookahead" };
            yield return new object[] { @"a(?!b).", RegexOptions.None, "negative lookahead" };
            yield return new object[] { @"(?<=a)b", RegexOptions.None, "positive lookbehind" };
            yield return new object[] { @"(?<!c)b", RegexOptions.None, "negative lookbehind" };
            yield return new object[] { @"(?>(abc)*).", RegexOptions.None, "atomic" };
            yield return new object[] { @"\G(\w+\s?\w*),?", RegexOptions.None, "contiguous matches" };
            yield return new object[] { @"(?>a*).", RegexOptions.None, "atomic" };
        }
    }
}
