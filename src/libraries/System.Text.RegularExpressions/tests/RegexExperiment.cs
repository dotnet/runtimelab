// Licensed to the .NET Foundation under one or more agreements.if #DEBUG
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Xunit;

namespace System.Text.RegularExpressions.Tests
{
    /// <summary>
    /// This class is to be ignored wrt unit tests.
    /// It contains temporary experimental code, such as lightweight profiling and debuggging locally.
    /// Set <see cref="Enabled"/> to true to run the tests.
    /// </summary>
    public class RegexExperiment
    {
        public static bool Enabled => false;

        /// <summary>Temporary local output directory for experiment results.</summary>
        private static readonly string s_tmpWorkingDir = Path.GetTempPath();

        /// <summary>Works as a console.</summary>
        private static string OutputFilePath => Path.Combine(s_tmpWorkingDir, "vsoutput.txt");

        /// <summary>Output directory for generated dgml files.</summary>
        private static string DgmlOutputDirectoryPath => Path.Combine(s_tmpWorkingDir, "dgml");

        private static string ExperimentDirectoryPath => Path.Combine(s_tmpWorkingDir, "experiments");

        [ConditionalFact(nameof(Enabled))]
        public void RegenerateUnicodeTables()
        {
            MethodInfo? genUnicode = typeof(Regex).GetMethod("GenerateUnicodeTables", BindingFlags.NonPublic | BindingFlags.Static);
            // GenerateUnicodeTables is not available in Release build
            if (genUnicode is not null)
            {
                genUnicode.Invoke(null, new object[] { s_tmpWorkingDir });
            }
        }

        [Theory]
        [InlineData(@"\b", 3)]
        [InlineData(@"((?<=\w)(?!\w)|(?<!\w)(?=\w))", 3)] // similar to word-border anchor
        [InlineData(@"((?<=\w)(?=\W)|(?<=\W)(?=\w))", 1)]
        public void TestLookaround(string pattern, int expectedCount)
        {
            Assert.Equal(expectedCount, Regex.Matches("one two three", $@"{pattern}\w+{pattern}").Count);
        }

        private static void WriteOutput(string message) =>
            File.AppendAllText(OutputFilePath, message);

        /// <summary>
        /// Save the regex as a DFA in DGML format in the textwriter.
        /// </summary>
        /// <param name="r"></param>
        private static bool TrySaveDGML(Regex regex, TextWriter writer, int bound = -1, bool hideStateInfo = false, bool addDotStar = false, bool inReverse = false, bool onlyDFAinfo = false, int maxLabelLength = -1, bool asNFA = false)
        {
            MethodInfo? saveDgml = regex.GetType().GetMethod("SaveDGML", BindingFlags.NonPublic | BindingFlags.Instance);
            if (saveDgml is null)
            {
                return false;
            }
            else
            {
                saveDgml.Invoke(regex, new object[] { writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength, asNFA });
                return true;
            }
        }

        /// <summary>
        /// View the regex as a DFA in DGML format in VS.
        /// </summary>
        /// <param name="r"></param>
        internal static void ViewDGML(Regex regex, int bound = -1, bool hideStateInfo = true, bool addDotStar = false, bool inReverse = false, bool onlyDFAinfo = false, string name = "DFA", int maxLabelLength = 20, bool asNFA = false)
        {
            if (!Directory.Exists(DgmlOutputDirectoryPath))
            {
                Directory.CreateDirectory(DgmlOutputDirectoryPath);
            }

            var sw = new StringWriter();
            // If TrySaveDGML returns false then Regex.SaveDGML is not supported (in Release build)
            if (TrySaveDGML(regex, sw, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength, asNFA))
            {
                if (asNFA)
                {
                    name = "NFA";
                }

                File.WriteAllText(Path.Combine(DgmlOutputDirectoryPath, $"{(inReverse ? name + "r" : (addDotStar ? name + "1" : name))}.dgml"), sw.ToString());
            }
        }

