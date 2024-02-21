// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SwiftRuntimeLibrary;

namespace SwiftReflector.SwiftXmlReflection
{
    public class TypeSpecToken
    {
        TypeSpecToken(TypeTokenKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }
        public TypeTokenKind Kind { get; private set; }
        public string Value { get; private set; }
        public static TypeSpecToken LabelFromString(string value)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));
            return new TypeSpecToken(TypeTokenKind.TypeLabel, value);
        }
        public static TypeSpecToken FromString(string value)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));
            return new TypeSpecToken(TypeTokenKind.TypeName, value);
        }
        static TypeSpecToken leftparenthesis = new TypeSpecToken(TypeTokenKind.LeftParenthesis, "(");
        static TypeSpecToken rightparenthesis = new TypeSpecToken(TypeTokenKind.RightParenthesis, ")");
        static TypeSpecToken leftangle = new TypeSpecToken(TypeTokenKind.LeftAngle, "<");
        static TypeSpecToken rightangle = new TypeSpecToken(TypeTokenKind.RightAngle, ">");
        static TypeSpecToken comma = new TypeSpecToken(TypeTokenKind.Comma, ",");
        static TypeSpecToken arrow = new TypeSpecToken(TypeTokenKind.Arrow, "->");
        static TypeSpecToken at = new TypeSpecToken(TypeTokenKind.At, "@");
        static TypeSpecToken questionmark = new TypeSpecToken(TypeTokenKind.QuestionMark, "?");
        static TypeSpecToken exclamationpoint = new TypeSpecToken(TypeTokenKind.ExclamationPoint, "!");
        static TypeSpecToken done = new TypeSpecToken(TypeTokenKind.Done, "");
        static TypeSpecToken leftbracket = new TypeSpecToken(TypeTokenKind.LeftBracket, "[");
        static TypeSpecToken rightbracket = new TypeSpecToken(TypeTokenKind.RightBracket, "]");
        static TypeSpecToken colon = new TypeSpecToken(TypeTokenKind.Colon, ":");
        static TypeSpecToken period = new TypeSpecToken(TypeTokenKind.Period, ".'");
        static TypeSpecToken ampersand = new TypeSpecToken(TypeTokenKind.Ampersand, "&");

        public static TypeSpecToken LeftParenthesis { get { return leftparenthesis; } }
        public static TypeSpecToken RightParenthesis { get { return rightparenthesis; } }
        public static TypeSpecToken LeftAngle { get { return leftangle; } }
        public static TypeSpecToken RightAngle { get { return rightangle; } }
        public static TypeSpecToken Comma { get { return comma; } }
        public static TypeSpecToken Arrow { get { return arrow; } }
        public static TypeSpecToken At { get { return at; } }
        public static TypeSpecToken QuestionMark { get { return questionmark; } }
        public static TypeSpecToken ExclamationPoint { get { return exclamationpoint; } }
        public static TypeSpecToken Done { get { return done; } }
        public static TypeSpecToken LeftBracket { get { return leftbracket; } }
        public static TypeSpecToken RightBracket { get { return rightbracket; } }
        public static TypeSpecToken Colon { get { return colon; } }
        public static TypeSpecToken Period { get { return period; } }
        public static TypeSpecToken Ampersand { get { return ampersand; } }
        public override string ToString()
        {
            return Value;
        }
    }

}

