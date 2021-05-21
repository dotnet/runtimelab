using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal record StubEnvironment(
        Compilation Compilation,
        bool SupportedTargetFramework,
        Version TargetFrameworkVersion,
        DllImportGeneratorOptions Options);

    internal class DllImportStub
    {
        private TypePositionInfo returnTypeInfo;
        private IEnumerable<TypePositionInfo> paramsTypeInfo;

// We don't need the warnings around not setting the various
// non-nullable fields/properties on this type in the constructor
// since we always use a property initializer.
#pragma warning disable 8618
        private DllImportStub()
        {
        }
#pragma warning restore

        public string? StubTypeNamespace { get; init; }

        public IEnumerable<TypeDeclarationSyntax> StubContainingTypes { get; init; }

        public TypeSyntax StubReturnType { get => this.returnTypeInfo.ManagedType.AsTypeSyntax(); }

        public IEnumerable<ParameterSyntax> StubParameters
        {
            get
            {
                foreach (var typeinfo in paramsTypeInfo)
                {
                    if (typeinfo.ManagedIndex != TypePositionInfo.UnsetIndex
                        && typeinfo.ManagedIndex != TypePositionInfo.ReturnIndex)
                    {
                        yield return Parameter(Identifier(typeinfo.InstanceIdentifier))
                            .WithType(typeinfo.ManagedType.AsTypeSyntax())
                            .WithModifiers(TokenList(Token(typeinfo.RefKindSyntax)));
                    }
                }
            }
        }

        public BlockSyntax StubCode { get; init; }

        public AttributeListSyntax[] AdditionalAttributes { get; init; }

        /// <summary>
        /// Flags used to indicate members on GeneratedDllImport attribute.
        /// </summary>
        [Flags]
        public enum DllImportMember
        {
            None = 0,
            BestFitMapping = 1 << 0,
            CallingConvention = 1 << 1,
            CharSet = 1 << 2,
            EntryPoint = 1 << 3,
            ExactSpelling = 1 << 4,
            PreserveSig = 1 << 5,
            SetLastError = 1 << 6,
            ThrowOnUnmappableChar = 1 << 7,
            All = ~None
        }

        /// <summary>
        /// GeneratedDllImportAttribute data
        /// </summary>
        /// <remarks>
        /// The names of these members map directly to those on the
        /// DllImportAttribute and should not be changed.
        /// </remarks>
        public class GeneratedDllImportData
        {
            public string ModuleName { get; set; } = null!;

            /// <summary>
            /// Value set by the user on the original declaration.
            /// </summary>
            public DllImportMember IsUserDefined = DllImportMember.None;

            // Default values for the below fields are based on the
            // documented semanatics of DllImportAttribute:
            //   - https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.dllimportattribute
            public bool BestFitMapping { get; set; } = true;
            public CallingConvention CallingConvention { get; set; } = CallingConvention.Winapi;
            public CharSet CharSet { get; set; } = CharSet.Ansi;
            public string EntryPoint { get; set; } = null!;
            public bool ExactSpelling { get; set; } = false; // VB has different and unusual default behavior here.
            public bool PreserveSig { get; set; } = true;
            public bool SetLastError { get; set; } = false;
            public bool ThrowOnUnmappableChar { get; set; } = false;
        }

        public static DllImportStub Create(
            IMethodSymbol method,
            GeneratedDllImportData dllImportData,
            StubEnvironment env,
            GeneratorDiagnostics diagnostics,
            CancellationToken token = default)
        {
            // Cancel early if requested
            token.ThrowIfCancellationRequested();

            // Determine the namespace
            string? stubTypeNamespace = null;
            if (!(method.ContainingNamespace is null)
                && !method.ContainingNamespace.IsGlobalNamespace)
            {
                stubTypeNamespace = method.ContainingNamespace.ToString();
            }

            // Determine containing type(s)
            var containingTypes = new List<TypeDeclarationSyntax>();
            INamedTypeSymbol currType = method.ContainingType;
            while (!(currType is null))
            {
                // Use the declaring syntax as a basis for this type declaration.
                // Since we're generating source for the method, we know that the current type
                // has to be declared in source.
                TypeDeclarationSyntax typeDecl = (TypeDeclarationSyntax)currType.DeclaringSyntaxReferences[0].GetSyntax();
                // Remove current members, attributes, and base list so we don't double declare them.
                typeDecl = typeDecl.WithMembers(List<MemberDeclarationSyntax>())
                                   .WithAttributeLists(List<AttributeListSyntax>())
                                   .WithBaseList(null);

                containingTypes.Add(typeDecl);

                currType = currType.ContainingType;
            }

            // Compute the current default string encoding value.
            var defaultEncoding = CharEncoding.Undefined;
            if (dllImportData.IsUserDefined.HasFlag(DllImportMember.CharSet))
            {
                defaultEncoding = dllImportData.CharSet switch
                {
                    CharSet.Unicode => CharEncoding.Utf16,
                    CharSet.Auto => CharEncoding.PlatformDefined,
                    CharSet.Ansi => CharEncoding.Ansi,
                    _ => CharEncoding.Undefined, // [Compat] Do not assume a specific value for None
                };
            }

            var defaultInfo = new DefaultMarshallingInfo(defaultEncoding);

            // Determine parameter and return types
            var paramsTypeInfo = new List<TypePositionInfo>();
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                var param = method.Parameters[i];
                var typeInfo = TypePositionInfo.CreateForParameter(param, defaultInfo, env.Compilation, diagnostics, method.ContainingType);
                typeInfo = typeInfo with 
                {
                    ManagedIndex = i,
                    NativeIndex = paramsTypeInfo.Count
                };
                paramsTypeInfo.Add(typeInfo);
            }

            TypePositionInfo retTypeInfo = TypePositionInfo.CreateForType(method.ReturnType, method.GetReturnTypeAttributes(), defaultInfo, env.Compilation, diagnostics, method.ContainingType);
            retTypeInfo = retTypeInfo with
            {
                ManagedIndex = TypePositionInfo.ReturnIndex,
                NativeIndex = TypePositionInfo.ReturnIndex
            };

            var managedRetTypeInfo = retTypeInfo;
            IMarshallingGeneratorFactory generatorFactory;
            if (env.Options.GenerateForwarders)
            {
                generatorFactory = new ForwarderMarshallingGeneratorFactory();
            }
            else
            {
                generatorFactory = new DefaultMarshallingGeneratorFactory(new InteropGenerationOptions(env.Options.UseMarshalType));

                // Do not manually handle PreserveSig when generating forwarders.
                // We want the runtime to handle everything.
                if (!dllImportData.PreserveSig)
                {
                    // Use a marshalling generator that supports the HRESULT return->exception marshalling.
                    generatorFactory = new NoPreserveSigMarshallingGeneratorFactory(generatorFactory);

                    // Create type info for native HRESULT return
                    retTypeInfo = TypePositionInfo.CreateForType(env.Compilation.GetSpecialType(SpecialType.System_Int32), NoMarshallingInfo.Instance);
                    retTypeInfo = retTypeInfo with
                    {
                        NativeIndex = TypePositionInfo.ReturnIndex
                    };

                    // Create type info for native out param
                    if (!method.ReturnsVoid)
                    {
                        // Transform the managed return type info into an out parameter and add it as the last param
                        TypePositionInfo nativeOutInfo = managedRetTypeInfo with
                        {
                            InstanceIdentifier = PInvokeStubCodeGenerator.ReturnIdentifier,
                            RefKind = RefKind.Out,
                            RefKindSyntax = SyntaxKind.OutKeyword,
                            ManagedIndex = TypePositionInfo.ReturnIndex,
                            NativeIndex = paramsTypeInfo.Count
                        };
                        paramsTypeInfo.Add(nativeOutInfo);
                    }
                }
            }

            // Generate stub code
            var stubGenerator = new PInvokeStubCodeGenerator(
                method,
                paramsTypeInfo,
                retTypeInfo,
                diagnostics,
                dllImportData.SetLastError && !env.Options.GenerateForwarders,
                generatorFactory);
            string stubTargetName = "__PInvoke__";
            var code = stubGenerator.GeneratePInvokeBody(IdentifierName(stubTargetName));
            code = code.AddStatements(CreateTargetFunctionAsLocalStatement(stubGenerator, env.Options, dllImportData, stubTargetName, method.Name));

            var additionalAttrs = new List<AttributeListSyntax>();

            // Define additional attributes for the stub definition.
            if (env.TargetFrameworkVersion >= new Version(5, 0))
            {
                additionalAttrs.Add(
                    AttributeList(
                        SeparatedList(new []
                        {
                            // Adding the skip locals init indiscriminately since the source generator is
                            // targeted at non-blittable method signatures which typically will contain locals
                            // in the generated code.
                            Attribute(ParseName(TypeNames.System_Runtime_CompilerServices_SkipLocalsInitAttribute))
                        })));
            }

            return new DllImportStub()
            {
                returnTypeInfo = managedRetTypeInfo,
                paramsTypeInfo = paramsTypeInfo,
                StubTypeNamespace = stubTypeNamespace,
                StubContainingTypes = containingTypes,
                StubCode = code,
                AdditionalAttributes = additionalAttrs.ToArray(),
            };
        }

        private static LocalFunctionStatementSyntax CreateTargetFunctionAsLocalStatement(
            PInvokeStubCodeGenerator stubGenerator,
            DllImportGeneratorOptions options,
            GeneratedDllImportData dllImportData,
            string stubTargetName,
            string stubMethodName)
        {
            var (parameterList, returnType) = stubGenerator.GenerateTargetMethodSignatureData();
            return LocalFunctionStatement(returnType, stubTargetName)
                .AddModifiers(
                    Token(SyntaxKind.ExternKeyword),
                    Token(SyntaxKind.StaticKeyword),
                    Token(SyntaxKind.UnsafeKeyword))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                .WithAttributeLists(
                    SingletonList(AttributeList(
                        SingletonSeparatedList(
                            CreateDllImportAttributeForTarget(
                                GetTargetDllImportDataFromStubData(
                                    dllImportData,
                                    stubMethodName,
                                    options.GenerateForwarders))))))
                .WithParameterList(parameterList);
        }

        private static AttributeSyntax CreateDllImportAttributeForTarget(GeneratedDllImportData targetDllImportData)
        {
            var newAttributeArgs = new List<AttributeArgumentSyntax>
            {
                AttributeArgument(LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    Literal(targetDllImportData.ModuleName))),
                AttributeArgument(
                    NameEquals(nameof(DllImportAttribute.EntryPoint)),
                    null,
                    CreateStringExpressionSyntax(targetDllImportData.EntryPoint))
            };

            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.BestFitMapping))
            {
                var name = NameEquals(nameof(DllImportAttribute.BestFitMapping));
                var value = CreateBoolExpressionSyntax(targetDllImportData.BestFitMapping);
                newAttributeArgs.Add(AttributeArgument(name, null, value));
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.CallingConvention))
            {
                var name = NameEquals(nameof(DllImportAttribute.CallingConvention));
                var value = CreateEnumExpressionSyntax(targetDllImportData.CallingConvention);
                newAttributeArgs.Add(AttributeArgument(name, null, value));
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.CharSet))
            {
                var name = NameEquals(nameof(DllImportAttribute.CharSet));
                var value = CreateEnumExpressionSyntax(targetDllImportData.CharSet);
                newAttributeArgs.Add(AttributeArgument(name, null, value));
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.ExactSpelling))
            {
                var name = NameEquals(nameof(DllImportAttribute.ExactSpelling));
                var value = CreateBoolExpressionSyntax(targetDllImportData.ExactSpelling);
                newAttributeArgs.Add(AttributeArgument(name, null, value));
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.PreserveSig))
            {
                var name = NameEquals(nameof(DllImportAttribute.PreserveSig));
                var value = CreateBoolExpressionSyntax(targetDllImportData.PreserveSig);
                newAttributeArgs.Add(AttributeArgument(name, null, value));
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.SetLastError))
            {
                var name = NameEquals(nameof(DllImportAttribute.SetLastError));
                var value = CreateBoolExpressionSyntax(targetDllImportData.SetLastError);
                newAttributeArgs.Add(AttributeArgument(name, null, value));
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.ThrowOnUnmappableChar))
            {
                var name = NameEquals(nameof(DllImportAttribute.ThrowOnUnmappableChar));
                var value = CreateBoolExpressionSyntax(targetDllImportData.ThrowOnUnmappableChar);
                newAttributeArgs.Add(AttributeArgument(name, null, value));
            }

            // Create new attribute
            return Attribute(
                ParseName(typeof(DllImportAttribute).FullName),
                AttributeArgumentList(SeparatedList(newAttributeArgs)));

            static ExpressionSyntax CreateBoolExpressionSyntax(bool trueOrFalse)
            {
                return LiteralExpression(
                    trueOrFalse
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression);
            }

            static ExpressionSyntax CreateStringExpressionSyntax(string str)
            {
                return LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    Literal(str));
            }

            static ExpressionSyntax CreateEnumExpressionSyntax<T>(T value) where T : Enum
            {
                return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(typeof(T).FullName),
                    IdentifierName(value.ToString()));
            }
        }

        private static GeneratedDllImportData GetTargetDllImportDataFromStubData(GeneratedDllImportData dllImportData, string originalMethodName, bool forwardAll)
        {
            DllImportMember membersToForward = DllImportStub.DllImportMember.All
                               // https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.dllimportattribute.preservesig
                               // If PreserveSig=false (default is true), the P/Invoke stub checks/converts a returned HRESULT to an exception.
                               & ~DllImportStub.DllImportMember.PreserveSig
                               // https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.dllimportattribute.setlasterror
                               // If SetLastError=true (default is false), the P/Invoke stub gets/caches the last error after invoking the native function.
                               & ~DllImportStub.DllImportMember.SetLastError;
            if (forwardAll)
            {
                membersToForward = DllImportStub.DllImportMember.All;
            }

            var targetDllImportData = new GeneratedDllImportData
            {
                CharSet = dllImportData.CharSet,
                BestFitMapping = dllImportData.BestFitMapping,
                CallingConvention = dllImportData.CallingConvention,
                EntryPoint = dllImportData.EntryPoint,
                ModuleName = dllImportData.ModuleName,
                ExactSpelling = dllImportData.ExactSpelling,
                SetLastError = dllImportData.SetLastError,
                PreserveSig = dllImportData.PreserveSig,
                ThrowOnUnmappableChar = dllImportData.ThrowOnUnmappableChar,
                IsUserDefined = dllImportData.IsUserDefined & membersToForward
            };

            // If the EntryPoint property is not set, we will compute and
            // add it based on existing semantics (i.e. method name).
            //
            // N.B. The export discovery logic is identical regardless of where
            // the name is defined (i.e. method name vs EntryPoint property).
            if (!targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.EntryPoint))
            {
                targetDllImportData.EntryPoint = originalMethodName;
            }

            return targetDllImportData;
        }
    }
}
