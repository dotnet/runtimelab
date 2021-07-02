using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

        private List<AttributeSyntax> GenerateSyntaxForForwardedAttributes(AttributeData? suppressGCTransitionAttribute, AttributeData? unmanagedCallConvAttribute)
        {
            const string CallConvsField = "CallConvs";
            // Manually rehydrate the forwarded attributes with fully qualified types so we don't have to worry about any using directives.
            List<AttributeSyntax> attributes = new();

            if (suppressGCTransitionAttribute is not null)
            {
                attributes.Add(Attribute(ParseName(TypeNames.SuppressGCTransitionAttribute)));
            }
            if (unmanagedCallConvAttribute is not null)
            {
                AttributeSyntax unmanagedCallConvSyntax = Attribute(ParseName(TypeNames.UnmanagedCallConvAttribute));
                foreach (var arg in unmanagedCallConvAttribute.NamedArguments)
                {
                    if (arg.Key == CallConvsField)
                    {
                        InitializerExpressionSyntax callConvs = InitializerExpression(SyntaxKind.ArrayInitializerExpression);
                        foreach (var callConv in arg.Value.Values)
                        {
                            callConvs = callConvs.AddExpressions(
                                TypeOfExpression(((ITypeSymbol)callConv.Value!).AsTypeSyntax()));
                        }

                        ArrayTypeSyntax arrayOfSystemType = ArrayType(ParseTypeName(TypeNames.System_Type), SingletonList(ArrayRankSpecifier()));

                        unmanagedCallConvSyntax = unmanagedCallConvSyntax.AddArgumentListArguments(
                            AttributeArgument(
                                ArrayCreationExpression(arrayOfSystemType)
                                .WithInitializer(callConvs))
                            .WithNameEquals(NameEquals(IdentifierName(CallConvsField))));
                    }
                }
                attributes.Add(unmanagedCallConvSyntax);
            }
            return attributes;
        }

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
            DllImportStubContext stub,
            BlockSyntax stubCode)
        {
            // Create stub function
            var stubMethod = MethodDeclaration(stub.StubReturnType, userDeclaredMethod.Identifier)
                .AddAttributeLists(stub.AdditionalAttributes)
                .WithModifiers(StripTriviaFromModifiers(userDeclaredMethod.Modifiers))
                .WithParameterList(ParameterList(SeparatedList(stub.StubParameters)))
                .WithBody(stubCode);

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

        private GeneratedDllImportData ProcessGeneratedDllImportAttribute(AttributeData attrData)
        {
            var stubDllImportData = new GeneratedDllImportData();

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
                    case nameof(GeneratedDllImportData.BestFitMapping):
                        stubDllImportData = stubDllImportData with
                        {
                            BestFitMapping = (bool)namedArg.Value.Value!,
                            IsUserDefined = stubDllImportData.IsUserDefined | DllImportMember.BestFitMapping,
                        };
                        break;
                    case nameof(GeneratedDllImportData.CallingConvention):
                        stubDllImportData = stubDllImportData with
                        {
                            CallingConvention = (CallingConvention)namedArg.Value.Value!,
                            IsUserDefined = stubDllImportData.IsUserDefined | DllImportMember.CallingConvention,
                        };
                        break;
                    case nameof(GeneratedDllImportData.CharSet):
                        stubDllImportData = stubDllImportData with
                        {
                            CharSet = (CharSet)namedArg.Value.Value!,
                            IsUserDefined = stubDllImportData.IsUserDefined | DllImportMember.CharSet,
                        };
                        break;
                    case nameof(GeneratedDllImportData.EntryPoint):
                        stubDllImportData = stubDllImportData with
                        {
                            EntryPoint = (string)namedArg.Value.Value!,
                            IsUserDefined = stubDllImportData.IsUserDefined | DllImportMember.EntryPoint,
                        };
                        break;
                    case nameof(GeneratedDllImportData.ExactSpelling):
                        stubDllImportData = stubDllImportData with
                        {
                            ExactSpelling = (bool)namedArg.Value.Value!,
                            IsUserDefined = stubDllImportData.IsUserDefined | DllImportMember.ExactSpelling,
                        };
                        break;
                    case nameof(GeneratedDllImportData.PreserveSig):
                        stubDllImportData = stubDllImportData with
                        {
                            PreserveSig = (bool)namedArg.Value.Value!,
                            IsUserDefined = stubDllImportData.IsUserDefined | DllImportMember.PreserveSig,
                        };
                        break;
                    case nameof(GeneratedDllImportData.SetLastError):
                        stubDllImportData = stubDllImportData with
                        {
                            SetLastError = (bool)namedArg.Value.Value!,
                            IsUserDefined = stubDllImportData.IsUserDefined | DllImportMember.SetLastError,
                        };
                        break;
                    case nameof(GeneratedDllImportData.ThrowOnUnmappableChar):
                        stubDllImportData = stubDllImportData with
                        {
                            ThrowOnUnmappableChar = (bool)namedArg.Value.Value!,
                            IsUserDefined = stubDllImportData.IsUserDefined | DllImportMember.ThrowOnUnmappableChar,
                        };
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

        public class IncrementalityTracker
        {
            public enum StepName
            {
                CalculateStubInformation,
                GenerateSingleStub,
                NormalizeWhitespace,
                ConcatenateStubs,
                OutputSourceFile
            }

            public record ExecutedStepInfo(StepName Step, object Input);

            private List<ExecutedStepInfo> executedSteps = new();
            public IEnumerable<ExecutedStepInfo> ExecutedSteps => executedSteps;

            internal void RecordExecutedStep(ExecutedStepInfo step) => executedSteps.Add(step);
        }

        public IncrementalityTracker? IncrementalTracker { get; set; }

        public void Initialize(IncrementalGeneratorInitializationContext context)
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

            context.RegisterSourceOutput(
                compilationAndTargetFramework
                    .Combine(methodsToGenerate.Collect()),
                static (context, data) =>
                {
                    if (!data.Left.isSupported && data.Right.Any())
                    {
                        // We don't block source generation when the TFM is unsupported.
                        // This allows a user to copy generated source and use it as a starting point
                        // for manual marshalling if desired.
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                GeneratorDiagnostics.TargetFrameworkNotSupported,
                                Location.None,
                                MinimumSupportedFrameworkVersion.ToString(2)));
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
                            data.Right.GlobalOptions,
                            data.Left.compilation.SourceModule.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == TypeNames.System_Runtime_CompilerServices_SkipLocalsInitAttribute))
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
                    (data, ct) =>
                    {
                        IncrementalTracker?.RecordExecutedStep(new IncrementalityTracker.ExecutedStepInfo(IncrementalityTracker.StepName.CalculateStubInformation, data));
                        return (data.Syntax, ComputeStubContext(data.Syntax, data.Symbol, data.Environment, ct));
                    }
                )
                .WithComparer(Comparers.CalculatedContextWithSyntax)
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Select(
                    (data, ct) =>
                    {
                        IncrementalTracker?.RecordExecutedStep(new IncrementalityTracker.ExecutedStepInfo(IncrementalityTracker.StepName.GenerateSingleStub, data));
                        return (GenerateSource(data.Left.Item2.StubContext, data.Left.Item2.DllImportData, data.Left.Item1, data.Left.Item2.ForwardedAttributes, data.Right.GlobalOptions), data.Left.Item2.Diagnostics);
                    })
                .WithComparer(Comparers.GeneratedSyntax)
                // Handle NormalizeWhitespace as a separate stage for incremental runs since it is an expensive operation.
                .Select(
                    (data, ct) =>
                    {
                        IncrementalTracker?.RecordExecutedStep(new IncrementalityTracker.ExecutedStepInfo(IncrementalityTracker.StepName.NormalizeWhitespace, data));
                        return (data.Item1.NormalizeWhitespace().ToFullString(), data.Item2);
                    })
                .Collect()
                .WithComparer(Comparers.GeneratedSourceSet)
                .Select((generatedSources, ct) =>
                {
                    IncrementalTracker?.RecordExecutedStep(new IncrementalityTracker.ExecutedStepInfo(IncrementalityTracker.StepName.ConcatenateStubs, generatedSources));
                    StringBuilder source = new StringBuilder();
                    // Mark in source that the file is auto-generated.
                    source.AppendLine("// <auto-generated/>");
                    ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
                    foreach (var generated in generatedSources)
                    {
                        source.AppendLine(generated.Item1);
                        diagnostics.AddRange(generated.Item2);
                    }
                    return (source: source.ToString(), diagnostics: diagnostics.ToImmutable());
                })
                .WithComparer(Comparers.GeneratedSource);

            context.RegisterSourceOutput(methodSourceAndDiagnostics,
                (context, data) =>
                {
                    IncrementalTracker?.RecordExecutedStep(new IncrementalityTracker.ExecutedStepInfo(IncrementalityTracker.StepName.OutputSourceFile, data));
                    foreach (var diagnostic in data.Item2)
                    {
                        context.ReportDiagnostic(diagnostic);
                    }

                    context.AddSource("GeneratedDllImports.g.cs", data.Item1);
                });
        }

        internal sealed record IncrementalStubGenerationContext(DllImportStubContext StubContext, ImmutableArray<AttributeSyntax> ForwardedAttributes, GeneratedDllImportData DllImportData, ImmutableArray<Diagnostic> Diagnostics)
        {
            public bool Equals(IncrementalStubGenerationContext? other)
            {
                return other is not null
                    && StubContext.Equals(other.StubContext)
                    && DllImportData.Equals(other.DllImportData)
                    && ForwardedAttributes.SequenceEqual(other.ForwardedAttributes, (IEqualityComparer<AttributeSyntax>)new SyntaxEquivalentComparer())
                    && Diagnostics.SequenceEqual(other.Diagnostics);
            }

            public override int GetHashCode()
            {
                return (StubContext, DllImportData, ForwardedAttributes.Length, Diagnostics.Length).GetHashCode();
            }
        }

        private IncrementalStubGenerationContext ComputeStubContext(MethodDeclarationSyntax syntax, IMethodSymbol symbol, StubEnvironment environment, CancellationToken ct)
        {
            INamedTypeSymbol? lcidConversionAttrType = environment.Compilation.GetTypeByMetadataName(TypeNames.LCIDConversionAttribute);
            INamedTypeSymbol? suppressGCTransitionAttrType = environment.Compilation.GetTypeByMetadataName(TypeNames.SuppressGCTransitionAttribute);
            INamedTypeSymbol? unmanagedCallConvAttrType = environment.Compilation.GetTypeByMetadataName(TypeNames.UnmanagedCallConvAttribute);
            // Get any attributes of interest on the method
            AttributeData? generatedDllImportAttr = null;
            AttributeData? lcidConversionAttr = null;
            AttributeData? suppressGCTransitionAttribute = null;
            AttributeData? unmanagedCallConvAttribute = null;
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
                else if (suppressGCTransitionAttrType != null && SymbolEqualityComparer.Default.Equals(attr.AttributeClass, suppressGCTransitionAttrType))
                {
                    suppressGCTransitionAttribute = attr;
                }
                else if (unmanagedCallConvAttrType != null && SymbolEqualityComparer.Default.Equals(attr.AttributeClass, unmanagedCallConvAttrType))
                {
                    unmanagedCallConvAttribute = attr;
                }
            }

            Debug.Assert(generatedDllImportAttr is not null);
            
            var generatorDiagnostics = new GeneratorDiagnostics();

            // Process the GeneratedDllImport attribute
            GeneratedDllImportData stubDllImportData = this.ProcessGeneratedDllImportAttribute(generatedDllImportAttr!);
            Debug.Assert(stubDllImportData is not null);

            if (stubDllImportData!.IsUserDefined.HasFlag(DllImportMember.BestFitMapping))
            {
                generatorDiagnostics.ReportConfigurationNotSupported(generatedDllImportAttr!, nameof(GeneratedDllImportData.BestFitMapping));
            }

            if (stubDllImportData!.IsUserDefined.HasFlag(DllImportMember.ThrowOnUnmappableChar))
            {
                generatorDiagnostics.ReportConfigurationNotSupported(generatedDllImportAttr!, nameof(GeneratedDllImportData.ThrowOnUnmappableChar));
            }

            if (lcidConversionAttr != null)
            {
                // Using LCIDConversion with GeneratedDllImport is not supported
                generatorDiagnostics.ReportConfigurationNotSupported(lcidConversionAttr, nameof(TypeNames.LCIDConversionAttribute));
            }
            List<AttributeSyntax> additionalAttributes = GenerateSyntaxForForwardedAttributes(suppressGCTransitionAttribute, unmanagedCallConvAttribute);

            // Create the stub.
            var dllImportStub = DllImportStubContext.Create(symbol, stubDllImportData!, environment, generatorDiagnostics, ct);

            return new IncrementalStubGenerationContext(dllImportStub, additionalAttributes.ToImmutableArray(), stubDllImportData, generatorDiagnostics.Diagnostics.ToImmutableArray());
        }

        private MemberDeclarationSyntax GenerateSource(
            DllImportStubContext dllImportStub,
            GeneratedDllImportData dllImportData,
            MethodDeclarationSyntax originalSyntax,
            ImmutableArray<AttributeSyntax> forwardedAttributes,
            AnalyzerConfigOptions options)
        {
            // Generate stub code
            var stubGenerator = new StubCodeGenerator(dllImportData, dllImportStub.BoundGenerators, dllImportStub.CodeContext, options);
            var code = stubGenerator.GenerateSyntax(originalSyntax.Identifier.Text, forwardedAttributes: forwardedAttributes.Length != 0 ? AttributeList(SeparatedList(forwardedAttributes)) : null);

            return PrintGeneratedSource(originalSyntax, dllImportStub, code);
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
