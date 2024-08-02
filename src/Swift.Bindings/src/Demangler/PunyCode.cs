// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BindingsGeneration.Demangling {
	/// <Summary>
	/// A class for decoding puny code. Encoder has been removed.
	/// Note that this is not true puny code but a variant where _ is used as the
	/// delimeter and ent symbology is for encoding is [a-zA-J].
	/// Also non-symbol ASCII caracters (except [$_a-zA-Z0=9]) are mapped to
	/// the code range d800-d880 and are encoded like non-ascii characters.
	/// </Summary>
	public class PunyCode {
		const string kEncodingStr = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJ";

		static Dictionary<char, int> decodeTable = MakeDecodeTable(kEncodingStr);
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
		}

		static Dictionary<char, int> MakeDecodeTable (string s)
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

		/// <Summary>
		/// Decode a puny coded string
		/// </Summary>
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
	}
}

