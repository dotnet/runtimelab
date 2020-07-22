﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace System.Text.Json.SourceGeneration
{
    /// <summary>
    /// Base JsonSerializerSourceGenerator. This class will invoke CodeGenerator within Execute
    /// to generate wanted output code for JsonSerializers.
    /// </summary>
    [Generator]
    public class JsonSerializerSourceGenerator : ISourceGenerator
    {
        public Dictionary<string, Type> foundTypes = new Dictionary<string, Type>();

        public void Execute(SourceGeneratorContext context)
        {
            JsonSerializableSyntaxReceiver receiver = (JsonSerializableSyntaxReceiver)context.SyntaxReceiver;

            MetadataLoadContext metadataLoadContext = new MetadataLoadContext(context.Compilation);

            INamedTypeSymbol namedTypeSymbol;
            ITypeSymbol typeSymbol;
            IdentifierNameSyntax identifierNameNode;
            SemanticModel semanticModel;
            Type convertedType;
            TypeDeclarationSyntax typeDeclarationNode;

            // Map type name to type objects.
            foreach (KeyValuePair<string, TypeDeclarationSyntax> entry in receiver.InternalClassTypeDict)
            {
                typeDeclarationNode = entry.Value;
                semanticModel = context.Compilation.GetSemanticModel(typeDeclarationNode.SyntaxTree);
                namedTypeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(typeDeclarationNode);
                convertedType = new TypeWrapper(namedTypeSymbol, metadataLoadContext);
                foundTypes[entry.Key] = convertedType;
            }

            foreach (KeyValuePair<string, IdentifierNameSyntax> entry in receiver.ExternalClassTypeDict)
            {
                identifierNameNode = entry.Value;
                semanticModel = context.Compilation.GetSemanticModel(identifierNameNode.SyntaxTree);
                typeSymbol = context.Compilation.GetSemanticModel(identifierNameNode.SyntaxTree).GetTypeInfo(identifierNameNode).ConvertedType;
                convertedType = new TypeWrapper(typeSymbol, metadataLoadContext);
                foundTypes[entry.Key] = convertedType;
            }

            // Create sources for all found types.
            StringBuilder member = new StringBuilder();
            string foundMethods, foundFields, foundProperties, foundCtorParams, foundCtors;

            foreach (KeyValuePair<string, Type> entry in foundTypes)
            {
                foreach(MethodInfo method in entry.Value.GetMethods())
                {
                    member.Append(@$"""{method.Name}"", "); 
                }
                foundMethods = member.ToString();
                member.Clear();

                foreach(FieldInfo field in entry.Value.GetFields())
                {
                    member.Append(@$"{{""{field.Name}"", ""{field.FieldType.Name}""}}, "); 
                }
                foundFields = member.ToString();
                member.Clear();

                foreach(PropertyInfo property in entry.Value.GetProperties())
                {
                    member.Append(@$"{{""{property.Name}"", ""{property.PropertyType.Name}""}}, "); 
                }
                foundProperties = member.ToString();
                member.Clear();

                foreach(ConstructorInfo ctor in entry.Value.GetConstructors())
                {
                    foreach(ParameterInfo param in ctor.GetParameters())
                    {
                        member.Append(@$"{{""{param.Name}"", ""{param.ParameterType.Name}""}}, "); 
                    }
                }
                foundCtorParams = member.ToString();
                member.Clear();

                foreach(ConstructorInfo ctor in entry.Value.GetConstructors())
                {
                    member.Append($@"""{ctor.Name}"", ");
                }
                foundCtors = member.ToString();
                member.Clear();

                context.AddSource($"{entry.Key}ClassInfo", SourceText.From($@"
using System;
using System.Collections.Generic;

namespace HelloWorldGenerated
{{
    public class {entry.Key}ClassInfo
    {{
        public {entry.Key}ClassInfo() {{ }}

        private List<string> ClassCtors = new List<string>()
        {{ {foundCtors} }};
        private Dictionary<string, string> ClassCtorParams = new Dictionary<string, string>()
        {{ {foundCtorParams} }};
        private List<string> ClassMethods = new List<string>()
        {{ {foundMethods} }};
        private Dictionary<string, string> ClassFields = new Dictionary<string, string>()
        {{ {foundFields} }};
        private Dictionary<string, string> ClassProperties = new Dictionary<string, string>()
        {{ {foundProperties} }};

        public string GetClassName()
        {{
            return ""{entry.Key}ClassInfo"";
        }}

        public List<string> Ctors
        {{ get {{ return ClassCtors; }} }}

        public Dictionary<string, string> CtorParams
        {{ get {{ return ClassCtorParams; }} }}

        public List<string> Methods 
        {{ get {{ return ClassMethods; }} }}

        public Dictionary<string, string> Fields
        {{ get {{ return ClassFields; }} }}

        public Dictionary<string, string> Properties
        {{ get {{ return ClassProperties; }} }}
    }}
}}
", Encoding.UTF8));
            }
        }

        public void Initialize(InitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new JsonSerializableSyntaxReceiver());
        }

        public class JsonSerializableSyntaxReceiver : ISyntaxReceiver
        {
            public Dictionary<string, IdentifierNameSyntax> ExternalClassTypeDict = new Dictionary<string, IdentifierNameSyntax>();
            public Dictionary<string, TypeDeclarationSyntax> InternalClassTypeDict = new Dictionary<string, TypeDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Look for classes or structs for JsonSerializable Attribute.
                if (syntaxNode is ClassDeclarationSyntax || syntaxNode is StructDeclarationSyntax)
                {
                    // Find JsonSerializable Attributes.
                    IEnumerable<AttributeSyntax>? serializableAttributes = null;
                    AttributeListSyntax attributeList = ((TypeDeclarationSyntax)syntaxNode).AttributeLists.SingleOrDefault();
                    if (attributeList != null)
                    {
                        serializableAttributes = attributeList.Attributes.Where(node => (node is AttributeSyntax attr && attr.Name.ToString() == "JsonSerializable")).Cast<AttributeSyntax>();
                    }

                    if (serializableAttributes?.Any() == true)
                    {
                        // JsonSerializableAttribute has AllowMultiple as False, should only have 1 attribute.
                        Debug.Assert(serializableAttributes.Count() == 1);
                        AttributeSyntax attributeNode = serializableAttributes.First();

                        // Check if the attribute is being passed a type.
                        if (attributeNode.DescendantNodes().Where(node => node is TypeOfExpressionSyntax).Any())
                        {
                            // Get JsonSerializable attribute arguments.
                            AttributeArgumentSyntax attributeArgumentNode = (AttributeArgumentSyntax)attributeNode.DescendantNodes().Where(node => node is AttributeArgumentSyntax).SingleOrDefault();
                            // Get external class token from arguments.
                            IdentifierNameSyntax externalTypeNode = (IdentifierNameSyntax)attributeArgumentNode?.DescendantNodes().Where(node => node is IdentifierNameSyntax).SingleOrDefault();
                            ExternalClassTypeDict[((TypeDeclarationSyntax)syntaxNode).Identifier.Text] = externalTypeNode;
                        }
                        else
                        {
                            InternalClassTypeDict[((TypeDeclarationSyntax)syntaxNode).Identifier.Text] = ((TypeDeclarationSyntax)syntaxNode);
                        }
                    }
                }
            }
        }
    }
}
