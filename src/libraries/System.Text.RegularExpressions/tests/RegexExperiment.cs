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
    /// </summary>
    public class RegexExperiment
    {
        private const string tmpWorkingDir = @"c:\tmp\";
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

        //[Fact]
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
        /// It will print timing info and match info for both DFA and Compiled option.
        /// Place sample regexes in the regexesfile (one per line) and sample input in inputfile.
        /// </summary>
        //[Fact]
        private void TestRunPerformance()
        {
            string input = File.ReadAllText(inputfile);
            string[] rawregexes = File.ReadAllLines(regexesfile);
            WriteOutput("\n========= date:{0} =========\n", System.DateTime.Now);
            int totDFA = 0;
            int totCOM = 0;
            int tDFA = 0;
            int tCOM = 0;
            for (int i = 0; i < rawregexes.Length; i++)
            {
                var rawregex = rawregexes[i];
                Regex re = new(rawregex, DFA, new TimeSpan(0, 0, 10));
                Regex reC = new(rawregex, RegexOptions.Compiled, new TimeSpan(0, 0, 10));
                WriteOutput("\n--- Regex:{0}\n", i);
                //-------------------
                //-- measure DFA
                //-------------------
                tDFA = MeasureMatchTime(re, "DFA", input);
                //-------------------
                //-- measure COMPILED
                //-------------------
                tCOM = MeasureMatchTime(reC, "COM", input);
                //ignore the cases when one times out
                if (tDFA >= 0 && tCOM >= 0)
                {
                    totDFA += tDFA;
                    totCOM += tCOM;
                }
            }
            WriteOutput("\ntotal time: DFA:{0}ms, COM:{1}ms\n", totDFA, totCOM);
        }

        private static int MeasureMatchTime(Regex re, string tag, string input)
        {
            File.AppendAllText(outputfile, tag + ": warmup:...");
            Stopwatch sw = new Stopwatch();
            int t = 0;
            try
            {
                sw.Start();
                re.Match(input);
                t = (int)sw.ElapsedMilliseconds;
                sw.Reset();
                WriteOutput("{0}ms, run:...", t);
                sw.Start();
                var match1 = re.Match(input);
                t = (int)sw.ElapsedMilliseconds;
                sw.Reset();
                WriteOutput("{0}ms, Match:{1} (Index:{2} Length:{3})\n", t, match1.Success, match1.Index, match1.Length);
                return t;
            }
            catch (TimeoutException)
            {
                WriteOutput(" TIMEOUT\n");
                return -1;
            }
            catch (Exception)
            {
                WriteOutput(" ERROR\n");
                return -1;
            }
        }

        /// <summary>
        /// Test serialization/deserialization and measure performance for all regexes in the regexesfile.
        /// </summary>
        //[Fact]
        private void TestRunSerialization()
        {
            Stopwatch sw = new Stopwatch();
            string[] rawregexes = File.ReadAllLines(regexesfile);
            WriteOutput("\n========= TimeStamp:{0} =========\n", System.DateTime.Now);
            int k = rawregexes.Length;
            Regex[] rs = new Regex[k];
            string[] ser = new string[k];
            Regex[] drs = new Regex[k];
            //construct
            sw.Start();
            for (int i = 0; i < k; i++)
                rs[i] = new Regex(rawregexes[i], DFA);
            int totConstrTime = (int)sw.ElapsedMilliseconds;
            sw.Reset();

            //serialize
            sw.Start();
            //repeat ten times
            for (int j = 0; j < 10; j++)
                for (int i = 0; i < k; i++)
                    ser[i] = Serialize(rs[i]);
            int totSerTime = (int)sw.ElapsedMilliseconds / 10;
            sw.Reset();
            //save the serializations
            File.WriteAllLines(serializedout, ser);
            //deserialize
            sw.Start();
            //repeat ten times
            for (int j = 0; j < 10; j++)
                for (int i = 0; i < k; i++)
                    drs[i] = Deserialize(ser[i]);
            int totDeSerTime = (int)sw.ElapsedMilliseconds / 10;
            sw.Reset();
            File.AppendAllText(outputfile, string.Format("\nconstr:{0}ms, serialization:{1}ms, deserialization:{2}ms\n", totConstrTime, totSerTime, totDeSerTime));
        }

    }
}
