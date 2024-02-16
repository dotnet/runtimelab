// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using SyntaxDynamo;
using System.Linq;
using System.Text;

namespace SwiftReflector.SwiftInterfaceReflector
{
    public class ObjCSelectorFactory
    {
        XElement funcElement;

        static string[] prepositions = new string[] {
            "above", "after", "along", "alongside", "as", "at",
            "before", "below", "by", "following", "for", "from",
            "given", "in", "including", "inside", "into", "matching",
            "of", "on", "passing", "preceding", "since", "to",
            "until", "using", "via", "when", "with", "within"
        };

        static string[] pluralSuffixes = new string[] {
            "s", "es", "ies"
        };

        public ObjCSelectorFactory(XElement funcElement)
        {
            this.funcElement = Exceptions.ThrowOnNull(funcElement, nameof(funcElement));
        }

        public string Generate()
        {
            // it's in an @objc attribute
            var providedSelector = ProvidedObjCSelector();
            if (!String.IsNullOrEmpty(providedSelector))
                return providedSelector;

            if (IsDeInit())
                return "dealloc";

            var argNames = GetArgumentNames();

            var baseName = IsInit() ? "init" :
                (IsProperty() ? PropertyName() : FunctionName());

            if (IsSetter())
            {
                return SetterSelector(baseName);
            }

            if (IsGetter())
            {
                return GetterSelector(baseName);
            }

            if (IsSubscriptGetter())
            {
                return "objectAtIndexedSubscript:";
            }

            if (IsSubscriptSetter())
            {
                return "setObject:atIndexedSubscript:";
            }

            if (IsInit() && argNames.Count == 1 && IsObjCZeroParameterWithLongSelector())
            {
                var firstName = argNames[0];
                var sb = new StringBuilder();
                sb.Append("init");
                if (!IsPreposition(FirstWord(firstName)))
                    sb.Append("With");
                sb.Append(CapitalizeFirstLetter(firstName));
                return sb.ToString();
            }

            // at this point, the Apple code needs to know if there are
            // foreign async or foreign error conventions.
            // The definition of this is opaque enough that it's not clear
            // what the conditions are for this predicate, so we're going to
            // pretend it doesn't exist for now.

            var asyncConvention = false;
            var errorConvention = false;

            var numSelectorPieces = argNames.Count + (asyncConvention ? 1 : 0)
                + (errorConvention ? 1 : 0);

            if (numSelectorPieces == 0)
                return baseName;

            if (numSelectorPieces == 1 && argNames.Count == 1 && argNames[0] == "_")
                return baseName;

            var argIndex = 0;
            var selector = new StringBuilder();
            for (var piece = 0; piece != numSelectorPieces; ++piece)
            {
                if (piece > 0)
                {
                    if (asyncConvention)
                    {
                        selector.Append("completionHandler:");
                        continue;
                    }

                    // If we have an error convention that inserts an error parameter
                    // here, add "error".
                    if (errorConvention)
                    {
                        selector.Append("error:");
                        continue;
                    }

                    // Selector pieces beyond the first are simple.
                    selector.Append(argNames[argIndex++]).Append('.');
                    continue;
                }
                var firstPiece = baseName;
                var scratch = new StringBuilder();
                scratch.Append(firstPiece);
                if (asyncConvention)
                {
                    // The completion handler is first; append "WithCompletionHandler".
                    scratch.Append("WithCompletionHandler");
                    firstPiece = scratch.ToString();
                }
                else if (errorConvention)
                {
                    scratch.Append("AndReturnError");
                    firstPiece = scratch.ToString();
                }
                else if (argNames[argIndex] != "_" && argNames[argIndex] != "")
                {
                    // If the first argument name doesn't start with a preposition, and the
                    // method name doesn't end with a preposition, add "with".
                    var firstName = argNames[argIndex++];
                    if (!IsPreposition(FirstWord(firstName)) &&
                        !IsPreposition(LastWord(firstPiece)))
                    {
                        scratch.Append("With");
                    }
                    scratch.Append(CapitalizeFirstLetter(firstName));
                    firstPiece = scratch.ToString();
                }
                else
                {
                    ++argIndex;
                }

                selector.Append(firstPiece);
                if (argNames.Count > 0)
                    selector.Append(':');
            }
            return selector.ToString();
        }

        List<string> GetArgumentNames()
        {
            var parameterList = funcElement.Descendants(SwiftInterfaceReflector.kParameterList).Last();
            var parameters = parameterList.Descendants(SwiftInterfaceReflector.kParameter).Select(
                p =>
                {
                    var publicName = p.Attribute(SwiftInterfaceReflector.kPublicName)?.Value;
                    var privateName = p.Attribute(SwiftInterfaceReflector.kPrivateName).Value;
                    return String.IsNullOrEmpty(publicName) ? privateName : publicName;
                });
            return parameters.ToList();
        }

