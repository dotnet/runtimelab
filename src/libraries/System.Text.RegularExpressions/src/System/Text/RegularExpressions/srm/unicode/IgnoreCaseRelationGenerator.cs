﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
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

            if (path != "" && !path.EndsWith("/"))
                path = path + "/";

            string version = System.Environment.Version.ToString();

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
            FileInfo fi = new FileInfo(string.Format("{1}{0}.cs", classname, path));
            if (fi.Exists)
                fi.IsReadOnly = false;
            StreamWriter sw = new StreamWriter(string.Format("{1}{0}.cs", classname, path));
            sw.WriteLine(prefix);

            WriteIgnoreCaseBDD(sw);

            sw.WriteLine(suffix);
            sw.Close();
        }

        private static void WriteIgnoreCaseBDD(StreamWriter sw)
        {
            sw.WriteLine("/// <summary>");
            sw.WriteLine("/// Serialized BDD for mapping characters to their case-ignoring equivalence classes.");
            sw.WriteLine("/// </summary>");
            CharSetSolver solver = new CharSetSolver();

            Dictionary<char, BDD> ignoreCase = ComputeIgnoreCaseDictionary(solver);

            BDD ignorecase = solver.False;
            foreach (var kv in ignoreCase)
            {
                var a = solver.MkCharSetFromRange(kv.Key, kv.Key);
                var b = kv.Value;
                ignorecase = solver.MkOr(ignorecase, solver.MkAnd(solver.ShiftLeft(a, 16), b));
            }
            var ignorecase_repr = ignorecase.SerializeToString();

            sw.WriteLine("public const string s_IgnoreCaseBDD_repr =");
            sw.Write('"');
            sw.Write(ignorecase_repr);
            sw.WriteLine("\";");
        }

        private static Dictionary<char, BDD> ComputeIgnoreCaseDictionary(CharSetSolver solver)
        {
            var ignoreCase = new Dictionary<char, BDD>();
            for (uint i = 0; i <= 0xFFFF; i++)
            {
                char c = (char)i;
                char cU = char.ToUpper(c); // (char.IsLetter(char.ToUpper(c)) ? char.ToUpper(c) : c);
                char cL = char.ToLower(c); // (char.IsLetter(char.ToLower(c)) ? char.ToLower(c) : c);
                if (c != cU || c != cL || cU != cL)
                {
                    //make sure that the regex engine considers c as being equivalent to cU and cL, else ignore c
                    //in some cases c != cU but the regex engine does not consider the chacarters equivalent wrt the ignore-case option.
                    //These characters are:
                    //c=\xB5,cU=\u039C
                    //c=\u0131,cU=I
                    //c=\u017F,cU=S
                    //c=\u0345,cU=\u0399
                    //c=\u03C2,cU=\u03A3
                    //c=\u03D0,cU=\u0392
                    //c=\u03D1,cU=\u0398
                    //c=\u03D5,cU=\u03A6
                    //c=\u03D6,cU=\u03A0
                    //c=\u03F0,cU=\u039A
                    //c=\u03F1,cU=\u03A1
                    //c=\u03F5,cU=\u0395
                    //c=\u1E9B,cU=\u1E60
                    //c=\u1FBE,cU=\u0399
                    if (System.Text.RegularExpressions.Regex.IsMatch(cU.ToString() + cL.ToString(), "^(?i:" + StringUtility.Escape(c, true) + ")+$"))
                    {
                        BDD equiv = solver.False;

                        if (ignoreCase.ContainsKey(c))
                            equiv = solver.MkOr(equiv, ignoreCase[c]);
                        if (ignoreCase.ContainsKey(cU))
                            equiv = solver.MkOr(equiv, ignoreCase[cU]);
                        if (ignoreCase.ContainsKey(cL))
                            equiv = solver.MkOr(equiv, ignoreCase[cL]);

                        equiv = solver.MkOr(equiv, solver.MkOr(solver.MkCharSetFromRange(c, c), solver.MkOr(solver.MkCharSetFromRange(cU, cU), solver.MkCharSetFromRange(cL, cL))));

                        foreach (char d in solver.GenerateAllCharacters(equiv))
                            ignoreCase[d] = equiv;
                    }
                }
            }
            return ignoreCase;
        }
    };
#endif
}
