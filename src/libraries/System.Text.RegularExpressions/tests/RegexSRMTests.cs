// Licensed to the .NET Foundation under one or more agreements.if #DEBUG
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

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

        internal static RegexOptions DFA = (RegexOptions)0x400;

        //for some reason the following two tests always fail as part of a larger test run and claim that timeout exception never occurs
        //while they succeed individually, perhaps a bug in the unit testing framework
        //thus commenting them out as unit tests
        //[Fact]
        //public void TestSRMTimeout() => TestSRMTimeout_(new Regex(@"a.{20}", DFA, new TimeSpan(0, 0, 0, 0, 1)));
        //[Fact]
        //public void TestSRMTimeoutAfterDeser() => TestSRMTimeout_(Deserialize(Serialize(new Regex(@"a.{20}", DFA, new TimeSpan(0, 0, 0, 0, 1)))));


        private void TestSRMTimeout_(Regex re)
        {
            Random rnd = new Random(0);
            byte[] buffer = new byte[1000000];
            rnd.NextBytes(buffer);
            //random 1MB string with a lot of a's
            string input = new String(Array.ConvertAll(buffer, b => b < 10 ? 'a' : (char)b));
            Type timeoutExceptionType = typeof(TimeoutException);
            Assert.Throws(timeoutExceptionType, () => { re.Match(input); });
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
        public void BasicSRMTestBorderAnchors()
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
            //---
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
            return $"(?({regex})[0-[0]]|.*)";
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

        static int _NotSupportedException_count;
        [Theory]
        [MemberData(nameof(MonoTests_RegexTestCases)), MemberData(nameof(MonoTests_RegexTestCases))]
        public void ValidateSRMRegex_Monotests(string pattern, RegexOptions options, string input, string expected)
        {
            string result;
            string result_isMatch;
            try
            {
                var re = new Regex(pattern, options | DFA);
                var match = re.Match(input);
                result_isMatch = (re.IsMatch(input) ? "Pass." : "Fail.");

                if (match.Success)
                {
                    result = "Pass.";
                    result += $" Group[0]=({match.Index},{match.Length})";
                }
                else
                {
                    result = "Fail.";
                }
            }
            catch (ArgumentException)
            {
                result = "Error.";
                result_isMatch = "Error.";
            }
            catch (NotSupportedException nse)
            {
                // incompatible with DFA option
                _NotSupportedException_count += 1;
                WriteLine(_NotSupportedException_count + ":" + nse.Message);

                string[] possible_errors = new string[]
                {"RightToLeft", "conditional", "lookahead", "lookbehind", "backreference",
                    "atomic", "contiguous", "characterless", "0-length match", "ECMAScript"};

                Assert.True(Array.Exists(possible_errors, nse.Message.Contains));

                // make also sure that "?>" appears in the original regex
                // if the error claims that the regex contains an atomic loop
                // this is to make sure that internal lifting of nonatomic loops to atomic loops 
                // (to avoid backtracking) is not enabled when the DFA option is used
                if (nse.Message.Contains("atomic"))
                {
                    Assert.Contains("?>", pattern);
                }

                // make sure that the test reex is just an anchor here
                if (nse.Message.Contains("characterless"))
                {
                    Assert.True(pattern == "^" || pattern == "$" || pattern == "\\z" || pattern == "\\Z"
                        || pattern == "\\A" || pattern == "^(){3,5}" || pattern == "(?i)");
                }

                return;
            }
            catch (Exception ex)
            {
                result = ex.ToString();
                result_isMatch = result;
            }

            if (!expected.StartsWith(result))
                WriteLine(expected + " <> " + result);

            // capture groups not supported, so validate Pass/Fail and Group[0]
            Assert.StartsWith(result, expected, StringComparison.Ordinal);
            Assert.StartsWith(result_isMatch, expected, StringComparison.Ordinal);
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
            yield return new object[] { @"\A(abc)*\Z", RegexOptions.None, "0-length match" };
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
            yield return new object[] { @"^(){3,5}", RegexOptions.None, "characterless" };
            yield return new object[] { @"^", RegexOptions.None, "characterless" };
            yield return new object[] { @"\Z", RegexOptions.None, "characterless" };
            yield return new object[] { @"$", RegexOptions.None, "characterless" };
            yield return new object[] { @"\z", RegexOptions.None, "characterless" };
            yield return new object[] { @"\b", RegexOptions.None, "characterless" };
            yield return new object[] { @"\B", RegexOptions.None, "characterless" };
            yield return new object[] { @"\A\Z", RegexOptions.None, "characterless" }; 
        }

        /// <summary>
        /// Copy of MonoTests.RegexTestCases, order is rearranged, tricky ones (that exposed issues/bugs) come first
        /// </summary>
        public static IEnumerable<object[]> MonoTests_RegexTestCases()
        {
            //-----------
            //tricky ones:
            yield return new object[] { @"(?:..)*?a", RegexOptions.None, "aba", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"(^|x)(c)", RegexOptions.None, "ca", "Pass. Group[0]=(0,1) Group[1]=(0,0) Group[2]=(0,1)" };
            yield return new object[] { @"((?s)^a(.))((?m)^b$)", RegexOptions.None, "a\nb\nc\n", "Pass. Group[0]=(0,3) Group[1]=(0,2) Group[2]=(1,1) Group[3]=(2,1)" };
            yield return new object[] { @"((?s-i:a.))b", RegexOptions.IgnoreCase, "a\nB", "Pass. Group[0]=(0,3) Group[1]=(0,2)" };
            yield return new object[] { @"(?>a*).", RegexOptions.ExplicitCapture, "aaaa", "Fail." };
            //----------
            yield return new object[] { @"abc", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc", RegexOptions.None, "xbc", "Fail." };
            yield return new object[] { @"abc", RegexOptions.None, "axc", "Fail." };
            yield return new object[] { @"abc", RegexOptions.None, "abx", "Fail." };
            yield return new object[] { @"abc", RegexOptions.None, "xabcy", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"abc", RegexOptions.None, "ababc", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"ab*c", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab*bc", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab*bc", RegexOptions.None, "abbc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab*bc", RegexOptions.None, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @".{1}", RegexOptions.None, "abbbbc", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @".{3,4}", RegexOptions.None, "abbbbc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab{0,}bc", RegexOptions.None, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab+bc", RegexOptions.None, "abbc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab+bc", RegexOptions.None, "abc", "Fail." };
            yield return new object[] { @"ab+bc", RegexOptions.None, "abq", "Fail." };
            yield return new object[] { @"ab{1,}bc", RegexOptions.None, "abq", "Fail." };
            yield return new object[] { @"ab+bc", RegexOptions.None, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{1,}bc", RegexOptions.None, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{1,3}bc", RegexOptions.None, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{3,4}bc", RegexOptions.None, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{4,5}bc", RegexOptions.None, "abbbbc", "Fail." };
            yield return new object[] { @"ab?bc", RegexOptions.None, "abbc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab?bc", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab{0,1}bc", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab?bc", RegexOptions.None, "abbbbc", "Fail." };
            yield return new object[] { @"ab?c", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab{0,1}c", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^abc$", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^abc$", RegexOptions.None, "abcc", "Fail." };
            yield return new object[] { @"^abc", RegexOptions.None, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^abc$", RegexOptions.None, "aabc", "Fail." };
            yield return new object[] { @"abc$", RegexOptions.None, "aabc", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"abc$", RegexOptions.None, "aabcd", "Fail." };
            yield return new object[] { @"^", RegexOptions.None, "abc", "Pass. Group[0]=(0,0)" };
            yield return new object[] { @"$", RegexOptions.None, "abc", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"a.c", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a.c", RegexOptions.None, "axc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a.*c", RegexOptions.None, "axyzc", "Pass. Group[0]=(0,5)" };
            yield return new object[] { @"a.*c", RegexOptions.None, "axyzd", "Fail." };
            yield return new object[] { @"a[bc]d", RegexOptions.None, "abc", "Fail." };
            yield return new object[] { @"a[bc]d", RegexOptions.None, "abd", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[b-d]e", RegexOptions.None, "abd", "Fail." };
            yield return new object[] { @"a[b-d]e", RegexOptions.None, "ace", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[b-d]", RegexOptions.None, "aac", "Pass. Group[0]=(1,2)" };
            yield return new object[] { @"a[-b]", RegexOptions.None, "a-", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a[b-]", RegexOptions.None, "a-", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a[b-a]", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"a[]b", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"a[", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"a]", RegexOptions.None, "a]", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a[]]b", RegexOptions.None, "a]b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[^bc]d", RegexOptions.None, "aed", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[^bc]d", RegexOptions.None, "abd", "Fail." };
            yield return new object[] { @"a[^-b]c", RegexOptions.None, "adc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[^-b]c", RegexOptions.None, "a-c", "Fail." };
            yield return new object[] { @"a[^]b]c", RegexOptions.None, "a]c", "Fail." };
            yield return new object[] { @"a[^]b]c", RegexOptions.None, "adc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"\ba\b", RegexOptions.None, "a-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\ba\b", RegexOptions.None, "-a", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"\ba\b", RegexOptions.None, "-a-", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"\by\b", RegexOptions.None, "xy", "Fail." };
            yield return new object[] { @"\by\b", RegexOptions.None, "yz", "Fail." };
            yield return new object[] { @"\by\b", RegexOptions.None, "xyz", "Fail." };
            yield return new object[] { @"\Ba\B", RegexOptions.None, "a-", "Fail." };
            yield return new object[] { @"\Ba\B", RegexOptions.None, "-a", "Fail." };
            yield return new object[] { @"\Ba\B", RegexOptions.None, "-a-", "Fail." };
            yield return new object[] { @"\By\b", RegexOptions.None, "xy", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"\by\B", RegexOptions.None, "yz", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\By\B", RegexOptions.None, "xyz", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"\w", RegexOptions.None, "a", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\w", RegexOptions.None, "-", "Fail." };
            yield return new object[] { @"\W", RegexOptions.None, "a", "Fail." };
            yield return new object[] { @"\W", RegexOptions.None, "-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"a\sb", RegexOptions.None, "a b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a\sb", RegexOptions.None, "a-b", "Fail." };
            yield return new object[] { @"a\Sb", RegexOptions.None, "a b", "Fail." };
            yield return new object[] { @"a\Sb", RegexOptions.None, "a-b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"\d", RegexOptions.None, "1", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\d", RegexOptions.None, "-", "Fail." };
            yield return new object[] { @"\D", RegexOptions.None, "1", "Fail." };
            yield return new object[] { @"\D", RegexOptions.None, "-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"[\w]", RegexOptions.None, "a", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"[\w]", RegexOptions.None, "-", "Fail." };
            yield return new object[] { @"[\W]", RegexOptions.None, "a", "Fail." };
            yield return new object[] { @"[\W]", RegexOptions.None, "-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"a[\s]b", RegexOptions.None, "a b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[\s]b", RegexOptions.None, "a-b", "Fail." };
            yield return new object[] { @"a[\S]b", RegexOptions.None, "a b", "Fail." };
            yield return new object[] { @"a[\S]b", RegexOptions.None, "a-b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"[\d]", RegexOptions.None, "1", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"[\d]", RegexOptions.None, "-", "Fail." };
            yield return new object[] { @"[\D]", RegexOptions.None, "1", "Fail." };
            yield return new object[] { @"[\D]", RegexOptions.None, "-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"ab|cd", RegexOptions.None, "abc", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"ab|cd", RegexOptions.None, "abcd", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"()ef", RegexOptions.None, "def", "Pass. Group[0]=(1,2) Group[1]=(1,0)" };
            yield return new object[] { @"*a", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"(*)b", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"$b", RegexOptions.None, "b", "Fail." };
            yield return new object[] { @"a\", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"a\(b", RegexOptions.None, "a(b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a\(*b", RegexOptions.None, "ab", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a\(*b", RegexOptions.None, "a((b", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"a\\b", RegexOptions.None, "a\\b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc)", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"(abc", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"((a))", RegexOptions.None, "abc", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=(0,1)" };
            yield return new object[] { @"(a)b(c)", RegexOptions.None, "abc", "Pass. Group[0]=(0,3) Group[1]=(0,1) Group[2]=(2,1)" };
            yield return new object[] { @"a+b+c", RegexOptions.None, "aabbabc", "Pass. Group[0]=(4,3)" };
            yield return new object[] { @"a{1,}b{1,}c", RegexOptions.None, "aabbabc", "Pass. Group[0]=(4,3)" };
            yield return new object[] { @"a**", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"a.+?c", RegexOptions.None, "abcabc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"(a+|b)*", RegexOptions.None, "ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)(1,1)" };
            yield return new object[] { @"(a+|b){0,}", RegexOptions.None, "ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)(1,1)" };
            yield return new object[] { @"(a+|b)+", RegexOptions.None, "ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)(1,1)" };
            yield return new object[] { @"(a+|b){1,}", RegexOptions.None, "ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)(1,1)" };
            yield return new object[] { @"(a+|b)?", RegexOptions.None, "ab", "Pass. Group[0]=(0,1) Group[1]=(0,1)" };
            yield return new object[] { @"(a+|b){0,1}", RegexOptions.None, "ab", "Pass. Group[0]=(0,1) Group[1]=(0,1)" };
            yield return new object[] { @")(", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"[^ab]*", RegexOptions.None, "cde", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc", RegexOptions.None, "", "Fail." };
            yield return new object[] { @"a*", RegexOptions.None, "", "Pass. Group[0]=(0,0)" };
            yield return new object[] { @"([abc])*d", RegexOptions.None, "abbbcd", "Pass. Group[0]=(0,6) Group[1]=(0,1)(1,1)(2,1)(3,1)(4,1)" };
            yield return new object[] { @"([abc])*bcd", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4) Group[1]=(0,1)" };
            yield return new object[] { @"a|b|c|d|e", RegexOptions.None, "e", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"(a|b|c|d|e)f", RegexOptions.None, "ef", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"abcd*efg", RegexOptions.None, "abcdefg", "Pass. Group[0]=(0,7)" };
            yield return new object[] { @"ab*", RegexOptions.None, "xabyabbbz", "Pass. Group[0]=(1,2)" };
            yield return new object[] { @"ab*", RegexOptions.None, "xayabbbz", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"(ab|cd)e", RegexOptions.None, "abcde", "Pass. Group[0]=(2,3) Group[1]=(2,2)" };
            yield return new object[] { @"[abhgefdc]ij", RegexOptions.None, "hij", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^(ab|cd)e", RegexOptions.None, "abcde", "Fail." };
            yield return new object[] { @"(abc|)ef", RegexOptions.None, "abcdef", "Pass. Group[0]=(4,2) Group[1]=(4,0)" };
            yield return new object[] { @"(a|b)c*d", RegexOptions.None, "abcd", "Pass. Group[0]=(1,3) Group[1]=(1,1)" };
            yield return new object[] { @"(ab|ab*)bc", RegexOptions.None, "abc", "Pass. Group[0]=(0,3) Group[1]=(0,1)" };
            yield return new object[] { @"a([bc]*)c*", RegexOptions.None, "abc", "Pass. Group[0]=(0,3) Group[1]=(1,2)" };
            yield return new object[] { @"a([bc]*)(c*d)", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4) Group[1]=(1,2) Group[2]=(3,1)" };
            yield return new object[] { @"a([bc]+)(c*d)", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4) Group[1]=(1,2) Group[2]=(3,1)" };
            yield return new object[] { @"a([bc]*)(c+d)", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4) Group[1]=(1,1) Group[2]=(2,2)" };
            yield return new object[] { @"a[bcd]*dcdcde", RegexOptions.None, "adcdcde", "Pass. Group[0]=(0,7)" };
            yield return new object[] { @"a[bcd]+dcdcde", RegexOptions.None, "adcdcde", "Fail." };
            yield return new object[] { @"(ab|a)b*c", RegexOptions.None, "abc", "Pass. Group[0]=(0,3) Group[1]=(0,2)" };
            yield return new object[] { @"((a)(b)c)(d)", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4) Group[1]=(0,3) Group[2]=(0,1) Group[3]=(1,1) Group[4]=(3,1)" };
            yield return new object[] { @"[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.None, "alpha", "Pass. Group[0]=(0,5)" };
            yield return new object[] { @"^a(bc+|b[eh])g|.h$", RegexOptions.None, "abh", "Pass. Group[0]=(1,2) Group[1]=" };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.None, "effgz", "Pass. Group[0]=(0,5) Group[1]=(0,5) Group[2]=" };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.None, "ij", "Pass. Group[0]=(0,2) Group[1]=(0,2) Group[2]=(1,1)" };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.None, "effg", "Fail." };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.None, "bcdd", "Fail." };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.None, "reffgz", "Pass. Group[0]=(1,5) Group[1]=(1,5) Group[2]=" };
            yield return new object[] { @"((((((((((a))))))))))", RegexOptions.None, "a", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1) Group[10]=(0,1)" };
            yield return new object[] { @"((((((((((a))))))))))\10", RegexOptions.None, "aa", "Pass. Group[0]=(0,2) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1) Group[10]=(0,1)" };
            yield return new object[] { @"((((((((((a))))))))))!", RegexOptions.None, "aa", "Fail." };
            yield return new object[] { @"((((((((((a))))))))))!", RegexOptions.None, "a!", "Pass. Group[0]=(0,2) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1) Group[10]=(0,1)" };
            yield return new object[] { @"(((((((((a)))))))))", RegexOptions.None, "a", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1)" };
            yield return new object[] { @"multiple words of text", RegexOptions.None, "uh-uh", "Fail." };
            yield return new object[] { @"multiple words", RegexOptions.None, "multiple words, yeah", "Pass. Group[0]=(0,14)" };
            yield return new object[] { @"(.*)c(.*)", RegexOptions.None, "abcde", "Pass. Group[0]=(0,5) Group[1]=(0,2) Group[2]=(3,2)" };
            yield return new object[] { @"\((.*), (.*)\)", RegexOptions.None, "(a, b)", "Pass. Group[0]=(0,6) Group[1]=(1,1) Group[2]=(4,1)" };
            yield return new object[] { @"[k]", RegexOptions.None, "ab", "Fail." };
            yield return new object[] { @"abcd", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"a(bc)d", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4) Group[1]=(1,2)" };
            yield return new object[] { @"a[-]?c", RegexOptions.None, "ac", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"(abc)\1", RegexOptions.None, "abcabc", "Pass. Group[0]=(0,6) Group[1]=(0,3)" };
            yield return new object[] { @"([a-c]*)\1", RegexOptions.None, "abcabc", "Pass. Group[0]=(0,6) Group[1]=(0,3)" };
            yield return new object[] { @"\1", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"\2", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"(a)|\1", RegexOptions.None, "a", "Pass. Group[0]=(0,1) Group[1]=(0,1)" };
            yield return new object[] { @"(a)|\1", RegexOptions.None, "x", "Fail." };
            yield return new object[] { @"(a)|\2", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"(([a-c])b*?\2)*", RegexOptions.None, "ababbbcbc", "Pass. Group[0]=(0,5) Group[1]=(0,3)(3,2) Group[2]=(0,1)(3,1)" };
            yield return new object[] { @"(([a-c])b*?\2){3}", RegexOptions.None, "ababbbcbc", "Pass. Group[0]=(0,9) Group[1]=(0,3)(3,3)(6,3) Group[2]=(0,1)(3,1)(6,1)" };
            yield return new object[] { @"((\3|b)\2(a)x)+", RegexOptions.None, "aaxabxbaxbbx", "Fail." };
            yield return new object[] { @"((\3|b)\2(a)x)+", RegexOptions.None, "aaaxabaxbaaxbbax", "Pass. Group[0]=(12,4) Group[1]=(12,4) Group[2]=(12,1) Group[3]=(14,1)" };
            yield return new object[] { @"((\3|b)\2(a)){2,}", RegexOptions.None, "bbaababbabaaaaabbaaaabba", "Pass. Group[0]=(15,9) Group[1]=(15,3)(18,3)(21,3) Group[2]=(15,1)(18,1)(21,1) Group[3]=(17,1)(20,1)(23,1)" };
            yield return new object[] { @"abc", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc", RegexOptions.IgnoreCase, "XBC", "Fail." };
            yield return new object[] { @"abc", RegexOptions.IgnoreCase, "AXC", "Fail." };
            yield return new object[] { @"abc", RegexOptions.IgnoreCase, "ABX", "Fail." };
            yield return new object[] { @"abc", RegexOptions.IgnoreCase, "XABCY", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"abc", RegexOptions.IgnoreCase, "ABABC", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"ab*c", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab*bc", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab*bc", RegexOptions.IgnoreCase, "ABBC", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab*?bc", RegexOptions.IgnoreCase, "ABBBBC", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{0,}?bc", RegexOptions.IgnoreCase, "ABBBBC", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab+?bc", RegexOptions.IgnoreCase, "ABBC", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab+bc", RegexOptions.IgnoreCase, "ABC", "Fail." };
            yield return new object[] { @"ab+bc", RegexOptions.IgnoreCase, "ABQ", "Fail." };
            yield return new object[] { @"ab{1,}bc", RegexOptions.IgnoreCase, "ABQ", "Fail." };
            yield return new object[] { @"ab+bc", RegexOptions.IgnoreCase, "ABBBBC", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{1,}?bc", RegexOptions.IgnoreCase, "ABBBBC", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{1,3}?bc", RegexOptions.IgnoreCase, "ABBBBC", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{3,4}?bc", RegexOptions.IgnoreCase, "ABBBBC", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{4,5}?bc", RegexOptions.IgnoreCase, "ABBBBC", "Fail." };
            yield return new object[] { @"ab??bc", RegexOptions.IgnoreCase, "ABBC", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab??bc", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab{0,1}?bc", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab??bc", RegexOptions.IgnoreCase, "ABBBBC", "Fail." };
            yield return new object[] { @"ab??c", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab{0,1}?c", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^abc$", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^abc$", RegexOptions.IgnoreCase, "ABCC", "Fail." };
            yield return new object[] { @"^abc", RegexOptions.IgnoreCase, "ABCC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^abc$", RegexOptions.IgnoreCase, "AABC", "Fail." };
            yield return new object[] { @"abc$", RegexOptions.IgnoreCase, "AABC", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"^", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,0)" };
            yield return new object[] { @"$", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"a.c", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a.c", RegexOptions.IgnoreCase, "AXC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a.*?c", RegexOptions.IgnoreCase, "AXYZC", "Pass. Group[0]=(0,5)" };
            yield return new object[] { @"a.*c", RegexOptions.IgnoreCase, "AXYZD", "Fail." };
            yield return new object[] { @"a[bc]d", RegexOptions.IgnoreCase, "ABC", "Fail." };
            yield return new object[] { @"a[bc]d", RegexOptions.IgnoreCase, "ABD", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[b-d]e", RegexOptions.IgnoreCase, "ABD", "Fail." };
            yield return new object[] { @"a[b-d]e", RegexOptions.IgnoreCase, "ACE", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[b-d]", RegexOptions.IgnoreCase, "AAC", "Pass. Group[0]=(1,2)" };
            yield return new object[] { @"a[-b]", RegexOptions.IgnoreCase, "A-", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a[b-]", RegexOptions.IgnoreCase, "A-", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a[b-a]", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"a[]b", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"a[", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"a]", RegexOptions.IgnoreCase, "A]", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a[]]b", RegexOptions.IgnoreCase, "A]B", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[^bc]d", RegexOptions.IgnoreCase, "AED", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[^bc]d", RegexOptions.IgnoreCase, "ABD", "Fail." };
            yield return new object[] { @"a[^-b]c", RegexOptions.IgnoreCase, "ADC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[^-b]c", RegexOptions.IgnoreCase, "A-C", "Fail." };
            yield return new object[] { @"a[^]b]c", RegexOptions.IgnoreCase, "A]C", "Fail." };
            yield return new object[] { @"a[^]b]c", RegexOptions.IgnoreCase, "ADC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab|cd", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"ab|cd", RegexOptions.IgnoreCase, "ABCD", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"()ef", RegexOptions.IgnoreCase, "DEF", "Pass. Group[0]=(1,2) Group[1]=(1,0)" };
            yield return new object[] { @"*a", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"(*)b", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"$b", RegexOptions.IgnoreCase, "B", "Fail." };
            yield return new object[] { @"a\", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"a\(b", RegexOptions.IgnoreCase, "A(B", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a\(*b", RegexOptions.IgnoreCase, "AB", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a\(*b", RegexOptions.IgnoreCase, "A((B", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"a\\b", RegexOptions.IgnoreCase, "A\\B", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc)", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"(abc", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"((a))", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=(0,1)" };
            yield return new object[] { @"(a)b(c)", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3) Group[1]=(0,1) Group[2]=(2,1)" };
            yield return new object[] { @"a+b+c", RegexOptions.IgnoreCase, "AABBABC", "Pass. Group[0]=(4,3)" };
            yield return new object[] { @"a{1,}b{1,}c", RegexOptions.IgnoreCase, "AABBABC", "Pass. Group[0]=(4,3)" };
            yield return new object[] { @"a**", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"a.+?c", RegexOptions.IgnoreCase, "ABCABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a.*?c", RegexOptions.IgnoreCase, "ABCABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a.{0,5}?c", RegexOptions.IgnoreCase, "ABCABC", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"(a+|b)*", RegexOptions.IgnoreCase, "AB", "Pass. Group[0]=(0,2) Group[1]=(0,1)(1,1)" };
            yield return new object[] { @"(a+|b){0,}", RegexOptions.IgnoreCase, "AB", "Pass. Group[0]=(0,2) Group[1]=(0,1)(1,1)" };
            yield return new object[] { @"(a+|b)+", RegexOptions.IgnoreCase, "AB", "Pass. Group[0]=(0,2) Group[1]=(0,1)(1,1)" };
            yield return new object[] { @"(a+|b){1,}", RegexOptions.IgnoreCase, "AB", "Pass. Group[0]=(0,2) Group[1]=(0,1)(1,1)" };
            yield return new object[] { @"(a+|b)?", RegexOptions.IgnoreCase, "AB", "Pass. Group[0]=(0,1) Group[1]=(0,1)" };
            yield return new object[] { @"(a+|b){0,1}", RegexOptions.IgnoreCase, "AB", "Pass. Group[0]=(0,1) Group[1]=(0,1)" };
            yield return new object[] { @"(a+|b){0,1}?", RegexOptions.IgnoreCase, "AB", "Pass. Group[0]=(0,0) Group[1]=" };
            yield return new object[] { @")(", RegexOptions.IgnoreCase, "-", "Error." };
            yield return new object[] { @"[^ab]*", RegexOptions.IgnoreCase, "CDE", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc", RegexOptions.IgnoreCase, "", "Fail." };
            yield return new object[] { @"a*", RegexOptions.IgnoreCase, "", "Pass. Group[0]=(0,0)" };
            yield return new object[] { @"([abc])*d", RegexOptions.IgnoreCase, "ABBBCD", "Pass. Group[0]=(0,6) Group[1]=(0,1)(1,1)(2,1)(3,1)(4,1)" };
            yield return new object[] { @"([abc])*bcd", RegexOptions.IgnoreCase, "ABCD", "Pass. Group[0]=(0,4) Group[1]=(0,1)" };
            yield return new object[] { @"a|b|c|d|e", RegexOptions.IgnoreCase, "E", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"(a|b|c|d|e)f", RegexOptions.IgnoreCase, "EF", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"abcd*efg", RegexOptions.IgnoreCase, "ABCDEFG", "Pass. Group[0]=(0,7)" };
            yield return new object[] { @"ab*", RegexOptions.IgnoreCase, "XABYABBBZ", "Pass. Group[0]=(1,2)" };
            yield return new object[] { @"ab*", RegexOptions.IgnoreCase, "XAYABBBZ", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"(ab|cd)e", RegexOptions.IgnoreCase, "ABCDE", "Pass. Group[0]=(2,3) Group[1]=(2,2)" };
            yield return new object[] { @"[abhgefdc]ij", RegexOptions.IgnoreCase, "HIJ", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^(ab|cd)e", RegexOptions.IgnoreCase, "ABCDE", "Fail." };
            yield return new object[] { @"(abc|)ef", RegexOptions.IgnoreCase, "ABCDEF", "Pass. Group[0]=(4,2) Group[1]=(4,0)" };
            yield return new object[] { @"(a|b)c*d", RegexOptions.IgnoreCase, "ABCD", "Pass. Group[0]=(1,3) Group[1]=(1,1)" };
            yield return new object[] { @"(ab|ab*)bc", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3) Group[1]=(0,1)" };
            yield return new object[] { @"a([bc]*)c*", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3) Group[1]=(1,2)" };
            yield return new object[] { @"a([bc]*)(c*d)", RegexOptions.IgnoreCase, "ABCD", "Pass. Group[0]=(0,4) Group[1]=(1,2) Group[2]=(3,1)" };
            yield return new object[] { @"a([bc]+)(c*d)", RegexOptions.IgnoreCase, "ABCD", "Pass. Group[0]=(0,4) Group[1]=(1,2) Group[2]=(3,1)" };
            yield return new object[] { @"a([bc]*)(c+d)", RegexOptions.IgnoreCase, "ABCD", "Pass. Group[0]=(0,4) Group[1]=(1,1) Group[2]=(2,2)" };
            yield return new object[] { @"a[bcd]*dcdcde", RegexOptions.IgnoreCase, "ADCDCDE", "Pass. Group[0]=(0,7)" };
            yield return new object[] { @"a[bcd]+dcdcde", RegexOptions.IgnoreCase, "ADCDCDE", "Fail." };
            yield return new object[] { @"(ab|a)b*c", RegexOptions.IgnoreCase, "ABC", "Pass. Group[0]=(0,3) Group[1]=(0,2)" };
            yield return new object[] { @"((a)(b)c)(d)", RegexOptions.IgnoreCase, "ABCD", "Pass. Group[0]=(0,4) Group[1]=(0,3) Group[2]=(0,1) Group[3]=(1,1) Group[4]=(3,1)" };
            yield return new object[] { @"[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.IgnoreCase, "ALPHA", "Pass. Group[0]=(0,5)" };
            yield return new object[] { @"^a(bc+|b[eh])g|.h$", RegexOptions.IgnoreCase, "ABH", "Pass. Group[0]=(1,2) Group[1]=" };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.IgnoreCase, "EFFGZ", "Pass. Group[0]=(0,5) Group[1]=(0,5) Group[2]=" };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.IgnoreCase, "IJ", "Pass. Group[0]=(0,2) Group[1]=(0,2) Group[2]=(1,1)" };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.IgnoreCase, "EFFG", "Fail." };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.IgnoreCase, "BCDD", "Fail." };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.IgnoreCase, "REFFGZ", "Pass. Group[0]=(1,5) Group[1]=(1,5) Group[2]=" };
            yield return new object[] { @"((((((((((a))))))))))", RegexOptions.IgnoreCase, "A", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1) Group[10]=(0,1)" };
            yield return new object[] { @"((((((((((a))))))))))\10", RegexOptions.IgnoreCase, "AA", "Pass. Group[0]=(0,2) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1) Group[10]=(0,1)" };
            yield return new object[] { @"((((((((((a))))))))))!", RegexOptions.IgnoreCase, "AA", "Fail." };
            yield return new object[] { @"((((((((((a))))))))))!", RegexOptions.IgnoreCase, "A!", "Pass. Group[0]=(0,2) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1) Group[10]=(0,1)" };
            yield return new object[] { @"(((((((((a)))))))))", RegexOptions.IgnoreCase, "A", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1)" };
            yield return new object[] { @"(?:(?:(?:(?:(?:(?:(?:(?:(?:(a))))))))))", RegexOptions.IgnoreCase, "A", "Pass. Group[0]=(0,1) Group[1]=(0,1)" };
            yield return new object[] { @"(?:(?:(?:(?:(?:(?:(?:(?:(?:(a|b|c))))))))))", RegexOptions.IgnoreCase, "C", "Pass. Group[0]=(0,1) Group[1]=(0,1)" };
            yield return new object[] { @"multiple words of text", RegexOptions.IgnoreCase, "UH-UH", "Fail." };
            yield return new object[] { @"multiple words", RegexOptions.IgnoreCase, "MULTIPLE WORDS, YEAH", "Pass. Group[0]=(0,14)" };
            yield return new object[] { @"(.*)c(.*)", RegexOptions.IgnoreCase, "ABCDE", "Pass. Group[0]=(0,5) Group[1]=(0,2) Group[2]=(3,2)" };
            yield return new object[] { @"\((.*), (.*)\)", RegexOptions.IgnoreCase, "(A, B)", "Pass. Group[0]=(0,6) Group[1]=(1,1) Group[2]=(4,1)" };
            yield return new object[] { @"[k]", RegexOptions.IgnoreCase, "AB", "Fail." };
            yield return new object[] { @"abcd", RegexOptions.IgnoreCase, "ABCD", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"a(bc)d", RegexOptions.IgnoreCase, "ABCD", "Pass. Group[0]=(0,4) Group[1]=(1,2)" };
            yield return new object[] { @"a[-]?c", RegexOptions.IgnoreCase, "AC", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"(abc)\1", RegexOptions.IgnoreCase, "ABCABC", "Pass. Group[0]=(0,6) Group[1]=(0,3)" };
            yield return new object[] { @"([a-c]*)\1", RegexOptions.IgnoreCase, "ABCABC", "Pass. Group[0]=(0,6) Group[1]=(0,3)" };
            yield return new object[] { @"a(?!b).", RegexOptions.None, "abad", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"a(?=d).", RegexOptions.None, "abad", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"a(?=c|d).", RegexOptions.None, "abad", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"a(?:b|c|d)(.)", RegexOptions.None, "ace", "Pass. Group[0]=(0,3) Group[1]=(2,1)" };
            yield return new object[] { @"a(?:b|c|d)*(.)", RegexOptions.None, "ace", "Pass. Group[0]=(0,3) Group[1]=(2,1)" };
            yield return new object[] { @"a(?:b|c|d)+?(.)", RegexOptions.None, "ace", "Pass. Group[0]=(0,3) Group[1]=(2,1)" };
            yield return new object[] { @"a(?:b|c|d)+?(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,3) Group[1]=(2,1)" };
            yield return new object[] { @"a(?:b|c|d)+(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,8) Group[1]=(7,1)" };
            yield return new object[] { @"a(?:b|c|d){2}(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,4) Group[1]=(3,1)" };
            yield return new object[] { @"a(?:b|c|d){4,5}(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,7) Group[1]=(6,1)" };
            yield return new object[] { @"a(?:b|c|d){4,5}?(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,6) Group[1]=(5,1)" };
            yield return new object[] { @"((foo)|(bar))*", RegexOptions.None, "foobar", "Pass. Group[0]=(0,6) Group[1]=(0,3)(3,3) Group[2]=(0,3) Group[3]=(3,3)" };
            yield return new object[] { @":(?:", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"a(?:b|c|d){6,7}(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,8) Group[1]=(7,1)" };
            yield return new object[] { @"a(?:b|c|d){6,7}?(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,8) Group[1]=(7,1)" };
            yield return new object[] { @"a(?:b|c|d){5,6}(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,8) Group[1]=(7,1)" };
            yield return new object[] { @"a(?:b|c|d){5,6}?(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,7) Group[1]=(6,1)" };
            yield return new object[] { @"a(?:b|c|d){5,7}(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,8) Group[1]=(7,1)" };
            yield return new object[] { @"a(?:b|c|d){5,7}?(.)", RegexOptions.None, "acdbcdbe", "Pass. Group[0]=(0,7) Group[1]=(6,1)" };
            yield return new object[] { @"a(?:b|(c|e){1,2}?|d)+?(.)", RegexOptions.None, "ace", "Pass. Group[0]=(0,3) Group[1]=(1,1) Group[2]=(2,1)" };
            yield return new object[] { @"^(.+)?B", RegexOptions.None, "AB", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"^([^a-z])|(\^)$", RegexOptions.None, ".", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=" };
            yield return new object[] { @"^[<>]&", RegexOptions.None, "<&OUT", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"^(a\1?){4}$", RegexOptions.None, "aaaaaaaaaa", "Pass. Group[0]=(0,10) Group[1]=(0,1)(1,2)(3,3)(6,4)" };
            yield return new object[] { @"^(a\1?){4}$", RegexOptions.None, "aaaaaaaaa", "Fail." };
            yield return new object[] { @"^(a\1?){4}$", RegexOptions.None, "aaaaaaaaaaa", "Fail." };
            yield return new object[] { @"^(a(?(1)\1)){4}$", RegexOptions.None, "aaaaaaaaaa", "Pass. Group[0]=(0,10) Group[1]=(0,1)(1,2)(3,3)(6,4)" };
            yield return new object[] { @"^(a(?(1)\1)){4}$", RegexOptions.None, "aaaaaaaaa", "Fail." };
            yield return new object[] { @"^(a(?(1)\1)){4}$", RegexOptions.None, "aaaaaaaaaaa", "Fail." };
            yield return new object[] { @"((a{4})+)", RegexOptions.None, "aaaaaaaaa", "Pass. Group[0]=(0,8) Group[1]=(0,8) Group[2]=(0,4)(4,4)" };
            yield return new object[] { @"(((aa){2})+)", RegexOptions.None, "aaaaaaaaaa", "Pass. Group[0]=(0,8) Group[1]=(0,8) Group[2]=(0,4)(4,4) Group[3]=(0,2)(2,2)(4,2)(6,2)" };
            yield return new object[] { @"(((a{2}){2})+)", RegexOptions.None, "aaaaaaaaaa", "Pass. Group[0]=(0,8) Group[1]=(0,8) Group[2]=(0,4)(4,4) Group[3]=(0,2)(2,2)(4,2)(6,2)" };
            yield return new object[] { @"(?:(f)(o)(o)|(b)(a)(r))*", RegexOptions.None, "foobar", "Pass. Group[0]=(0,6) Group[1]=(0,1) Group[2]=(1,1) Group[3]=(2,1) Group[4]=(3,1) Group[5]=(4,1) Group[6]=(5,1)" };
            yield return new object[] { @"(?<=a)b", RegexOptions.None, "ab", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"(?<=a)b", RegexOptions.None, "cb", "Fail." };
            yield return new object[] { @"(?<=a)b", RegexOptions.None, "b", "Fail." };
            yield return new object[] { @"(?<!c)b", RegexOptions.None, "ab", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"(?<!c)b", RegexOptions.None, "cb", "Fail." };
            yield return new object[] { @"(?<!c)b", RegexOptions.None, "b", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"(?<%)b", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"(?:..)*a", RegexOptions.None, "aba", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^(?:b|a(?=(.)))*\1", RegexOptions.None, "abc", "Pass. Group[0]=(0,2) Group[1]=(1,1)" };
            yield return new object[] { @"^(){3,5}", RegexOptions.None, "abc", "Pass. Group[0]=(0,0) Group[1]=(0,0)(0,0)(0,0)" };
            yield return new object[] { @"^(a+)*ax", RegexOptions.None, "aax", "Pass. Group[0]=(0,3) Group[1]=(0,1)" };
            yield return new object[] { @"^((a|b)+)*ax", RegexOptions.None, "aax", "Pass. Group[0]=(0,3) Group[1]=(0,1) Group[2]=(0,1)" };
            yield return new object[] { @"^((a|bc)+)*ax", RegexOptions.None, "aax", "Pass. Group[0]=(0,3) Group[1]=(0,1) Group[2]=(0,1)" };
            yield return new object[] { @"(a|x)*ab", RegexOptions.None, "cab", "Pass. Group[0]=(1,2) Group[1]=" };
            yield return new object[] { @"(a)*ab", RegexOptions.None, "cab", "Pass. Group[0]=(1,2) Group[1]=" };
            yield return new object[] { @"(?:(?i)a)b", RegexOptions.None, "ab", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"((?i)a)b", RegexOptions.None, "ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"(?:(?i)a)b", RegexOptions.None, "Ab", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"((?i)a)b", RegexOptions.None, "Ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"(?:(?i)a)b", RegexOptions.None, "aB", "Fail." };
            yield return new object[] { @"((?i)a)b", RegexOptions.None, "aB", "Fail." };
            yield return new object[] { @"(?i:a)b", RegexOptions.None, "ab", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"((?i:a))b", RegexOptions.None, "ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"(?i:a)b", RegexOptions.None, "Ab", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"((?i:a))b", RegexOptions.None, "Ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"(?i:a)b", RegexOptions.None, "aB", "Fail." };
            yield return new object[] { @"((?i:a))b", RegexOptions.None, "aB", "Fail." };
            yield return new object[] { @"(?:(?-i)a)b", RegexOptions.IgnoreCase, "ab", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"((?-i)a)b", RegexOptions.IgnoreCase, "ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"((?-i)a)b", RegexOptions.IgnoreCase, "aB", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"(?:(?-i)a)b", RegexOptions.IgnoreCase, "Ab", "Fail." };
            yield return new object[] { @"((?-i)a)b", RegexOptions.IgnoreCase, "Ab", "Fail." };
            yield return new object[] { @"(?:(?-i)a)b", RegexOptions.IgnoreCase, "aB", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"(?:(?-i)a)b", RegexOptions.IgnoreCase, "AB", "Fail." };
            yield return new object[] { @"((?-i)a)b", RegexOptions.IgnoreCase, "AB", "Fail." };
            yield return new object[] { @"(?-i:a)b", RegexOptions.IgnoreCase, "ab", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"((?-i:a))b", RegexOptions.IgnoreCase, "ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"(?-i:a)b", RegexOptions.IgnoreCase, "aB", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"((?-i:a))b", RegexOptions.IgnoreCase, "aB", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"(?-i:a)b", RegexOptions.IgnoreCase, "Ab", "Fail." };
            yield return new object[] { @"((?-i:a))b", RegexOptions.IgnoreCase, "Ab", "Fail." };
            yield return new object[] { @"(?-i:a)b", RegexOptions.IgnoreCase, "AB", "Fail." };
            yield return new object[] { @"((?-i:a))b", RegexOptions.IgnoreCase, "AB", "Fail." };
            yield return new object[] { @"((?-i:a.))b", RegexOptions.IgnoreCase, "a\nB", "Fail." };
            yield return new object[] { @"((?s-i:a.))b", RegexOptions.IgnoreCase, "B\nB", "Fail." };
            yield return new object[] { @"(?:c|d)(?:)(?:a(?:)(?:b)(?:b(?:))(?:b(?:)(?:b)))", RegexOptions.None, "cabbbb", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"(?:c|d)(?:)(?:aaaaaaaa(?:)(?:bbbbbbbb)(?:bbbbbbbb(?:))(?:bbbbbbbb(?:)(?:bbbbbbbb)))", RegexOptions.None, "caaaaaaaabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "Pass. Group[0]=(0,41)" };
            yield return new object[] { @"(ab)\d\1", RegexOptions.IgnoreCase, "Ab4ab", "Pass. Group[0]=(0,5) Group[1]=(0,2)" };
            yield return new object[] { @"(ab)\d\1", RegexOptions.IgnoreCase, "ab4Ab", "Pass. Group[0]=(0,5) Group[1]=(0,2)" };
            yield return new object[] { @"foo\w*\d{4}baz", RegexOptions.None, "foobar1234baz", "Pass. Group[0]=(0,13)" };
            yield return new object[] { @"x(~~)*(?:(?:F)?)?", RegexOptions.None, "x~~", "Pass. Group[0]=(0,3) Group[1]=(1,2)" };
            yield return new object[] { @"^a(?#xxx){3}c", RegexOptions.None, "aaac", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"(?<![cd])b", RegexOptions.None, "dbcb", "Fail." };
            yield return new object[] { @"(?<![cd])[ab]", RegexOptions.None, "dbaacb", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"(?<!(c|d))b", RegexOptions.None, "dbcb", "Fail." };
            yield return new object[] { @"(?<!(c|d))[ab]", RegexOptions.None, "dbaacb", "Pass. Group[0]=(2,1) Group[1]=" };
            yield return new object[] { @"(?<!cd)[ab]", RegexOptions.None, "cdaccb", "Pass. Group[0]=(5,1)" };
            yield return new object[] { @"^(?:a?b?)*$", RegexOptions.None, "a--", "Fail." };
            yield return new object[] { @"((?m)^b$)", RegexOptions.None, "a\nb\nc\n", "Pass. Group[0]=(2,1) Group[1]=(2,1)" };
            yield return new object[] { @"(?m)^b", RegexOptions.None, "a\nb\n", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"(?m)^(b)", RegexOptions.None, "a\nb\n", "Pass. Group[0]=(2,1) Group[1]=(2,1)" };
            yield return new object[] { @"((?m)^b)", RegexOptions.None, "a\nb\n", "Pass. Group[0]=(2,1) Group[1]=(2,1)" };
            yield return new object[] { @"\n((?m)^b)", RegexOptions.None, "a\nb\n", "Pass. Group[0]=(1,2) Group[1]=(2,1)" };
            yield return new object[] { @"((?s).)c(?!.)", RegexOptions.None, "a\nb\nc\n", "Pass. Group[0]=(3,2) Group[1]=(3,1)" };
            yield return new object[] { @"((?s)b.)c(?!.)", RegexOptions.None, "a\nb\nc\n", "Pass. Group[0]=(2,3) Group[1]=(2,2)" };
            yield return new object[] { @"^b", RegexOptions.None, "a\nb\nc\n", "Fail." };
            yield return new object[] { @"()^b", RegexOptions.None, "a\nb\nc\n", "Fail." };
            yield return new object[] { @"((?m)^b)", RegexOptions.None, "a\nb\nc\n", "Pass. Group[0]=(2,1) Group[1]=(2,1)" };
            yield return new object[] { @"(x)?(?(1)a|b)", RegexOptions.None, "a", "Fail." };
            yield return new object[] { @"(x)?(?(1)b|a)", RegexOptions.None, "a", "Pass. Group[0]=(0,1) Group[1]=" };
            yield return new object[] { @"()?(?(1)b|a)", RegexOptions.None, "a", "Pass. Group[0]=(0,1) Group[1]=" };
            yield return new object[] { @"()(?(1)b|a)", RegexOptions.None, "a", "Fail." };
            yield return new object[] { @"()?(?(1)a|b)", RegexOptions.None, "a", "Pass. Group[0]=(0,1) Group[1]=(0,0)" };
            yield return new object[] { @"^(\()?blah(?(1)(\)))$", RegexOptions.None, "(blah)", "Pass. Group[0]=(0,6) Group[1]=(0,1) Group[2]=(5,1)" };
            yield return new object[] { @"^(\()?blah(?(1)(\)))$", RegexOptions.None, "blah", "Pass. Group[0]=(0,4) Group[1]= Group[2]=" };
            yield return new object[] { @"^(\()?blah(?(1)(\)))$", RegexOptions.None, "blah)", "Fail." };
            yield return new object[] { @"^(\()?blah(?(1)(\)))$", RegexOptions.None, "(blah", "Fail." };
            yield return new object[] { @"^(\(+)?blah(?(1)(\)))$", RegexOptions.None, "(blah)", "Pass. Group[0]=(0,6) Group[1]=(0,1) Group[2]=(5,1)" };
            yield return new object[] { @"^(\(+)?blah(?(1)(\)))$", RegexOptions.None, "blah", "Pass. Group[0]=(0,4) Group[1]= Group[2]=" };
            yield return new object[] { @"^(\(+)?blah(?(1)(\)))$", RegexOptions.None, "blah)", "Fail." };
            yield return new object[] { @"^(\(+)?blah(?(1)(\)))$", RegexOptions.None, "(blah", "Fail." };
            yield return new object[] { @"(?(1)a|b|c)", RegexOptions.None, "a", "Error." };
            yield return new object[] { @"(?(?!a)a|b)", RegexOptions.None, "a", "Fail." };
            yield return new object[] { @"(?(?!a)b|a)", RegexOptions.None, "a", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"(?(?=a)b|a)", RegexOptions.None, "a", "Fail." };
            yield return new object[] { @"(?(?=a)a|b)", RegexOptions.None, "a", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"(?=(a+?))(\1ab)", RegexOptions.None, "aaab", "Pass. Group[0]=(1,3) Group[1]=(1,1) Group[2]=(1,3)" };
            yield return new object[] { @"^(?=(a+?))\1ab", RegexOptions.None, "aaab", "Fail." };
            yield return new object[] { @"(\w+:)+", RegexOptions.None, "one:", "Pass. Group[0]=(0,4) Group[1]=(0,4)" };
            yield return new object[] { @"$(?<=^(a))", RegexOptions.None, "a", "Pass. Group[0]=(1,0) Group[1]=(0,1)" };
            yield return new object[] { @"([\w:]+::)?(\w+)$", RegexOptions.None, "abcd:", "Fail." };
            yield return new object[] { @"([\w:]+::)?(\w+)$", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4) Group[1]= Group[2]=(0,4)" };
            yield return new object[] { @"([\w:]+::)?(\w+)$", RegexOptions.None, "xy:z:::abcd", "Pass. Group[0]=(0,11) Group[1]=(0,7) Group[2]=(7,4)" };
            yield return new object[] { @"^[^bcd]*(c+)", RegexOptions.None, "aexycd", "Pass. Group[0]=(0,5) Group[1]=(4,1)" };
            yield return new object[] { @"(a*)b+", RegexOptions.None, "caab", "Pass. Group[0]=(1,3) Group[1]=(1,2)" };
            yield return new object[] { @"(>a+)ab", RegexOptions.None, "aaab", "Fail." };
            yield return new object[] { @"(?>a+)b", RegexOptions.None, "aaab", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"([[:]+)", RegexOptions.None, "a:[b]:", "Pass. Group[0]=(1,2) Group[1]=(1,2)" };
            yield return new object[] { @"([[=]+)", RegexOptions.None, "a=[b]=", "Pass. Group[0]=(1,2) Group[1]=(1,2)" };
            yield return new object[] { @"([[.]+)", RegexOptions.None, "a.[b].", "Pass. Group[0]=(1,2) Group[1]=(1,2)" };
            yield return new object[] { @"[a[:]b[:c]", RegexOptions.None, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"((?>a+)b)", RegexOptions.None, "aaab", "Pass. Group[0]=(0,4) Group[1]=(0,4)" };
            yield return new object[] { @"(?>(a+))b", RegexOptions.None, "aaab", "Pass. Group[0]=(0,4) Group[1]=(0,3)" };
            yield return new object[] { @"((?>[^()]+)|\([^()]*\))+", RegexOptions.None, "((abc(ade)ufh()()x", "Pass. Group[0]=(2,16) Group[1]=(2,3)(5,5)(10,3)(13,2)(15,2)(17,1)" };
            yield return new object[] { @"(?<=x+)", RegexOptions.None, "xxxxy", "Pass. Group[0]=(1,0)" };
            yield return new object[] { @"a{37,17}", RegexOptions.None, "-", "Error." };
            yield return new object[] { @"\Z", RegexOptions.None, "a\nb\n", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"\z", RegexOptions.None, "a\nb\n", "Pass. Group[0]=(4,0)" };
            yield return new object[] { @"$", RegexOptions.None, "a\nb\n", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"\Z", RegexOptions.None, "b\na\n", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"\z", RegexOptions.None, "b\na\n", "Pass. Group[0]=(4,0)" };
            yield return new object[] { @"$", RegexOptions.None, "b\na\n", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"\Z", RegexOptions.None, "b\na", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"\z", RegexOptions.None, "b\na", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"$", RegexOptions.None, "b\na", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"\Z", RegexOptions.Multiline, "a\nb\n", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"\z", RegexOptions.Multiline, "a\nb\n", "Pass. Group[0]=(4,0)" };
            yield return new object[] { @"$", RegexOptions.Multiline, "a\nb\n", "Pass. Group[0]=(1,0)" };
            yield return new object[] { @"\Z", RegexOptions.Multiline, "b\na\n", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"\z", RegexOptions.Multiline, "b\na\n", "Pass. Group[0]=(4,0)" };
            yield return new object[] { @"$", RegexOptions.Multiline, "b\na\n", "Pass. Group[0]=(1,0)" };
            yield return new object[] { @"\Z", RegexOptions.Multiline, "b\na", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"\z", RegexOptions.Multiline, "b\na", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"$", RegexOptions.Multiline, "b\na", "Pass. Group[0]=(1,0)" };
            yield return new object[] { @"a\Z", RegexOptions.None, "a\nb\n", "Fail." };
            yield return new object[] { @"a\z", RegexOptions.None, "a\nb\n", "Fail." };
            yield return new object[] { @"a$", RegexOptions.None, "a\nb\n", "Fail." };
            yield return new object[] { @"a\Z", RegexOptions.None, "b\na\n", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"a\z", RegexOptions.None, "b\na\n", "Fail." };
            yield return new object[] { @"a$", RegexOptions.None, "b\na\n", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"a\Z", RegexOptions.None, "b\na", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"a\z", RegexOptions.None, "b\na", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"a$", RegexOptions.None, "b\na", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"a\z", RegexOptions.Multiline, "a\nb\n", "Fail." };
            yield return new object[] { @"a$", RegexOptions.Multiline, "a\nb\n", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"a\Z", RegexOptions.Multiline, "b\na\n", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"a\z", RegexOptions.Multiline, "b\na\n", "Fail." };
            yield return new object[] { @"a$", RegexOptions.Multiline, "b\na\n", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"a\Z", RegexOptions.Multiline, "b\na", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"a\z", RegexOptions.Multiline, "b\na", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"a$", RegexOptions.Multiline, "b\na", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"aa\Z", RegexOptions.None, "aa\nb\n", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.None, "aa\nb\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.None, "aa\nb\n", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.None, "b\naa\n", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa\z", RegexOptions.None, "b\naa\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.None, "b\naa\n", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa\Z", RegexOptions.None, "b\naa", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa\z", RegexOptions.None, "b\naa", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa$", RegexOptions.None, "b\naa", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa\z", RegexOptions.Multiline, "aa\nb\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.Multiline, "aa\nb\n", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"aa\Z", RegexOptions.Multiline, "b\naa\n", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa\z", RegexOptions.Multiline, "b\naa\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.Multiline, "b\naa\n", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa\Z", RegexOptions.Multiline, "b\naa", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa\z", RegexOptions.Multiline, "b\naa", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa$", RegexOptions.Multiline, "b\naa", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"aa\Z", RegexOptions.None, "ac\nb\n", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.None, "ac\nb\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.None, "ac\nb\n", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.None, "b\nac\n", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.None, "b\nac\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.None, "b\nac\n", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.None, "b\nac", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.None, "b\nac", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.None, "b\nac", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.Multiline, "ac\nb\n", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.Multiline, "ac\nb\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.Multiline, "ac\nb\n", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.Multiline, "b\nac\n", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.Multiline, "b\nac\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.Multiline, "b\nac\n", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.Multiline, "b\nac", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.Multiline, "b\nac", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.Multiline, "b\nac", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.None, "ca\nb\n", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.None, "ca\nb\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.None, "ca\nb\n", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.None, "b\nca\n", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.None, "b\nca\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.None, "b\nca\n", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.None, "b\nca", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.None, "b\nca", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.None, "b\nca", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.Multiline, "ca\nb\n", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.Multiline, "ca\nb\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.Multiline, "ca\nb\n", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.Multiline, "b\nca\n", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.Multiline, "b\nca\n", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.Multiline, "b\nca\n", "Fail." };
            yield return new object[] { @"aa\Z", RegexOptions.Multiline, "b\nca", "Fail." };
            yield return new object[] { @"aa\z", RegexOptions.Multiline, "b\nca", "Fail." };
            yield return new object[] { @"aa$", RegexOptions.Multiline, "b\nca", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.None, "ab\nb\n", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.None, "ab\nb\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.None, "ab\nb\n", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.None, "b\nab\n", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab\z", RegexOptions.None, "b\nab\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.None, "b\nab\n", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab\Z", RegexOptions.None, "b\nab", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab\z", RegexOptions.None, "b\nab", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab$", RegexOptions.None, "b\nab", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab\z", RegexOptions.Multiline, "ab\nb\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.Multiline, "ab\nb\n", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"ab\Z", RegexOptions.Multiline, "b\nab\n", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab\z", RegexOptions.Multiline, "b\nab\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.Multiline, "b\nab\n", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab\Z", RegexOptions.Multiline, "b\nab", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab\z", RegexOptions.Multiline, "b\nab", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab$", RegexOptions.Multiline, "b\nab", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"ab\Z", RegexOptions.None, "ac\nb\n", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.None, "ac\nb\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.None, "ac\nb\n", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.None, "b\nac\n", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.None, "b\nac\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.None, "b\nac\n", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.None, "b\nac", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.None, "b\nac", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.None, "b\nac", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.Multiline, "ac\nb\n", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.Multiline, "ac\nb\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.Multiline, "ac\nb\n", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.Multiline, "b\nac\n", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.Multiline, "b\nac\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.Multiline, "b\nac\n", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.Multiline, "b\nac", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.Multiline, "b\nac", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.Multiline, "b\nac", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.None, "ca\nb\n", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.None, "ca\nb\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.None, "ca\nb\n", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.None, "b\nca\n", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.None, "b\nca\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.None, "b\nca\n", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.None, "b\nca", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.None, "b\nca", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.None, "b\nca", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.Multiline, "ca\nb\n", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.Multiline, "ca\nb\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.Multiline, "ca\nb\n", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.Multiline, "b\nca\n", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.Multiline, "b\nca\n", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.Multiline, "b\nca\n", "Fail." };
            yield return new object[] { @"ab\Z", RegexOptions.Multiline, "b\nca", "Fail." };
            yield return new object[] { @"ab\z", RegexOptions.Multiline, "b\nca", "Fail." };
            yield return new object[] { @"ab$", RegexOptions.Multiline, "b\nca", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.None, "abb\nb\n", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.None, "abb\nb\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.None, "abb\nb\n", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.None, "b\nabb\n", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb\z", RegexOptions.None, "b\nabb\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.None, "b\nabb\n", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb\Z", RegexOptions.None, "b\nabb", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb\z", RegexOptions.None, "b\nabb", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb$", RegexOptions.None, "b\nabb", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb\z", RegexOptions.Multiline, "abb\nb\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.Multiline, "abb\nb\n", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abb\Z", RegexOptions.Multiline, "b\nabb\n", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb\z", RegexOptions.Multiline, "b\nabb\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.Multiline, "b\nabb\n", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb\Z", RegexOptions.Multiline, "b\nabb", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb\z", RegexOptions.Multiline, "b\nabb", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb$", RegexOptions.Multiline, "b\nabb", "Pass. Group[0]=(2,3)" };
            yield return new object[] { @"abb\Z", RegexOptions.None, "ac\nb\n", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.None, "ac\nb\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.None, "ac\nb\n", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.None, "b\nac\n", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.None, "b\nac\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.None, "b\nac\n", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.None, "b\nac", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.None, "b\nac", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.None, "b\nac", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.Multiline, "ac\nb\n", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.Multiline, "ac\nb\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.Multiline, "ac\nb\n", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.Multiline, "b\nac\n", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.Multiline, "b\nac\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.Multiline, "b\nac\n", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.Multiline, "b\nac", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.Multiline, "b\nac", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.Multiline, "b\nac", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.None, "ca\nb\n", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.None, "ca\nb\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.None, "ca\nb\n", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.None, "b\nca\n", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.None, "b\nca\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.None, "b\nca\n", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.None, "b\nca", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.None, "b\nca", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.None, "b\nca", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.Multiline, "ca\nb\n", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.Multiline, "ca\nb\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.Multiline, "ca\nb\n", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.Multiline, "b\nca\n", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.Multiline, "b\nca\n", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.Multiline, "b\nca\n", "Fail." };
            yield return new object[] { @"abb\Z", RegexOptions.Multiline, "b\nca", "Fail." };
            yield return new object[] { @"abb\z", RegexOptions.Multiline, "b\nca", "Fail." };
            yield return new object[] { @"abb$", RegexOptions.Multiline, "b\nca", "Fail." };
            yield return new object[] { @"(^|x)(c)", RegexOptions.None, "ca", "Pass. Group[0]=(0,1) Group[1]=(0,0) Group[2]=(0,1)" };
            yield return new object[] { @"a*abc?xyz+pqr{3}ab{2,}xy{4,5}pq{0,6}AB{0,}zz", RegexOptions.None, "x", "Fail." };
            yield return new object[] { @"round\(((?>[^()]+))\)", RegexOptions.None, "_I(round(xs * sz),1)", "Pass. Group[0]=(3,14) Group[1]=(9,7)" };
            yield return new object[] { @"foo.bart", RegexOptions.None, "foo.bart", "Pass. Group[0]=(0,8)" };
            yield return new object[] { @"^d[x][x][x]", RegexOptions.Multiline, "abcd\ndxxx", "Pass. Group[0]=(5,4)" };
            yield return new object[] { @".X(.+)+X", RegexOptions.None, "bbbbXcXaaaaaaaa", "Pass. Group[0]=(3,4) Group[1]=(5,1)" };
            yield return new object[] { @".X(.+)+XX", RegexOptions.None, "bbbbXcXXaaaaaaaa", "Pass. Group[0]=(3,5) Group[1]=(5,1)" };
            yield return new object[] { @".XX(.+)+X", RegexOptions.None, "bbbbXXcXaaaaaaaa", "Pass. Group[0]=(3,5) Group[1]=(6,1)" };
            yield return new object[] { @".X(.+)+X", RegexOptions.None, "bbbbXXaaaaaaaaa", "Fail." };
            yield return new object[] { @".X(.+)+XX", RegexOptions.None, "bbbbXXXaaaaaaaaa", "Fail." };
            yield return new object[] { @".XX(.+)+X", RegexOptions.None, "bbbbXXXaaaaaaaaa", "Fail." };
            yield return new object[] { @".X(.+)+[X]", RegexOptions.None, "bbbbXcXaaaaaaaa", "Pass. Group[0]=(3,4) Group[1]=(5,1)" };
            yield return new object[] { @".X(.+)+[X][X]", RegexOptions.None, "bbbbXcXXaaaaaaaa", "Pass. Group[0]=(3,5) Group[1]=(5,1)" };
            yield return new object[] { @".XX(.+)+[X]", RegexOptions.None, "bbbbXXcXaaaaaaaa", "Pass. Group[0]=(3,5) Group[1]=(6,1)" };
            yield return new object[] { @".X(.+)+[X]", RegexOptions.None, "bbbbXXaaaaaaaaa", "Fail." };
            yield return new object[] { @".X(.+)+[X][X]", RegexOptions.None, "bbbbXXXaaaaaaaaa", "Fail." };
            yield return new object[] { @".XX(.+)+[X]", RegexOptions.None, "bbbbXXXaaaaaaaaa", "Fail." };
            yield return new object[] { @".[X](.+)+[X]", RegexOptions.None, "bbbbXcXaaaaaaaa", "Pass. Group[0]=(3,4) Group[1]=(5,1)" };
            yield return new object[] { @".[X](.+)+[X][X]", RegexOptions.None, "bbbbXcXXaaaaaaaa", "Pass. Group[0]=(3,5) Group[1]=(5,1)" };
            yield return new object[] { @".[X][X](.+)+[X]", RegexOptions.None, "bbbbXXcXaaaaaaaa", "Pass. Group[0]=(3,5) Group[1]=(6,1)" };
            yield return new object[] { @".[X](.+)+[X]", RegexOptions.None, "bbbbXXaaaaaaaaa", "Fail." };
            yield return new object[] { @".[X](.+)+[X][X]", RegexOptions.None, "bbbbXXXaaaaaaaaa", "Fail." };
            yield return new object[] { @".[X][X](.+)+[X]", RegexOptions.None, "bbbbXXXaaaaaaaaa", "Fail." };
            yield return new object[] { @"tt+$", RegexOptions.None, "xxxtt", "Pass. Group[0]=(3,2)" };
            yield return new object[] { @"([\d-z]+)", RegexOptions.None, "a0-za", "Pass. Group[0]=(1,3) Group[1]=(1,3)" };
            yield return new object[] { @"([\d-\s]+)", RegexOptions.None, "a0- z", "Pass. Group[0]=(1,3) Group[1]=(1,3)" };
            yield return new object[] { @"\GX.*X", RegexOptions.None, "aaaXbX", "Fail." };
            yield return new object[] { @"(\d+\.\d+)", RegexOptions.None, "3.1415926", "Pass. Group[0]=(0,9) Group[1]=(0,9)" };
            yield return new object[] { @"(\ba.{0,10}br)", RegexOptions.None, "have a web browser", "Pass. Group[0]=(5,8) Group[1]=(5,8)" };
            yield return new object[] { @"\.c(pp|xx|c)?$", RegexOptions.IgnoreCase, "Changes", "Fail." };
            yield return new object[] { @"\.c(pp|xx|c)?$", RegexOptions.IgnoreCase, "IO.c", "Pass. Group[0]=(2,2) Group[1]=" };
            yield return new object[] { @"(\.c(pp|xx|c)?$)", RegexOptions.IgnoreCase, "IO.c", "Pass. Group[0]=(2,2) Group[1]=(2,2) Group[2]=" };
            yield return new object[] { @"^([a-z]:)", RegexOptions.None, "C:/", "Fail." };
            yield return new object[] { @"^\S\s+aa$", RegexOptions.Multiline, "\nx aa", "Pass. Group[0]=(1,4)" };
            yield return new object[] { @"(^|a)b", RegexOptions.None, "ab", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"^([ab]*?)(b)?(c)$", RegexOptions.None, "abac", "Pass. Group[0]=(0,4) Group[1]=(0,3) Group[2]= Group[3]=(3,1)" };
            yield return new object[] { @"(\w)?(abc)\1b", RegexOptions.None, "abcab", "Fail." };
            yield return new object[] { @"^(?:.,){2}c", RegexOptions.None, "a,b,c", "Pass. Group[0]=(0,5)" };
            yield return new object[] { @"^(.,){2}c", RegexOptions.None, "a,b,c", "Pass. Group[0]=(0,5) Group[1]=(0,2)(2,2)" };
            yield return new object[] { @"^(?:[^,]*,){2}c", RegexOptions.None, "a,b,c", "Pass. Group[0]=(0,5)" };
            yield return new object[] { @"^([^,]*,){2}c", RegexOptions.None, "a,b,c", "Pass. Group[0]=(0,5) Group[1]=(0,2)(2,2)" };
            yield return new object[] { @"^([^,]*,){3}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]*,){3,}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]*,){0,3}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]{1,3},){3}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]{1,3},){3,}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]{1,3},){0,3}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]{1,},){3}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]{1,},){3,}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]{1,},){0,3}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]{0,3},){3}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]{0,3},){3,}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"^([^,]{0,3},){0,3}d", RegexOptions.None, "aaa,b,c,d", "Pass. Group[0]=(0,9) Group[1]=(0,4)(4,2)(6,2)" };
            yield return new object[] { @"(?i)", RegexOptions.None, "", "Pass. Group[0]=(0,0)" };
            yield return new object[] { @"(?!\A)x", RegexOptions.Multiline, "a\nxb\n", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"^(a(b)?)+$", RegexOptions.None, "aba", "Pass. Group[0]=(0,3) Group[1]=(0,2)(2,1) Group[2]=(1,1)" };
            yield return new object[] { @"^(aa(bb)?)+$", RegexOptions.None, "aabbaa", "Pass. Group[0]=(0,6) Group[1]=(0,4)(4,2) Group[2]=(2,2)" };
            yield return new object[] { @"^.{9}abc.*\n", RegexOptions.Multiline, "123\nabcabcabcabc\n", "Pass. Group[0]=(4,13)" };
            yield return new object[] { @"^(a)?a$", RegexOptions.None, "a", "Pass. Group[0]=(0,1) Group[1]=" };
            yield return new object[] { @"^(a)?(?(1)a|b)+$", RegexOptions.None, "a", "Fail." };
            yield return new object[] { @"^(a\1?)(a\1?)(a\2?)(a\3?)$", RegexOptions.None, "aaaaaa", "Pass. Group[0]=(0,6) Group[1]=(0,1) Group[2]=(1,2) Group[3]=(3,1) Group[4]=(4,2)" };
            yield return new object[] { @"^(a\1?){4}$", RegexOptions.None, "aaaaaa", "Pass. Group[0]=(0,6) Group[1]=(0,1)(1,2)(3,1)(4,2)" };
            yield return new object[] { @"^(0+)?(?:x(1))?", RegexOptions.None, "x1", "Pass. Group[0]=(0,2) Group[1]= Group[2]=(1,1)" };
            yield return new object[] { @"^([0-9a-fA-F]+)(?:x([0-9a-fA-F]+)?)(?:x([0-9a-fA-F]+))?", RegexOptions.None, "012cxx0190", "Pass. Group[0]=(0,10) Group[1]=(0,4) Group[2]= Group[3]=(6,4)" };
            yield return new object[] { @"^(b+?|a){1,2}c", RegexOptions.None, "bbbac", "Pass. Group[0]=(0,5) Group[1]=(0,3)(3,1)" };
            yield return new object[] { @"^(b+?|a){1,2}c", RegexOptions.None, "bbbbac", "Pass. Group[0]=(0,6) Group[1]=(0,4)(4,1)" };
            yield return new object[] { @"\((\w\. \w+)\)", RegexOptions.None, "cd. (A. Tw)", "Pass. Group[0]=(4,7) Group[1]=(5,5)" };
            yield return new object[] { @"((?:aaaa|bbbb)cccc)?", RegexOptions.None, "aaaacccc", "Pass. Group[0]=(0,8) Group[1]=(0,8)" };
            yield return new object[] { @"((?:aaaa|bbbb)cccc)?", RegexOptions.None, "bbbbcccc", "Pass. Group[0]=(0,8) Group[1]=(0,8)" };
            yield return new object[] { @"^(foo)|(bar)$", RegexOptions.None, "foobar", "Pass. Group[0]=(0,3) Group[1]=(0,3) Group[2]=" };
            yield return new object[] { @"^(foo)|(bar)$", RegexOptions.RightToLeft, "foobar", "Pass. Group[0]=(3,3) Group[1]= Group[2]=(3,3)" };
            yield return new object[] { @"b", RegexOptions.RightToLeft, "babaaa", "Pass. Group[0]=(2,1)" };
            yield return new object[] { @"bab", RegexOptions.RightToLeft, "babababaa", "Pass. Group[0]=(4,3)" };
            yield return new object[] { @"abb", RegexOptions.RightToLeft, "abb", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"b$", RegexOptions.RightToLeft | RegexOptions.Multiline, "aab\naab", "Pass. Group[0]=(6,1)" };
            yield return new object[] { @"^a", RegexOptions.RightToLeft | RegexOptions.Multiline, "aab\naab", "Pass. Group[0]=(4,1)" };
            yield return new object[] { @"^aaab", RegexOptions.RightToLeft | RegexOptions.Multiline, "aaab\naab", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"abb{2}", RegexOptions.RightToLeft, "abbb", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"abb{1,2}", RegexOptions.RightToLeft, "abbb", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"abb{1,2}", RegexOptions.RightToLeft, "abbbbbaaaa", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"\Ab", RegexOptions.RightToLeft, "bab\naaa", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\Abab$", RegexOptions.RightToLeft, "bab", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"b\Z", RegexOptions.RightToLeft, "bab\naaa", "Fail." };
            yield return new object[] { @"b\Z", RegexOptions.RightToLeft, "babaaab", "Pass. Group[0]=(6,1)" };
            yield return new object[] { @"b\z", RegexOptions.RightToLeft, "babaaa", "Fail." };
            yield return new object[] { @"b\z", RegexOptions.RightToLeft, "babaaab", "Pass. Group[0]=(6,1)" };
            yield return new object[] { @"a\G", RegexOptions.RightToLeft, "babaaa", "Pass. Group[0]=(5,1)" };
            yield return new object[] { @"\Abaaa\G", RegexOptions.RightToLeft, "baaa", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"\bc", RegexOptions.RightToLeft, "aaa c aaa c a", "Pass. Group[0]=(10,1)" };
            yield return new object[] { @"\bc", RegexOptions.RightToLeft, "c aaa c", "Pass. Group[0]=(6,1)" };
            yield return new object[] { @"\bc", RegexOptions.RightToLeft, "aaa ac", "Fail." };
            yield return new object[] { @"\bc", RegexOptions.RightToLeft, "c aaa", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\bc", RegexOptions.RightToLeft, "aaacaaa", "Fail." };
            yield return new object[] { @"\bc", RegexOptions.RightToLeft, "aaac aaa", "Fail." };
            yield return new object[] { @"\bc", RegexOptions.RightToLeft, "aaa ca caaa", "Pass. Group[0]=(7,1)" };
            yield return new object[] { @"\Bc", RegexOptions.RightToLeft, "ac aaa ac", "Pass. Group[0]=(8,1)" };
            yield return new object[] { @"\Bc", RegexOptions.RightToLeft, "aaa c", "Fail." };
            yield return new object[] { @"\Bc", RegexOptions.RightToLeft, "ca aaa", "Fail." };
            yield return new object[] { @"\Bc", RegexOptions.RightToLeft, "aaa c aaa", "Fail." };
            yield return new object[] { @"\Bc", RegexOptions.RightToLeft, " acaca ", "Pass. Group[0]=(4,1)" };
            yield return new object[] { @"\Bc", RegexOptions.RightToLeft, "aaac aaac", "Pass. Group[0]=(8,1)" };
            yield return new object[] { @"\Bc", RegexOptions.RightToLeft, "aaa caaa", "Fail." };
            yield return new object[] { @"b(a?)b", RegexOptions.RightToLeft, "aabababbaaababa", "Pass. Group[0]=(11,3) Group[1]=(12,1)" };
            yield return new object[] { @"b{4}", RegexOptions.RightToLeft, "abbbbaabbbbaabbb", "Pass. Group[0]=(7,4)" };
            yield return new object[] { @"b\1aa(.)", RegexOptions.RightToLeft, "bBaaB", "Pass. Group[0]=(0,5) Group[1]=(4,1)" };
            yield return new object[] { @"b(.)aa\1", RegexOptions.RightToLeft, "bBaaB", "Fail." };
            yield return new object[] { @"^(a\1?){4}$", RegexOptions.RightToLeft, "aaaaaa", "Pass. Group[0]=(0,6) Group[1]=(5,1)(3,2)(2,1)(0,2)" };
            yield return new object[] { @"^([0-9a-fA-F]+)(?:x([0-9a-fA-F]+)?)(?:x([0-9a-fA-F]+))?", RegexOptions.RightToLeft, "012cxx0190", "Pass. Group[0]=(0,10) Group[1]=(0,4) Group[2]= Group[3]=(6,4)" };
            yield return new object[] { @"^(b+?|a){1,2}c", RegexOptions.RightToLeft, "bbbac", "Pass. Group[0]=(0,5) Group[1]=(3,1)(0,3)" };
            yield return new object[] { @"\((\w\. \w+)\)", RegexOptions.RightToLeft, "cd. (A. Tw)", "Pass. Group[0]=(4,7) Group[1]=(5,5)" };
            yield return new object[] { @"((?:aaaa|bbbb)cccc)?", RegexOptions.RightToLeft, "aaaacccc", "Pass. Group[0]=(0,8) Group[1]=(0,8)" };
            yield return new object[] { @"((?:aaaa|bbbb)cccc)?", RegexOptions.RightToLeft, "bbbbcccc", "Pass. Group[0]=(0,8) Group[1]=(0,8)" };
            yield return new object[] { @"(?<=a)b", RegexOptions.RightToLeft, "ab", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"(?<=a)b", RegexOptions.RightToLeft, "cb", "Fail." };
            yield return new object[] { @"(?<=a)b", RegexOptions.RightToLeft, "b", "Fail." };
            yield return new object[] { @"(?<!c)b", RegexOptions.RightToLeft, "ab", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"(?<!c)b", RegexOptions.RightToLeft, "cb", "Fail." };
            yield return new object[] { @"(?<!c)b", RegexOptions.RightToLeft, "b", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"a(?=d).", RegexOptions.RightToLeft, "adabad", "Pass. Group[0]=(4,2)" };
            yield return new object[] { @"a(?=c|d).", RegexOptions.RightToLeft, "adabad", "Pass. Group[0]=(4,2)" };
            yield return new object[] { @"ab*c", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab*bc", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab*bc", RegexOptions.RightToLeft, "abbc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab*bc", RegexOptions.RightToLeft, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @".{1}", RegexOptions.RightToLeft, "abbbbc", "Pass. Group[0]=(5,1)" };
            yield return new object[] { @".{3,4}", RegexOptions.RightToLeft, "abbbbc", "Pass. Group[0]=(2,4)" };
            yield return new object[] { @"ab{0,}bc", RegexOptions.RightToLeft, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab+bc", RegexOptions.RightToLeft, "abbc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab+bc", RegexOptions.RightToLeft, "abc", "Fail." };
            yield return new object[] { @"ab+bc", RegexOptions.RightToLeft, "abq", "Fail." };
            yield return new object[] { @"ab{1,}bc", RegexOptions.RightToLeft, "abq", "Fail." };
            yield return new object[] { @"ab+bc", RegexOptions.RightToLeft, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{1,}bc", RegexOptions.RightToLeft, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{1,3}bc", RegexOptions.RightToLeft, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{3,4}bc", RegexOptions.RightToLeft, "abbbbc", "Pass. Group[0]=(0,6)" };
            yield return new object[] { @"ab{4,5}bc", RegexOptions.RightToLeft, "abbbbc", "Fail." };
            yield return new object[] { @"ab?bc", RegexOptions.RightToLeft, "abbc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"ab?bc", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab{0,1}bc", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab?bc", RegexOptions.RightToLeft, "abbbbc", "Fail." };
            yield return new object[] { @"ab?c", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"ab{0,1}c", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^abc$", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^abc$", RegexOptions.RightToLeft, "abcc", "Fail." };
            yield return new object[] { @"^abc", RegexOptions.RightToLeft, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^abc$", RegexOptions.RightToLeft, "aabc", "Fail." };
            yield return new object[] { @"abc$", RegexOptions.RightToLeft, "aabc", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"abc$", RegexOptions.RightToLeft, "aabcd", "Fail." };
            yield return new object[] { @"^", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,0)" };
            yield return new object[] { @"$", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(3,0)" };
            yield return new object[] { @"a.c", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a.c", RegexOptions.RightToLeft, "axc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a.*c", RegexOptions.RightToLeft, "axyzc", "Pass. Group[0]=(0,5)" };
            yield return new object[] { @"a.*c", RegexOptions.RightToLeft, "axyzd", "Fail." };
            yield return new object[] { @"a[bc]d", RegexOptions.RightToLeft, "abc", "Fail." };
            yield return new object[] { @"a[bc]d", RegexOptions.RightToLeft, "abd", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[b-d]e", RegexOptions.RightToLeft, "abd", "Fail." };
            yield return new object[] { @"a[b-d]e", RegexOptions.RightToLeft, "ace", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[b-d]", RegexOptions.RightToLeft, "aac", "Pass. Group[0]=(1,2)" };
            yield return new object[] { @"a[-b]", RegexOptions.RightToLeft, "a-", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a[b-]", RegexOptions.RightToLeft, "a-", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a[b-a]", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"a[]b", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"a[", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"a]", RegexOptions.RightToLeft, "a]", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a[]]b", RegexOptions.RightToLeft, "a]b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[^bc]d", RegexOptions.RightToLeft, "aed", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[^bc]d", RegexOptions.RightToLeft, "abd", "Fail." };
            yield return new object[] { @"a[^-b]c", RegexOptions.RightToLeft, "adc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[^-b]c", RegexOptions.RightToLeft, "a-c", "Fail." };
            yield return new object[] { @"a[^]b]c", RegexOptions.RightToLeft, "a]c", "Fail." };
            yield return new object[] { @"a[^]b]c", RegexOptions.RightToLeft, "adc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"\ba\b", RegexOptions.RightToLeft, "a-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\ba\b", RegexOptions.RightToLeft, "-a", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"\ba\b", RegexOptions.RightToLeft, "-a-", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"\by\b", RegexOptions.RightToLeft, "xy", "Fail." };
            yield return new object[] { @"\by\b", RegexOptions.RightToLeft, "yz", "Fail." };
            yield return new object[] { @"\by\b", RegexOptions.RightToLeft, "xyz", "Fail." };
            yield return new object[] { @"\Ba\B", RegexOptions.RightToLeft, "a-", "Fail." };
            yield return new object[] { @"\Ba\B", RegexOptions.RightToLeft, "-a", "Fail." };
            yield return new object[] { @"\Ba\B", RegexOptions.RightToLeft, "-a-", "Fail." };
            yield return new object[] { @"\By\b", RegexOptions.RightToLeft, "xy", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"\by\B", RegexOptions.RightToLeft, "yz", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\By\B", RegexOptions.RightToLeft, "xyz", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"\w", RegexOptions.RightToLeft, "a", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\w", RegexOptions.RightToLeft, "-", "Fail." };
            yield return new object[] { @"\W", RegexOptions.RightToLeft, "a", "Fail." };
            yield return new object[] { @"\W", RegexOptions.RightToLeft, "-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"a\sb", RegexOptions.RightToLeft, "a b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a\sb", RegexOptions.RightToLeft, "a-b", "Fail." };
            yield return new object[] { @"a\Sb", RegexOptions.RightToLeft, "a b", "Fail." };
            yield return new object[] { @"a\Sb", RegexOptions.RightToLeft, "a-b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"\d", RegexOptions.RightToLeft, "1", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"\d", RegexOptions.RightToLeft, "-", "Fail." };
            yield return new object[] { @"\D", RegexOptions.RightToLeft, "1", "Fail." };
            yield return new object[] { @"\D", RegexOptions.RightToLeft, "-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"[\w]", RegexOptions.RightToLeft, "a", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"[\w]", RegexOptions.RightToLeft, "-", "Fail." };
            yield return new object[] { @"[\W]", RegexOptions.RightToLeft, "a", "Fail." };
            yield return new object[] { @"[\W]", RegexOptions.RightToLeft, "-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"a[\s]b", RegexOptions.RightToLeft, "a b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a[\s]b", RegexOptions.RightToLeft, "a-b", "Fail." };
            yield return new object[] { @"a[\S]b", RegexOptions.RightToLeft, "a b", "Fail." };
            yield return new object[] { @"a[\S]b", RegexOptions.RightToLeft, "a-b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"[\d]", RegexOptions.RightToLeft, "1", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"[\d]", RegexOptions.RightToLeft, "-", "Fail." };
            yield return new object[] { @"[\D]", RegexOptions.RightToLeft, "1", "Fail." };
            yield return new object[] { @"[\D]", RegexOptions.RightToLeft, "-", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"ab|cd", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"ab|cd", RegexOptions.RightToLeft, "abcd", "Pass. Group[0]=(2,2)" };
            yield return new object[] { @"()ef", RegexOptions.RightToLeft, "def", "Pass. Group[0]=(1,2) Group[1]=(1,0)" };
            yield return new object[] { @"*a", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"(*)b", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"$b", RegexOptions.RightToLeft, "b", "Fail." };
            yield return new object[] { @"a\", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"a\(b", RegexOptions.RightToLeft, "a(b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"a\(*b", RegexOptions.RightToLeft, "ab", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"a\(*b", RegexOptions.RightToLeft, "a((b", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"a\\b", RegexOptions.RightToLeft, "a\\b", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc)", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"(abc", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"((a))", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=(0,1)" };
            yield return new object[] { @"(a)b(c)", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3) Group[1]=(0,1) Group[2]=(2,1)" };
            yield return new object[] { @"a+b+c", RegexOptions.RightToLeft, "aabbabc", "Pass. Group[0]=(4,3)" };
            yield return new object[] { @"a{1,}b{1,}c", RegexOptions.RightToLeft, "aabbabc", "Pass. Group[0]=(4,3)" };
            yield return new object[] { @"a**", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"a.+?c", RegexOptions.RightToLeft, "abcabc", "Pass. Group[0]=(3,3)" };
            yield return new object[] { @"(a+|b)*", RegexOptions.RightToLeft, "ab", "Pass. Group[0]=(0,2) Group[1]=(1,1)(0,1)" };
            yield return new object[] { @"(a+|b){0,}", RegexOptions.RightToLeft, "ab", "Pass. Group[0]=(0,2) Group[1]=(1,1)(0,1)" };
            yield return new object[] { @"(a+|b)+", RegexOptions.RightToLeft, "ab", "Pass. Group[0]=(0,2) Group[1]=(1,1)(0,1)" };
            yield return new object[] { @"(a+|b){1,}", RegexOptions.RightToLeft, "ab", "Pass. Group[0]=(0,2) Group[1]=(1,1)(0,1)" };
            yield return new object[] { @"(a+|b)?", RegexOptions.RightToLeft, "ab", "Pass. Group[0]=(1,1) Group[1]=(1,1)" };
            yield return new object[] { @"(a+|b){0,1}", RegexOptions.RightToLeft, "ab", "Pass. Group[0]=(1,1) Group[1]=(1,1)" };
            yield return new object[] { @")(", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"[^ab]*", RegexOptions.RightToLeft, "cde", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc", RegexOptions.RightToLeft, "", "Fail." };
            yield return new object[] { @"a*", RegexOptions.RightToLeft, "", "Pass. Group[0]=(0,0)" };
            yield return new object[] { @"([abc])*d", RegexOptions.RightToLeft, "abbbcd", "Pass. Group[0]=(0,6) Group[1]=(4,1)(3,1)(2,1)(1,1)(0,1)" };
            yield return new object[] { @"([abc])*bcd", RegexOptions.RightToLeft, "abcd", "Pass. Group[0]=(0,4) Group[1]=(0,1)" };
            yield return new object[] { @"a|b|c|d|e", RegexOptions.RightToLeft, "e", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"(a|b|c|d|e)f", RegexOptions.RightToLeft, "ef", "Pass. Group[0]=(0,2) Group[1]=(0,1)" };
            yield return new object[] { @"abcd*efg", RegexOptions.RightToLeft, "abcdefg", "Pass. Group[0]=(0,7)" };
            yield return new object[] { @"ab*", RegexOptions.RightToLeft, "xabyabbbz", "Pass. Group[0]=(4,4)" };
            yield return new object[] { @"ab*", RegexOptions.RightToLeft, "xayabbbz", "Pass. Group[0]=(3,4)" };
            yield return new object[] { @"(ab|cd)e", RegexOptions.RightToLeft, "abcde", "Pass. Group[0]=(2,3) Group[1]=(2,2)" };
            yield return new object[] { @"[abhgefdc]ij", RegexOptions.RightToLeft, "hij", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"^(ab|cd)e", RegexOptions.RightToLeft, "abcde", "Fail." };
            yield return new object[] { @"(abc|)ef", RegexOptions.RightToLeft, "abcdef", "Pass. Group[0]=(4,2) Group[1]=(4,0)" };
            yield return new object[] { @"(a|b)c*d", RegexOptions.RightToLeft, "abcd", "Pass. Group[0]=(1,3) Group[1]=(1,1)" };
            yield return new object[] { @"(ab|ab*)bc", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3) Group[1]=(0,1)" };
            yield return new object[] { @"a([bc]*)c*", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3) Group[1]=(1,1)" };
            yield return new object[] { @"a([bc]*)(c*d)", RegexOptions.RightToLeft, "abcd", "Pass. Group[0]=(0,4) Group[1]=(1,1) Group[2]=(2,2)" };
            yield return new object[] { @"a([bc]+)(c*d)", RegexOptions.RightToLeft, "abcd", "Pass. Group[0]=(0,4) Group[1]=(1,1) Group[2]=(2,2)" };
            yield return new object[] { @"a([bc]*)(c+d)", RegexOptions.RightToLeft, "abcd", "Pass. Group[0]=(0,4) Group[1]=(1,1) Group[2]=(2,2)" };
            yield return new object[] { @"a[bcd]*dcdcde", RegexOptions.RightToLeft, "adcdcde", "Pass. Group[0]=(0,7)" };
            yield return new object[] { @"a[bcd]+dcdcde", RegexOptions.RightToLeft, "adcdcde", "Fail." };
            yield return new object[] { @"(ab|a)b*c", RegexOptions.RightToLeft, "abc", "Pass. Group[0]=(0,3) Group[1]=(0,1)" };
            yield return new object[] { @"((a)(b)c)(d)", RegexOptions.RightToLeft, "abcd", "Pass. Group[0]=(0,4) Group[1]=(0,3) Group[2]=(0,1) Group[3]=(1,1) Group[4]=(3,1)" };
            yield return new object[] { @"[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.RightToLeft, "alpha", "Pass. Group[0]=(0,5)" };
            yield return new object[] { @"^a(bc+|b[eh])g|.h$", RegexOptions.RightToLeft, "abh", "Pass. Group[0]=(1,2) Group[1]=" };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.RightToLeft, "effgz", "Pass. Group[0]=(0,5) Group[1]=(0,5) Group[2]=" };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.RightToLeft, "ij", "Pass. Group[0]=(0,2) Group[1]=(0,2) Group[2]=(1,1)" };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.RightToLeft, "effg", "Fail." };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.RightToLeft, "bcdd", "Fail." };
            yield return new object[] { @"(bc+d$|ef*g.|h?i(j|k))", RegexOptions.RightToLeft, "reffgz", "Pass. Group[0]=(1,5) Group[1]=(1,5) Group[2]=" };
            yield return new object[] { @"((((((((((a))))))))))", RegexOptions.RightToLeft, "a", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1) Group[10]=(0,1)" };
            yield return new object[] { @"((((((((((a))))))))))\10", RegexOptions.RightToLeft, "aa", "Fail." };
            yield return new object[] { @"\10((((((((((a))))))))))", RegexOptions.RightToLeft, "aa", "Pass. Group[0]=(0,2) Group[1]=(1,1) Group[2]=(1,1) Group[3]=(1,1) Group[4]=(1,1) Group[5]=(1,1) Group[6]=(1,1) Group[7]=(1,1) Group[8]=(1,1) Group[9]=(1,1) Group[10]=(1,1)" };
            yield return new object[] { @"((((((((((a))))))))))!", RegexOptions.RightToLeft, "aa", "Fail." };
            yield return new object[] { @"((((((((((a))))))))))!", RegexOptions.RightToLeft, "a!", "Pass. Group[0]=(0,2) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1) Group[10]=(0,1)" };
            yield return new object[] { @"(((((((((a)))))))))", RegexOptions.RightToLeft, "a", "Pass. Group[0]=(0,1) Group[1]=(0,1) Group[2]=(0,1) Group[3]=(0,1) Group[4]=(0,1) Group[5]=(0,1) Group[6]=(0,1) Group[7]=(0,1) Group[8]=(0,1) Group[9]=(0,1)" };
            yield return new object[] { @"multiple words of text", RegexOptions.RightToLeft, "uh-uh", "Fail." };
            yield return new object[] { @"multiple words", RegexOptions.RightToLeft, "multiple words, yeah", "Pass. Group[0]=(0,14)" };
            yield return new object[] { @"(.*)c(.*)", RegexOptions.RightToLeft, "abcde", "Pass. Group[0]=(0,5) Group[1]=(0,2) Group[2]=(3,2)" };
            yield return new object[] { @"\((.*), (.*)\)", RegexOptions.RightToLeft, "(a, b)", "Pass. Group[0]=(0,6) Group[1]=(1,1) Group[2]=(4,1)" };
            yield return new object[] { @"[k]", RegexOptions.RightToLeft, "ab", "Fail." };
            yield return new object[] { @"abcd", RegexOptions.RightToLeft, "abcd", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"a(bc)d", RegexOptions.RightToLeft, "abcd", "Pass. Group[0]=(0,4) Group[1]=(1,2)" };
            yield return new object[] { @"a[-]?c", RegexOptions.RightToLeft, "ac", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"(abc)\1", RegexOptions.RightToLeft, "abcabc", "Fail." };
            yield return new object[] { @"\1(abc)", RegexOptions.RightToLeft, "abcabc", "Pass. Group[0]=(0,6) Group[1]=(3,3)" };
            yield return new object[] { @"([a-c]*)\1", RegexOptions.RightToLeft, "abcabc", "Fail." };
            yield return new object[] { @"\1([a-c]*)", RegexOptions.RightToLeft, "abcabc", "Pass. Group[0]=(0,6) Group[1]=(3,3)" };
            yield return new object[] { @"\1", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"\2", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"(a)|\1", RegexOptions.RightToLeft, "a", "Pass. Group[0]=(0,1) Group[1]=(0,1)" };
            yield return new object[] { @"(a)|\1", RegexOptions.RightToLeft, "x", "Fail." };
            yield return new object[] { @"(a)|\2", RegexOptions.RightToLeft, "-", "Error." };
            yield return new object[] { @"(([a-c])b*?\2)*", RegexOptions.RightToLeft, "ababbbcbc", "Pass. Group[0]=(9,0) Group[1]= Group[2]=" };
            yield return new object[] { @"(([a-c])b*?\2){3}", RegexOptions.RightToLeft, "ababbbcbc", "Fail." };
            yield return new object[] { @"((\3|b)\2(a)x)+", RegexOptions.RightToLeft, "aaxabxbaxbbx", "Fail." };
            yield return new object[] { @"((\3|b)\2(a)x)+", RegexOptions.RightToLeft, "aaaxabaxbaaxbbax", "Fail." };
            yield return new object[] { @"((\3|b)\2(a)){2,}", RegexOptions.RightToLeft, "bbaababbabaaaaabbaaaabba", "Fail." };
            yield return new object[] { @"\((?>[^()]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!))\)", RegexOptions.None, "((a(b))c)", "Pass. Group[0]=(0,9) Group[1]=" };
            yield return new object[] { @"^\((?>[^()]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!))\)$", RegexOptions.None, "((a(b))c)", "Pass. Group[0]=(0,9) Group[1]=" };
            yield return new object[] { @"^\((?>[^()]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!))\)$", RegexOptions.None, "((a(b))c", "Fail." };
            yield return new object[] { @"^\((?>[^()]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!))\)$", RegexOptions.None, "())", "Fail." };
            yield return new object[] { @"(((?<foo>\()[^()]*)+((?<bar-foo>\))[^()]*)+)+(?(foo)(?!))", RegexOptions.None, "((a(b))c)", "Pass. Group[0]=(0,9) Group[1]=(0,9) Group[2]=(0,1)(1,2)(3,2) Group[3]=(5,1)(6,2)(8,1) Group[4]= Group[5]=(4,1)(2,4)(1,7)" };
            yield return new object[] { @"^(((?<foo>\()[^()]*)+((?<bar-foo>\))[^()]*)+)+(?(foo)(?!))$", RegexOptions.None, "((a(b))c)", "Pass. Group[0]=(0,9) Group[1]=(0,9) Group[2]=(0,1)(1,2)(3,2) Group[3]=(5,1)(6,2)(8,1) Group[4]= Group[5]=(4,1)(2,4)(1,7)" };
            yield return new object[] { @"(((?<foo>\()[^()]*)+((?<bar-foo>\))[^()]*)+)+(?(foo)(?!))", RegexOptions.None, "x(a((b)))b)x", "Pass. Group[0]=(1,9) Group[1]=(1,9) Group[2]=(1,2)(3,1)(4,2) Group[3]=(6,1)(7,1)(8,2) Group[4]= Group[5]=(5,1)(4,3)(2,6)" };
            yield return new object[] { @"(((?<foo>\()[^()]*)+((?<bar-foo>\))[^()]*)+)+(?(foo)(?!))", RegexOptions.None, "x((a((b)))x", "Pass. Group[0]=(2,9) Group[1]=(2,9) Group[2]=(2,2)(4,1)(5,2) Group[3]=(7,1)(8,1)(9,2) Group[4]= Group[5]=(6,1)(5,3)(3,6)" };
            yield return new object[] { @"^(((?<foo>\()[^()]*)+((?<bar-foo>\))[^()]*)+)+(?(foo)(?!))$", RegexOptions.None, "((a(b))c", "Fail." };
            yield return new object[] { @"^(((?<foo>\()[^()]*)+((?<bar-foo>\))[^()]*)+)+(?(foo)(?!))$", RegexOptions.None, "((a(b))c))", "Fail." };
            yield return new object[] { @"^(((?<foo>\()[^()]*)+((?<bar-foo>\))[^()]*)+)+(?(foo)(?!))$", RegexOptions.None, ")(", "Fail." };
            yield return new object[] { @"^(((?<foo>\()[^()]*)+((?<bar-foo>\))[^()]*)+)+(?(foo)(?!))$", RegexOptions.None, "((a((b))c)", "Fail." };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[n]", "Pass. Group[0]=(0,3) Group[1]=(1,1)" };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "n", "Pass. Group[0]=(0,1) Group[1]=(0,1)" };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "n[i]e", "Fail." };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[n", "Fail." };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "]n]", "Fail." };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, @"\[n\]", "Fail." };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, @"[n\]", "Pass. Group[0]=(0,4) Group[1]=(1,2)" };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, @"[n\[]", "Pass. Group[0]=(0,5) Group[1]=(1,3)" };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, @"[[n]", "Pass. Group[0]=(0,4) Group[1]=(1,2)" };
            yield return new object[] { @"^((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[s] . [n]", "Pass. Group[0]=(0,9) Group[1]=(1,1) Group[2]=(7,1)" };
            yield return new object[] { @"^((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[s] . n", "Pass. Group[0]=(0,7) Group[1]=(1,1) Group[2]=(6,1)" };
            yield return new object[] { @"^((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "s.[ n ]", "Pass. Group[0]=(0,7) Group[1]=(0,1) Group[2]=(3,3)" };
            yield return new object[] { @"^((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, " . n", "Pass. Group[0]=(0,4) Group[1]=(0,1) Group[2]=(3,1)" };
            yield return new object[] { @"^((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "s. ", "Pass. Group[0]=(0,3) Group[1]=(0,1) Group[2]=(2,1)" };
            yield return new object[] { @"^((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[.]. ", "Pass. Group[0]=(0,5) Group[1]=(1,1) Group[2]=(4,1)" };
            yield return new object[] { @"^((\[(?<CATALOG>[^\]]+)\])|(?<CATALOG>[^\.\[\]]+))\s*\.\s*((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[c].[s].[n]", "Pass. Group[0]=(0,11) Group[1]=(1,1) Group[2]=(5,1) Group[3]=(9,1)" };
            yield return new object[] { @"^((\[(?<CATALOG>[^\]]+)\])|(?<CATALOG>[^\.\[\]]+))\s*\.\s*((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, " c . s . n ", "Pass. Group[0]=(0,11) Group[1]=(0,3) Group[2]=(5,2) Group[3]=(9,2)" };
            yield return new object[] { @"^((\[(?<CATALOG>[^\]]+)\])|(?<CATALOG>[^\.\[\]]+))\s*\.\s*((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, " . [.] . [ ]", "Pass. Group[0]=(0,12) Group[1]=(0,1) Group[2]=(4,1) Group[3]=(10,1)" };
            yield return new object[] { @"^((\[(?<CATALOG>[^\]]+)\])|(?<CATALOG>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "c.n", "Pass. Group[0]=(0,3) Group[1]=(0,1) Group[2]=(2,1)" };
            yield return new object[] { @"^((\[(?<CATALOG>[^\]]+)\])|(?<CATALOG>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[c] .[n]", "Pass. Group[0]=(0,8) Group[1]=(1,1) Group[2]=(6,1)" };
            yield return new object[] { @"^((\[(?<CATALOG>[^\]]+)\])|(?<CATALOG>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "c.n.", "Fail." };
            yield return new object[] { @"^((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<CATALOG>[^\]]+)\])|(?<CATALOG>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "s.c.n", "Pass. Group[0]=(0,5) Group[1]=(0,1) Group[2]=(2,1) Group[3]=(4,1)" };
            yield return new object[] { @"^((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<CATALOG>[^\]]+)\])|(?<CATALOG>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[s].[c].[n]", "Pass. Group[0]=(0,11) Group[1]=(1,1) Group[2]=(5,1) Group[3]=(9,1)" };
            yield return new object[] { @"^((\[(?<SCHEMA>[^\]]+)\])|(?<SCHEMA>[^\.\[\]]+))\s*\.\s*((\[(?<CATALOG>[^\]]+)\])|(?<CATALOG>[^\.\[\]]+))\s*\.\s*((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[s].[c].", "Fail." };
            yield return new object[] { @"^((\[(?<ColName>.+)\])|(?<ColName>\S+))([ ]+(?<Order>ASC|DESC))?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "[id]]", "Pass. Group[0]=(0,5) Group[1]=(1,3) Group[2]=" };
            yield return new object[] { @"a{1,2147483647}", RegexOptions.None, "a", "Pass. Group[0]=(0,1)" };
            yield return new object[] { @"^((\[(?<NAME>[^\]]+)\])|(?<NAME>[^\.\[\]]+))$", RegexOptions.None, "[a]", "Pass. Group[0]=(0,3) Group[1]=(0,3) Group[2]=(0,3) Group[3]=(1,1)" };

            // Ported from https://github.com/mono/mono/blob/0f2995e95e98e082c7c7039e17175cf2c6a00034/mcs/class/System/Test/System.Text.RegularExpressions/RegexMatchTests.cs
            yield return new object[] { @"(a)(b)(c)", RegexOptions.ExplicitCapture, "abc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"(a)(?<1>b)(c)", RegexOptions.ExplicitCapture, "abc", "Pass. Group[0]=(0,3) Group[1]=(1,1)" };
            yield return new object[] { @"(a)(?<2>b)(c)", RegexOptions.None, "abc", "Pass. Group[0]=(0,3) Group[1]=(0,1) Group[2]=(1,1)(2,1)" };
            yield return new object[] { @"(a)(?<foo>b)(c)", RegexOptions.ExplicitCapture, "abc", "Pass. Group[0]=(0,3) Group[1]=(1,1)" };
            yield return new object[] { @"(F)(2)(3)(4)(5)(6)(7)(8)(9)(10)(L)\11", RegexOptions.None, "F2345678910LL", "Pass. Group[0]=(0,13) Group[1]=(0,1) Group[2]=(1,1) Group[3]=(2,1) Group[4]=(3,1) Group[5]=(4,1) Group[6]=(5,1) Group[7]=(6,1) Group[8]=(7,1) Group[9]=(8,1) Group[10]=(9,2) Group[11]=(11,1)" };
            yield return new object[] { @"(F)(2)(3)(4)(5)(6)(7)(8)(9)(10)(L)\11", RegexOptions.ExplicitCapture, "F2345678910LL", "Fail." };
            yield return new object[] { @"(F)(2)(3)(4)(5)(6)(?<S>7)(8)(9)(10)(L)\1", RegexOptions.None, "F2345678910L71", "Fail." };
            yield return new object[] { @"(F)(2)(3)(4)(5)(6)(7)(8)(9)(10)(L)\11", RegexOptions.None, "F2345678910LF1", "Fail." };
            yield return new object[] { @"(F)(2)(3)(4)(5)(6)(?<S>7)(8)(9)(10)(L)\11", RegexOptions.None, "F2345678910L71", "Pass. Group[0]=(0,13) Group[1]=(0,1) Group[2]=(1,1) Group[3]=(2,1) Group[4]=(3,1) Group[5]=(4,1) Group[6]=(5,1) Group[7]=(7,1) Group[8]=(8,1) Group[9]=(9,2) Group[10]=(11,1) Group[11]=(6,1)" };
            yield return new object[] { @"(F)(2)(3)(?<S>4)(5)(6)(?'S'7)(8)(9)(10)(L)\10", RegexOptions.None, "F2345678910L71", "Pass. Group[0]=(0,13) Group[1]=(0,1) Group[2]=(1,1) Group[3]=(2,1) Group[4]=(4,1) Group[5]=(5,1) Group[6]=(7,1) Group[7]=(8,1) Group[8]=(9,2) Group[9]=(11,1) Group[10]=(3,1)(6,1)" };
            yield return new object[] { @"(F)(2)(3)(?<S>4)(5)(6)(?'S'7)(8)(9)(10)(L)\10", RegexOptions.ExplicitCapture, "F2345678910L70", "Fail." };
            yield return new object[] { @"(F)(2)(3)(?<S>4)(5)(6)(?'S'7)(8)(9)(10)(L)\1", RegexOptions.ExplicitCapture, "F2345678910L70", "Pass. Group[0]=(0,13) Group[1]=(3,1)(6,1)" };
            yield return new object[] { @"(?n:(F)(2)(3)(?<S>4)(5)(6)(?'S'7)(8)(9)(10)(L)\1)", RegexOptions.None, "F2345678910L70", "Pass. Group[0]=(0,13) Group[1]=(3,1)(6,1)" };
            yield return new object[] { @"(F)(2)(3)(?<S>4)(5)(6)(?'S'7)(8)(9)(10)(L)(?(10)\10)", RegexOptions.None, "F2345678910L70", "Pass. Group[0]=(0,13) Group[1]=(0,1) Group[2]=(1,1) Group[3]=(2,1) Group[4]=(4,1) Group[5]=(5,1) Group[6]=(7,1) Group[7]=(8,1) Group[8]=(9,2) Group[9]=(11,1) Group[10]=(3,1)(6,1)" };
            yield return new object[] { @"(F)(2)(3)(?<S>4)(5)(6)(?'S'7)(8)(9)(10)(L)(?(S)|\10)", RegexOptions.None, "F2345678910L70", "Pass. Group[0]=(0,12) Group[1]=(0,1) Group[2]=(1,1) Group[3]=(2,1) Group[4]=(4,1) Group[5]=(5,1) Group[6]=(7,1) Group[7]=(8,1) Group[8]=(9,2) Group[9]=(11,1) Group[10]=(3,1)(6,1)" };
            yield return new object[] { @"(F)(2)(3)(?<S>4)(5)(6)(?'S'7)(8)(9)(10)(L)(?(7)|\10)", RegexOptions.None, "F2345678910L70", "Pass. Group[0]=(0,12) Group[1]=(0,1) Group[2]=(1,1) Group[3]=(2,1) Group[4]=(4,1) Group[5]=(5,1) Group[6]=(7,1) Group[7]=(8,1) Group[8]=(9,2) Group[9]=(11,1) Group[10]=(3,1)(6,1)" };
            yield return new object[] { @"(F)(2)(3)(?<S>4)(5)(6)(?'S'7)(8)(9)(10)(L)(?(K)|\10)", RegexOptions.None, "F2345678910L70", "Pass. Group[0]=(0,13) Group[1]=(0,1) Group[2]=(1,1) Group[3]=(2,1) Group[4]=(4,1) Group[5]=(5,1) Group[6]=(7,1) Group[7]=(8,1) Group[8]=(9,2) Group[9]=(11,1) Group[10]=(3,1)(6,1)" };
            yield return new object[] { @"\P{IsHebrew}", RegexOptions.None, "\u05D0a", "Pass. Group[0]=(1,1)" };
            yield return new object[] { @"\p{IsHebrew}", RegexOptions.None, "abc\u05D0def", "Pass. Group[0]=(3,1)" };
            yield return new object[] { @"(?<=a+)(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"(?<=a*)(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"(?<=a{1,5})(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"(?<=a{1})(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"(?<=a{1,})(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"(?<=a+?)(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"(?<=a*?)(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"(?<=a{1,5}?)(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"(?<=a{1}?)(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(1,3)" };
            yield return new object[] { @"(?<!a+)(?:a)*bc", RegexOptions.None, "aabc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"(?<!a*)(?:a)*bc", RegexOptions.None, "aabc", "Fail." };
            yield return new object[] { @"abc*(?=c*)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"abc*(?=c+)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc*(?=c{1})", RegexOptions.None, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc*(?=c{1,5})", RegexOptions.None, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc*(?=c{1,})", RegexOptions.None, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc*(?=c*?)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"abc*(?=c+?)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc*(?=c{1}?)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc*(?=c{1,5}?)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc*(?=c{1,}?)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,3)" };
            yield return new object[] { @"abc*?(?=c*)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"abc*?(?=c+)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"abc*?(?=c{1})", RegexOptions.None, "abcc", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"abc*?(?=c{1,5})", RegexOptions.None, "abcc", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"abc*?(?=c{1,})", RegexOptions.None, "abcc", "Pass. Group[0]=(0,2)" };
            yield return new object[] { @"abc*(?!c*)", RegexOptions.None, "abcc", "Fail." };
            yield return new object[] { @"abc*(?!c+)", RegexOptions.None, "abcc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"abc*(?!c{1})", RegexOptions.None, "abcc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"abc*(?!c{1,5})", RegexOptions.None, "abcc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"abc*(?!c{1,})", RegexOptions.None, "abcc", "Pass. Group[0]=(0,4)" };
            yield return new object[] { @"(a)(?<1>b)(?'1'c)", RegexOptions.ExplicitCapture, "abc", "Pass. Group[0]=(0,3) Group[1]=(1,1)(2,1)" };
            yield return new object[] { @"(?<ab>ab)c\1", RegexOptions.None, "abcabc", "Pass. Group[0]=(0,5) Group[1]=(0,2)" };
            yield return new object[] { @"\1", RegexOptions.ECMAScript, "-", "Fail." };
            yield return new object[] { @"\2", RegexOptions.ECMAScript, "-", "Fail." };
            yield return new object[] { @"(a)|\2", RegexOptions.ECMAScript, "-", "Fail." };
            yield return new object[] { @"\4400", RegexOptions.None, "asdf 012", "Pass. Group[0]=(4,2)" };
            yield return new object[] { @"\4400", RegexOptions.ECMAScript, "asdf 012", "Fail." };
            yield return new object[] { @"\4400", RegexOptions.None, "asdf$0012", "Fail." };
            yield return new object[] { @"\4400", RegexOptions.ECMAScript, "asdf$0012", "Pass. Group[0]=(4,3)" };
            yield return new object[] { @"(?<2>ab)(?<c>c)(?<d>d)", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4) Group[1]=(2,1) Group[2]=(0,2) Group[3]=(3,1)" };// 61
            yield return new object[] { @"(?<1>ab)(c)", RegexOptions.None, "abc", "Pass. Group[0]=(0,3) Group[1]=(0,2)(2,1)" };
            yield return new object[] { @"(?<44>a)", RegexOptions.None, "a", "Pass. Group[0]=(0,1) Group[44]=(0,1)" };
            yield return new object[] { @"(?<44>a)(?<8>b)", RegexOptions.None, "ab", "Pass. Group[0]=(0,2) Group[8]=(1,1) Group[44]=(0,1)" };
            yield return new object[] { @"(?<44>a)(?<8>b)(?<1>c)(d)", RegexOptions.None, "abcd", "Pass. Group[0]=(0,4) Group[1]=(2,1)(3,1) Group[8]=(1,1) Group[44]=(0,1)" };
            yield return new object[] { @"(?<44>a)(?<44>b)", RegexOptions.None, "ab", "Pass. Group[0]=(0,2) Group[44]=(0,1)(1,1)" };
            yield return new object[] { @"(?<44>a)\440", RegexOptions.None, "a ", "Pass. Group[0]=(0,2) Group[44]=(0,1)" };
            yield return new object[] { @"(?<44>a)\440", RegexOptions.ECMAScript, "a ", "Fail." };
            yield return new object[] { @"(?<44>a)\440", RegexOptions.None, "aa0", "Fail." };
            yield return new object[] { @"(?<44>a)\440", RegexOptions.ECMAScript, "aa0", "Pass. Group[0]=(0,3) Group[44]=(0,1)" };
        }
    }

    public class AttRegexTests_SRM
    {
        static RegexOptions DFA = (RegexOptions)0x400;

        static void WriteLine(string s)
        {
#if DEBUG
            // the string will appear in the Output window
            System.Diagnostics.Debug.WriteLine(s);
#endif
        }

        [Theory]
        // basic.dat
        [InlineData("abracadabra$", "abracadabracadabra", "(7,18)")]
        [InlineData("a...b", "abababbb", "(2,7)")]
        [InlineData("XXXXXX", "..XXXXXX", "(2,8)")]
        [InlineData("\\)", "()", "(1,2)")]
        [InlineData("a]", "a]a", "(0,2)")]
        [InlineData("}", "}", "(0,1)")]
        [InlineData("\\}", "}", "(0,1)")]
        [InlineData("\\]", "]", "(0,1)")]
        [InlineData("]", "]", "(0,1)")]
        [InlineData("{", "{", "(0,1)")]
        [InlineData("^a", "ax", "(0,1)")]
        [InlineData("\\^a", "a^a", "(1,3)")]
        [InlineData("a\\^", "a^", "(0,2)")]
        [InlineData("a$", "aa", "(1,2)")]
        [InlineData("a\\$", "a$", "(0,2)")]
        [InlineData("^$", "NULL", "(0,0)")]
        [InlineData("$^", "NULL", "(0,0)")]
        [InlineData("a($)", "aa", "(1,2)(2,2)")]
        [InlineData("a*(^a)", "aa", "(0,1)(0,1)")]
        [InlineData("(..)*(...)*", "a", "(0,0)")]
        [InlineData("(..)*(...)*", "abcd", "(0,4)(2,4)")]
        [InlineData("(ab|a)(bc|c)", "abc", "(0,3)(0,2)(2,3)")]
        [InlineData("(ab)c|abc", "abc", "(0,3)(0,2)")]
        [InlineData("a{0}b", "ab", "(1,2)")]
        [InlineData("(a*)(b?)(b+)b{3}", "aaabbbbbbb", "(0,10)(0,3)(3,4)(4,7)")]
        [InlineData("(a*)(b{0,1})(b{1,})b{3}", "aaabbbbbbb", "(0,10)(0,3)(3,4)(4,7)")]
        [InlineData("a{9876543210}", "NULL", "BADBR")]
        [InlineData("((a|a)|a)", "a", "(0,1)(0,1)(0,1)")]
        [InlineData("(a*)(a|aa)", "aaaa", "(0,4)(0,3)(3,4)")]
        [InlineData("a*(a.|aa)", "aaaa", "(0,4)(2,4)")]
        [InlineData("(a|b)?.*", "b", "(0,1)(0,1)")]
        [InlineData("(a|b)c|a(b|c)", "ac", "(0,2)(0,1)")]
        [InlineData("(a|b)*c|(a|ab)*c", "abc", "(0,3)(1,2)")]
        [InlineData("(a|b)*c|(a|ab)*c", "xc", "(1,2)")]
        [InlineData("(.a|.b).*|.*(.a|.b)", "xa", "(0,2)(0,2)")]
        [InlineData("a?(ab|ba)ab", "abab", "(0,4)(0,2)")]
        [InlineData("a?(ac{0}b|ba)ab", "abab", "(0,4)(0,2)")]
        [InlineData("ab|abab", "abbabab", "(0,2)")]
        [InlineData("aba|bab|bba", "baaabbbaba", "(5,8)")]
        [InlineData("aba|bab", "baaabbbaba", "(6,9)")]
        [InlineData("(aa|aaa)*|(a|aaaaa)", "aa", "(0,2)(0,2)")]
        [InlineData("(a.|.a.)*|(a|.a...)", "aa", "(0,2)(0,2)")]
        [InlineData("ab|a", "xabc", "(1,2)")] // is (1,3) in non-DFA mode in AttRegexTests.Test
        [InlineData("ab|a", "xxabc", "(2,3)")] // is (2,4) in non-DFA mode in AttRegexTests.Test
        [InlineData("(?i)(Ab|cD)*", "aBcD", "(0,4)(2,4)")]
        [InlineData("[^-]", "--a", "(2,3)")]
        [InlineData("[a-]*", "--a", "(0,3)")]
        [InlineData("[a-m-]*", "--amoma--", "(0,4)")]
        [InlineData(":::1:::0:|:::1:1:0:", ":::0:::1:::1:::0:", "(8,17)")]
        [InlineData(":::1:::0:|:::1:1:1:", ":::0:::1:::1:::0:", "(8,17)")]
        [InlineData("\n", "\n", "(0,1)")]
        [InlineData("[^a]", "\n", "(0,1)")]
        [InlineData("\na", "\na", "(0,2)")]
        [InlineData("(a)(b)(c)", "abc", "(0,3)(0,1)(1,2)(2,3)")]
        [InlineData("xxx", "xxx", "(0,3)")]
        [InlineData("(^|[ (,;])((([Ff]eb[^ ]* *|0*2/|\\* */?)0*[6-7]))([^0-9]|$)", "feb 6,", "(0,6)")]
        [InlineData("(^|[ (,;])((([Ff]eb[^ ]* *|0*2/|\\* */?)0*[6-7]))([^0-9]|$)", "2/7", "(0,3)")]
        [InlineData("(^|[ (,;])((([Ff]eb[^ ]* *|0*2/|\\* */?)0*[6-7]))([^0-9]|$)", "feb 1,Feb 6", "(5,11)")]
        [InlineData("((((((((((((((((((((((((((((((x))))))))))))))))))))))))))))))", "x", "(0,1)(0,1)(0,1)")]
        [InlineData("((((((((((((((((((((((((((((((x))))))))))))))))))))))))))))))*", "xx", "(0,2)(1,2)(1,2)")]
        [InlineData("a?(ab|ba)*", "ababababababababababababababababababababababababababababababababababababababababa", "(0,81)(79,81)")]
        [InlineData("abaa|abbaa|abbbaa|abbbbaa", "ababbabbbabbbabbbbabbbbaa", "(18,25)")]
        [InlineData("abaa|abbaa|abbbaa|abbbbaa", "ababbabbbabbbabbbbabaa", "(18,22)")]
        [InlineData("aaac|aabc|abac|abbc|baac|babc|bbac|bbbc", "baaabbbabac", "(7,11)")]
        [InlineData(".*", "\x0001\x00ff", "(0,2)")]
        [InlineData("aaaa|bbbb|cccc|ddddd|eeeeee|fffffff|gggg|hhhh|iiiii|jjjjj|kkkkk|llll", "XaaaXbbbXcccXdddXeeeXfffXgggXhhhXiiiXjjjXkkkXlllXcbaXaaaa", "(53,57)")]
        [InlineData("aaaa\nbbbb\ncccc\nddddd\neeeeee\nfffffff\ngggg\nhhhh\niiiii\njjjjj\nkkkkk\nllll", "XaaaXbbbXcccXdddXeeeXfffXgggXhhhXiiiXjjjXkkkXlllXcbaXaaaa", "NOMATCH")]
        [InlineData("a*a*a*a*a*b", "aaaaaaaaab", "(0,10)")]
        [InlineData("^", "NULL", "(0,0)")]
        [InlineData("$", "NULL", "(0,0)")]
        [InlineData("^a$", "a", "(0,1)")]
        [InlineData("abc", "abc", "(0,3)")]
        [InlineData("abc", "xabcy", "(1,4)")]
        [InlineData("abc", "ababc", "(2,5)")]
        [InlineData("ab*c", "abc", "(0,3)")]
        [InlineData("ab*bc", "abc", "(0,3)")]
        [InlineData("ab*bc", "abbc", "(0,4)")]
        [InlineData("ab*bc", "abbbbc", "(0,6)")]
        [InlineData("ab+bc", "abbc", "(0,4)")]
        [InlineData("ab+bc", "abbbbc", "(0,6)")]
        [InlineData("ab?bc", "abbc", "(0,4)")]
        [InlineData("ab?bc", "abc", "(0,3)")]
        [InlineData("ab?c", "abc", "(0,3)")]
        [InlineData("^abc$", "abc", "(0,3)")]
        [InlineData("^abc", "abcc", "(0,3)")]
        [InlineData("abc$", "aabc", "(1,4)")]
        [InlineData("^", "abc", "(0,0)")]
        [InlineData("$", "abc", "(3,3)")]
        [InlineData("a.c", "abc", "(0,3)")]
        [InlineData("a.c", "axc", "(0,3)")]
        [InlineData("a.*c", "axyzc", "(0,5)")]
        [InlineData("a[bc]d", "abd", "(0,3)")]
        [InlineData("a[b-d]e", "ace", "(0,3)")]
        [InlineData("a[b-d]", "aac", "(1,3)")]
        [InlineData("a[-b]", "a-", "(0,2)")]
        [InlineData("a[b-]", "a-", "(0,2)")]
        [InlineData("a]", "a]", "(0,2)")]
        [InlineData("a[]]b", "a]b", "(0,3)")]
        [InlineData("a[^bc]d", "aed", "(0,3)")]
        [InlineData("a[^-b]c", "adc", "(0,3)")]
        [InlineData("a[^]b]c", "adc", "(0,3)")]
        [InlineData("ab|cd", "abc", "(0,2)")]
        [InlineData("ab|cd", "abcd", "(0,2)")]
        [InlineData("a\\(b", "a(b", "(0,3)")]
        [InlineData("a\\(*b", "ab", "(0,2)")]
        [InlineData("a\\(*b", "a((b", "(0,4)")]
        [InlineData("((a))", "abc", "(0,1)(0,1)(0,1)")]
        [InlineData("(a)b(c)", "abc", "(0,3)(0,1)(2,3)")]
        [InlineData("a+b+c", "aabbabc", "(4,7)")]
        [InlineData("a*", "aaa", "(0,3)")]
        [InlineData("(a*)*", "-", "(0,0)(0,0)")]
        [InlineData("(a*)+", "-", "(0,0)(0,0)")]
        [InlineData("(a*|b)*", "-", "(0,0)(0,0)")]
        [InlineData("(a+|b)*", "ab", "(0,2)(1,2)")]
        [InlineData("(a+|b)+", "ab", "(0,2)(1,2)")]
        [InlineData("(a+|b)?", "ab", "(0,1)(0,1)")]
        [InlineData("[^ab]*", "cde", "(0,3)")]
        [InlineData("(^)*", "-", "(0,0)(0,0)")]
        [InlineData("a*", "NULL", "(0,0)")]
        [InlineData("([abc])*d", "abbbcd", "(0,6)(4,5)")]
        [InlineData("([abc])*bcd", "abcd", "(0,4)(0,1)")]
        [InlineData("a|b|c|d|e", "e", "(0,1)")]
        [InlineData("(a|b|c|d|e)f", "ef", "(0,2)(0,1)")]
        [InlineData("((a*|b))*", "-", "(0,0)(0,0)(0,0)")]
        [InlineData("abcd*efg", "abcdefg", "(0,7)")]
        [InlineData("ab*", "xabyabbbz", "(1,3)")]
        [InlineData("ab*", "xayabbbz", "(1,2)")]
        [InlineData("(ab|cd)e", "abcde", "(2,5)(2,4)")]
        [InlineData("[abhgefdc]ij", "hij", "(0,3)")]
        [InlineData("(a|b)c*d", "abcd", "(1,4)(1,2)")]
        [InlineData("(ab|ab*)bc", "abc", "(0,3)(0,1)")]
        [InlineData("a([bc]*)c*", "abc", "(0,3)(1,3)")]
        [InlineData("a([bc]*)(c*d)", "abcd", "(0,4)(1,3)(3,4)")]
        [InlineData("a([bc]+)(c*d)", "abcd", "(0,4)(1,3)(3,4)")]
        [InlineData("a([bc]*)(c+d)", "abcd", "(0,4)(1,2)(2,4)")]
        [InlineData("a[bcd]*dcdcde", "adcdcde", "(0,7)")]
        [InlineData("(ab|a)b*c", "abc", "(0,3)(0,2)")]
        [InlineData("((a)(b)c)(d)", "abcd", "(0,4)(0,3)(0,1)(1,2)(3,4)")]
        [InlineData("[A-Za-z_][A-Za-z0-9_]*", "alpha", "(0,5)")]
        [InlineData("^a(bc+|b[eh])g|.h$", "abh", "(1,3)")]
        [InlineData("(bc+d$|ef*g.|h?i(j|k))", "effgz", "(0,5)(0,5)")]
        [InlineData("(bc+d$|ef*g.|h?i(j|k))", "ij", "(0,2)(0,2)(1,2)")]
        [InlineData("(bc+d$|ef*g.|h?i(j|k))", "reffgz", "(1,6)(1,6)")]
        [InlineData("(((((((((a)))))))))", "a", "(0,1)(0,1)(0,1)(0,1)(0,1)(0,1)(0,1)(0,1)(0,1)(0,1)")]
        [InlineData("multiple words", "multiple words yeah", "(0,14)")]
        [InlineData("(.*)c(.*)", "abcde", "(0,5)(0,2)(3,5)")]
        [InlineData("abcd", "abcd", "(0,4)")]
        [InlineData("a(bc)d", "abcd", "(0,4)(1,3)")]
        [InlineData("a[-]?c", "ac", "(0,3)")]
        [InlineData("a+(b|c)*d+", "aabcdd", "(0,6)(3,4)")]
        [InlineData("^.+$", "vivi", "(0,4)")]
        [InlineData("^(.+)$", "vivi", "(0,4)(0,4)")]
        [InlineData("^([^!.]+).att.com!(.+)$", "gryphon.att.com!eby", "(0,19)(0,7)(16,19)")]
        [InlineData("^([^!]+!)?([^!]+)$", "bar!bas", "(0,7)(0,4)(4,7)")]
        [InlineData("^([^!]+!)?([^!]+)$", "foo!bas", "(0,7)(0,4)(4,7)")]
        [InlineData("^.+!([^!]+!)([^!]+)$", "foo!bar!bas", "(0,11)(4,8)(8,11)")]
        [InlineData("((foo)|(bar))!bas", "foo!bas", "(0,7)(0,3)(0,3)")]
        [InlineData("((foo)|bar)!bas", "bar!bas", "(0,7)(0,3)")]
        [InlineData("((foo)|bar)!bas", "foo!bar!bas", "(4,11)(4,7)")]
        [InlineData("((foo)|bar)!bas", "foo!bas", "(0,7)(0,3)(0,3)")]
        [InlineData("(foo|(bar))!bas", "bar!bas", "(0,7)(0,3)(0,3)")]
        [InlineData("(foo|(bar))!bas", "foo!bar!bas", "(4,11)(4,7)(4,7)")]
        [InlineData("(foo|(bar))!bas", "foo!bas", "(0,7)(0,3)")]
        [InlineData("(foo|bar)!bas", "bar!bas", "(0,7)(0,3)")]
        [InlineData("(foo|bar)!bas", "foo!bar!bas", "(4,11)(4,7)")]
        [InlineData("(foo|bar)!bas", "foo!bas", "(0,7)(0,3)")]
        [InlineData("^([^!]+!)?([^!]+)$|^.+!([^!]+!)([^!]+)$", "bar!bas", "(0,7)(0,4)(4,7)")]
        [InlineData("^([^!]+!)?([^!]+)$|^.+!([^!]+!)([^!]+)$", "foo!bas", "(0,7)(0,4)(4,7)")]
        [InlineData("^(([^!]+!)?([^!]+)|.+!([^!]+!)([^!]+))$", "bar!bas", "(0,7)(0,7)(0,4)(4,7)")]
        [InlineData("^(([^!]+!)?([^!]+)|.+!([^!]+!)([^!]+))$", "foo!bas", "(0,7)(0,7)(0,4)(4,7)")]
        [InlineData(".*(/XXX).*", "/XXX", "(0,4)(0,4)")]
        [InlineData(".*(\\\\XXX).*", "\\XXX", "(0,4)(0,4)")]
        [InlineData("\\\\XXX", "\\XXX", "(0,4)")]
        [InlineData(".*(/000).*", "/000", "(0,4)(0,4)")]
        [InlineData(".*(\\\\000).*", "\\000", "(0,4)(0,4)")]
        [InlineData("\\\\000", "\\000", "(0,4)")]

        // repetition.dat
        [InlineData("((..)|(.))", "NULL", "NOMATCH")]
        [InlineData("((..)|(.))((..)|(.))", "NULL", "NOMATCH")]
        [InlineData("((..)|(.))((..)|(.))((..)|(.))", "NULL", "NOMATCH")]
        [InlineData("((..)|(.)){1}", "NULL", "NOMATCH")]
        [InlineData("((..)|(.)){2}", "NULL", "NOMATCH")]
        [InlineData("((..)|(.)){3}", "NULL", "NOMATCH")]
        [InlineData("((..)|(.))*", "NULL", "(0,0)")]
        [InlineData("((..)|(.))((..)|(.))", "a", "NOMATCH")]
        [InlineData("((..)|(.))((..)|(.))((..)|(.))", "a", "NOMATCH")]
        [InlineData("((..)|(.)){2}", "a", "NOMATCH")]
        [InlineData("((..)|(.)){3}", "a", "NOMATCH")]
        [InlineData("((..)|(.))((..)|(.))((..)|(.))", "aa", "NOMATCH")]
        [InlineData("((..)|(.)){3}", "aa", "NOMATCH")]
        [InlineData("X(.?){0,}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){1,}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){2,}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){3,}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){4,}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){5,}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){6,}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){7,}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){8,}Y", "X1234567Y", "(0,9)(8,8)")]
        [InlineData("X(.?){0,8}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){1,8}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){2,8}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){3,8}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){4,8}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){5,8}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){6,8}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){7,8}Y", "X1234567Y", "(0,9)(8,8)")] // was "(0,9)(7,8)"
        [InlineData("X(.?){8,8}Y", "X1234567Y", "(0,9)(8,8)")]
        [InlineData("(a|ab|c|bcd){0,}(d*)", "ababcd", "(0,1)(1,1)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(a|ab|c|bcd){1,}(d*)", "ababcd", "(0,6)")] // is "(0,1)(1,1)" in non DFA mode and was "(0,6)(3,6)(6,6)"
        [InlineData("(a|ab|c|bcd){2,}(d*)", "ababcd", "(0,6)(3,6)(6,6)")]
        [InlineData("(a|ab|c|bcd){3,}(d*)", "ababcd", "(0,6)(3,6)(6,6)")]
        [InlineData("(a|ab|c|bcd){4,}(d*)", "ababcd", "NOMATCH")]
        [InlineData("(a|ab|c|bcd){0,10}(d*)", "ababcd", "(0,1)(1,1)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(a|ab|c|bcd){1,10}(d*)", "ababcd", "(0,6)")] // is "(0,1)(1,1)" in non DFA mode and was "(0,6)(3,6)(6,6)"
        [InlineData("(a|ab|c|bcd){2,10}(d*)", "ababcd", "(0,6)(3,6)(6,6)")]
        [InlineData("(a|ab|c|bcd){3,10}(d*)", "ababcd", "(0,6)(3,6)(6,6)")]
        [InlineData("(a|ab|c|bcd){4,10}(d*)", "ababcd", "NOMATCH")]
        [InlineData("(a|ab|c|bcd)*(d*)", "ababcd", "(0,1)(1,1)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(a|ab|c|bcd)+(d*)", "ababcd", "(0,6)")] // is "(0,1)(1,1)" in non DFA mode was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd){0,}(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd){1,}(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd){2,}(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd){3,}(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd){4,}(d*)", "ababcd", "NOMATCH")]
        [InlineData("(ab|a|c|bcd){0,10}(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd){1,10}(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd){2,10}(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd){3,10}(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd){4,10}(d*)", "ababcd", "NOMATCH")]
        [InlineData("(ab|a|c|bcd)*(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"
        [InlineData("(ab|a|c|bcd)+(d*)", "ababcd", "(0,6)(4,5)(5,6)")] // was "(0,6)(3,6)(6,6)"

        // unknownassoc.dat
        [InlineData("(a|ab)(c|bcd)(d*)", "abcd", "(0,4)(0,1)(1,4)(4,4)")]
        [InlineData("(a|ab)(bcd|c)(d*)", "abcd", "(0,4)(0,1)(1,4)(4,4)")]
        [InlineData("(ab|a)(c|bcd)(d*)", "abcd", "(0,4)(0,2)(2,3)(3,4)")]
        [InlineData("(ab|a)(bcd|c)(d*)", "abcd", "(0,4)(0,2)(2,3)(3,4)")]
        [InlineData("(a*)(b|abc)(c*)", "abc", "(0,3)(0,1)(1,2)(2,3)")]
        [InlineData("(a*)(abc|b)(c*)", "abc", "(0,3)(0,1)(1,2)(2,3)")]
        [InlineData("(a|ab)(c|bcd)(d|.*)", "abcd", "(0,4)(0,1)(1,4)(4,4)")]
        [InlineData("(a|ab)(bcd|c)(d|.*)", "abcd", "(0,4)(0,1)(1,4)(4,4)")]
        [InlineData("(ab|a)(c|bcd)(d|.*)", "abcd", "(0,4)(0,2)(2,3)(3,4)")]
        [InlineData("(ab|a)(bcd|c)(d|.*)", "abcd", "(0,4)(0,2)(2,3)(3,4)")]

        // nullsubexpr.dat
        [InlineData("(a*)*", "a", "(0,1)(0,1)")]
        [InlineData("(a*)*", "x", "(0,0)(0,0)")]
        [InlineData("(a*)*", "aaaaaa", "(0,6)(0,6)")]
        [InlineData("(a*)*", "aaaaaax", "(0,6)(0,6)")]
        [InlineData("(a*)+", "a", "(0,1)(0,1)")]
        [InlineData("(a+)*", "a", "(0,1)(0,1)")]
        [InlineData("(a+)*", "x", "(0,0)")]
        [InlineData("(a+)+", "a", "(0,1)(0,1)")]
        [InlineData("(a+)+", "x", "NOMATCH")]
        [InlineData("([a]*)*", "a", "(0,1)(0,1)")]
        [InlineData("([a]*)+", "a", "(0,1)(0,1)")]
        [InlineData("([^b]*)*", "a", "(0,1)(0,1)")]
        [InlineData("([^b]*)*", "b", "(0,0)(0,0)")]
        [InlineData("([^b]*)*", "aaaaaab", "(0,6)(0,6)")]
        [InlineData("([ab]*)*", "a", "(0,1)(0,1)")]
        [InlineData("([ab]*)*", "ababab", "(0,6)(0,6)")]
        [InlineData("([ab]*)*", "bababa", "(0,6)(0,6)")]
        [InlineData("([ab]*)*", "b", "(0,1)(0,1)")]
        [InlineData("([ab]*)*", "bbbbbb", "(0,6)(0,6)")]
        [InlineData("([ab]*)*", "aaaabcde", "(0,5)(0,5)")]
        [InlineData("([^a]*)*", "b", "(0,1)(0,1)")]
        [InlineData("([^a]*)*", "aaaaaa", "(0,0)(0,0)")]
        [InlineData("([^ab]*)*", "ccccxx", "(0,6)(0,6)")]
        [InlineData("([^ab]*)*", "ababab", "(0,0)(0,0)")]
        [InlineData("((z)+|a)*", "zabcde", "(0,2)(1,2)")]
        [InlineData("a+?", "aaaaaa", "(0,1)")]
        [InlineData("(a)", "aaa", "(0,1)(0,1)")]
        [InlineData("(a*?)", "aaa", "(0,0)(0,0)")]
        [InlineData("(a)*?", "aaa", "(0,0)")]
        [InlineData("(a*?)*?", "aaa", "(0,0)")]
        [InlineData("(a*)*(x)", "x", "(0,1)(0,0)(0,1)")]
        [InlineData("(a*)*(x)(\\1)", "x", "(0,1)(0,0)(0,1)(1,1)")]
        [InlineData("(a*)*(x)(\\1)", "ax", "(0,2)(1,1)(1,2)(2,2)")]
        [InlineData("(a*)*(x)(\\1)", "axa", "(0,2)(1,1)(1,2)(2,2)")] // was "(0,3)(0,1)(1,2)(2,3)"
        [InlineData("(a*)*(x)(\\1)(x)", "axax", "(0,4)(0,1)(1,2)(2,3)(3,4)")]
        [InlineData("(a*)*(x)(\\1)(x)", "axxa", "(0,3)(1,1)(1,2)(2,2)(2,3)")]
        [InlineData("(a*)*(x)", "ax", "(0,2)(1,1)(1,2)")]
        [InlineData("(a*)*(x)", "axa", "(0,2)(1,1)(1,2)")] // was "(0,2)(0,1)(1,2)"
        [InlineData("(a*)+(x)", "x", "(0,1)(0,0)(0,1)")]
        [InlineData("(a*)+(x)", "ax", "(0,2)(1,1)(1,2)")] // was "(0,2)(0,1)(1,2)"
        [InlineData("(a*)+(x)", "axa", "(0,2)(1,1)(1,2)")] // was "(0,2)(0,1)(1,2)"
        [InlineData("(a*){2}(x)", "x", "(0,1)(0,0)(0,1)")]
        [InlineData("(a*){2}(x)", "ax", "(0,2)(1,1)(1,2)")]
        [InlineData("(a*){2}(x)", "axa", "(0,2)(1,1)(1,2)")]
        public void Test(string pattern, string input, string captures)
        {
            if (input == "NULL")
            {
                input = "";
            }

            foreach (RegexOptions options in new[] { DFA })
            {
                if (captures == "BADBR")
                {
                    // should be RegexParseException that is not an ArgumentException
                    //Assert.ThrowsAny<ArgumentExceptionException>(() => Regex.IsMatch(input, pattern, options));
                    try
                    {
                        Regex.IsMatch(input, pattern, options);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    Assert.True(false, "should be unreachble");
                }
                else if (captures == "NOMATCH")
                {
                    Assert.False(Regex.IsMatch(input, pattern, options));
                }
                else
                {
                    Match match = Match.Empty;
                    try
                    {
                        match = Regex.Match(input, pattern, options);
                        Assert.True(match.Success);
                    }
                    catch (NotSupportedException e)
                    {
                        WriteLine(e.Message);
                        continue;
                    }
                    var expected = new HashSet<(int start, int end)>(
                        captures
                        .Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Split(','))
                        .Select(s => (start: int.Parse(s[0]), end: int.Parse(s[1])))
                        .Distinct()
                        .OrderBy(c => c.start)
                        .ThenBy(c => c.end));

                    var actual = new HashSet<(int start, int end)>(
                        match.Groups
                        .Cast<Group>()
                        .Select(g => (start: g.Index, end: g.Index + g.Length))
                        .Distinct()
                        .OrderBy(g => g.start)
                        .ThenBy(g => g.end));

                    // SRM only provides the top-level match
                    // so actual is always a singleton that must be a subset of expected
                    // oberserve that WITHOUT the DFA option
                    // the test is the opposite: expected.IsSubsetOf(actual)
                    // see also AttRegexTests.Test
                    if (!actual.IsSubsetOf(expected))
                    {
                        throw new Xunit.Sdk.XunitException($"Actual: {string.Join(", ", actual)}{Environment.NewLine}Expected: {string.Join(", ", expected)}");
                    }
                }
            }
        }
    }
}
