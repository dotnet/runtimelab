﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace System.Text.Json.CodeGenerator
{
    // Base JsonSerializerSourceGenerator. This class will invoke CodeGenerator within Execute
    // to generate wanted output code for JsonSerializers.
    [Generator]
    public class JsonSerializerSourceGenerator : ISourceGenerator
    {
        public void Execute(SourceGeneratorContext context)
        {
            // Foreach type found, call code generator.
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

            List<Type> foundTypes = new List<Type>();
            MetadataLoadContext mlc = new MetadataLoadContext(context.Compilation);

            SemanticModel semanticModel;
            INamedTypeSymbol namedTypeSymbol;
            ITypeSymbol typeSymbol;
            foreach (TypeDeclarationSyntax tds in receiver.InternalClassTypeNode)
            {
                // Possibly could be optimized.
                semanticModel = context.Compilation.GetSemanticModel(tds.SyntaxTree);
                namedTypeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(tds);
                foundTypes.Add(new TypeWrapper(namedTypeSymbol, mlc));
            }
            
            foreach (IdentifierNameSyntax ins in receiver.ExternalClassTypeNode)
            {
                // Possibly could be optimized.
                semanticModel = context.Compilation.GetSemanticModel(ins.SyntaxTree);
                typeSymbol = context.Compilation.GetSemanticModel(ins.SyntaxTree).GetTypeInfo(ins).ConvertedType;

                Type tempGenerateType = new TypeWrapper(typeSymbol, mlc);

                sourceBuilder.Append($@"Console.WriteLine(@"" - PRINTING TYPESYMBOL EXTERNAL");
                foreach (char c in tempGenerateType.FullName)
                {
                    if (c != '"' && c != '\'')
                        sourceBuilder.Append($@"{c}");
                }

                sourceBuilder.AppendLine($@""");");
                foundTypes.Add(tempGenerateType);
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

        // Temporary function for now that reads all types. Should search types with attribute JsonSerializable.
        internal class JsonSerializableSyntaxReceiver : ISyntaxReceiver
        {
            public List<IdentifierNameSyntax> ExternalClassTypeNode = new List<IdentifierNameSyntax>();
            public List<TypeDeclarationSyntax> InternalClassTypeNode = new List<TypeDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Get all the type decl in all syntax tree.
                if (syntaxNode is AttributeSyntax attribute && attribute.Name.ToString() == "JsonSerializable")
                {
                    if (attribute.Parent.Parent is ClassDeclarationSyntax cds)
                    {
                        if (cds.ToString().Contains("typeof"))
                        {
                            IdentifierNameSyntax ins = (IdentifierNameSyntax)cds.DescendantNodes().Where(node => node is IdentifierNameSyntax).ToList()[1];
                            ExternalClassTypeNode.Add(ins);
                        }
                        else
                        {
                            InternalClassTypeNode.Add(cds);
                        }
                    }
                    if (attribute.Parent.Parent is StructDeclarationSyntax sds)
                    {
                        if (sds.ToString().Contains("typeof"))
                        {
                            IdentifierNameSyntax ins = (IdentifierNameSyntax)sds.DescendantNodes().Where(node => node is IdentifierNameSyntax).ToList()[1];
                            ExternalClassTypeNode.Add(ins);
                        }
                        else
                        {
                            InternalClassTypeNode.Add(sds);
                        }
                    }
                }
            }
        }
    }
}
