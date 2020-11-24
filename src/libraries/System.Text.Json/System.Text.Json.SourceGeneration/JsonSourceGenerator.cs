// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
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
    public sealed class JsonSourceGenerator : ISourceGenerator
    {
        public Dictionary<string, Type>? SerializableTypes { get; private set; }

        public void Execute(GeneratorExecutionContext executionContext)
        {
#if LAUNCH_DEBUGGER_ON_EXECUTE
            Debugger.Launch();
#endif
            JsonSerializableSyntaxReceiver receiver = (JsonSerializableSyntaxReceiver)executionContext.SyntaxReceiver;
            MetadataLoadContext metadataLoadContext = new(executionContext.Compilation);

            // Discover serializable types indicated by JsonSerializableAttribute.
            foreach (CompilationUnitSyntax compilationUnit in receiver.CompilationUnits)
            {
                SemanticModel compilationSemanticModel = executionContext.Compilation.GetSemanticModel(compilationUnit.SyntaxTree);

                foreach (AttributeListSyntax attributeListSyntax in compilationUnit.AttributeLists)
                {
                    AttributeSyntax attributeSyntax = attributeListSyntax.Attributes.Single();
                    IMethodSymbol attributeSymbol = compilationSemanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;

                    if (attributeSymbol?.ToString().StartsWith("System.Text.Json.Serialization.JsonSerializableAttribute") == true)
                    {
                        // Get JsonSerializableAttribute arguments.
                        AttributeArgumentSyntax attributeArgumentNode = (AttributeArgumentSyntax)attributeSyntax.DescendantNodes().Where(node => node is AttributeArgumentSyntax).SingleOrDefault();

                        // There should be one `Type` parameter in the constructor of the attribute.
                        TypeOfExpressionSyntax typeNode = (TypeOfExpressionSyntax)attributeArgumentNode.ChildNodes().Single();

                        ExpressionSyntax typeNameSyntax = (ExpressionSyntax)typeNode.ChildNodes().Single();

                        ITypeSymbol typeSymbol = (ITypeSymbol)compilationSemanticModel.GetTypeInfo(typeNameSyntax).ConvertedType;

                        Type type = new TypeWrapper(typeSymbol, metadataLoadContext);
                        (SerializableTypes ??= new Dictionary<string, Type>())[type.FullName] = type;
                    }
                }
            }

            if (SerializableTypes == null)
            {
                return;
            }

            Debug.Assert(SerializableTypes.Count >= 1);

            JsonSourceGeneratorHelper helper = new(executionContext, metadataLoadContext);
            helper.GenerateSerializationMetadata(SerializableTypes);
        }

        public void Initialize(GeneratorInitializationContext executionContext)
        {
            executionContext.RegisterForSyntaxNotifications(() => new JsonSerializableSyntaxReceiver());
        }
    }
}
