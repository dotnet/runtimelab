// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using System.IO;

namespace BindingsGeneration;

/// <summary>
/// a simple 1 char look-ahead state machine to tokenize the language of type specs
/// </summary>
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

    /// <summary>
    /// Builds a set of characters that are illegal for names
    /// </summary>
    static TypeSpecTokenizer()
    {
        // Since identifiers in Swift can be nearly any unicode, including emoji, we
        // can't just ask "IsLetterOrNumber". Instead, I build the set of characters that are specifically
        // forbidden.
        var sb = new StringBuilder();
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
        invalidNameChars = sb.ToString();
    }

    /// <summary>
    /// Builds a tokenizer that will build tokens from the given reader
    /// <summary>
    public TypeSpecTokenizer(TextReader reader)
    {
        this.reader = reader;
        buffer = new ();
        state = State.Start;
    }

    TypeSpecToken? curr = null;

    /// <summary>
    /// Return the next token without consuming it.
    /// </summary>
    public TypeSpecToken Peek()
    {
        if (curr is null) {
            curr = Next();
        }
        return curr;
    }

    /// <summary>
    /// Return the next token consuming it
    /// </summary>
    public TypeSpecToken Next()
    {
        // this is based on a simple state machine that switches between
        // 3 states: Start, InName, and InArrow. Each of the sub handlers will return
        // either null (more work to do) or a finished token.
        if (curr is not null) {
            var retval = curr;
            curr = null;
            return retval;
        }
        TypeSpecToken? token = null;
        do {
            switch (state) {
                case State.InName:
                    token = DoName();
                    break;
                case State.InArrow:
                    token = DoArrow();
                    break;
                case State.Start:
                    token = DoStart();
                    break;
            }            
        } while (token is null);
        return token;
    }

    /// <summary>
    /// Return true if and only if the next token is a name and is equal to the supplied string
    /// </summary>
    public bool NextIs(string name)
    {
        return Peek().Kind == TypeTokenKind.TypeName && Peek().Value == name;
    }

    /// <summary>
    /// Scans in a name or label token
    /// </summary>
    TypeSpecToken? DoName()
    {
        // parses a name until we hit an invalid character for a name
        int curr = reader.Peek();
        if (curr < 0 || InvalidNameCharacter((char)curr)) {
            // if the invalid character is a ':', this is a label, otherwise it's a name
            if (curr == ':') {
                reader.Read(); // drop the colon
                state = State.Start;
                var token = TypeSpecToken.LabelFromString(buffer.ToString());
                buffer.Clear();
                return token;
            } else {
                state = State.Start;
                var token = TypeSpecToken.FromString(buffer.ToString());
                buffer.Clear();
                return token;
            }
        } else {
            buffer.Append((char)reader.Read());
            return null;
        }
    }

    /// <summary>
    /// parses an arrow (->)
    /// </summary>
    TypeSpecToken? DoArrow()
    {
        if (buffer.Length == 0) {
            if (reader.Peek() == (int)'-') {
                buffer.Append((char)reader.Read());
                return null;
            } 
        } else {
            if (reader.Peek() == (int)'>') {
                reader.Read();
                buffer.Clear();
                state = State.Start;
                return TypeSpecToken.Arrow;
            }
        }
        throw new Exception($"Unexpected character in arrow token: {(char)reader.Peek()}");
    }

    /// <summary>
    /// Handle the start state condition
    /// </summary>
    TypeSpecToken? DoStart ()
    {
        // in the start state. If it's a known single character token, return that.
        // If it's a '-' switch to InArrow state
        // If it's whitespace, consume it
        // If it's a legal name character, switch to InName state
        // If it's end of stream, return Done
        // If its anything else, throw
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
                throw new Exception($"Unexpected/illegal char {c}");
            }
            state = State.InName;
            return null;
        }
    }

    /// <summary>
    /// Return true if and only if c is invalid for a name
    /// <summary>
    static bool InvalidNameCharacter (char c)
    {
        return invalidNameChars.IndexOf (c) >= 0;
    }
}