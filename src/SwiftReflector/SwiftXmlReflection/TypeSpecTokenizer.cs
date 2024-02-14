// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using System.IO;
using SwiftReflector.ExceptionTools;

namespace SwiftReflector.SwiftXmlReflection {

	public class TypeSpecTokenizer {
		enum State {
			Start,
			InName,
			InArrow,
		};

		State state;
		StringBuilder buffer;
		TextReader reader;
		static string invalidNameChars;

		static TypeSpecTokenizer ()
		{
			// OK - thanks Apple. Since identifiers in Swift can be any messed up unicode, including emoji, we
			// can't just ask "IsLetterOrNumber". Instead, I build the set of characters that are specifically
			// forbidden. Guh.
			StringBuilder sb = new StringBuilder ();
			for (char c = (char)0; c < '.'; c++) {
				sb.Append (c);
			}
			sb.Append ('/');
			for (char c = ':'; c < 'A'; c++) {
				sb.Append (c);
			}
			for (char c = '['; c < '_'; c++) {
				sb.Append (c);
			}
			sb.Append ('`');
			for (char c = '{'; c <= (char)127; c++) {
				sb.Append (c);
			}
			invalidNameChars = sb.ToString ();
		}

		public TypeSpecTokenizer (TextReader reader)
		{
			this.reader = reader;
			buffer = new StringBuilder ();
			state = State.Start;
		}

		TypeSpecToken curr = null;

		public TypeSpecToken Peek ()
		{
			if (curr == null) {
				curr = Next ();
			}
			return curr;
		}

		public TypeSpecToken Next ()
		{
			if (curr != null) {
				TypeSpecToken retval = curr;
				curr = null;
				return retval;
			}
			TypeSpecToken token = null;
			do {
				switch (state) {
				case State.InName:
					token = DoName ();
					break;
				case State.InArrow:
					token = DoArrow ();
					break;
				case State.Start:
					token = DoStart ();
					break;
				}
			} while (token == null);
			return token;
		}

		public bool NextIs (string name)
		{
			return Peek ().Kind == TypeTokenKind.TypeName && Peek ().Value == name;
		}

		TypeSpecToken DoName ()
		{
			int curr = reader.Peek ();
			if (curr < 0 || InvalidNameCharacter ((char)curr)) {
				if (curr == ':') {
					reader.Read (); // drop the colon
					state = State.Start;
					TypeSpecToken token = TypeSpecToken.LabelFromString (buffer.ToString ());
					buffer.Clear ();
					return token;
				} else {
					state = State.Start;
					TypeSpecToken token = TypeSpecToken.FromString (buffer.ToString ());
					buffer.Clear ();
					return token;
				}
			} else {
				buffer.Append ((char)reader.Read ());
				return null;
			}
		}

		TypeSpecToken DoArrow ()
		{
			if (buffer.Length == 0) {
				if (reader.Peek () == (int)'-') {
					buffer.Append ((char)reader.Read ());
					return null;
				}
			} else {
				if (reader.Peek () == (int)'>') {
					reader.Read ();
					buffer.Clear ();
					state = State.Start;
					return TypeSpecToken.Arrow;
				}
			}
			throw ErrorHelper.CreateError (ReflectorError.kTypeParseBase + 4, $"Unexpected character {(char)reader.Peek ()} while parsing '->'.");
		}

		TypeSpecToken DoStart ()
		{
			int currentChar = reader.Peek ();
			if (currentChar < 0)
				return TypeSpecToken.Done;
			char c = (char)currentChar;
			switch (c) {
			case '(':
				reader.Read ();
				return TypeSpecToken.LeftParenthesis;
			case ')':
				reader.Read ();
				return TypeSpecToken.RightParenthesis;
			case '<':
				reader.Read ();
				return TypeSpecToken.LeftAngle;
			case '>':
				reader.Read ();
				return TypeSpecToken.RightAngle;
			case ',':
				reader.Read ();
				return TypeSpecToken.Comma;
			case '@':
				reader.Read ();
				return TypeSpecToken.At;
			case '?':
				reader.Read ();
				return TypeSpecToken.QuestionMark;
			case '!':
				reader.Read ();
				return TypeSpecToken.ExclamationPoint;
			case '-':
				state = State.InArrow;
				return null;
			case '[':
				reader.Read ();
				return TypeSpecToken.LeftBracket;
			case ']':
				reader.Read ();
				return TypeSpecToken.RightBracket;
			case ':':
				reader.Read ();
				return TypeSpecToken.Colon;
			case '.':
				reader.Read ();
				return TypeSpecToken.Period;
			case '&':
				reader.Read ();
				return TypeSpecToken.Ampersand;
			default:
				if (Char.IsWhiteSpace (c)) {
					reader.Read ();
					return null;
				}
				if (InvalidNameCharacter (c)) {
					throw ErrorHelper.CreateError (ReflectorError.kTypeParseBase + 7, $"Unexpected/illegal char {c}");
				}
				state = State.InName;
				return null;
			}
		}

		static bool InvalidNameCharacter (char c)
		{
			return invalidNameChars.IndexOf (c) >= 0;
		}
	}
}

