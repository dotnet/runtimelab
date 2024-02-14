// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SwiftRuntimeLibrary;

namespace SwiftReflector {
	public class CSKeywords {
		static HashSet<string> keyWords;
		static string [] keyArr = new string [] {
			"abstract", "as", "base", "bool", "break", "byte",
			"case", "catch", "char", "checked", "class", "const",
			"continue", "decimal", "default", "delegate", "do",
			"double", "else", "enum", "event", "explicit", "extern",
			"false", "finally", "fixed", "float", "for", "foreach",
			"goto", "if", "implicit", "in", "int", "interface", "internal",
			"is", "lock", "long", "namespace", "new", "null", "object",
			"operator", "out", "override", "params", "private", "protected",
			"public", "readonly", "ref", "return", "sbyte", "sealed", "short",
			"sizeof", "stackalloc", "static", "string", "struct", "switch",
			"this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
			"unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
			"add", "alias", "ascending", "async", "await", "descending", "dynamic",
			"from", "get", "global", "group", "into", "join", "let", "orderby",
			"partial", "remove", "select", "set", "value", "var", "where", "yield"
		};
		static CSKeywords ()
		{
			keyWords = new HashSet<string> ();
			foreach (string s in keyArr) {
				keyWords.Add (s);
			}
		}

		public static bool IsKeyword (string s)
		{
			Exceptions.ThrowOnNull (s, "s");
			return keyWords.Contains (s);
		}
	}
}

