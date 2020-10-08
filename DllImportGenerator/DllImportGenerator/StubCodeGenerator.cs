﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal sealed class StubCodeGenerator : StubCodeContext
    {
        public override bool PinningSupported => true;

        public override bool StackSpaceUsable => true;

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

        public IEnumerable<Diagnostic> Diagnostics => diagnostics;
        private readonly List<Diagnostic> diagnostics = new List<Diagnostic>();

        private readonly IMethodSymbol stubMethod;
        private readonly List<(TypePositionInfo TypeInfo, IMarshallingGenerator Generator)> paramMarshallers;
        private readonly (TypePositionInfo TypeInfo, IMarshallingGenerator Generator) retMarshaller;

        public StubCodeGenerator(
            IMethodSymbol stubMethod,
            IEnumerable<TypePositionInfo> paramsTypeInfo,
            TypePositionInfo retTypeInfo)
        {
            Debug.Assert(retTypeInfo.IsNativeReturnPosition);

            this.CurrentStage = Stages[0];
            this.stubMethod = stubMethod;

            // Get marshallers for parameters
            this.paramMarshallers = paramsTypeInfo.Select(p =>
            {
                IMarshallingGenerator generator;
                if (!MarshallingGenerators.TryCreate(p, this, out generator))
                {
                    IParameterSymbol paramSymbol = stubMethod.Parameters[p.ManagedIndex];
                    this.diagnostics.Add(
                        paramSymbol.CreateDiagnostic(
                            GeneratorDiagnostics.ParameterTypeNotSupported,
                            paramSymbol.Type.ToDisplayString(),
                            paramSymbol.Name));
                }

                return (p, generator);
            }).ToList();

            // Get marshaller for return
            IMarshallingGenerator retGenerator;
            if (!MarshallingGenerators.TryCreate(retTypeInfo, this, out retGenerator))
            {
                this.diagnostics.Add(
                    stubMethod.CreateDiagnostic(
                        GeneratorDiagnostics.ReturnTypeNotSupported,
                        stubMethod.ReturnType.ToDisplayString(),
                        stubMethod.Name));
            }

            this.retMarshaller = (retTypeInfo, retGenerator);
        }

        /// <summary>
        /// Generate an identifier for the native return value and update the context with the new value
        /// </summary>
        /// <returns>Identifier for the native return value</returns>
        public void GenerateReturnNativeIdentifier()
        {
            if (CurrentStage != Stage.Setup)
                throw new InvalidOperationException();

            // Update the native identifier for the return value
            ReturnNativeIdentifier = $"{ReturnIdentifier}{GeneratedNativeIdentifierSuffix}";
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
            this.CurrentStage = Stages[0];
            string dllImportName = stubMethod.Name + "__PInvoke__";
            var statements = new List<StatementSyntax>();

            if (retMarshaller.Generator.UsesNativeIdentifier(retMarshaller.TypeInfo, this))
            {
                this.GenerateReturnNativeIdentifier();
            }

            foreach (var marshaller in paramMarshallers)
            {
                TypePositionInfo info = marshaller.TypeInfo;
                if (info.RefKind != RefKind.Out || info.IsManagedReturnPosition)
                    continue;

                // Assign out params to default
                statements.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(info.InstanceIdentifier),
                        LiteralExpression(
                            SyntaxKind.DefaultLiteralExpression,
                            Token(SyntaxKind.DefaultKeyword)))));
            }

            bool invokeReturnsVoid = retMarshaller.TypeInfo.ManagedType.SpecialType == SpecialType.System_Void;
            bool stubReturnsVoid = stubMethod.ReturnsVoid;

            // Stub return is not the same as invoke return
            if (!stubReturnsVoid && !retMarshaller.TypeInfo.IsManagedReturnPosition)
            {
                Debug.Assert(paramMarshallers.Any() && paramMarshallers.Last().TypeInfo.IsManagedReturnPosition);

                // Declare variable for stub return value
                TypePositionInfo info = paramMarshallers.Last().TypeInfo;
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(
                        info.ManagedType.AsTypeSyntax(),
                        SingletonSeparatedList(
                            VariableDeclarator(this.GetIdentifiers(info).managed)))));
            }

            if (!invokeReturnsVoid)
            {
                // Declare variable for invoke return value
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(
                        retMarshaller.TypeInfo.ManagedType.AsTypeSyntax(),
                        SingletonSeparatedList(
                            VariableDeclarator(this.GetIdentifiers(retMarshaller.TypeInfo).managed)))));
            }

            var invoke = InvocationExpression(IdentifierName(dllImportName));
            var fixedStatements = new List<FixedStatementSyntax>();
            foreach (var stage in Stages)
            {
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

                    // Nest invocation in fixed statements
                    if (fixedStatements.Any())
                    {
                        fixedStatements.Reverse();
                        invokeStatement = fixedStatements.First().WithStatement(Block(invokeStatement));
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

            // Return
            if (!stubReturnsVoid)
                statements.Add(ReturnStatement(IdentifierName(ReturnIdentifier)));

            // Wrap all statements in an unsafe block
            var codeBlock = Block(UnsafeStatement(Block(statements)));

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
        }
    }
}
