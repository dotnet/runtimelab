// Licensed to the .NET Foundation under one or more agreements.if #DEBUG
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Xunit;
using System.Diagnostics;
using System.Reflection;

namespace System.Text.RegularExpressions.Tests
{
    /// <summary>
    /// This class is to be ignored wrt unit tests.
    /// It contains temporary experimental code, such as lightweight profiling and debuggging locally.
    /// The entry point is TestRun().
    /// </summary>
    public class RegexExperiment
    {
        [Fact]
        public void TestRun()
        {
            //call the actual code from here
            //ViewSampleRegexInDGML();
            //TestRunPerformance();
            //RegenerateUnicodeTables();
            //string b = @"((?<=\w)(?!\w)|(?<!\w)(?=\w))"; //word-border anchor
            //string b1 = @"((?<=\w)(?=\W)|(?<=\W)(?=\w))";
            //TestLookaround(string.Format(@"{0}\w+{0}",b), "one two three", 3);
            //TestLookaround(string.Format(@"{0}\w+{0}", b1), "one two three", 1);
        }

        private void TestLookaround(string pattern, string input, int matchcount)
        {
            var regex = new Regex(pattern);
            List<Match> matches = new List<Match>();
            var match = regex.Match(input);
            while (match.Success)
            {
                matches.Add(match);
                match = match.NextMatch();
            }
            Assert.Equal(matchcount, matches.Count);
        }

        private const string experimentDirectory = @"\\maku1\experiments\";

        /// <summary>
        /// Temporary local output directory for experiment results.
        /// </summary>
        private const string tmpWorkingDir = @"c:\tmp\runtimelab\";
        /// <summary>
        /// Works as a console.
        /// </summary>
        private const string outputfile = tmpWorkingDir + "vsoutput.txt";
        /// <summary>
        /// Local input file.
        /// </summary>
        private const string inputfile = tmpWorkingDir + "vsinput.txt";
        /// <summary>
        /// Local regexes file.
        /// </summary>
        private const string regexesfile = tmpWorkingDir + "vsregexes.txt";
        /// <summary>
        /// Serialized regexes are stored in this file, one per line.
        /// </summary>
        private const string serializedout = tmpWorkingDir + "serialized.txt";
        /// <summary>
        /// Output directory for generated dgml files.
        /// </summary>
        private const string dgmloutdirectory = tmpWorkingDir + @"dgml\";

        private static MethodInfo _Deserialize = typeof(Regex).GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static);
        private static Regex Deserialize(string s) => _Deserialize.Invoke(null, new object[] { s }) as Regex;

        private static MethodInfo _Serialize = typeof(Regex).GetMethod("Serialize", BindingFlags.NonPublic | BindingFlags.Instance);
        private static string Serialize(Regex r) => _Serialize.Invoke(r, null) as string;

        private static void WriteOutput(string format, params object[] args) => File.AppendAllText(outputfile, string.Format(format, args));


        /// <summary>
        /// Save the regex as a DFA in DGML format in the textwriter.
        /// </summary>
        /// <param name="r"></param>
        private static void SaveDGML(Regex regex, TextWriter writer, int bound = -1, bool hideStateInfo = false, bool addDotStar = false, bool inReverse = false, bool onlyDFAinfo = false, int maxLabelLength = -1)
        {
            MethodInfo saveDgml = regex.GetType().GetMethod("SaveDGML", BindingFlags.NonPublic | BindingFlags.Instance);
            saveDgml.Invoke(regex, new object[] { writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength });
        }

        private void RegenerateUnicodeTables()
        {
            MethodInfo genUnicode = typeof(Regex).GetMethod("GenerateUnicodeTables", BindingFlags.NonPublic | BindingFlags.Static);
            genUnicode.Invoke(null, new object[] { tmpWorkingDir });
        }

        /// <summary>
        /// View the regex as a DFA in DGML format in VS.
        /// </summary>
        /// <param name="r"></param>
        internal static void ViewDGML(Regex regex, int bound = -1, bool hideStateInfo = true, bool addDotStar = false, bool inReverse = false, bool onlyDFAinfo = false, string name = "DFA", int maxLabelLength = 20)
        {
            StringWriter sw = new StringWriter();
            SaveDGML(regex, sw, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength);
            File.WriteAllText(string.Format("{1}{0}.dgml", inReverse ? name + "r" : (addDotStar ? name + "1" : name), dgmloutdirectory), sw.ToString());
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
        private void TestRunPerformance()
        {
            var dirs = Directory.GetDirectories(experimentDirectory);
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
                string input = File.ReadAllText(experiments[i].FullName + "\\input.txt");
                string[] rawregexes = File.ReadAllLines(experiments[i].FullName + "\\regexes.txt");
                TestRunPerformance(experiments[i].Name, input, rawregexes);
            }
        }

