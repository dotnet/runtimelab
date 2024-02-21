// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
// using Xamarin;
using System.Text;
using SyntaxDynamo;
using System.Collections;

namespace SwiftReflector
{
    public static class Extensions
    {
        public static Tuple<string, string> SplitModuleFromName(this string s)
        {
            int dotIndex = s.IndexOf('.');
            if (dotIndex < 0)
                return new Tuple<string, string>(null, s);
            if (dotIndex == 0)
                return new Tuple<string, string>(null, s.Substring(1));
            return new Tuple<string, string>(s.Substring(0, dotIndex), s.Substring(dotIndex + 1));
        }

        public static string ModuleFromName(this string s)
        {
            return s.SplitModuleFromName().Item1;
        }

        public static string NameWithoutModule(this string s)
        {
            return s.SplitModuleFromName().Item2;
        }

        public static string InterleaveStrings(this IEnumerable<string> elements, string separator, bool includeSepFirst = false)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in elements.Interleave(separator, includeSepFirst))
                sb.Append(s);
            return sb.ToString();
        }

        public static string InterleaveCommas(this IEnumerable<string> elements)
        {
            return elements.InterleaveStrings(", ");
        }
    }
}

