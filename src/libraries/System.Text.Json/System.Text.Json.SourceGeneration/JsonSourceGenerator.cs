// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

namespace System.Text.Json.SourceGeneration
{
    /// <summary>
    /// Base JsonSerializerSourceGenerator. This class will invoke CodeGenerator within Execute
    /// to generate wanted output code for JsonSerializers.
    /// </summary>
    [Generator]
    public class JsonSerializerSourceGenerator : ISourceGenerator
    {
        public List<Type> foundTypes = new List<Type>();

        public void Execute(SourceGeneratorContext context)
        {
            // Temporary boilerplate code.
            StringBuilder sourceBuilder = new StringBuilder(@"
using System;
namespace HelloWorldGenerated
{
    public static class HelloWorld
    {
        public static string SayHello() 
        {
            return ""Hello"";
");

            if (!(context.SyntaxReceiver is JsonSerializableSyntaxReceiver receiver))
                return;

            MetadataLoadContext mlc = new MetadataLoadContext(context.Compilation);

            SemanticModel semanticModel;
            INamedTypeSymbol namedTypeSymbol;
            ITypeSymbol typeSymbol;
            foreach (TypeDeclarationSyntax tds in receiver.InternalClassTypeNode)
            {
                semanticModel = context.Compilation.GetSemanticModel(tds.SyntaxTree);
                namedTypeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(tds);
                foundTypes.Add(new TypeWrapper(namedTypeSymbol, mlc));
            }

            foreach (IdentifierNameSyntax ins in receiver.ExternalClassTypeNode)
            {
                semanticModel = context.Compilation.GetSemanticModel(ins.SyntaxTree);
                typeSymbol = context.Compilation.GetSemanticModel(ins.SyntaxTree).GetTypeInfo(ins).ConvertedType;

                //sourceBuilder.Append($@"Console.WriteLine(@"" - PRINTING TYPESYMBOL EXTERNAL");
                //foreach (char c in tempGenerateType.FullName)
                //{
                //    if (c != '"' && c != '\'')
                //        sourceBuilder.Append($@"{c}");
                //}

                //sourceBuilder.AppendLine($@""");");
                foundTypes.Add(new TypeWrapper(typeSymbol, mlc));
            }


            sourceBuilder.Append(@"
        }
    }
}");

            context.AddSource("helloWorldGenerated", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }

        public void Initialize(InitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new JsonSerializableSyntaxReceiver());
        }

        public class JsonSerializableSyntaxReceiver : ISyntaxReceiver
        {
            public List<IdentifierNameSyntax> ExternalClassTypeNode = new List<IdentifierNameSyntax>();
            public List<TypeDeclarationSyntax> InternalClassTypeNode = new List<TypeDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Look for classes or structs for JsonSerializable Attribute.
                if (syntaxNode is ClassDeclarationSyntax || syntaxNode is StructDeclarationSyntax)
                {
                    // Find JsonSerializable Attributes.
                    IEnumerable<AttributeSyntax> serializableAttributes = syntaxNode.DescendantNodes().Where(node => (node is AttributeSyntax && node.ToString() == "JsonSerializable")).Cast<AttributeSyntax>();
                    if (serializableAttributes.Count() > 0)
                    {
                        foreach (AttributeSyntax attributeNode in serializableAttributes)
                        {
                            // Check if the attribute is being passed a type.
                            if (attributeNode.DescendantNodes().Where(node => node is TypeOfExpressionSyntax).Count() > 0)
                            {
                                // Get JsonSerializable attribute arguments.
                                AttributeArgumentSyntax attributeArgumentNode = (AttributeArgumentSyntax)attributeNode.DescendantNodes().Where(node => node is AttributeArgumentSyntax).Single();
                                // Get external class token from arguments.
                                IdentifierNameSyntax externalTypeNode = (IdentifierNameSyntax)attributeArgumentNode.DescendantNodes().Where(node => node is IdentifierNameSyntax).Single();
                                ExternalClassTypeNode.Add(externalTypeNode);
                            }
                            else
                            {
                                InternalClassTypeNode.Add((TypeDeclarationSyntax)syntaxNode);
                            }
                        }
                    }
                }
                //if (syntaxNode is AttributeSyntax attribute && attribute.Name.ToString() == "JsonSerializable")
                //{
                //    if (attribute.Parent.Parent is ClassDeclarationSyntax cds)
                //    {
                //        if (cds.ToString().Contains("typeof"))
                //        {
                //            IdentifierNameSyntax ins = (IdentifierNameSyntax)cds.DescendantNodes().Where(node => node is IdentifierNameSyntax).ToList()[1];
                //            ExternalClassTypeNode.Add(ins);
                //        }
                //        else
                //        {
                //            InternalClassTypeNode.Add(cds);
                //        }
                //    }
                //    if (attribute.Parent.Parent is StructDeclarationSyntax sds)
                //    {
                //        if (sds.ToString().Contains("typeof"))
                //        {
                //            IdentifierNameSyntax ins = (IdentifierNameSyntax)sds.DescendantNodes().Where(node => node is IdentifierNameSyntax).ToList()[1];
                //            ExternalClassTypeNode.Add(ins);
                //        }
                //        else
                //        {
                //            InternalClassTypeNode.Add(sds);
                //        }
                //    }
                //}
            }
        }
    }
}
