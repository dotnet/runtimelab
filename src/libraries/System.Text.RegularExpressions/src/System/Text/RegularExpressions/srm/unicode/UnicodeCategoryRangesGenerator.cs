// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.IO;

namespace System.Text.RegularExpressions.SRM.Unicode
{
#if DEBUG
    /// <summary>
    /// Utility for generating unicode category ranges and corresponing binary decision diagrams
    /// </summary>
    internal static class UnicodeCategoryRangesGenerator
    {
        /// <summary>
        ///  Generator for BDD Unicode category definitions.
        /// </summary>
        /// <param name="namespacename">namespace for the class</param>
        /// <param name="classname">name of the class</param>
        /// <param name="path">path where the file classname.cs is written</param>
        public static void Generate(string namespacename, string classname, string path)
        {
            if (namespacename == null)
                throw new ArgumentNullException(nameof(namespacename));
            if (classname == null)
                throw new ArgumentNullException(nameof(classname));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path != "" && !path.EndsWith("/"))
                path = path + "/";

            string version = System.Environment.Version.ToString();

            string prefix = @"// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This is a programmatically generated file from Regex.GenerateUnicodeTables.
// It provides serialized BDD Unicode category definitions for System.Environment.Version = " + version + @"

namespace " + namespacename + @"
{
internal static class " + classname + @"
{";

            string suffix = @"}
}
";
            FileInfo fi = new FileInfo(string.Format("{1}{0}.cs", classname, path));
            if (fi.Exists)
                fi.IsReadOnly = false;
            StreamWriter sw = new StreamWriter(string.Format("{1}{0}.cs", classname, path));
            sw.WriteLine(prefix);

            WriteSerializedBDDs(sw);
            sw.WriteLine();

            sw.WriteLine(suffix);
            sw.Close();
        }

        private static void WriteSerializedBDDs(StreamWriter sw)
        {
            int maxChar = 0xFFFF;
            var catMap = new Dictionary<UnicodeCategory, Ranges>();
            for (int c = 0; c < 30; c++)
                catMap[(UnicodeCategory)c] = new Ranges();
            Ranges whitespace = new Ranges();
            Ranges wordcharacter = new Ranges();
            System.Text.RegularExpressions.Regex s_regex = new(@"\s");
            System.Text.RegularExpressions.Regex w_regex = new(@"\w");
            for (int i = 0; i <= maxChar; i++)
            {
                char ch = (char)i;
                if (s_regex.IsMatch(ch.ToString())) //(char.IsWhiteSpace(ch))
                    whitespace.Add(i);
                UnicodeCategory cat = char.GetUnicodeCategory(ch);
                catMap[cat].Add(i);
                int catCode = (int)cat;
                if (w_regex.IsMatch(ch.ToString()))
                    wordcharacter.Add(i);
            }
            //generate bdd reprs for each of the category ranges
            BDD[] catBDDs = new BDD[30];
            CharSetSolver bddb = new CharSetSolver();
            for (int c = 0; c < 30; c++)
                catBDDs[c] = bddb.MkBddForIntRanges(catMap[(UnicodeCategory)c].ranges);

            BDD whitespaceBdd = bddb.MkBddForIntRanges(whitespace.ranges);

            BDD wordCharBdd = bddb.MkBddForIntRanges(wordcharacter.ranges);

            sw.WriteLine(@"/// <summary>
/// Serialized BDD representations of all the Unicode categories.
/// </summary>");
            sw.WriteLine("public static string[] s_UnicodeCategoryBdd_repr = new string[]{");
            for (int i = 0; i < 30; i++)
            {
                UnicodeCategory c = (UnicodeCategory)i;
                sw.WriteLine("//{0}({1}):", c, i);
                BDD catBdd = catBDDs[i];
                sw.Write('"');
                sw.Write(catBdd.SerializeToString());
                sw.WriteLine("\",");
            }
            sw.WriteLine("};");

            sw.WriteLine(@"/// <summary>
/// Serialized BDD representation of the set of all whitespace characters.
/// </summary>");
            sw.WriteLine("public const string s_UnicodeWhitespaceBdd_repr = ");
            sw.Write('"');
            sw.Write(whitespaceBdd.SerializeToString());
            sw.WriteLine("\";");

            sw.WriteLine(@"/// <summary>
/// Serialized BDD representation of the set of all word characters
/// </summary>");
            sw.WriteLine("public const string s_UnicodeWordCharacterBdd_repr = ");
            sw.Write('"');
            sw.Write(wordCharBdd.SerializeToString());
            sw.WriteLine("\";");
        }
    }

    /// <summary>
    /// Used internally for creating a collection of ranges for serialization.
    /// </summary>
    internal class Ranges
    {
        public List<int[]> ranges = new List<int[]>();
        public Ranges()
        {
        }
        public void Add(int n)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                if (ranges[i][1] == (n - 1))
                {
                    ranges[i][1] = n;
                    return;
                }
            }
            ranges.Add(new int[] { n, n });
        }

        public int Count
        {
            get { return ranges.Count; }
        }
    }
#endif
}