        string ProvidedObjCSelector()
        {
            //< attributes >
            //   < attribute name = "objc" />

            //  </ attributes >
            // find the first objc attribute

            var elem = funcElement.Descendants(SwiftInterfaceReflector.kAttribute)
                .FirstOrDefault(el => el.Attribute(SwiftInterfaceReflector.kName)?.Value == SwiftInterfaceReflector.kObjC);
            if (elem == null)
                return null;
            var parameters = elem.Descendants(SwiftInterfaceReflector.kAttributeParameter);
            if (parameters == null)
                return null;
            var sb = new StringBuilder();
            foreach (var piece in parameters)
            {
                sb.Append(piece.Attribute(SwiftInterfaceReflector.kValue)?.Value ?? "");
            }
            return sb.ToString();
        }

        string GetterSelector(string baseName)
        {
            return baseName;
        }

        string SetterSelector(string baseName)
        {
            return $"set{CapitalizeFirstLetter(baseName)}:";
        }

        string CapitalizeFirstLetter(string baseName)
        {
            if (Char.IsLower(baseName[0]))
            {
                return Char.ToUpper(baseName[0]) + baseName.Substring(1);
            }
            return baseName;
        }

        bool IsObjCZeroParameterWithLongSelector()
        {
            var parameterList = funcElement.Descendants(SwiftInterfaceReflector.kParameterList).Last();
            var onlyParameter = parameterList.Descendants(SwiftInterfaceReflector.kParameter).FirstOrDefault();
            if (onlyParameter == null)
                return false;
            return onlyParameter.Attribute(SwiftInterfaceReflector.kType)?.Value == "()";
        }

        bool IsDeInit()
        {
            return FunctionName() == SwiftInterfaceReflector.kDotDtor;
        }

        bool IsInit()
        {
            return FunctionName() == SwiftInterfaceReflector.kDotCtor;
        }

        bool IsProperty()
        {
            return funcElement.Attribute(SwiftInterfaceReflector.kIsProperty)?.Value == "true";
        }

        string PropertyName()
        {
            return FunctionName().Substring("get_".Length);
        }

        bool IsGetter()
        {
            var funcName = FunctionName();
            return IsProperty() && funcName.StartsWith("get_", StringComparison.Ordinal) &&
                funcName != SwiftInterfaceReflector.kGetSubscript;
        }

        bool IsSetter()
        {
            var funcName = FunctionName();
            return IsProperty() && funcName.StartsWith("set_", StringComparison.Ordinal) &&
                funcName != SwiftInterfaceReflector.kSetSubscript;
        }

        bool IsSubscriptGetter()
        {
            return FunctionName() == SwiftInterfaceReflector.kGetSubscript;
        }

        bool IsSubscriptSetter()
        {
            return FunctionName() == SwiftInterfaceReflector.kSetSubscript;
        }

        static bool IsPreposition(string s)
        {
            return prepositions.Contains(s);
        }

        static bool IsPluralSuffix(string s)
        {
            return pluralSuffixes.Contains(s);
        }

        string FunctionName()
        {
            return funcElement.Attribute(SwiftInterfaceReflector.kName)?.Value;
        }

        IEnumerable<string> Words(string src)
        {
            if (String.IsNullOrEmpty(src))
                yield break;
            var length = src.Length;
            var start = 0;

            while (start < length)
            {
                if (src[start] == '_')
                {
                    start++;
                    yield return "_";
                    continue;
                }
                var i = start;
                while (i < length && Char.IsUpper(src[i]))
                    i++;
                if (i - start > 1)
                {
                    var endOfNext = i;
                    while (endOfNext < length && Char.IsLower(src[endOfNext]))
                        endOfNext++;
                    if (i == length || IsPluralSuffix(src.Substring(i, endOfNext - i))
                        && src.Substring(i, endOfNext - i).EndsWith("Is", StringComparison.Ordinal))
                    {
                        var word = src.Substring(start, endOfNext - start);
                        start = endOfNext;
                        yield return word;
                        continue;
                    }
                    else
                    {
                        if (Char.IsLower(src[i]))
                            i--;
                        var word = src.Substring(start, i - start);
                        start = i;
                        yield return word;
                        continue;
                    }
                }

                while (i < length && !Char.IsUpper(src[i]) && src[i] != '_')
                    i++;
                var thisword = src.Substring(start, i - start);
                start = i;
                yield return thisword;
            }
            yield break;
        }

        string FirstWord(string src)
        {
            return Words(src).FirstOrDefault() ?? "";
        }

        string LastWord(string src)
        {
            return Words(src).LastOrDefault() ?? "";
        }
    }
}
