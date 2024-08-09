// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Collections.Generic;


namespace BindingsGeneration;

/// <summary>
/// A class to parse swift type specifications into the TypeSpec type
/// The TypeSpec language is a little language to represent a swift type.
/// It will lool like:
/// typespec: [attribute]*[inout][any][typelable]typespec[generic-clause]['?']
/// typespec: tuple-typespec|named-typespec|func-typespec|protocol-list-type-spec
/// tuple-typespec:'('typespec-list')'
/// named-typespec: name
/// func-type-spec: ypespec [async][throws]t '->' typespec
/// protocol-list-type-spec: named-typespec &amp; named-typespec [&amp; named-type-spec]*
/// generic-clause: '<' typespec-list '>'
/// typespeclist: typespec [',' typespec]*
/// attribute @name['(' parameters ')']
/// </summary>
public class TypeSpecParser {
    TextReader reader;
    TypeSpecTokenizer tokenizer;

    /// <summary>
    /// Private constructor for a TypeSpecParser using the supplied TextReader as a source
    /// </summary>
    TypeSpecParser(TextReader reader)
    {
        this.reader = reader;
        tokenizer = new TypeSpecTokenizer(reader);
    }

    /// <summary>
    /// Private utility method to parse and return a TypeSpec
    /// </summary>
    TypeSpec? Parse()
    {
        // This is a very simple recursive descent parser with 1 token look-ahead
        // The typespec language is very simple and doesn't need anything more
        var token = tokenizer.Peek();
        TypeSpec? type = null;
        List<TypeSpecAttribute> attrs = new ();
        var inout = false;
        var isAny = false;
        string? typeLabel = null;
        var throwsClosure = false;
        var asyncClosure = false;
        var expectClosure = false;

        // Prefix

        // parse any attributes
        if (token.Kind == TypeTokenKind.At) {
            attrs = ParseAttributes();
            token = tokenizer.Peek();
        }

        // looks like it's inout, consume and continue
        if (token.Kind == TypeTokenKind.TypeName && token.Value == "inout") {
            inout = true;
            tokenizer.Next ();
            token = tokenizer.Peek ();
        }

        // any, consume and continute
        if (token.Kind == TypeTokenKind.TypeName && token.Value == "any") {
            isAny = true;
            tokenizer.Next ();
            token = tokenizer.Peek ();
        }

        // label, consume and continue
        if (token.Kind == TypeTokenKind.TypeLabel) {
            typeLabel = token.Value;
            tokenizer.Next ();
            token = tokenizer.Peek ();
        }

        // meat of the type

        if (token.Kind == TypeTokenKind.LeftParenthesis) { // tuple
            tokenizer.Next ();
            TupleTypeSpec tuple = ParseTuple ();
            type = tuple.Elements.Count == 1 ? tuple.Elements [0] : tuple;
            typeLabel = type.TypeLabel;
            type.TypeLabel = null;
        } else if (token.Kind == TypeTokenKind.TypeName) { // name
            tokenizer.Next ();
            var tokenValue = token.Value.StartsWith ("ObjectiveC.", StringComparison.Ordinal) ?
                            "Foundation" + token.Value.Substring ("ObjectiveC".Length) : token.Value;
            if (tokenValue == "Swift.Void")
                type = TupleTypeSpec.Empty;
            else
                type = new NamedTypeSpec(tokenValue);
        } else if (token.Kind == TypeTokenKind.LeftBracket) { // array
            tokenizer.Next ();
            type = ParseArrayOrDictionary ();
        } else { // illegal
            throw new Exception($"Unexpected token {token.Value}.");
        }

        if (tokenizer.NextIs("async")) {
            tokenizer.Next();
            asyncClosure = true;
            expectClosure = true;
        }

        if (tokenizer.NextIs("throws")) {
            tokenizer.Next();
            throwsClosure = true;
            expectClosure = true;
        }

        if (tokenizer.Peek().Kind == TypeTokenKind.Arrow) {
            tokenizer.Next();
            type = ParseClosure(type, throwsClosure, asyncClosure);
            expectClosure = false;
            throwsClosure = false;
            asyncClosure = false;
        } else if (expectClosure) {
            var errorCase = asyncClosure && throwsClosure ? "'async throws'" : asyncClosure ? "'async'" : "'throws'";
            throw new Exception($"Unexpected token {tokenizer.Peek ().Value} after {errorCase} in a closure.");
        } else if (tokenizer.Peek().Kind == TypeTokenKind.LeftAngle) {
            tokenizer.Next();
            type = Genericize (type);
        }

        if (tokenizer.Peek().Kind == TypeTokenKind.Period) {
            tokenizer.Next();
            var currType = type as NamedTypeSpec;
            if (currType is null)
                throw new Exception($"In parsing an inner type (type.type), first element is a {type.Kind} instead of a NamedTypeSpec.");
            var nextType = Parse() as NamedTypeSpec;
            if (nextType is null)
                throw new Exception($"In parsing an inner type (type.type), the second element is a {type.Kind} instead of a NamedTypeSpec");
            currType.InnerType = nextType;
        }

        // Postfix

        if (tokenizer.Peek ().Kind == TypeTokenKind.Ampersand) {
            if (type is NamedTypeSpec ns) {
                type = ParseProtocolList (ns);
            } else {
                throw new Exception($"In parsing a protocol list type, expected a NamedTypeSpec but got a {type.GetType().Name}");
            }
        }

        // this handles arbitrary nested optionals (eg, Int?????????)
        while (tokenizer.Peek ().Kind == TypeTokenKind.QuestionMark) {
            tokenizer.Next ();
            type = WrapAsBoundGeneric (type, "Swift.Optional");
        }

        if (tokenizer.Peek ().Kind == TypeTokenKind.ExclamationPoint) {
            tokenizer.Next ();
            type = WrapAsBoundGeneric (type, "Swift.ImplicitlyUnwrappedOptional");
        }

        type.IsInOut = inout;
        type.IsAny = isAny;
        type.TypeLabel = typeLabel;

        if (type != null && attrs != null) {
            type.Attributes.AddRange (attrs);
        }

        return type;
    }

