// Licensed to the .NET Foundation under one or more agreements.if #DEBUG
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;
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
        //[Fact]
        //public void TestRun()
        //{
        //    //call the actual code from here
        //    //TestRunSampleRegex();
        //    TestRunPerformance();
        //}

        private const string tmpWorkingDir = @"c:\tmp\runtimelab\";
        /// <summary>
        /// Contains sample input text for regexes.
        /// </summary>
        private const string inputfile = tmpWorkingDir + "vsinput.txt";
        /// <summary>
        /// Works as a console.
        /// </summary>
        private const string outputfile = tmpWorkingDir + "vsoutput.txt";
        /// <summary>
        /// Contains regexes, one per line.
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

        private static RegexOptions DFA = (RegexOptions)0x400;

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
        private static void ViewDGML(Regex regex, int bound = -1, bool hideStateInfo = true, bool addDotStar = false, bool inReverse = false, bool onlyDFAinfo = false, string name = "DFA", int maxLabelLength = 20)
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
            string input = File.ReadAllText(inputfile);
            string[] rawregexes = File.ReadAllLines(regexesfile);
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
            catch (TimeoutException)
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

        private void TestRunSampleRegex()
        {
            string rawregex = @"^[a-z]+\-[a-z]+$";
            string input = File.ReadAllText(inputfile);
            TestRunRegex("sample", rawregex, input, true, true);
        }

        private void TestRunRegex(string name, string rawregex, string input, bool viewDGML = false, bool dotStar = false)
        {
            Regex reC = new Regex(rawregex, RegexOptions.Compiled, new TimeSpan(0,0,10));
            Regex reN = new Regex(rawregex, RegexOptions.None, new TimeSpan(0, 0, 10));
            Regex reD = new Regex(rawregex, DFA, new TimeSpan(0, 0, 10));
            input = input + "every week on mond I go there " + input + "every week on Tuesdays I go there";
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
            var rs = Array.ConvertAll(rawregexes, s => new Regex(s, DFA, new TimeSpan(0,0,1)));
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