        private void TestRunPerformance(string name, string input, string[] rawregexes)
        {
            WriteOutput("\n---------- {0} ----------", name);
            for (int i = 0; i < rawregexes.Length; i++)
                TestRunRegex((i + 1).ToString(), rawregexes[i], input);
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
            return $"(?({regex})[0-[0]]|.*)";
        }

        private void ViewSampleRegexInDGML()
        {
            string rawregex = @"\bis\w*\b";
            //string rawregex = And(".*[0-9].*", ".*[A-Z].*");
            Regex re = new Regex(rawregex, RegexHelpers.RegexOptionNonBacktracking | RegexOptions.Singleline);
            ViewDGML(re);
            ViewDGML(re, inReverse: true);
            ViewDGML(re, addDotStar: true);
        }

        private void TestRunRegex(string name, string rawregex, string input, bool viewDGML = false, bool dotStar = false)
        {
            Regex reC = new Regex(rawregex, RegexOptions.Compiled, new TimeSpan(0, 0, 10));
            Regex reN = new Regex(rawregex, RegexOptions.None, new TimeSpan(0, 0, 10));
            Regex reD = new Regex(rawregex, RegexHelpers.RegexOptionNonBacktracking);
            Match mC;
            Match mN;
            Match mD;
            long tC;
            long tN;
            long tD;
            if (viewDGML)
                ViewDGML(reD, addDotStar: dotStar);
            WriteOutput("\n{0}", name);
            //first call in each case is a warmup
            //DFA
            MeasureMatchTime(reD, input, out _);
            tD = MeasureMatchTime(reD, input, out mD);
            WriteMatchOutput(tD, mD);
            //Compiled
            MeasureMatchTime(reC, input, out _);
            tC = MeasureMatchTime(reC, input, out mC);
            WriteMatchOutput(tC, mC);
            //None
            MeasureMatchTime(reN, input, out _);
            tN = MeasureMatchTime(reN, input, out mN);
            WriteMatchOutput(tN, mN);
        }
        private void WriteMatchOutput(long t, Match m)
        {
            if (t == -1)
                WriteOutput(",10000,TIMEOUT");
            else if (t == -2)
                WriteOutput(",10000,ERROR");
            else if (m.Success)
                WriteOutput(",{0},Yes({1}:{2})", t, m.Index, m.Length);
            else 
                WriteOutput(",{0},No", t);
        }

        /// <summary>
        /// Test serialization/deserialization and measure performance for all regexes in the regexesfile.
        /// </summary>
        private void TestRunSerialization()
        {
            string[] rawregexes = File.ReadAllLines(regexesfile);
            WriteOutput("\n========= TimeStamp:{0} =========\n", System.DateTime.Now);
            int k = rawregexes.Length;
            //construct
            var sw = Stopwatch.StartNew();
            var rs = Array.ConvertAll(rawregexes, s => new Regex(s, RegexHelpers.RegexOptionNonBacktracking, new TimeSpan(0,0,1)));
            int totConstrTime = (int)sw.ElapsedMilliseconds;
            //-------
            //serialize
            sw = Stopwatch.StartNew();
            //repeat ten times
            var ser = Array.ConvertAll(rs, Serialize);
            for (int j = 0; j < 9; j++)
                ser = Array.ConvertAll(rs, Serialize);
            int totSerTime = (int)sw.ElapsedMilliseconds / 10;
            //-------
            //save the serializations
            File.WriteAllLines(serializedout, ser);
            //----------
            //deserialize
            sw = Stopwatch.StartNew();
            //repeat ten times
            var drs = Array.ConvertAll(ser, Deserialize);
            for (int j = 0; j < 9; j++)
                drs = Array.ConvertAll(ser, Deserialize);
            int totDeSerTime = (int)sw.ElapsedMilliseconds / 10;
            //----------
            File.AppendAllText(outputfile, string.Format("\nconstr:{0}ms, serialization:{1}ms, deserialization:{2}ms\n", totConstrTime, totSerTime, totDeSerTime));
        }
    }
}
