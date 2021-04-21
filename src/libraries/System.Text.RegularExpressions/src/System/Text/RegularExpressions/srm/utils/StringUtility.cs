// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Provides some character escaping routines for strings.
    /// </summary>
    internal static class StringUtility
    {
        #region Escaping strings
        ///// <summary>
        /////
        ///// </summary>

        /// <summary>
        /// Make an escaped string from a character.
        /// </summary>
        /// <param name="c">given character</param>
        /// <param name="useNumericRepresentationOnly">if true then use numeric hexadecimal escaping of all characters</param>
        /// <returns></returns>
        public static string Escape(char c, bool useNumericRepresentationOnly = false)
        {
            int code = (int)c;

            if (useNumericRepresentationOnly)
            {
                if (code <= 0xF)
                    return string.Format("\\x0{0:X}", code);
                else if (code <= 0xFF)
                    return string.Format("\\x{0:X}", code);
                else if (code <= 0xFFF)
                    return string.Format("\\u0{0:X}", code);
                else
                    return string.Format("\\u{0:X}", code);
            }

            //special characters
            switch (c)
            {
                case '.':
                    return @"\.";
                case '[':
                    return @"\[";
                case ']':
                    return @"\]";
                case '(':
                    return @"\(";
                case ')':
                    return @"\)";
                case '{':
                    return @"\{";
                case '}':
                    return @"\}";
                case '?':
                    return @"\?";
                case '+':
                    return @"\+";
                case '*':
                    return @"\*";
                case '|':
                    return @"\|";
                case '\\':
                    return @"\\";
                case '^':
                    return @"\^";
                case '$':
                    return @"\$";
                case '-':
                    return @"\-";
                case ':':
                    return @"\:";
                case '\"':
                    return "\\\"";
                case '\0':
                    return @"\0";
                case '\t':
                    return @"\t";
                case '\r':
                    return @"\r";
                case '\v':
                    return @"\v";
                case '\f':
                    return @"\f";
                case '\n':
                    return @"\n";
                default:
                    break;
            }

            if (code > 255)
                return ToUnicodeRepr(code);

            if (code <= 255 && code > 126)
                return string.Format("\\x{0:X}", code);

            if (code >= 32 && code <= 126)
                return c.ToString();

            if (code <= 15)
                return string.Format("\\x0{0:X}", code);
            else
                return string.Format("\\x{0:X}", code);
        }

        /// <summary>
        /// Make an escaped string from a character
        /// </summary>
        internal static string EscapeWithNumericSpace(char c)
        {
            int code = (int)c;
            if (code == 32)
                return string.Format("\\x{0:X}", code);
            else
                return Escape(c);
        }

        private static string ToUnicodeRepr(int i)
        {
            string s = string.Format("{0:X}", i);
            if (s.Length == 1)
                s = "\\u000" + s;
            else if (s.Length == 2)
                s = "\\u00" + s;
            else if (s.Length == 3)
                s = "\\u0" + s;
            else
                s = "\\u" + s;
            return s;
        }

        /// <summary>
        /// Makes an escaped string from a literal string s.
        /// </summary>
        public static string Escape(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
                sb.Append(Escape(c));
            return sb.ToString();
        }

        #endregion
    }
}
