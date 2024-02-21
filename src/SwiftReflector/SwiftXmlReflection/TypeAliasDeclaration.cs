// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Xml.Linq;
using SyntaxDynamo;
using SwiftReflector.ExceptionTools;

namespace SwiftReflector.SwiftXmlReflection
{
    public class TypeAliasDeclaration
    {
        public TypeAliasDeclaration()
        {
        }

        public Accessibility Access { get; private set; }

        string typeName;
        public string TypeName
        {
            get { return typeName; }
            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
                typeName = value;
                if (typeName.IndexOf(':') >= 0)
                    throw ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 12, $"typealias {value} has a generic constraint which is not supported");
                try
                {
                    typeSpec = TypeSpecParser.Parse(typeName);
                }
                catch (RuntimeException ex)
                {
                    throw ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 11, $"Unable to parse typealias name '{value}': {ex.Message}");
                }
            }
        }

        TypeSpec typeSpec;
        public TypeSpec TypeSpec
        {
            get { return typeSpec; }
            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
                typeSpec = value;
                typeName = value.ToString();
            }
        }

        string targetTypeName;
        public string TargetTypeName
        {
            get { return targetTypeName; }
            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
                targetTypeName = value;
                try
                {
                    targetTypeSpec = TypeSpecParser.Parse(targetTypeName);
                }
                catch (RuntimeException ex)
                {
                    throw ErrorHelper.CreateError(ReflectorError.kReflectionErrorBase + 11, $"Unable to parse typealias target name '{value}': {ex.Message}");
                }
            }
        }

        TypeSpec targetTypeSpec;
        public TypeSpec TargetTypeSpec
        {
            get { return targetTypeSpec; }
            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
                targetTypeSpec = value;
                targetTypeName = value.ToString();
            }
        }

        public String ModuleName
        {
            get
            {
                var spec = TypeSpec as NamedTypeSpec;
                return spec?.Module;
            }
        }

        public XElement ToXElement()
        {
            return new XElement("typealias", new XAttribute("name", TypeName),
                new XAttribute("type", TargetTypeName));
        }

        public static TypeAliasDeclaration FromXElement(string moduleName, XElement element)
        {
            var aliasName = element.Attribute("name").Value;
            if (!aliasName.Contains("."))
            {
                ArgumentNullException.ThrowIfNull(moduleName, nameof(moduleName));
                aliasName = $"{moduleName}.{aliasName}";
            }
            return new TypeAliasDeclaration()
            {
                Access = TypeDeclaration.AccessibilityFromString((string)element.Attribute("accessibility")),
                TypeName = aliasName,
                TargetTypeName = element.Attribute("type").Value
            };
        }
    }
}
