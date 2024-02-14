// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using System.Collections.Generic;
using SwiftRuntimeLibrary;

namespace SwiftReflector {
	public class StringSlice {
		string slice;

		public StringSlice (string s)
		{
			slice = Exceptions.ThrowOnNull (s, "s");
		}

		public bool StartsWith (char c)
		{
			return Current == c;
		}

		public bool StartsWith (string s)
		{
			if (s == null)
				throw new ArgumentNullException (nameof(s));
			if (s == "")
				return true; // I guess?
			if (s.Length > Length)
				return false;
			for (int i = 0; i < s.Length; i++) {
				if (s [i] != this [i])
					return false;
			}
			return true;
		}

		public char Current {
			get {
				// returns the current character in the slice
				// this[0] always points to the character (see
				// the indexer).
				if (IsAtEnd)
					throw new ArgumentException ("at end");
				return this [0];
			}
		}

		public bool IsAtEnd { get { return Position == slice.Length; } }

		public int Position { get; private set; }
		public int Length { get { return slice.Length - Position; } }

		public string Original { get { return slice; } }

		public override string ToString ()
		{
			if (IsAtEnd)
				return "";
			return Position == 0 ? slice : slice.Substring (Current);
		}

		public char Advance ()
		{
			if (IsAtEnd) {
				throw new IndexOutOfRangeException ();
			}
			char c = Current;
			Position++;
			return c;
		}

		public bool AdvanceIfEquals (char c)
		{
			return AdvanceIf (sl => sl.Current == c);
		}

		public bool AdvanceIf (Predicate<StringSlice> pred)
		{
			if (pred == null)
				throw new ArgumentNullException ();
			bool eq = pred (this);
			if (eq)
				Advance ();
			return eq;
		}

		public string Advance (int n)
		{
			if (n < 0 || n + Position > slice.Length)
				throw new ArgumentOutOfRangeException (nameof(n));
			if (n == 0)
				return "";
			string sub = slice.Substring (Position, n);
			Position += n;
			return sub;
		}

		public void Rewind()
		{
			if (Position <= 0)
				throw new InvalidOperationException ("Can't rewind a slice already at the start.");
			Position -= 1;
		}

		public char this [int index] {
			get {
				if (index + Position >= slice.Length)
					throw new IndexOutOfRangeException ();
				return slice [index + Position];
			}
		}

		static bool IsNameStart (char c)
		{
			return Char.IsDigit (c) || c == 'X';
		}

		public bool IsNameNext {
			get {
				return !IsAtEnd && IsNameStart (Current);
			}
		}

		public string ConsumeRemaining()
		{
			if (IsAtEnd)
				return "";
			var result = slice.Substring (Position);
			Advance (result.Length);
			return result;
		}

		public string Substring(int position, int length)
		{
			return slice.Substring (position, length);
		}

		public string ExtractSwiftString (out bool isPunyCode)
		{
			if (!IsNameNext) {
				isPunyCode = false;
				return null;
			}

			int count = 0;

			if ((isPunyCode = Current == 'X'))
				Advance ();

			while (!IsAtEnd && Char.IsDigit (Current)) {
				count = count * 10 + Advance () - '0';
			}
			return Advance (count);
		}

		public SwiftName ExtractSwiftName ()
		{
			bool isPunyCode = false;
			string s = ExtractSwiftString (out isPunyCode);
			if (s == null)
				return null;
			return new SwiftName (s, isPunyCode);
		}

		public SwiftName ExtractSwiftNameMaybeOperator (out OperatorType oper)
		{
			oper = OperatorType.None;
			if (StartsWith ('o')) {
				Advance ();
				char c = Advance ();
				switch (c) {
				case 'p':
					oper = OperatorType.Prefix;
					break;
				case 'P':
					oper = OperatorType.Postfix;
					break;
				case 'i':
					oper = OperatorType.Infix;
					break;
				default:
					break;
				}
			}
			bool isPunyCode = false;
			string s = ExtractSwiftString (out isPunyCode);
			if (s == null)
				return null;
			if (oper != OperatorType.None) {
				s = DecodeOperatorName (s);
			}
			return new SwiftName (s, isPunyCode);
		}

		static string _opChars = "& @/= >    <*!|+?%-~   ^ .";
		static string DecodeOperatorName (string s)
		{
			var sb = new StringBuilder ();
			foreach (char c in s) {
				if (c > 32767) {
					sb.Append (c);
					continue;
				}
				if (c < 'a' || c > 'z')
					throw new ArgumentOutOfRangeException (nameof(s), String.Format ("operator name '{0}' contains illegal characters", s));
				char o = _opChars [c - 'a'];
				sb.Append (o);
			}
			return sb.ToString ();
		}


	}
}

