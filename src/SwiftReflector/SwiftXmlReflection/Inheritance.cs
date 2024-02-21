// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.ExceptionTools;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SwiftRuntimeLibrary;

namespace SwiftReflector.SwiftXmlReflection
{
    public class Inheritance : IXElementConvertible
    {
        public Inheritance(string inheritedTypeName, InheritanceKind inheritanceKind)
        {
            this.InheritanceKind = inheritanceKind;
            InheritedTypeName = inheritedTypeName;
        }

        public InheritanceKind InheritanceKind { get; private set; }

        string inheritedTypeName;
        public string InheritedTypeName
        {
            get { return inheritedTypeName; }
            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
                inheritedTypeName = value;
                try
                {
                    InheritedTypeSpec = TypeSpecParser.Parse(inheritedTypeName);
                }
                catch (RuntimeException ex)
                {
                    throw ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 7, $"Unable to parse type name '{inheritedTypeName}': {ex.Message}");
                }
            }
        }
        public TypeSpec InheritedTypeSpec { get; private set; }

        public static Inheritance FromXElement(TypeAliasFolder folder, XElement elem)
        {
            string typeName = (string)elem.Attribute("type");
            string inheritanceKindStr = (string)elem.Attribute("inheritanceKind");
            InheritanceKind kind = ToInheritanceKind(inheritanceKindStr);
            var inheritance = new Inheritance(typeName, kind);
            inheritance.InheritedTypeSpec = folder.FoldAlias(null, inheritance.InheritedTypeSpec);
            return inheritance;
        }

        public XElement ToXElement()
        {
            return new XElement("inherit", new XAttribute("type", InheritedTypeName),
                new XAttribute("inheritanceKind", ToString(InheritanceKind)));
        }

        static string ToString(InheritanceKind kind)
        {
            switch (kind)
            {
                case InheritanceKind.Class:
                    return "class";
                case InheritanceKind.Protocol:
                    return "protocol";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        static InheritanceKind ToInheritanceKind(string kindStr)
        {
            ArgumentNullException.ThrowIfNull(kindStr, nameof(kindStr));
            switch (kindStr)
            {
                case "protocol":
                    return InheritanceKind.Protocol;
                case "class":
                    return InheritanceKind.Class;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kindStr), String.Format("Expected either protocol or class, but got {0}.",
                        kindStr));
            }
        }
    }
}

