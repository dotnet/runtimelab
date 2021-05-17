using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Interop.SourceGeneration;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    /// <summary>
    /// Base code generator for generating the body of a source-generated P/Invoke and providing customization for how to invoke/define the native method.
    /// </summary>
    /// <remarks>
    /// This type enables multiple code generators for P/Invoke-style marshalling
    /// to reuse the same basic method body, but with different designs of how to emit the target native method.
    /// This enables users to write code generators that work with slightly different semantics.
    /// For example, the source generator for [GeneratedDllImport] emits the target P/Invoke as
    /// a local function inside the generated stub body.
    /// However, other managed-to-native code generators using a P/Invoke style might want to define
    /// the target DllImport outside of the stub as a static non-local function or as a function pointer field.
    /// This refactoring allows the code generator to have control over where the target method is declared
    /// and how it is declared.
    /// </remarks>
    internal sealed class PInvokeStubCodeGenerator : StubCodeContext
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

        private readonly IGeneratorDiagnostics diagnostics;
        private readonly IMethodSymbol stubMethod;
        private readonly bool setLastError;
        private readonly IEnumerable<TypePositionInfo> paramsTypeInfo;
        private readonly IReadOnlyList<(TypePositionInfo TypeInfo, IMarshallingGenerator Generator)> paramMarshallers;
        private readonly (TypePositionInfo TypeInfo, IMarshallingGenerator Generator) retMarshaller;

        public PInvokeStubCodeGenerator(
            IMethodSymbol stubMethod,
            IEnumerable<TypePositionInfo> paramsTypeInfo,
            TypePositionInfo retTypeInfo,
            IGeneratorDiagnostics generatorDiagnostics,
            bool setLastError,
            IMarshallingGeneratorFactory generatorFactory)
        {
            Debug.Assert(retTypeInfo.IsNativeReturnPosition);

            this.stubMethod = stubMethod;
            this.setLastError = setLastError;
            this.paramsTypeInfo = paramsTypeInfo.ToList();
            this.diagnostics = generatorDiagnostics;

            // Get marshallers for parameters
            this.paramMarshallers = paramsTypeInfo.Select(p => CreateGenerator(p)).ToList();

            // Get marshaller for return
            this.retMarshaller = CreateGenerator(retTypeInfo);

            (TypePositionInfo info, IMarshallingGenerator gen) CreateGenerator(TypePositionInfo p)
            {
                try
                {
                    return (p, generatorFactory.Create(p, this));
                }
                catch (MarshallingNotSupportedException e)
                {
                    this.diagnostics.ReportMarshallingNotSupported(this.stubMethod, p, e.NotSupportedDetails);
                    return (p, new Forwarder());
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

        public BlockSyntax GeneratePInvokeBody(ExpressionSyntax targetMethodExpression)
        {
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
            if (this.setLastError)
            {
                // Declare variable for last error
                setupStatements.Add(MarshallerHelpers.DeclareWithDefault(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    LastErrorIdentifier));
            }

            var tryStatements = new List<StatementSyntax>();
            var finallyStatements = new List<StatementSyntax>();
            var invoke = InvocationExpression(targetMethodExpression);
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
                    if (this.setLastError)
                    {
                        // Marshal.SetLastSystemError(0);
                        var clearLastError = ExpressionStatement(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ParseName(TypeNames.System_Runtime_InteropServices_Marshal),
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
                                    ParseName(TypeNames.System_Runtime_InteropServices_Marshal),
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

            if (this.setLastError)
            {
                // Marshal.SetLastPInvokeError(<lastError>);
                allStatements.Add(ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            ParseName(TypeNames.System_Runtime_InteropServices_Marshal),
                            IdentifierName("SetLastPInvokeError")),
                        ArgumentList(SingletonSeparatedList(
                            Argument(IdentifierName(LastErrorIdentifier)))))));
            }

            // Return
            if (!stubReturnsVoid)
                allStatements.Add(ReturnStatement(IdentifierName(ReturnIdentifier)));

            // Wrap all statements in an unsafe block
            return Block(UnsafeStatement(Block(allStatements)));

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

        public (ParameterListSyntax ParameterList, TypeSyntax ReturnType) GenerateTargetMethodSignatureData()
        {
            return (
                ParameterList(
                    SeparatedList(
                        paramMarshallers.Select(marshaler => marshaler.Generator.AsParameter(marshaler.TypeInfo)))),
                retMarshaller.Generator.AsNativeType(retMarshaller.TypeInfo)
            );
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