    /// <summary>
    /// Parse the attributes that might be present in a TypeSpec
    /// </summary>
    List<TypeSpecAttribute> ParseAttributes()
    {
        // An attribute is
        // @name
        // or
        // @name [ parameters ]
        // The spec says that it could be ( parameters ), [ parameters ], or { parameters }
        // but the reflection code should make certain that it's [ parameters ].
        List<TypeSpecAttribute> attrs = new List<TypeSpecAttribute>();
        while (true) {
            if (tokenizer.Peek().Kind != TypeTokenKind.At) {
                return attrs;
            }
            tokenizer.Next();
            if (tokenizer.Peek().Kind != TypeTokenKind.TypeName) {
                throw new Exception($"Unexpected token {tokenizer.Peek().Value}, expected a name while parsing an attribute.");
            }
            string name = tokenizer.Next ().Value;
            TypeSpecAttribute attr = new TypeSpecAttribute(name);
            if (tokenizer.Peek().Kind == TypeTokenKind.LeftBracket) {
                tokenizer.Next();
                ParseAttributeParameters(attr.Parameters);
            }
            attrs.Add(attr);
        }
    }

    /// <summary>
    /// Parses a comma separated list of parameters ending with a right bracket
    /// </summary>
    void ParseAttributeParameters (List<string> parameters)
    {
        // Attribute parameters are funny
        // The contents between the brackets vary.
        // They may be comma separated. They may not.
        // Therefore this code is likely to break, but since I'm responsible for
        // generating the text of the attributes parsed here, I can try to ensure
        // that it will always fit the pattern.
        while (true) {
            if (tokenizer.Peek().Kind == TypeTokenKind.RightBracket) {
                tokenizer.Next();
                return;
            }
            var value = tokenizer.Next();
            if (value.Kind != TypeTokenKind.TypeName) {
                throw new Exception($"Unexpected token {value.Value} while parsing attribute parameter.");
            }
            parameters.Add(value.Value);
            if (tokenizer.Peek().Kind == TypeTokenKind.Comma) {
                tokenizer.Next();
            }
        }
    }

