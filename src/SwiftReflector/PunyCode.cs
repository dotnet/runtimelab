// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SwiftReflector {
	public class PunyCode {
		const string kEncodingStr = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJ";

		Dictionary<char, int> decodeTable;
		const int kBase = 36;
		const int tMin = 1;
		const int tMax = 26;
		const int skew = 38;
		const int damp = 700;
		const int initialBias = 72;
		const int initialN = 0x80;
		const char delimiter = '_';

		public PunyCode ()
		{
			decodeTable = MakeDecodeTable (kEncodingStr);
		}

		public Dictionary<char, int> MakeDecodeTable (string s)
		{
			var table = new Dictionary<char, int> ();
			for (int i = 0; i < s.Length; i++) {
				table [s [i]] = i;
			}
			return table;
		}

		static int Adapt (int delta, int numPoints, bool firstTime)
		{
			delta = delta / (firstTime ? damp : 2);

			delta += delta / numPoints;
			int k = 0;
			while (delta > ((kBase - tMin) * tMax) / 2) {
				delta = delta / (kBase - tMin);
				k = k + kBase;
			}
			k += ((kBase - tMin + 1) * delta) / (delta + skew);
			return k;
		}

		public string Decode (string input)
		{

			int n = initialN;
			int i = 0;
			int bias = initialBias;
			var output = new StringBuilder ();

			int pos = 0;
			var delim = input.LastIndexOf ('_');
			if (delim > 0) {
				output.Append (input.Substring (0, delim));
				pos = delim + 1;
			}

			int outputLength = output.Length;
			int inputLength = input.Length;
			while (pos < inputLength) {
				int oldi = i;
				int w = 1;
				for (int k = kBase; ; k += kBase) {
					int digit = decodeTable [input [pos++]];
					i = i + (digit * w);
					int t = Math.Max (Math.Min (k - bias, tMax), tMin);
					if (digit < t) {
						break;
					}
					w = w * (kBase - t);
				}
				bias = Adapt (i - oldi, ++outputLength, (oldi == 0));
				n = n + i / outputLength;
				i = i % outputLength;
				if (n >= 0xd800 && n <= 0xdfff)
					output.Insert (i, (char)n);
				else
					output.Insert (i, Char.ConvertFromUtf32 (n));
				i++;
			}
			return output.ToString ();
		}

		static int digit_index (char value)
		{
			if (value >= 'a' && value <= 'z')
				return value - 'a';
			if (value >= 'A' && value <= 'J')
				return value - 'A' + 26;
			return -1;
		}

		// I'm leaving this here for possible future need.
		// This is a port of Apple's decode which is more or less equivalent to
		// the above Decode. They both have their plusses and minuses, but I like Apple's
		// lees, so it's not (currently) active.

		public string AppleDecode (string inputPunyCode)
		{
			var output = new StringBuilder ();
			int i = 0;
			var n = initialN;
			var bias = initialBias;

			var lastDelimiter = inputPunyCode.LastIndexOf (delimiter);
			if (lastDelimiter > 0) {
				for (int x=0; x < lastDelimiter; x++) {
					if (inputPunyCode [x] > 0x7f)
						return output.ToString ();
					output.Append (inputPunyCode [x]);
				}
			}

			var inputPunySlice = new StringSlice (inputPunyCode.Substring (lastDelimiter + 1));

			while (!inputPunySlice.IsAtEnd) {
				var oldi = i;
				var w = 1;
				for (int k = kBase; ; k += kBase) {
					if (inputPunySlice.IsAtEnd)
						return output.ToString ();
					var codePoint = inputPunySlice.Advance ();

					var digit = digit_index (codePoint);
					if (digit < 0)
						return output.ToString ();

					i = i + digit * w;
					var t = k <= bias ? tMin
						: (k >= bias + tMax ? tMax : k - bias);
					if (digit < t)
						break;
					w = w * (kBase - t);
				}
				bias = Adapt (i - oldi, output.Length + 1, oldi == 0);
				n = n + i / (output.Length + 1);
				i = i % (output.Length + 1);
				if (n < 0x80)
					return output.ToString ();
				if (n >= 0xd800 && n <= 0xdfff)
					output.Insert (i, (char)n);
				else
					output.Insert (i, Char.ConvertFromUtf32 (n));
				i++;
			}
			return output.ToString ();
		}

		static PunyCode _puny = new PunyCode ();
		public static PunyCode PunySingleton { get { return _puny; } }
	}
}

