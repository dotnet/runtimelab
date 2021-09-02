// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Provides some character escaping routines for strings.
    /// </summary>
    internal static class StringUtility
    {
        /// <summary>
        /// Make an escaped string from a character.
        /// </summary>
        /// <param name="c">given character</param>
        /// <param name="useNumericRepresentationOnly">if true then use numeric hexadecimal escaping of all characters</param>
        public static string Escape(char c, bool useNumericRepresentationOnly = false)
        {
            int code = c;

            if (useNumericRepresentationOnly)
            {
                return code switch
                {
                    <= 0xFF => $"\\x{code:X2}",
                    _ => $"\\u{code:X4}"
                };
            }

            //special characters
            return c switch
            {
                '.' => @"\.",
                '[' => @"\[",
                ']' => @"\]",
                '(' => @"\(",
                ')' => @"\)",
                '{' => @"\{",
                '}' => @"\}",
                '?' => @"\?",
                '+' => @"\+",
                '*' => @"\*",
                '|' => @"\|",
                '\\' => @"\\",
                '^' => @"\^",
                '$' => @"\$",
                '-' => @"\-",
                ':' => @"\:",
                '\"' => "\\\"",
                '\0' => @"\0",
                '\t' => @"\t",
                '\r' => @"\r",
                '\v' => @"\v",
                '\f' => @"\f",
                '\n' => @"\n",
                _ when code >= 0x20 && code <= 0x7E => c.ToString(),
                _ when code <= 0xFF => $"\\x{code:X2}",
                _  => $"\\u{code:X4}",
            };
        }
    }
}
