// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

/// <summary>
/// Represents the type of token from the tokenizer
/// </summary>
public enum TypeTokenKind {
    /// <summary>
    /// The token is a nominal swift type, e.g., Foo.Bar.Baz
    /// </summary>
    TypeName,
    /// <summary>
    /// The token is a comma
    /// </summary>
    Comma,
    /// <summary>
    /// The token is a left parenthesis, e.g. '('
    /// </summary>
    LeftParenthesis,
    /// <summary>
    /// The token is a right parenthesis, e.g. ')'
    /// </summary>
    RightParenthesis,
    /// <summary>
    /// The token is a left angle
    /// </summary>
    LeftAngle,
    /// <summary>
    /// The token is a right angle
    /// </summary>
    RightAngle,
    /// <summary>
    /// The token is a left bracket
    /// </summary>
    LeftBracket,
    /// <summary>
    /// The token is a right bracket
    /// </summary>
    RightBracket,
    /// <summary>
    /// The token is an arrow, e.g., '->'
    /// </summary>
    Arrow,
    /// <summary>
    /// The token is an at sign
    /// </summary>
    At,
    /// <summary>
    /// The token is a question mark
    /// </summary>
    QuestionMark,
    /// <summary>
    /// The token is a type label, e.g., 'someLabel:'
    /// </summary>
    TypeLabel,
    /// <summary>
    /// The token is a colon
    /// </summary>
    Colon,
    /// <summary>
    /// The token is an exclamation point
    /// </summary>
    ExclamationPoint,
    /// <summary>
    /// The token is a period
    /// </summary>
    Period,
    /// <summary>
    /// The token is an ampersand
    /// </summary>
    Ampersand,
    /// <summary>
    /// This is a special token representing the end of the stream.
    /// </summary>
    Done,
}

/// <summary>
/// Represents a token for parsing a TypeSpec
/// </summary>
public class TypeSpecToken {
    TypeSpecToken(TypeTokenKind kind, string value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>
    /// The kind of this token
    /// </summary>
    public TypeTokenKind Kind { get; private set; }

    /// <summary>
    /// The string value of the token
    /// </summary>
    public string Value { get; private set; }

    /// <summary>
    /// Returns the Value of the token
    /// </summary>
    public override string ToString() => Value;
    
    /// <summary>
    /// Returns a label token from the given string
    /// </summary>
    public static TypeSpecToken LabelFromString(string value) => new(TypeTokenKind.TypeLabel, value);

    /// <summary>
    /// Returns a name token from the given string
    /// </summary>
    public static TypeSpecToken FromString(string value) => new(TypeTokenKind.TypeName, value);

    /// <summary>
    /// A singleton for the left parenthesis token
    /// </summary>
    public static TypeSpecToken LeftParenthesis { get; } = new (TypeTokenKind.LeftParenthesis, "(");
     /// <summary>
    /// A singleton for the right parenthesis token
    /// </summary>
   public static TypeSpecToken RightParenthesis { get; } = new (TypeTokenKind.RightParenthesis, ")");

    /// <summary>
    /// A singleton for the left angle token
    /// </summary>
    public static TypeSpecToken LeftAngle { get; } = new (TypeTokenKind.LeftAngle, "<");
    /// <summary>
    /// A singleton for the right angle token
    /// </summary>
    public static TypeSpecToken RightAngle { get; } = new (TypeTokenKind.RightAngle, ">");

    /// <summary>
    /// A singleton for the left bracket token
    /// </summary>
    public static TypeSpecToken LeftBracket { get;} = new (TypeTokenKind.LeftBracket, "[");
    /// <summary>
    /// A singleton for the right bracket token
    /// </summary>
    public static TypeSpecToken RightBracket { get;} = new (TypeTokenKind.RightBracket, "]");

    /// <summary>
    /// A singleton for the comma token
    /// </summary>
    public static TypeSpecToken Comma { get; } = new (TypeTokenKind.Comma, ",");
    /// <summary>
    /// A singleton for the arrow token
    /// </summary>
    public static TypeSpecToken Arrow { get; } = new (TypeTokenKind.Arrow, "->");
    /// <summary>
    /// A singleton for the at sign token
    /// </summary>
    public static TypeSpecToken At { get; } = new (TypeTokenKind.At, "@");
    /// <summary>
    /// A singleton for the question mark token
    /// </summary>
    public static TypeSpecToken QuestionMark { get; } = new (TypeTokenKind.QuestionMark, "?");
    /// <summary>
    /// A singleton for the exclamation point token
    /// </summary>
    public static TypeSpecToken ExclamationPoint { get; } = new (TypeTokenKind.ExclamationPoint, "!");
    /// <summary>
    /// A singleton for the done token
    /// </summary>
    public static TypeSpecToken Done { get; } = new (TypeTokenKind.Done, "");
    /// <summary>
    /// A singleton for the colon token
    /// </summary>
    public static TypeSpecToken Colon { get; } = new (TypeTokenKind.Colon, ":");
    /// <summary>
    /// A singleton for the period token
    /// </summary>
    public static TypeSpecToken Period { get; } = new (TypeTokenKind.Period, ".");
    /// <summary>
    /// A singleton for the ampersand token
    /// </summary>
    public static TypeSpecToken Ampersand { get; } = new(TypeTokenKind.Ampersand, "&");
}