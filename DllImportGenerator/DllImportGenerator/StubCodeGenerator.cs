﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal sealed class StubCodeGenerator : StubCodeContext
    {
        public override bool PinningSupported => true;

        public override bool StackSpaceUsable => true;

        public override bool CanUseAdditionalTemporaryState => true;

        /// <summary>
        /// Identifier for managed return value
        /// </summary>
        public const string ReturnIdentifier = "__retVal";

        /// <summary>
        /// Identifier for native return value
        /// </summary>
        /// <remarks>Same as the managed identifier by default</remarks>
        public string ReturnNativeIdentifier { get; private set; } = ReturnIdentifier;

        private const string InvokeReturnIdentifier = "__invokeRetVal";
        private const string LastErrorIdentifier = "__lastError";

        // Error code representing success. This maps to S_OK for Windows HRESULT semantics and 0 for POSIX errno semantics.
        private const int SuccessErrorCode = 0;

        private static readonly Stage[] Stages = new Stage[]
        {
            Stage.Setup,
            Stage.Marshal,
            Stage.Pin,
            Stage.Invoke,
            Stage.KeepAlive,
            Stage.Unmarshal,
            Stage.GuaranteedUnmarshal,
            Stage.Cleanup
        };

        private readonly GeneratorDiagnostics diagnostics;
        private readonly AnalyzerConfigOptions options;
        private readonly IMethodSymbol stubMethod;
        private readonly DllImportStub.GeneratedDllImportData dllImportData;
        private readonly IEnumerable<TypePositionInfo> paramsTypeInfo;
        private readonly List<(TypePositionInfo TypeInfo, IMarshallingGenerator Generator)> paramMarshallers;
        private readonly (TypePositionInfo TypeInfo, IMarshallingGenerator Generator) retMarshaller;

        public StubCodeGenerator(
            IMethodSymbol stubMethod,
            DllImportStub.GeneratedDllImportData dllImportData,
            IEnumerable<TypePositionInfo> paramsTypeInfo,
            TypePositionInfo retTypeInfo,
            GeneratorDiagnostics generatorDiagnostics,
            AnalyzerConfigOptions options)
        {
            Debug.Assert(retTypeInfo.IsNativeReturnPosition);

            this.stubMethod = stubMethod;
            this.dllImportData = dllImportData;
            this.paramsTypeInfo = paramsTypeInfo.ToList();
            this.diagnostics = generatorDiagnostics;
            this.options = options;

            // Get marshallers for parameters
            this.paramMarshallers = paramsTypeInfo.Select(p => CreateGenerator(p)).ToList();

            // Get marshaller for return
            this.retMarshaller = CreateGenerator(retTypeInfo);

            (TypePositionInfo info, IMarshallingGenerator gen) CreateGenerator(TypePositionInfo p)
            {
                try
                {
                    return (p, MarshallingGenerators.Create(p, this, options));
                }
                catch (MarshallingNotSupportedException e)
                {
                    this.diagnostics.ReportMarshallingNotSupported(this.stubMethod, p, e.NotSupportedDetails);
                    return (p, MarshallingGenerators.Forwarder);
                }
            }
        }

        public override (string managed, string native) GetIdentifiers(TypePositionInfo info)
        {
            if (info.IsManagedReturnPosition && !info.IsNativeReturnPosition)
            {
                return (ReturnIdentifier, ReturnNativeIdentifier);
            }
            else if (!info.IsManagedReturnPosition && info.IsNativeReturnPosition)
            {
                return (InvokeReturnIdentifier, InvokeReturnIdentifier);
            }
            else if (info.IsManagedReturnPosition && info.IsNativeReturnPosition)
            {
                return (ReturnIdentifier, ReturnNativeIdentifier);
            }
            else
            {
                // If the info isn't in either the managed or native return position,
                // then we can use the base implementation since we have an identifier name provided
                // in the original metadata.
                return base.GetIdentifiers(info);
            }
        }

        public (BlockSyntax Code, MethodDeclarationSyntax DllImport) GenerateSyntax()
        {
            string dllImportName = stubMethod.Name + "__PInvoke__";
            var setupStatements = new List<StatementSyntax>();

            if (retMarshaller.Generator.UsesNativeIdentifier(retMarshaller.TypeInfo, this))
            {
                // Update the native identifier for the return value
                ReturnNativeIdentifier = $"{ReturnIdentifier}{GeneratedNativeIdentifierSuffix}";
            }

            foreach (var marshaller in paramMarshallers)
            {
                TypePositionInfo info = marshaller.TypeInfo;
                if (info.IsManagedReturnPosition)
                    continue;

                if (info.RefKind == RefKind.Out)
                {
                    // Assign out params to default
                    setupStatements.Add(ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(info.InstanceIdentifier),
                            LiteralExpression(
                                SyntaxKind.DefaultLiteralExpression,
                                Token(SyntaxKind.DefaultKeyword)))));
                }

                // Declare variables for parameters
                AppendVariableDeclations(setupStatements, info, marshaller.Generator);
            }

            bool invokeReturnsVoid = retMarshaller.TypeInfo.ManagedType.SpecialType == SpecialType.System_Void;
            bool stubReturnsVoid = stubMethod.ReturnsVoid;

            // Stub return is not the same as invoke return
            if (!stubReturnsVoid && !retMarshaller.TypeInfo.IsManagedReturnPosition)
            {
                // Should only happen when PreserveSig=false
                Debug.Assert(!dllImportData.PreserveSig, "Expected PreserveSig=false when invoke return is not the stub return");

                // Stub return should be the last parameter for the invoke
                Debug.Assert(paramMarshallers.Any() && paramMarshallers.Last().TypeInfo.IsManagedReturnPosition, "Expected stub return to be the last parameter for the invoke");

                (TypePositionInfo stubRetTypeInfo, IMarshallingGenerator stubRetGenerator) = paramMarshallers.Last();
                if (stubRetGenerator.UsesNativeIdentifier(stubRetTypeInfo, this))
                {
                    // Update the native identifier for the return value
                    ReturnNativeIdentifier = $"{ReturnIdentifier}{GeneratedNativeIdentifierSuffix}";
                }

                // Declare variables for stub return value
                AppendVariableDeclations(setupStatements, stubRetTypeInfo, stubRetGenerator);
            }

            if (!invokeReturnsVoid)
            {
                // Declare variables for invoke return value
                AppendVariableDeclations(setupStatements, retMarshaller.TypeInfo, retMarshaller.Generator);
            }

            // Do not manually handle SetLastError when generating forwarders.
            // We want the runtime to handle everything.
            if (this.dllImportData.SetLastError && !options.GenerateForwarders())
            {
                // Declare variable for last error
                setupStatements.Add(MarshallerHelpers.DeclareWithDefault(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    LastErrorIdentifier));
            }

            var tryStatements = new List<StatementSyntax>();
            var finallyStatements = new List<StatementSyntax>();
            var invoke = InvocationExpression(IdentifierName(dllImportName));
            var fixedStatements = new List<FixedStatementSyntax>();
            foreach (var stage in Stages)
            {
                var statements = GetStatements(stage);
                int initialCount = statements.Count;
                this.CurrentStage = stage;

                if (!invokeReturnsVoid && (stage == Stage.Setup || stage == Stage.Unmarshal || stage == Stage.GuaranteedUnmarshal))
                {
                    // Handle setup and unmarshalling for return
                    var retStatements = retMarshaller.Generator.Generate(retMarshaller.TypeInfo, this);
                    statements.AddRange(retStatements);
                }

                // Generate code for each parameter for the current stage
                foreach (var marshaller in paramMarshallers)
                {
                    if (stage == Stage.Invoke)
                    {
                        // Get arguments for invocation
                        ArgumentSyntax argSyntax = marshaller.Generator.AsArgument(marshaller.TypeInfo, this);
                        invoke = invoke.AddArgumentListArguments(argSyntax);
                    }
                    else
                    {
                        var generatedStatements = marshaller.Generator.Generate(marshaller.TypeInfo, this);
                        if (stage == Stage.Pin)
                        {
                            // Collect all the fixed statements. These will be used in the Invoke stage.
                            foreach (var statement in generatedStatements)
                            {
                                if (statement is not FixedStatementSyntax fixedStatement)
                                    continue;

                                fixedStatements.Add(fixedStatement);
                            }
                        }
                        else
                        {
                            statements.AddRange(generatedStatements);
                        }
                    }
                }

                if (stage == Stage.Invoke)
                {
                    StatementSyntax invokeStatement;

                    // Assign to return value if necessary
                    if (invokeReturnsVoid)
                    {
                        invokeStatement = ExpressionStatement(invoke);
                    }
                    else
                    {
                        invokeStatement = ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(this.GetIdentifiers(retMarshaller.TypeInfo).native),
                                invoke));
                    }

                    // Do not manually handle SetLastError when generating forwarders.
                    // We want the runtime to handle everything.
                    if (this.dllImportData.SetLastError && !options.GenerateForwarders())
                    {
                        // Marshal.SetLastSystemError(0);
                        var clearLastError = ExpressionStatement(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ParseName(TypeNames.MarshalEx(options)),
                                    IdentifierName("SetLastSystemError")),
                                ArgumentList(SingletonSeparatedList(
                                    Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(SuccessErrorCode)))))));

                        // <lastError> = Marshal.GetLastSystemError();
                        var getLastError = ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(LastErrorIdentifier),
                                InvocationExpression(
                                    MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ParseName(TypeNames.MarshalEx(options)),
                                    IdentifierName("GetLastSystemError")))));

                        invokeStatement = Block(clearLastError, invokeStatement, getLastError);
                    }

                    // Nest invocation in fixed statements
                    if (fixedStatements.Any())
                    {
                        fixedStatements.Reverse();
                        invokeStatement = fixedStatements.First().WithStatement(invokeStatement);
                        foreach (var fixedStatement in fixedStatements.Skip(1))
                        {
                            invokeStatement = fixedStatement.WithStatement(Block(invokeStatement));
                        }
                    }

                    statements.Add(invokeStatement);
                }

                if (statements.Count > initialCount)
                {
                    // Comment separating each stage
                    var newLeadingTrivia = TriviaList(
                        Comment($"//"),
                        Comment($"// {stage}"),
                        Comment($"//"));
                    var firstStatementInStage = statements[initialCount];
                    newLeadingTrivia = newLeadingTrivia.AddRange(firstStatementInStage.GetLeadingTrivia());
                    statements[initialCount] = firstStatementInStage.WithLeadingTrivia(newLeadingTrivia);
                }
            }

            List<StatementSyntax> allStatements = setupStatements;
            if (finallyStatements.Count > 0)
            {
                // Add try-finally block if there are any statements in the finally block
                allStatements.Add(
                    TryStatement(Block(tryStatements), default, FinallyClause(Block(finallyStatements))));
            }
            else
            {
                allStatements.AddRange(tryStatements);
            }

            if (this.dllImportData.SetLastError && !options.GenerateForwarders())
            {
                // Marshal.SetLastPInvokeError(<lastError>);
                allStatements.Add(ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            ParseName(TypeNames.MarshalEx(options)),
                            IdentifierName("SetLastPInvokeError")),
                        ArgumentList(SingletonSeparatedList(
                            Argument(IdentifierName(LastErrorIdentifier)))))));
            }

            // Return
            if (!stubReturnsVoid)
                allStatements.Add(ReturnStatement(IdentifierName(ReturnIdentifier)));

            // Wrap all statements in an unsafe block
            var codeBlock = Block(UnsafeStatement(Block(allStatements)));

            // Define P/Invoke declaration
            var dllImport = MethodDeclaration(retMarshaller.Generator.AsNativeType(retMarshaller.TypeInfo), dllImportName)
                .AddModifiers(
                    Token(SyntaxKind.ExternKeyword),
                    Token(SyntaxKind.PrivateKeyword),
                    Token(SyntaxKind.StaticKeyword),
                    Token(SyntaxKind.UnsafeKeyword))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            foreach (var marshaller in paramMarshallers)
            {
                ParameterSyntax paramSyntax = marshaller.Generator.AsParameter(marshaller.TypeInfo);
                dllImport = dllImport.AddParameterListParameters(paramSyntax);
            }

            return (codeBlock, dllImport);

            List<StatementSyntax> GetStatements(Stage stage)
            {
                return stage switch
                {
                    Stage.Setup => setupStatements,
                    Stage.Marshal or Stage.Pin or Stage.Invoke or Stage.KeepAlive or Stage.Unmarshal => tryStatements,
                    Stage.GuaranteedUnmarshal or Stage.Cleanup => finallyStatements,
                    _ => throw new ArgumentOutOfRangeException(nameof(stage))
                };
            }
        }

        public override TypePositionInfo? GetTypePositionInfoForManagedIndex(int index)
        {
            foreach (var info in paramsTypeInfo)
            {
                if (info.ManagedIndex == index)
                {
                    return info;
                }
            }
            return null;
        }

        private void AppendVariableDeclations(List<StatementSyntax> statementsToUpdate, TypePositionInfo info, IMarshallingGenerator generator)
        {
            var (managed, native) = GetIdentifiers(info);

            // Declare variable for return value
            if (info.IsManagedReturnPosition || info.IsNativeReturnPosition)
            {
                statementsToUpdate.Add(MarshallerHelpers.DeclareWithDefault(
                    info.ManagedType.AsTypeSyntax(),
                    managed));
            }

            // Declare variable with native type for parameter or return value
            if (generator.UsesNativeIdentifier(info, this))
            {
                statementsToUpdate.Add(MarshallerHelpers.DeclareWithDefault(
                    generator.AsNativeType(info),
                    native));
            }
        }
    }
}
