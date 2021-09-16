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

        private const char Turkish_I_withDot = '\u0130';
        private const char Turkish_i_withoutDot = '\u0131';
        private const char Kelvin_sign = '\u212A';

        /// <summary>
        /// Specific cases that are very slow/difficult with backtracking but fast/easy without backtracking
        /// </summary>
        [Theory]
        [InlineData("((?:0*)+?(?:.*)+?)?", "0a", 2)]
        [InlineData("(?:(?:0?)+?(?:a?)+?)?", "0a", 2)]
        [InlineData(@"(?i:(\()((?<a>\w+(\.\w+)*)(,(?<a>\w+(\.\w+)*)*)?)(\)))", "some.text(this.is,the.match)", 1)]
        private void TestDifficultCasesForBacktracking(string pattern, string input, int matchcount)
        {
            var regex = new Regex(pattern, RegexOptions.NonBacktracking);
            List<Match> matches = new List<Match>();
            var match = regex.Match(input);
            while (match.Success)
            {
                matches.Add(match);
                match = match.NextMatch();
            }
            Assert.Equal(matchcount, matches.Count);
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

                var elems = new List<char>(s);
                elems.Sort();
                return new string(elems.ToArray());
            };

            for (int i = 0; i <= 0xFFFF; i++)
            {
                string s1 = F(table1[i], i);
                string s2 = F(table2[i], i);
                if (s1 != s2)
                {
                    diffs.Add($"{(char)i}:{s1}/{s2}");
                }
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
                Assert.False(Regex.IsMatch(c.ToString(), cU.ToString(), RegexOptions.IgnoreCase | RegexOptions.NonBacktracking));
            }

            Assert.False(Regex.IsMatch(Turkish_i_withoutDot.ToString(), "i", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking));
            Assert.True(Regex.IsMatch(Turkish_I_withDot.ToString(), "i", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking));
            Assert.True(Regex.IsMatch(Turkish_I_withDot.ToString(), "i", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking));
            Assert.False(Regex.IsMatch(Turkish_I_withDot.ToString(), "i", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant));

            // Turkish i without dot is not considered case-sensitive in the default en-US culture
            treatedAsCaseInsensitive.Add(Turkish_i_withoutDot);

            List<char> caseSensitiveChars = new();
            for (char c = '\0'; c < '\uFFFF'; c++)
                if (!treatedAsCaseInsensitive.Contains(c) && char.ToUpper(c) != char.ToLower(c))
                    caseSensitiveChars.Add(c);

            // test all case-sensitive characters exhaustively in DFA mode
            foreach (char c in caseSensitiveChars)
                Assert.True(Regex.IsMatch(char.ToUpper(c).ToString() + char.ToLower(c).ToString(),
                    c.ToString() + c.ToString(), RegexOptions.IgnoreCase | RegexOptions.NonBacktracking));
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

        [Theory]
        [InlineData("[abc]{0,10}", "a[abc]{0,3}", "xxxabbbbbbbyyy", true, "abbb")]
        [InlineData("[abc]{0,10}?", "a[abc]{0,3}?", "xxxabbbbbbbyyy", true, "a")]
        public void TestConjunctionOverCounting(string conjunct1, string conjunct2, string input, bool success, string match)
        {
            string pattern = And(conjunct1, conjunct2);
            Regex re = new Regex(pattern, RegexOptions.NonBacktracking);
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
            Regex re = new Regex(pattern, RegexOptions.NonBacktracking);
            Match m = re.Match(input);
            Assert.Equal(success, m.Success);
            Assert.Equal(match, m.Value);
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
            //equivalent regex as DFA
            string rawregex_alt = @"(the)\s*([12][0-9]|3[01]|0?[1-9])";
            var re_alt = new Regex(rawregex_alt, RegexOptions.NonBacktracking);
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
            Regex re = new Regex(rawregex, RegexOptions.NonBacktracking, new TimeSpan(0, 0, 0, 0, 100));
            //it takes over 4min with backtracking, so 1sec times out for sure
            Assert.Throws<RegexMatchTimeoutException>(() => { reC.Match(input); });
            //DFA needs less than 100ms
            Assert.False(re.Match(input).Success);
        }

        /// <summary>
        /// Test that timeout is being checked in DFA mode.
        /// </summary>
        [Fact]
        public void TestSRMTimeout() => TestSRMTimeout_(new Regex(@"a.{20}$", RegexOptions.NonBacktracking, new TimeSpan(0, 0, 0, 0, 10)));

        /// <summary>
        /// Test that serialization preserves timeout information.
        /// </summary>
        [Fact]
        public void TestSRMTimeoutAfterDeser() => TestSRMTimeout_(Deserialize(Serialize(new Regex(@"a.{20}$", RegexOptions.NonBacktracking, new TimeSpan(0, 0, 0, 0, 10)))));

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
            var re = new Regex(".*a+", RegexOptions.NonBacktracking | RegexOptions.Singleline);
            // RegexExperiment.ViewDGML(re, name : "TestDGMLGeneration");
            SaveDGML(re, sw);
            string str = sw.ToString();
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", str);
            Assert.Contains("DirectedGraph", str);
            // in debug mode re may be serialized and then deserialized internally 
            // if that happens the predicate label .*a+ becomes .*[2]+
            // the partition of characters in this regex is into two sets (in binary):
            //   01 or [1] representing [^a]  (first part)
            //   10 or [2] representinng 'a' (second part) which is where 2 comes from
            //   (3rd part would be 100 = [4], 4th 1000 = [8] etc)
            // '.' here is the union of all parts, i.e. 01 and 10 that is 11 (in binary internally) but printed as '.' also.
            Assert.True(str.Contains(".*a+") || str.Contains(".*[2]+"));
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
            var rs = Array.ConvertAll(rawregexes, s => new Regex(s, RegexOptions.NonBacktracking));
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
                Assert.True(ser[i] == ser2[i], $"idempotence of serialization of regex {i} failed");

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
        public void SRMTest_BV()
        {
             //this will need a total of 68 parts, thus will use the general BV algebra instead of BV64 algebra
            var re = new Regex(@"(abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789<>:;@)+", RegexOptions.NonBacktracking);
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
            var re = new Regex(pattern, RegexOptions.NonBacktracking);
            string input = "=====" + pattern_orig + "===" + pattern_WL + pattern_orig + "===" + pattern_WL + "===" + pattern_orig + "===" + pattern_orig;
            var match1 = re.Match(input); 
            Assert.True(match1.Success);
            Assert.Equal(5, match1.Index);
            Assert.Equal(2*(pattern_orig.Length + 3 + pattern_WL.Length), match1.Length);
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
            var re = new Regex(And(".*a.*",".*b.*"), RegexOptions.NonBacktracking | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            bool ok = re.IsMatch("xxaaxxBxaa");
            Assert.True(ok);
            bool fail = re.IsMatch("xxaaxxcxaa");
            Assert.False(fail);
        }

        [Fact]
        public void SRMTest_ConjuctionFindMatch()
        {
            // contains lower, upper, and a digit, and is between 2 and 4 characters long
            var re = new Regex(And(".*[a-z].*", ".*[A-Z].*", ".*[0-9].*", ".{2,4}"), RegexOptions.NonBacktracking | RegexOptions.Singleline);
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
                Not(".*(01|12|23|34|45|56|67|78|89).*")), RegexOptions.NonBacktracking | RegexOptions.Singleline);
            var match = re.Match("xxaac12Bxaas3455");
            Assert.True(match.Success);
            Assert.Equal(6, match.Index);
            Assert.Equal(7, match.Length);
        }

        [Fact]
        public void PasswordSearch()
        {
            var twoLower = ".*[a-z].*[a-z].*";
            var twoUpper = ".*[A-Z].*[A-Z].*";
            var threeDigits = ".*[0-9].*[0-9].*[0-9].*";
            var oneSpecial = @".*[\x21-\x2F\x3A-\x40\x5B-x60\x7B-\x7E].*";
            var noCountUp = Not(".*(012|123|234|345|456|567|678|789).*");
            var noCountDown = Not(".*(987|876|765|654|543|432|321|210).*");
            // Observe that the space character (immediately before '!' in ASCII) is excluded
            var length = "[!-~]{8,12}";
 
           // Just to make the chance that the randomly generated part actually has a match
           // be astronomically unlikely require 'P' and 'r' to be present also,
           // although this constraint is really bogus from password constraints point of view
            var contains_first_P_and_then_r = ".*P.*r.*";

            // Conjunction of all the above constraints
            var all = And(twoLower, twoUpper, threeDigits, oneSpecial, noCountUp, noCountDown, length, contains_first_P_and_then_r);

            // search for the password in a context surrounded by word boundaries
            var re = new Regex($@"\b{all}\b", RegexOptions.NonBacktracking | RegexOptions.Singleline);

            // Does not qualify because of 123 and connot end between 2 and 3 because of \b
            var almostPassw1 = "P@ssW0rd123";
            // Does not have at least two uppercase
            var almostPassw2 = "P@55w0rd";

            // These two qualify
            var password1 = "P@55W0rd";
            var password2 = "Pa5$w00rD";

            foreach (int k in new int[] {500, 1000, 5000, 10000, 50000, 100000})
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

        [Fact]
        public void PasswordSearchDual()
        {
            var Not_twoLower = Not(".*[a-z].*[a-z].*");
            var Not_twoUpper = Not(".*[A-Z].*[A-Z].*");
            var Not_threeDigits = Not(".*[0-9].*[0-9].*[0-9].*");
            var Not_oneSpecial = Not(@".*[\x21-\x2F\x3A-\x40\x5B-x60\x7B-\x7E].*");
            var countUp = ".*(012|123|234|345|456|567|678|789).*";
            var countDown = ".*(987|876|765|654|543|432|321|210).*";
            // Observe that the space character (immediately before '!' in ASCII) is excluded
            var Not_length = Not("[!-~]{8,12}");

            // Just to make the chance that the randomly generated part actually has a match
            // be astronomically unlikely require 'P' and 'r' to be present also,
            // although this constraint is really bogus from password constraints point of view
            var Not_contains_first_P_and_then_r = Not(".*P.*r.*");

            // Negated disjunction of all the above constraints
            // By deMorgan's laws we know that ~(A|B|...|C) = ~A&~B&...&~C and ~~A = A
            // So Not(Not_twoLower|...) is equivalent to twoLower&~(...)
            var all = Not($"{Not_twoLower}|{Not_twoUpper}|{Not_threeDigits}|{Not_oneSpecial}|{countUp}|{countDown}|{Not_length}|{Not_contains_first_P_and_then_r}");

            // search for the password in a context surrounded by word boundaries
            var re = new Regex($@"\b{all}\b", RegexOptions.NonBacktracking | RegexOptions.Singleline);

            // Does not qualify because of 123 and connot end between 2 and 3 because of \b
            var almostPassw1 = "P@ssW0rd123";
            // Does not have at least two uppercase
            var almostPassw2 = "P@55w0rd";

            // These two qualify
            var password1 = "P@55W0rd";
            var password2 = "Pa5$w00rD";

            foreach (int k in new int[] { 500, 1000, 5000, 10000, 50000, 100000})
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
    }
}
