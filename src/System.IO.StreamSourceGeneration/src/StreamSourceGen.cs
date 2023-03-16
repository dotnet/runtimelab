// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace System.IO.StreamSourceGeneration
{
    [Generator]
    public partial class StreamSourceGen : IIncrementalGenerator
    {
        internal const string StreamBoilerplateAttributeFullName = "System.IO.GenerateStreamBoilerplateAttribute";
        internal const string StreamFullName = "System.IO.Stream";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource("TaskToApm.g.cs", StreamBoilerplateConstants.TaskToApm);
            });

            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    StreamBoilerplateAttributeFullName,
                    predicate: (node, _) => node is ClassDeclarationSyntax c && c.Modifiers.Any(SyntaxKind.PartialKeyword),
                    transform: (context, _) => (ClassDeclarationSyntax)context.TargetNode);

            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses =
                context.CompilationProvider.Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

#if LAUNCH_DEBUGGER
            if (!Diagnostics.Debugger.IsAttached)
            {
                Diagnostics.Debugger.Launch();
            }
#endif
            List<StreamTypeInfo>? classesWithGenerationOptions = Parse(compilation, classes, context.CancellationToken);

            if (classesWithGenerationOptions == null)
            {
                return;
            }

            StringBuilder sb = new();

            foreach (StreamTypeInfo streamTypeInfo in classesWithGenerationOptions)
            {
                sb.Clear();
                Emit(sb, streamTypeInfo);

                INamedTypeSymbol typeSymbol = streamTypeInfo.TypeSymbol;
                string hintName = $"{typeSymbol.ContainingNamespace}.{typeSymbol.Name}.Boilerplate.g.cs";
                context.AddSource(hintName, sb.ToString());

                ReportDiagnostics(context, streamTypeInfo);
            }
        }
    }
}