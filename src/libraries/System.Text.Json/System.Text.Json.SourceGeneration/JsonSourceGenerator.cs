// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
            if (!(context.SyntaxReceiver is JsonSerializableSyntaxReceiver receiver))
                return;

            MetadataLoadContext mlc = new MetadataLoadContext(context.Compilation);

            SemanticModel semanticModel;
            INamedTypeSymbol namedTypeSymbol;
            ITypeSymbol typeSymbol;

            // Map type name to type objects.
            foreach (KeyValuePair<string, TypeDeclarationSyntax> entry in receiver.InternalClassTypeDict)
            {
                TypeDeclarationSyntax tds = entry.Value;
                semanticModel = context.Compilation.GetSemanticModel(tds.SyntaxTree);
                namedTypeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(tds);
                Type convertedType = new TypeWrapper(namedTypeSymbol, mlc);
                foundTypes[entry.Key] = convertedType;
            }
            foreach (KeyValuePair<string, IdentifierNameSyntax> entry in receiver.ExternalClassTypeDict)
            {
                IdentifierNameSyntax ins = entry.Value;
                semanticModel = context.Compilation.GetSemanticModel(ins.SyntaxTree);
                typeSymbol = context.Compilation.GetSemanticModel(ins.SyntaxTree).GetTypeInfo(ins).ConvertedType;
                Type convertedType = new TypeWrapper(typeSymbol, mlc);
                foundTypes[entry.Key] = convertedType;
            }

            // Create sources for all found types.
            foreach (KeyValuePair<string, Type> entry in foundTypes)
            {
                context.AddSource($"{entry.Key}ClassInfo", SourceText.From($@"
using System;

namespace HelloWorldGenerated
{{
    public class {entry.Key}ClassInfo
    {{
        public {entry.Key}ClassInfo() {{ }}

        public string TestMethod()
        {{
            return ""{entry.Key}"";
        }}
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
                    IEnumerable <AttributeSyntax> serializableAttributes = Enumerable.Empty<AttributeSyntax>();
                    AttributeListSyntax attributeList = ((TypeDeclarationSyntax)syntaxNode).AttributeLists.SingleOrDefault();
                    if (attributeList != null)
                    {
                        serializableAttributes = attributeList.Attributes.Where(node => (node is AttributeSyntax attr && attr.Name.ToString() == "JsonSerializable")).Cast<AttributeSyntax>();
                    }
                    if (serializableAttributes.Any())
                    {
                        foreach (AttributeSyntax attributeNode in serializableAttributes)
                        {
                            // Check if the attribute is being passed a type.
                            if (attributeNode.DescendantNodes().Where(node => node is TypeOfExpressionSyntax).Any())
                            {
                                // Get JsonSerializable attribute arguments.
                                AttributeArgumentSyntax attributeArgumentNode = (AttributeArgumentSyntax)attributeNode.DescendantNodes().Where(node => node is AttributeArgumentSyntax).SingleOrDefault();
                                // Get external class token from arguments.
                                IdentifierNameSyntax externalTypeNode = (IdentifierNameSyntax)attributeArgumentNode?.DescendantNodes().Where(node => node is IdentifierNameSyntax).SingleOrDefault();
                                if (externalTypeNode != null)
                                {
                                    ExternalClassTypeDict[((TypeDeclarationSyntax)syntaxNode).Identifier.Text] = externalTypeNode;
                                }
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
}
