// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace System.Text.RegularExpressions.SRM.Unicode
{
#if DEBUG
    internal static class IgnoreCaseRelationGenerator
    {
        public static void Generate(string namespacename, string classname, string path)
        {
            if (namespacename == null)
                throw new ArgumentNullException(nameof(namespacename));
            if (classname == null)
                throw new ArgumentNullException(nameof(classname));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path != "" && !path.EndsWith('/'))
                path += "/";

            string version = Environment.Version.ToString();

            string prefix = @"// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This is a programmatically generated file from Regex.GenerateUnicodeTables.
// It provides serialized BDD based Unicode ignore-case relation for System.Environment.Version = " + version + @"

namespace " + namespacename + @"
{
internal static class " + classname + @"
{";

            string suffix = @"}
}
";
            FileInfo fi = new FileInfo($"{path}{classname}.cs");
            if (fi.Exists)
                fi.IsReadOnly = false;
            StreamWriter sw = new StreamWriter($"{path}{classname}.cs");
            sw.WriteLine(prefix);

            WriteIgnoreCaseBDD(sw);

            sw.WriteLine(suffix);
            sw.Close();
        }

        private static void WriteIgnoreCaseBDD(StreamWriter sw)
        {
            sw.WriteLine("/// <summary>");
            sw.WriteLine("/// Serialized BDD for mapping characters to their case-ignoring equivalence classes in the default (en-US) culture");
            sw.WriteLine("/// </summary>");
            CharSetSolver solver = new CharSetSolver();

            Dictionary<char, BDD> ignoreCase = ComputeIgnoreCaseDictionary(solver, new CultureInfo("en-US"));
            BDD ignorecase = solver.False;
            foreach (KeyValuePair<char, BDD> kv in ignoreCase)
            {
                BDD a = solver.MkCharSetFromRange(kv.Key, kv.Key);
                BDD b = kv.Value;
                ignorecase = solver.MkOr(ignorecase, solver.MkAnd(solver.ShiftLeft(a, 16), b));
            }
            string ignorecase_repr = ignorecase.SerializeToString();
            sw.WriteLine("public const string s_IgnoreCaseBDD_repr =");
            sw.Write('"');
            sw.Write(ignorecase_repr);
            sw.WriteLine("\";");
        }

        private static Dictionary<char, BDD> ComputeIgnoreCaseDictionary(CharSetSolver solver, CultureInfo culture)
        {
            CultureInfo currculture = CultureInfo.CurrentCulture;
            //set temporarily to the given culture so that all character opreations are carried out in it
            CultureInfo.CurrentCulture = culture;

            var ignoreCase = new Dictionary<char, BDD>();

            for (uint i = 0; i <= 0xFFFF; i++)
            {
                char c = (char)i;
                char cU = char.ToUpper(c); // (char.IsLetter(char.ToUpper(c)) ? char.ToUpper(c) : c);
                char cL = char.ToLower(c); // (char.IsLetter(char.ToLower(c)) ? char.ToLower(c) : c);
                if (cU != cL)
                {
                    //c may be different from both cU as well as cL
                    //make sure that the regex engine considers c as being equivalent to cU and cL, else ignore c
                    //in some cases c != cU but the regex engine does not consider the chacarters equivalent wrt the ignore-case option.
                    if (RegularExpressions.Regex.IsMatch(cU.ToString() + cL.ToString(), "^(?i:" + StringUtility.Escape(c, true) + StringUtility.Escape(c, true) + ")$"))
                    {
                        BDD equiv = solver.False;

                        if (ignoreCase.ContainsKey(c))
                            equiv = solver.MkOr(equiv, ignoreCase[c]);
                        if (ignoreCase.ContainsKey(cU))
                            equiv = solver.MkOr(equiv, ignoreCase[cU]);
                        if (ignoreCase.ContainsKey(cL))
                            equiv = solver.MkOr(equiv, ignoreCase[cL]);

                        //this is to make sure all characters are included initially or when some is still missing
                        equiv = solver.MkOr(equiv, solver.MkOr(solver.MkCharSetFromRange(c, c), solver.MkOr(solver.MkCharSetFromRange(cU, cU), solver.MkCharSetFromRange(cL, cL))));

                        //update all the members with their case-invariance equivalence classes
                        foreach (char d in solver.GenerateAllCharacters(equiv))
                            ignoreCase[d] = equiv;
                    }
                }
            }

            //restore the original culture
            CultureInfo.CurrentCulture = currculture;
            return ignoreCase;
        }
    };
#endif
}
