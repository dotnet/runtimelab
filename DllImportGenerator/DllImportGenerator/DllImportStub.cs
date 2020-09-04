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
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal class DllImportStub
    {
        private TypePositionInfo returnTypeInfo;
        private IEnumerable<TypePositionInfo> paramsTypeInfo;

        private DllImportStub()
        {
        }

        public string StubTypeNamespace { get; private set; }

        public IEnumerable<string> StubContainingTypesDecl { get; private set; }

        public TypeSyntax StubReturnType { get => this.returnTypeInfo.ManagedType; }

        public IEnumerable<ParameterSyntax> StubParameters
        {
            get
            {
                foreach (var typeinfo in paramsTypeInfo)
                {
                    //if (typeinfo.ManagedIndex != TypePositionInfo.UnsetIndex)
                    {
                        yield return Parameter(Identifier(typeinfo.InstanceIdentifier))
                            .WithType(typeinfo.ManagedType)
                            .WithModifiers(TokenList(Token(typeinfo.RefKindSyntax)));
                    }
                }
            }
        }

        public BlockSyntax StubCode { get; private set; }

        public MethodDeclarationSyntax DllImportDeclaration { get; private set; }

        public IEnumerable<Diagnostic> Diagnostics { get; private set; }

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
            public string ModuleName { get; set; }

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
            public string EntryPoint { get; set; } = null;
            public bool ExactSpelling { get; set; } = false; // VB has different and unusual default behavior here.
            public bool PreserveSig { get; set; } = true;
            public bool SetLastError { get; set; } = false;
            public bool ThrowOnUnmappableChar { get; set; } = false;
        }

        public static DllImportStub Create(
            IMethodSymbol method,
            GeneratedDllImportData dllImportData,
            CancellationToken token = default)
        {
            // Cancel early if requested
            token.ThrowIfCancellationRequested();

            // Determine the namespace
            string stubTypeNamespace = null;
            if (!(method.ContainingNamespace is null)
                && !method.ContainingNamespace.IsGlobalNamespace)
            {
                stubTypeNamespace = method.ContainingNamespace.ToString();
            }

            // Determine type
            var stubContainingTypes = new List<string>();
            INamedTypeSymbol currType = method.ContainingType;
            while (!(currType is null))
            {
                var visibility = currType.DeclaredAccessibility switch
                {
                    Accessibility.Public => "public",
                    Accessibility.Private => "private",
                    Accessibility.Protected => "protected",
                    Accessibility.Internal => "internal",
                    _ => throw new NotSupportedException(), // [TODO] Proper error message
                };

                var typeKeyword = currType.TypeKind switch
                {
                    TypeKind.Class => "class",
                    TypeKind.Struct => "struct",
                    _ => throw new NotSupportedException(), // [TODO] Proper error message
                };

                stubContainingTypes.Add($"{visibility} partial {typeKeyword} {currType.Name}");
                currType = currType.ContainingType;
            }

            // Flip the order to that of how to declare the types
            stubContainingTypes.Reverse();

            // Determine parameter types
            var paramsTypeInfo = new List<TypePositionInfo>();
            foreach (var paramSymbol in method.Parameters)
            {
                paramsTypeInfo.Add(TypePositionInfo.CreateForParameter(paramSymbol));
            }

            var retTypeInfo = TypePositionInfo.CreateForType(method.ReturnType, method.GetReturnTypeAttributes());

            string dllImportName = method.Name + "__PInvoke__";

            var syntax = GenerateSyntax(dllImportName, paramsTypeInfo, retTypeInfo);

            return new DllImportStub()
            {
                returnTypeInfo = retTypeInfo,
                paramsTypeInfo = paramsTypeInfo,
                StubTypeNamespace = stubTypeNamespace,
                StubContainingTypesDecl = stubContainingTypes,
                StubCode = Block(UnsafeStatement(Block(syntax.StubCode))),
                DllImportDeclaration = syntax.DllImport,
                Diagnostics = Enumerable.Empty<Diagnostic>(),
            };
        }

        private static (TypePositionInfo TypeInfo, IMarshallingGenerator Generator) GetMarshalInfo(TypePositionInfo typeInfo)
        {
            IMarshallingGenerator generator;
            if (!MarshallingGenerator.TryCreate(typeInfo, out generator))
                throw new NotSupportedException();

            return (typeInfo, generator);
        }

        private static (IEnumerable<StatementSyntax> StubCode, MethodDeclarationSyntax DllImport) GenerateSyntax(string dllImportName, IEnumerable<TypePositionInfo> paramsTypeInfo, TypePositionInfo retTypeInfo)
        {
            bool returnsVoid = retTypeInfo.TypeSymbol.SpecialType == SpecialType.System_Void;
            var paramMarshallers = paramsTypeInfo.Select(p => GetMarshalInfo(p));
            var retMarshaller = GetMarshalInfo(retTypeInfo);

            var context = new StubCodeContext(GenerationStage.Setup);
            var statements = new List<StatementSyntax>();

            if (!returnsVoid)
            {
                // Declare variable for return value
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(
                        retMarshaller.TypeInfo.ManagedType,
                        SingletonSeparatedList(
                            VariableDeclarator(context.ReturnIdentifier)))));
            }

            var stages = new GenerationStage[]
            {
                GenerationStage.Setup,
                GenerationStage.Marshal,
                GenerationStage.Pin,
                GenerationStage.Invoke,
                GenerationStage.Unmarshal,
                GenerationStage.Cleanup
            };

            var invoke = InvocationExpression(IdentifierName(dllImportName));
            foreach (var stage in stages)
            {
                int initialCount = statements.Count;
                context.SetStage(stage);
                if (!returnsVoid && (stage == GenerationStage.Setup || stage == GenerationStage.Unmarshal))
                {
                    var retStatements = retMarshaller.Generator.Generate(retMarshaller.TypeInfo, context);
                    statements.AddRange(retStatements);
                }

                foreach (var marshaller in paramMarshallers)
                {
                    if (stage == GenerationStage.Invoke)
                    {
                        ArgumentSyntax argSyntax = marshaller.Generator.AsArgument(marshaller.TypeInfo);
                        invoke = invoke.AddArgumentListArguments(argSyntax);
                    }
                    else
                    {
                        var generatedStatements = marshaller.Generator.Generate(marshaller.TypeInfo, context);
                        statements.AddRange(generatedStatements);
                    }
                }

                if (stage == GenerationStage.Invoke)
                {
                    if (returnsVoid)
                    {
                        statements.Add(ExpressionStatement(invoke));
                    }
                    else
                    {
                        statements.Add(ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(context.ReturnNativeIdentifier),
                                invoke)));
                    }
                }

                if (statements.Count > initialCount)
                {
                    var newLeadingTrivia = TriviaList(
                        Comment($"//"),
                        Comment($"// {stage}"),
                        Comment($"//"));
                    var firstStatementInStage = statements[initialCount];
                    newLeadingTrivia = newLeadingTrivia.AddRange(firstStatementInStage.GetLeadingTrivia());
                    statements[initialCount] = firstStatementInStage.WithLeadingTrivia(newLeadingTrivia);
                }
            }

            if (!returnsVoid)
            {
                statements.Add(ReturnStatement(IdentifierName(context.ReturnIdentifier)));
            }

            var returnNativeType = retMarshaller.Generator.AsNativeType(retMarshaller.TypeInfo);
            var dllImport = MethodDeclaration(returnNativeType, dllImportName)
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.ExternKeyword),
                        Token(SyntaxKind.PrivateKeyword),
                        Token(SyntaxKind.StaticKeyword),
                        Token(SyntaxKind.UnsafeKeyword)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            foreach (var marshaller in paramMarshallers)
            {
                ParameterSyntax paramSyntax = marshaller.Generator.AsParameter(marshaller.TypeInfo);
                dllImport = dllImport.AddParameterListParameters(paramSyntax);
            }

            return (statements, dllImport);
        }
    }
}