        /// <summary>
        /// The intent is that this method is run in realease build for lightweight performance testing.
        /// One can e.g. open the outputfile in emacs with AUTO-REVERT-ON in order to follow the progress in real time.
        /// It will print timing info and match info for both DFA, Compiled option and None.
        /// Place sample regexes in the regexesfile (one per line) and sample input in inputfile.
        /// It will essentially produce a csv file with the info:
        /// regexnr, matchtime_DFA, result_DFA, matchtime_Compiled, result_Compiled, matchtime_None, result_None,
        /// where result_.. is one of
        ///   Yes(index,length)
        ///   No
        ///   TIMEOUT
        ///   ERROR 
        ///  and in the case of TIMEOUT or ERROR time is 10000 (the timeout limit of 10sec)
        /// </summary>
        [ConditionalFact(nameof(Enabled))]
        public void TestRunPerformance()
        {
            if (!Directory.Exists(ExperimentDirectoryPath))
            {
                Directory.CreateDirectory(ExperimentDirectoryPath);
            }

            string[] dirs = Directory.GetDirectories(ExperimentDirectoryPath);
            if (dirs.Length == 0)
            {
                WriteOutput("\nExperiments directory is empty");
                return;
            }

            DirectoryInfo experimentDI = Directory.GetParent(dirs[0]);
            DirectoryInfo[] experiments =
                Array.FindAll(experimentDI.GetDirectories(),
                             di => ((di.Attributes & FileAttributes.Hidden) != (FileAttributes.Hidden)) &&
                                   Array.Exists(di.GetFiles(), f => f.Name.Equals("regexes.txt")) &&
                                   Array.Exists(di.GetFiles(), f => f.Name.Equals("input.txt")));
            if (experiments.Length == 0)
            {
                WriteOutput("\nExperiments directory has no indiviual experiment subdirectories containing files 'regexes.txt' and 'input.txt'.");
                return;
            }

            for (int i = 0; i < experiments.Length; i++)
            {
                string input = File.ReadAllText(Path.Combine(experiments[i].FullName, "input.txt"));
                string[] rawRegexes = File.ReadAllLines(Path.Combine(experiments[i].FullName, "regexes.txt"));

                WriteOutput($"\n---------- {experiments[i].Name} ----------");
                for (int r = 0; r < rawRegexes.Length; r++)
                {
                    TestRunRegex((r + 1).ToString(), rawRegexes[r], input);
                }
            }
        }

        private static long MeasureMatchTime(Regex re, string input, out Match match)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                match = re.Match(input);
                return sw.ElapsedMilliseconds;
            }
            catch (RegexMatchTimeoutException)
            {
                match = Match.Empty;
                return -1;
            }
            catch (Exception)
            {
                match = Match.Empty;
                return -2;
            }
        }

        private static string And(params string[] regexes)
        {
            string conj = $"({regexes[regexes.Length - 1]})";
            for (int i = regexes.Length - 2; i >= 0; i--)
            {
                conj = $"(?({regexes[i]}){conj}|[0-[0]])";
            }

            return conj;
        }

        private static string Not(string regex) => $"(?({regex})[0-[0]]|.*)";

        [ConditionalFact(nameof(Enabled))]
        public void ViewSampleRegexInDGML()
        {
            try
            {
                //string rawregex = @"\bis\w*\b";
                string rawregex = And(".*[0-9].*[0-9].*", ".*[A-Z].*[A-Z].*", Not(".*(01|12).*"));
                //string rawregex = "a.{4}$";
                Regex re = new Regex($@"{rawregex}", RegexHelpers.RegexOptionNonBacktracking | RegexOptions.Singleline);
                ViewDGML(re);
                ViewDGML(re, inReverse: true);
                ViewDGML(re, addDotStar: true);
                ViewDGML(re, asNFA: true, bound: 12);
                ViewDGML(re, inReverse: true, asNFA: true, bound: 12);
                ViewDGML(re, addDotStar: true, asNFA: true, bound: 12);
            }
            catch (NotSupportedException e)
            {
                Assert.Contains("conditional", e.Message);
            }
        }

        private void TestRunRegex(string name, string rawregex, string input, bool viewDGML = false, bool dotStar = false)
        {
            var reNone = new Regex(rawregex, RegexOptions.None, new TimeSpan(0, 0, 10));
            var reCompiled = new Regex(rawregex, RegexOptions.Compiled, new TimeSpan(0, 0, 10));
            var reNonBacktracking = new Regex(rawregex, RegexOptions.NonBacktracking);

            if (viewDGML)
                ViewDGML(reNonBacktracking, addDotStar: dotStar);
            WriteOutput($"\n{name}");

            // First call in each case is a warmup

            // None
            MeasureMatchTime(reNone, input, out _);
            long tN = MeasureMatchTime(reNone, input, out Match mN);
            WriteMatchOutput(tN, mN);

            // Compiled
            MeasureMatchTime(reCompiled, input, out _);
            long tC = MeasureMatchTime(reCompiled, input, out Match mC);
            WriteMatchOutput(tC, mC);

            // Non-Backtracking
            MeasureMatchTime(reNonBacktracking, input, out _);
            long tD = MeasureMatchTime(reNonBacktracking, input, out Match mD);
            WriteMatchOutput(tD, mD);

            void WriteMatchOutput(long t, Match m)
            {
                WriteOutput(t switch
                {
                    -1 => ",10000,TIMEOUT",
                    -2 => ",10000,ERROR",
                    _ when m.Success => $",{t},Yes({m.Index}:{m.Length})",
                    _ => $",{t},No"
                });
            }
        }
    }
}