    /// <summary>
    /// Parse a protocol list of the form name &amp; name [&amp; name]*
    /// <summary>
    TypeSpec ParseProtocolList(NamedTypeSpec first)
    {
        var protocols = new List<NamedTypeSpec> ();
        protocols.Add(first);
        while (true) {
            if (tokenizer.Peek().Kind != TypeTokenKind.Ampersand)
                break;
            tokenizer.Next();
            var nextName = tokenizer.Next();
            if (nextName.Kind != TypeTokenKind.TypeName)
                throw new Exception($"Unexpected token '{nextName.Value}' with kind {nextName.Kind} while parsing a protocol list");
            protocols.Add(new NamedTypeSpec(nextName.Value));
        }
        return new ProtocolListTypeSpec (protocols);
    }

    /// <summary>
    /// Parses a comman separated list of tokens with the specified terminating token.
    /// Used for parsing generics and tuples.
    /// </summary>
    void ConsumeList (List<TypeSpec> elements, TypeTokenKind terminator, string typeImParsing)
    {
        while (true) {
            if (tokenizer.Peek().Kind == terminator) {
                tokenizer.Next();
                return;
            }
            var next = Parse ();
            if (next is null)
                throw new Exception($"Unexpected end while parsing a {typeImParsing}");
            elements.Add(next);
            if (tokenizer.Peek().Kind == TypeTokenKind.Comma) {
                tokenizer.Next();
            }
        }
    }

    /// <summary>
    /// Given an existing TypeSpec, add generic parameters to it
    /// </summary>
    TypeSpec Genericize(TypeSpec type)
    {
        ConsumeList (type.GenericParameters, TypeTokenKind.RightAngle, "generic parameter list");
        return type;
    }

    /// <summary>
    /// Given an existing TypeSpec type, create a new NamedTypeSpec with name
    /// and type as the generic parameter, e.g. name&lt;type&rt;
    /// </summary>
    TypeSpec WrapAsBoundGeneric(TypeSpec type, string name)
    {
        var result = new NamedTypeSpec(name);
        result.GenericParameters.Add(type);
        return result;
    }

    /// <summary>
    /// Parse a tuple and return it
    /// </summary>
    TupleTypeSpec ParseTuple()
    {
        TupleTypeSpec tuple = new TupleTypeSpec ();
        ConsumeList(tuple.Elements, TypeTokenKind.RightParenthesis, "tuple");
        return tuple;
    }

    /// <summary>
    /// Parse an array or dictionary.
    /// An array is in the form [ TypeSpec ] and a dictionary is in the form [ TypeSpec : TypeSpec ]
    /// </summary>
    NamedTypeSpec ParseArrayOrDictionary()
    {
        var keyType = Parse ();
        TypeSpec? valueType = null;
        if (keyType is null)
            throw new Exception("Unexpected end while parsing an array or dictionary.");
        if (tokenizer.Peek().Kind == TypeTokenKind.Colon) {
            tokenizer.Next();
            valueType = Parse();
            if (valueType is null)
                throw new Exception("Unexpected end while parsing a dictionary value type.");
        } else if (tokenizer.Peek().Kind != TypeTokenKind.RightBracket)
            throw new Exception("Expected a right bracket after an array or dictionary.");

        tokenizer.Next();

        if (valueType is null) {
            return WrapAsBoundGeneric(keyType, "Swift.Array");
        } else {
            var dictionary = new NamedTypeSpec("Swift.Dictionary");
            dictionary.GenericParameters.Add(keyType);
            dictionary.GenericParameters.Add(valueType);
            return dictionary;
        }
    }

    /// <summary>
    /// Parses a closure with the argument list and arrow already consumed already parsed.
    /// </summary>
    ClosureTypeSpec ParseClosure(TypeSpec arg, bool throws, bool isAsync)
    {
        var returnType = Parse();
        if (returnType is null)
            throw new Exception("Unexpected end while parsing a closure.");
        var closure = new ClosureTypeSpec ();
        closure.Arguments = arg;
        closure.ReturnType = returnType;
        closure.Throws = throws;
        closure.IsAsync = isAsync;
        return closure;
    }

    /// <summary>
    /// Parse a string representing a Swift type specification into a TypeSpec.
    /// Returns null on empty string and throws on a parse error.
    /// </summary>
    public static TypeSpec? Parse (string typeName)
    {
        TypeSpecParser parser = new TypeSpecParser (new StringReader (typeName));
        return parser.Parse ();
    }
}