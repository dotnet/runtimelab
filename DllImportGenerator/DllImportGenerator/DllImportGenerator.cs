using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    [Generator]
    public class DllImportGenerator : ISourceGenerator
    {
        private const string GeneratedDllImport = nameof(GeneratedDllImport);
        private const string GeneratedDllImportAttribute = nameof(GeneratedDllImportAttribute);

        private static readonly Version MinimumSupportedFrameworkVersion = new Version(5, 0);

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not SyntaxContextReceiver synRec
                || !synRec.Methods.Any())
            {
                return;
            }

            INamedTypeSymbol? lcidConversionAttrType = context.Compilation.GetTypeByMetadataName(TypeNames.LCIDConversionAttribute);

            // Fire the start/stop pair for source generation
            using var _ = Diagnostics.Events.SourceGenerationStartStop(synRec.Methods.Count);

            // Store a mapping between SyntaxTree and SemanticModel.
            // SemanticModels cache results and since we could be looking at
            // method declarations in the same SyntaxTree we want to benefit from
            // this caching.
            var syntaxToModel = new Dictionary<SyntaxTree, SemanticModel>();

            var generatorDiagnostics = new GeneratorDiagnostics(context);

            bool isSupported = IsSupportedTargetFramework(context.Compilation, out Version targetFrameworkVersion);
            if (!isSupported)
            {
                // We don't return early here, letting the source generation continue.
                // This allows a user to copy generated source and use it as a starting point
                // for manual marshalling if desired.
                generatorDiagnostics.ReportTargetFrameworkNotSupported(MinimumSupportedFrameworkVersion);
            }

            var env = new StubEnvironment(context.Compilation, isSupported, targetFrameworkVersion, context.AnalyzerConfigOptions.GlobalOptions);

            var generatedDllImports = new StringBuilder();

            // Mark in source that the file is auto-generated.
            generatedDllImports.AppendLine("// <auto-generated/>");

            foreach (SyntaxReference synRef in synRec.Methods)
            {
                var methodSyntax = (MethodDeclarationSyntax)synRef.GetSyntax(context.CancellationToken);

                // Get the model for the method.
                if (!syntaxToModel.TryGetValue(methodSyntax.SyntaxTree, out SemanticModel sm))
                {
                    sm = context.Compilation.GetSemanticModel(methodSyntax.SyntaxTree, ignoreAccessibility: true);
                    syntaxToModel.Add(methodSyntax.SyntaxTree, sm);
                }

                // Process the method syntax and get its SymbolInfo.
                var methodSymbolInfo = sm.GetDeclaredSymbol(methodSyntax, context.CancellationToken)!;

                // Get any attributes of interest on the method
                AttributeData? generatedDllImportAttr = null;
                AttributeData? lcidConversionAttr = null;
                foreach (var attr in methodSymbolInfo.GetAttributes())
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

                if (generatedDllImportAttr == null)
                    continue;

                // Process the GeneratedDllImport attribute
                DllImportStub.GeneratedDllImportData stubDllImportData = this.ProcessGeneratedDllImportAttribute(generatedDllImportAttr);
                Debug.Assert(stubDllImportData is not null);

                if (stubDllImportData!.IsUserDefined.HasFlag(DllImportStub.DllImportMember.BestFitMapping))
                {
                    generatorDiagnostics.ReportConfigurationNotSupported(generatedDllImportAttr, nameof(DllImportStub.GeneratedDllImportData.BestFitMapping));
                }

                if (stubDllImportData!.IsUserDefined.HasFlag(DllImportStub.DllImportMember.ThrowOnUnmappableChar))
                {
                    generatorDiagnostics.ReportConfigurationNotSupported(generatedDllImportAttr, nameof(DllImportStub.GeneratedDllImportData.ThrowOnUnmappableChar));
                }

                if (lcidConversionAttr != null)
                {
                    // Using LCIDConversion with GeneratedDllImport is not supported
                    generatorDiagnostics.ReportConfigurationNotSupported(lcidConversionAttr, nameof(TypeNames.LCIDConversionAttribute));
                }

                // Create the stub.
                var dllImportStub = DllImportStub.Create(methodSymbolInfo, stubDllImportData!, env, generatorDiagnostics, context.CancellationToken);

                PrintGeneratedSource(generatedDllImports, methodSyntax, dllImportStub);
            }

            Debug.WriteLine(generatedDllImports.ToString()); // [TODO] Find some way to emit this for debugging - logs?
            context.AddSource("DllImportGenerator.g.cs", SourceText.From(generatedDllImports.ToString(), Encoding.UTF8));
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxContextReceiver());
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

        private void PrintGeneratedSource(
            StringBuilder builder,
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

            builder.AppendLine(toPrint.NormalizeWhitespace().ToString());
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


        private class SyntaxContextReceiver : ISyntaxContextReceiver
        {
            public ICollection<SyntaxReference> Methods { get; } = new List<SyntaxReference>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                SyntaxNode syntaxNode = context.Node;

                // We only support C# method declarations.
                if (syntaxNode.Language != LanguageNames.CSharp
                    || !syntaxNode.IsKind(SyntaxKind.MethodDeclaration))
                {
                    return;
                }

                var methodSyntax = (MethodDeclarationSyntax)syntaxNode;

                // Verify the method has no generic types or defined implementation
                // and is marked static and partial.
                if (!(methodSyntax.TypeParameterList is null)
                    || !(methodSyntax.Body is null)
                    || !methodSyntax.Modifiers.Any(SyntaxKind.StaticKeyword)
                    || !methodSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    return;
                }

                // Verify that the types the method is declared in are marked partial.
                for (SyntaxNode? parentNode = methodSyntax.Parent; parentNode is TypeDeclarationSyntax typeDecl; parentNode = parentNode.Parent)
                {
                    if (!typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        return;
                    }
                }

                // Check if the method is marked with the GeneratedDllImport attribute.
                foreach (AttributeListSyntax listSyntax in methodSyntax.AttributeLists)
                {
                    foreach (AttributeSyntax attrSyntax in listSyntax.Attributes)
                    {
                        SymbolInfo info = context.SemanticModel.GetSymbolInfo(attrSyntax);
                        if (info.Symbol is IMethodSymbol attrConstructor
                            && attrConstructor.ContainingType.ToDisplayString() == TypeNames.GeneratedDllImportAttribute)
                        {
                            this.Methods.Add(syntaxNode.GetReference());
                            return;
                        }
                    }
                }
            }
        }
    }
}
