﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        private const string JsonSerializableAttributeSource = @"using System;

namespace System.Text.Json.SourceGeneration
{
    /// <summary>
    /// Instructs the System.Text.Json source generator to generate serialization metadata for a specified type at compile time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class JsonSerializableAttribute : Attribute
    {
        /// <summary>
        /// Indicates whether the specified type might be the runtime type of an object instance which was declared as
        /// a different type (polymorphic serialization).
        /// </summary>
        public bool CanBeDynamic { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref=""JsonSerializableAttribute""/> with the specified type.
        /// </summary>
        /// <param name=""type"">The Type of the property.</param>
        public JsonSerializableAttribute(Type type) { }
    }
}";

        public Dictionary<string, (Type, bool)>? SerializableTypes { get; private set; }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new JsonSerializableSyntaxReceiver());
            context.RegisterForPostInitialization(PostInitialize);
        }

        public void PostInitialize(GeneratorPostInitializationContext context)
        {
            context.AddSource("JsonSerializableAttribute.g.cs", SourceText.From(JsonSerializableAttributeSource, Encoding.UTF8));
        }

        public void Execute(GeneratorExecutionContext executionContext)
        {
#if LAUNCH_DEBUGGER_ON_EXECUTE
            Debugger.Launch();
            try
            {
#endif
            JsonSerializableSyntaxReceiver receiver = (JsonSerializableSyntaxReceiver)executionContext.SyntaxReceiver;
            MetadataLoadContext metadataLoadContext = new(executionContext.Compilation);

            TypeExtensions.NullableOfTType = metadataLoadContext.Resolve(typeof(Nullable<>));

            // Discover serializable types indicated by JsonSerializableAttribute.
            foreach (CompilationUnitSyntax compilationUnit in receiver.CompilationUnits)
            {
                SemanticModel compilationSemanticModel = executionContext.Compilation.GetSemanticModel(compilationUnit.SyntaxTree);

                foreach (AttributeListSyntax attributeListSyntax in compilationUnit.AttributeLists)
                {
                    AttributeSyntax attributeSyntax = attributeListSyntax.Attributes.Single();
                    IMethodSymbol attributeSymbol = compilationSemanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;

                    if (attributeSymbol?.ToString().StartsWith("System.Text.Json.SourceGeneration.JsonSerializableAttribute") == true)
                    {
                        // Get JsonSerializableAttribute arguments.
                        IEnumerable<SyntaxNode> attributeArguments = attributeSyntax.DescendantNodes().Where(node => node is AttributeArgumentSyntax);

                        int argumentCount = attributeArguments.Count();

                        // Compiler shouldn't allow invalid signature for the JsonSerializable attribute.
                        Debug.Assert(argumentCount == 1 || argumentCount == 2);

                        // Obtain the one `Type` argument that must be present in the constructor of the attribute.
                        AttributeArgumentSyntax typeArgumentNode = (AttributeArgumentSyntax)attributeArguments.First();
                        TypeOfExpressionSyntax typeNode = (TypeOfExpressionSyntax)typeArgumentNode.ChildNodes().Single();
                        ExpressionSyntax typeNameSyntax = (ExpressionSyntax)typeNode.ChildNodes().Single();
                        ITypeSymbol typeSymbol = (ITypeSymbol)compilationSemanticModel.GetTypeInfo(typeNameSyntax).ConvertedType;

                        bool canBeDynamic = false;
                        // Obtain the optional CanBeDynamic boolean property on the attribute, if present.
                        if (argumentCount == 2)
                        {
                            AttributeArgumentSyntax boolArgumentNode = (AttributeArgumentSyntax)attributeArguments.ElementAt(1);
                            IEnumerable<SyntaxNode> childNodes = boolArgumentNode.ChildNodes();
                            SyntaxNode booleanValueNode = childNodes.ElementAt(1);
                            canBeDynamic = boolArgumentNode.Expression.Kind() == SyntaxKind.TrueLiteralExpression;
                        }

                        Type type = new TypeWrapper(typeSymbol, metadataLoadContext);

                        // TODO: define behavior for when type.Namespace == "<global namespace>".

                        // TODO: make sure a type is only specified once here, to avoid errors with JsonSerializable.CanBeDynamic.
                        (SerializableTypes ??= new Dictionary<string, (Type, bool)>())[type.FullName] = (type, canBeDynamic);
                    }
                }
            }

            JsonSourceGeneratorHelper helper = new(executionContext, metadataLoadContext, SerializableTypes);

            if (SerializableTypes == null)
            {
                // Return empty JsonContext object to avoid compilation errors due to "using {generationNamespace}" without having any serializable types.
                helper.AddBaseJsonContextImplementation();
                return;
            }

            Debug.Assert(SerializableTypes.Count >= 1);

            helper.GenerateSerializationMetadata();
#if LAUNCH_DEBUGGER_ON_EXECUTE
            }
            catch (Exception e)
            {
                Debugger.Break();
                throw e;
            }
#endif
        }
    }
}
