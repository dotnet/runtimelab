// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using System.Collections.Generic;

namespace BindingsGeneration.Demangling {
	/// <Summary>
	/// Represents a a slice which includes facilities similar to StringStream, but
	/// simplified and more performant. This is build to work specifically with the
	/// demangler.
	/// </Summary>
	internal class StringSlice {
		string slice;

		/// <Summary>
		/// Construct a string slice from a string
		/// </Summary>
		public StringSlice (string s)
		{
			slice = s;
		}

		/// <Summary>
		/// Returns true if the slice starts with c
		/// </Summary>
		public bool StartsWith (char c)
		{
			return Current == c;
		}

		/// <Summary>
		/// Returns true if the slice starts with the given string
		/// </Summary>
		public bool StartsWith (string s)
		{
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

		/// <Summary>
		/// Returns the current character in the slice
		/// </Summary>
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

		/// <Summary>
		/// Returns true if the slice is at the end
		/// </Summary>
		public bool IsAtEnd { get { return Position == slice.Length; } }

		/// <Summary>
		/// Returns the current position of the slice
		/// </Summary>
		public int Position { get; private set; }

		/// <Summary>
		/// Returns the length of the (remaning) slice
		/// </Summary>
		public int Length { get { return slice.Length - Position; } }

		/// <Summary>
		/// Returns the original string
		/// </Summary>
		public string Original { get { return slice; } }

		/// <Summary>
		/// Returns the text of the slice in its current state
		/// </Summary>
		public override string ToString ()
		{
			if (IsAtEnd)
				return "";
			return Position == 0 ? slice : slice.Substring (Current);
		}

		/// <Summary>
		/// Advance the slice one character
		/// </Summary>
		public char Advance ()
		{
			if (IsAtEnd) {
				throw new IndexOutOfRangeException ();
			}
			char c = Current;
			Position++;
			return c;
		}

		/// <Summary>
		/// Advance the slice if and only if the current character matches c
		/// </Summary>
		public bool AdvanceIfEquals (char c)
		{
			return AdvanceIf (sl => sl.Current == c);
		}

		/// <Summary>
		/// Advance if and only if the predicate returns true
		/// </Summary>
		public bool AdvanceIf (Predicate<StringSlice> pred)
		{
			var eq = pred (this);
			if (eq)
				Advance ();
			return eq;
		}

		/// <Summary>
		/// Advance the slice by n characters returning the advanced characters
		/// </Summary>
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

		/// <Summary>
		/// Rewind the slice to the beginning
		/// </Summary>
		public void Rewind()
		{
			if (Position <= 0)
				throw new InvalidOperationException ("Can't rewind a slice already at the start.");
			Position -= 1;
		}

		/// <Summary>
		/// Returns the character indexed from the current position of the slice
		/// </Summary>
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

		/// <Summary>
		/// Returns true if the slice starts with the start of a Name
		/// </Summary>
		public bool IsNameNext {
			get {
				return !IsAtEnd && IsNameStart (Current);
			}
		}

		/// <Summary>
		/// Consumes the rest of the slice leaving it at the end and return the remaing characters in the slice
		/// </Summary>
		public string ConsumeRemaining()
		{
			if (IsAtEnd)
				return "";
			var result = slice.Substring (Position);
			Advance (result.Length);
			return result;
		}

		/// <Summary>
		/// Return a slice of the original string
		/// </Summary>
		public string Substring(int position, int length)
		{
			return slice.Substring (position, length);
		}

		/// <Summary>
		/// Extract a swift string returning it and setting isPunyCode to true if the swift string is in puny code
		/// </Summary>
		public string? ExtractSwiftString (out bool isPunyCode)
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
	}
}