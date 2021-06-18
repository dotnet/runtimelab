using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    [Generator]
    public class DllImportGenerator : IIncrementalGenerator
    {
        private const string GeneratedDllImport = nameof(GeneratedDllImport);
        private const string GeneratedDllImportAttribute = nameof(GeneratedDllImportAttribute);

        private static readonly Version MinimumSupportedFrameworkVersion = new Version(5, 0);

        private SyntaxTokenList StripTriviaFromModifiers(SyntaxTokenList tokenList)
        {
            SyntaxToken[] strippedTokens = new SyntaxToken[tokenList.Count];
            for (int i = 0; i < tokenList.Count; i++)
            {
                strippedTokens[i] = tokenList[i].WithoutTrivia();
            }
            return new SyntaxTokenList(strippedTokens);
        }

        private TypeDeclarationSyntax CreateTypeDeclarationWithoutTrivia(TypeDeclarationSyntax typeDeclaration)
        {
            return TypeDeclaration(
                typeDeclaration.Kind(),
                typeDeclaration.Identifier)
                .WithTypeParameterList(typeDeclaration.TypeParameterList)
                .WithModifiers(typeDeclaration.Modifiers);
        }

        private MemberDeclarationSyntax PrintGeneratedSource(
            MethodDeclarationSyntax userDeclaredMethod,
            DllImportStub stub)
        {
            // Create stub function
            var stubMethod = MethodDeclaration(stub.StubReturnType, userDeclaredMethod.Identifier)
                .AddAttributeLists(stub.AdditionalAttributes)
                .WithModifiers(StripTriviaFromModifiers(userDeclaredMethod.Modifiers))
                .WithParameterList(ParameterList(SeparatedList(stub.StubParameters)))
                .WithBody(stub.StubCode);

            // Stub should have at least one containing type
            Debug.Assert(stub.StubContainingTypes.Any());

            // Add stub function and DllImport declaration to the first (innermost) containing
            MemberDeclarationSyntax containingType = CreateTypeDeclarationWithoutTrivia(stub.StubContainingTypes.First())
                .AddMembers(stubMethod);

            // Add type to the remaining containing types (skipping the first which was handled above)
            foreach (var typeDecl in stub.StubContainingTypes.Skip(1))
            {
                containingType = CreateTypeDeclarationWithoutTrivia(typeDecl)
                    .WithMembers(SingletonList(containingType));
            }

            MemberDeclarationSyntax toPrint = containingType;

            // Add type to the containing namespace
            if (stub.StubTypeNamespace is not null)
            {
                toPrint = NamespaceDeclaration(IdentifierName(stub.StubTypeNamespace))
                    .AddMembers(toPrint);
            }

            return toPrint;
        }

        private static bool IsSupportedTargetFramework(Compilation compilation, out Version version)
        {
            IAssemblySymbol systemAssembly = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly;
            version = systemAssembly.Identity.Version;

            return systemAssembly.Identity.Name switch
            {
                // .NET Framework
                "mscorlib" => false,
                // .NET Standard
                "netstandard" => false,
                // .NET Core (when version < 5.0) or .NET
                "System.Runtime" or "System.Private.CoreLib" => version >= MinimumSupportedFrameworkVersion,
                _ => false,
            };
        }

        private DllImportStub.GeneratedDllImportData ProcessGeneratedDllImportAttribute(AttributeData attrData)
        {
            var stubDllImportData = new DllImportStub.GeneratedDllImportData();

            // Found the GeneratedDllImport, but it has an error so report the error.
            // This is most likely an issue with targeting an incorrect TFM.
            if (attrData.AttributeClass?.TypeKind is null or TypeKind.Error)
            {
                // [TODO] Report GeneratedDllImport has an error - corrupt metadata?
                throw new InvalidProgramException();
            }

            // Populate the DllImport data from the GeneratedDllImportAttribute attribute.
            stubDllImportData.ModuleName = attrData.ConstructorArguments[0].Value!.ToString();

            // All other data on attribute is defined as NamedArguments.
            foreach (var namedArg in attrData.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    default:
                        Debug.Fail($"An unknown member was found on {GeneratedDllImport}");
                        continue;
                    case nameof(DllImportStub.GeneratedDllImportData.BestFitMapping):
                        stubDllImportData.BestFitMapping = (bool)namedArg.Value.Value!;
                        stubDllImportData.IsUserDefined |= DllImportStub.DllImportMember.BestFitMapping;
                        break;
                    case nameof(DllImportStub.GeneratedDllImportData.CallingConvention):
                        stubDllImportData.CallingConvention = (CallingConvention)namedArg.Value.Value!;
                        stubDllImportData.IsUserDefined |= DllImportStub.DllImportMember.CallingConvention;
                        break;
                    case nameof(DllImportStub.GeneratedDllImportData.CharSet):
                        stubDllImportData.CharSet = (CharSet)namedArg.Value.Value!;
                        stubDllImportData.IsUserDefined |= DllImportStub.DllImportMember.CharSet;
                        break;
                    case nameof(DllImportStub.GeneratedDllImportData.EntryPoint):
                        stubDllImportData.EntryPoint = (string)namedArg.Value.Value!;
                        stubDllImportData.IsUserDefined |= DllImportStub.DllImportMember.EntryPoint;
                        break;
                    case nameof(DllImportStub.GeneratedDllImportData.ExactSpelling):
                        stubDllImportData.ExactSpelling = (bool)namedArg.Value.Value!;
                        stubDllImportData.IsUserDefined |= DllImportStub.DllImportMember.ExactSpelling;
                        break;
                    case nameof(DllImportStub.GeneratedDllImportData.PreserveSig):
                        stubDllImportData.PreserveSig = (bool)namedArg.Value.Value!;
                        stubDllImportData.IsUserDefined |= DllImportStub.DllImportMember.PreserveSig;
                        break;
                    case nameof(DllImportStub.GeneratedDllImportData.SetLastError):
                        stubDllImportData.SetLastError = (bool)namedArg.Value.Value!;
                        stubDllImportData.IsUserDefined |= DllImportStub.DllImportMember.SetLastError;
                        break;
                    case nameof(DllImportStub.GeneratedDllImportData.ThrowOnUnmappableChar):
                        stubDllImportData.ThrowOnUnmappableChar = (bool)namedArg.Value.Value!;
                        stubDllImportData.IsUserDefined |= DllImportStub.DllImportMember.ThrowOnUnmappableChar;
                        break;
                }
            }

            return stubDllImportData;
        }

        private sealed record SyntaxSymbolPair(MethodDeclarationSyntax Syntax, IMethodSymbol Symbol)
        {
            public bool Equals(SyntaxSymbolPair other)
            {
                return Syntax.IsEquivalentTo(other.Syntax)
                && SymbolEqualityComparer.Default.Equals(Symbol, other.Symbol);
            }

            public override int GetHashCode()
            {
                return (Syntax.ToFullString().GetHashCode(), SymbolEqualityComparer.Default.GetHashCode(Symbol)).GetHashCode();
            }
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterExecutionPipeline(
                context => 
                {
                    var methodsToGenerate = context.SyntaxProvider
                        .CreateSyntaxProvider(
                            static (node, ct) => ShouldVisitNode(node),
                            static (context, ct) => 
                                new SyntaxSymbolPair(
                                    (MethodDeclarationSyntax)context.Node,
                                    (IMethodSymbol)context.SemanticModel.GetDeclaredSymbol(context.Node, ct)!))
                        .Where(
                            static modelData => modelData.Symbol.IsStatic && modelData.Symbol.GetAttributes().Any(
                                static attribute => attribute.AttributeClass?.ToDisplayString() == TypeNames.GeneratedDllImportAttribute)
                        );

                    var compilationAndTargetFramework = context.CompilationProvider
                        .Select((compilation, ct) =>
                        {
                            bool isSupported = IsSupportedTargetFramework(compilation, out Version targetFrameworkVersion);
                            return (compilation, isSupported, targetFrameworkVersion);
                        });

                    context.RegisterSourceOutput(compilationAndTargetFramework,
                        static (context, data) =>
                        {
                            if (!data.isSupported)
                            {
                                // We don't block source generation when the TFM is unsupported.
                                // This allows a user to copy generated source and use it as a starting point
                                // for manual marshalling if desired.
                                context.ReportDiagnostic(
                                    Diagnostic.Create(
                                        GeneratorDiagnostics.TargetFrameworkNotSupported,
                                        Location.None,
                                        data.targetFrameworkVersion.ToString(2)));
                            }
                        });

                    var stubEnvironment = compilationAndTargetFramework
                        .Combine(context.AnalyzerConfigOptionsProvider)
                        .Select(
                            (data, ct) =>
                                new StubEnvironment(
                                    data.Left.compilation,
                                    data.Left.isSupported,
                                    data.Left.targetFrameworkVersion,
                                    data.Right.GlobalOptions)
                        );

                    var methodSourceAndDiagnostics = methodsToGenerate
                        .Combine(stubEnvironment)
                        .Select((data, ct) => new
                        {
                            data.Left.Syntax,
                            data.Left.Symbol,
                            Environment = data.Right
                        })
                        .Select(
                            (data, ct) => GenerateSource(data.Syntax, data.Symbol, data.Environment)
                        )
                        .WithComparer(new GeneratedSourceComparer())
                        .Collect()
                        .Select(static (generatedSources, ct) =>
                        {
                            StringBuilder source = new StringBuilder();
                            // Mark in source that the file is auto-generated.
                            source.AppendLine("// <auto-generated/>");
                            ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
                            foreach (var generated in generatedSources)
                            {
                                source.AppendLine(generated.Item1.NormalizeWhitespace().ToFullString());
                                diagnostics.AddRange(generated.Item2);
                            }
                            return (source: source.ToString(), diagnostics: diagnostics.ToImmutable());
                        });

                    context.RegisterSourceOutput(methodSourceAndDiagnostics,
                        static (context, data) =>
                        {
                            foreach (var diagnostic in data.diagnostics)
                            {
                                context.ReportDiagnostic(diagnostic);
                            }

                            context.AddSource("GeneratedDllImports.g.cs", data.source);
                        });
                }
            );
        }

        private class GeneratedSourceComparer : IEqualityComparer<(MemberDeclarationSyntax, ImmutableArray<Diagnostic>)>
        {
            public bool Equals((MemberDeclarationSyntax, ImmutableArray<Diagnostic>) x, (MemberDeclarationSyntax, ImmutableArray<Diagnostic>) y)
            {
                return x.Item1.IsEquivalentTo(y.Item1)
                && x.Item2.SequenceEqual(y.Item2);
            }

            public int GetHashCode((MemberDeclarationSyntax, ImmutableArray<Diagnostic>) obj)
            {
                return (obj.Item1.ToFullString(), obj.Item2.Aggregate(0, (hash, diagnostic) => (hash,  diagnostic).GetHashCode())).GetHashCode();
            }
        }

        private (MemberDeclarationSyntax, ImmutableArray<Diagnostic>) GenerateSource(MethodDeclarationSyntax syntax, IMethodSymbol symbol, StubEnvironment environment)
        {
            INamedTypeSymbol? lcidConversionAttrType = environment.Compilation.GetTypeByMetadataName(TypeNames.LCIDConversionAttribute);
            // Get any attributes of interest on the method
            AttributeData? generatedDllImportAttr = null;
            AttributeData? lcidConversionAttr = null;
            foreach (var attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass is not null
                    && attr.AttributeClass.ToDisplayString() == TypeNames.GeneratedDllImportAttribute)
                {
                    generatedDllImportAttr = attr;
                }
                else if (lcidConversionAttrType != null && SymbolEqualityComparer.Default.Equals(attr.AttributeClass, lcidConversionAttrType))
                {
                    lcidConversionAttr = attr;
                }
            }

            Debug.Assert(generatedDllImportAttr is not null);
            
            var generatorDiagnostics = new GeneratorDiagnostics();

            // Process the GeneratedDllImport attribute
            DllImportStub.GeneratedDllImportData stubDllImportData = this.ProcessGeneratedDllImportAttribute(generatedDllImportAttr!);
            Debug.Assert(stubDllImportData is not null);

            if (stubDllImportData!.IsUserDefined.HasFlag(DllImportStub.DllImportMember.BestFitMapping))
            {
                generatorDiagnostics.ReportConfigurationNotSupported(generatedDllImportAttr!, nameof(DllImportStub.GeneratedDllImportData.BestFitMapping));
            }

            if (stubDllImportData!.IsUserDefined.HasFlag(DllImportStub.DllImportMember.ThrowOnUnmappableChar))
            {
                generatorDiagnostics.ReportConfigurationNotSupported(generatedDllImportAttr!, nameof(DllImportStub.GeneratedDllImportData.ThrowOnUnmappableChar));
            }

            if (lcidConversionAttr != null)
            {
                // Using LCIDConversion with GeneratedDllImport is not supported
                generatorDiagnostics.ReportConfigurationNotSupported(lcidConversionAttr, nameof(TypeNames.LCIDConversionAttribute));
            }

            // Create the stub.
            var dllImportStub = DllImportStub.Create(symbol, stubDllImportData!, environment, generatorDiagnostics);

            return (PrintGeneratedSource(syntax, dllImportStub), generatorDiagnostics.Diagnostics.ToImmutableArray());
        }

        private static bool ShouldVisitNode(SyntaxNode syntaxNode)
        { 
            // We only support C# method declarations.
            if (syntaxNode.Language != LanguageNames.CSharp
                || !syntaxNode.IsKind(SyntaxKind.MethodDeclaration))
            {
                return false;
            }

            var methodSyntax = (MethodDeclarationSyntax)syntaxNode;

            // Verify the method has no generic types or defined implementation
            // and is marked static and partial.
            if (!(methodSyntax.TypeParameterList is null)
                || !(methodSyntax.Body is null)
                || !methodSyntax.Modifiers.Any(SyntaxKind.StaticKeyword)
                || !methodSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return false;
            }

            // Verify that the types the method is declared in are marked partial.
            for (SyntaxNode? parentNode = methodSyntax.Parent; parentNode is TypeDeclarationSyntax typeDecl; parentNode = parentNode.Parent)
            {
                if (!typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    return false;
                }
            }

            // Filter out methods with no attributes early.
            if (methodSyntax.AttributeLists.Count == 0)
            {
                return false;
            }

            return true;
        }
    }
}
