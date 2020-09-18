using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.SRM
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

            if (code > 255)
                return ToUnicodeRepr(code);

            if (code <= 255 && code > 126)
                return string.Format("\\x{0:X}", code);

            switch (c)
            {
                case '\0':
                    return @"\0";
                //case '\a':
                //    return @"\a";
                //case '\b':
                //    return @"\b";
                //case '\t':
                //    return @"\t";
                //case '\r':
                //    return @"\r";
                //case '\v':
                //    return @"\v";
                //case '\f':
                //    return @"\f";
                case '\n':
                    return @"\n";
                case '=':
                    return "=";
                case ';':
                    return ";";
                case '/':
                    return "/";
                case '!':
                    return "!";
                //case '>':
                //    return ">";
                //case '\"':
                //    return "\\\"";
                //case '\'':
                //    return "\\\'";
                //case ' ':
                //    return " ";
                //case '\\' :
                //    return @"\\";
                default:
                    if (code <= 15)
                    {
                        return string.Format("\\x0{0:X}", code);
                    }
                    else if (!(((int)'a') <= code && code <= ((int)'z'))
                         && !(((int)'A') <= code && code <= ((int)'Z'))
                         && !(((int)'0') <= code && code <= ((int)'9')))
                    {
                        return string.Format("\\x{0:X}", code);
                    }
                    else
                        return c.ToString();
            }
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

        static string ToUnicodeRepr(int i)
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
        /// Appends '\"' at the start and end of the encoded string.
        /// </summary>
        public static string Escape(string s)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\"");
            foreach (char c in s)
            {
                sb.Append(Escape(c));
            }
            sb.Append("\"");
            return sb.ToString();
        }

        /// <summary>
        /// Unescapes any escaped characters in in the input string. 
        /// (Same as System.Text.RegularExpressions.Regex.Unescape)
        /// </summary>
        public static string Unescape(string s)
        {
            return System.Text.RegularExpressions.Regex.Unescape(s);
        }
        #endregion

        internal static string SerializeStringToCharCodeSequence(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            var encodedChars = Array.ConvertAll(s.ToCharArray(), c => ((int)c).ToString());
            var serialized = string.Join(",", encodedChars);
            return serialized;
        }

        internal static string DeserializeStringFromCharCodeSequence(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            var encodedChars = s.Split(',');
            var deserialized = new String(Array.ConvertAll(encodedChars, x => (char)(int.Parse(x))));
            return deserialized;
        }
    }
}
