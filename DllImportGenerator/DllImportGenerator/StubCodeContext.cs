﻿using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    class StubCodeContext
    {
        public enum Stage
        {
            Setup,
            Marshal,
            Pin,
            Invoke,
            Unmarshal,
            Cleanup
        }

        public Stage CurrentStage { get; private set; }

        /// <summary>
        /// Identifier for managed return value
        /// </summary>
        public string ReturnIdentifier => returnIdentifier;

        /// <summary>
        /// Identifier for native return value
        /// </summary>
        /// <remarks>Same as the managed identifier by default</remarks>
        public string ReturnNativeIdentifier { get; private set; } = returnIdentifier;

        private const string returnIdentifier = "__retVal";
        private const string generatedNativeIdentifierSuffix = "_gen_native";

        private StubCodeContext(Stage stage)
        {
            CurrentStage = stage;
        }

        /// <summary>
        /// Generate an identifier for the native return value and update the context with the new value
        /// </summary>
        /// <returns>Identifier for the native return value</returns>
        public string GenerateReturnNativeIdentifier()
        {
            if (CurrentStage != Stage.Setup)
                throw new InvalidOperationException();

            // Update the native identifier for the return value
            ReturnNativeIdentifier = $"{ReturnIdentifier}{generatedNativeIdentifierSuffix}";
            return ReturnNativeIdentifier;
        }

        /// <summary>
        /// Get managed and native instance identifiers for the <paramref name="info"/>
        /// </summary>
        /// <param name="info">Object for which to get identifiers</param>
        /// <returns>Managed and native identifiers</returns>
        public (string managed, string native) GetIdentifiers(TypePositionInfo info)
        {
            string managedIdentifier = info.IsReturnType
                ? ReturnIdentifier
                : info.InstanceIdentifier;

            string nativeIdentifier = info.IsReturnType
                ? ReturnNativeIdentifier
                : ToNativeIdentifer(info.InstanceIdentifier);

            return (managedIdentifier, nativeIdentifier);
        }

        public static string ToNativeIdentifer(string managedIdentifier)
        {
            return $"__{managedIdentifier}{generatedNativeIdentifierSuffix}";
        }

        public static (BlockSyntax Code, MethodDeclarationSyntax DllImport) GenerateSyntax(
            string dllImportName,
            IEnumerable<TypePositionInfo> paramsTypeInfo,
            TypePositionInfo retTypeInfo)
        {
            var paramMarshallers = paramsTypeInfo.Select(p => GetMarshalInfo(p)).ToList();
            var retMarshaller = GetMarshalInfo(retTypeInfo);

            var context = new StubCodeContext(Stage.Setup);
            var statements = new List<StatementSyntax>();

            foreach (var marshaller in paramMarshallers)
            {
                TypePositionInfo info = marshaller.TypeInfo;
                if (info.RefKind != RefKind.Out)
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

            bool returnsVoid = retTypeInfo.ManagedType.SpecialType == SpecialType.System_Void;
            if (!returnsVoid)
            {
                // Declare variable for return value
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(
                        retMarshaller.TypeInfo.ManagedType.AsTypeSyntax(),
                        SingletonSeparatedList(
                            VariableDeclarator(context.ReturnIdentifier)))));
            }

            var stages = new Stage[]
            {
                Stage.Setup,
                Stage.Marshal,
                Stage.Pin,
                Stage.Invoke,
                Stage.Unmarshal,
                Stage.Cleanup
            };

            var invoke = InvocationExpression(IdentifierName(dllImportName));
            var fixedStatements = new List<FixedStatementSyntax>();
            foreach (var stage in stages)
            {
                int initialCount = statements.Count;
                context.CurrentStage = stage;

                if (!returnsVoid && (stage == Stage.Setup || stage == Stage.Unmarshal))
                {
                    // Handle setup and unmarshalling for return
                    var retStatements = retMarshaller.Generator.Generate(retMarshaller.TypeInfo, context);
                    statements.AddRange(retStatements);
                }

                // Generate code for each parameter for the current stage
                foreach (var marshaller in paramMarshallers)
                {
                    if (stage == Stage.Invoke)
                    {
                        // Get arguments for invocation
                        ArgumentSyntax argSyntax = marshaller.Generator.AsArgument(marshaller.TypeInfo);
                        invoke = invoke.AddArgumentListArguments(argSyntax);
                    }
                    else
                    {
                        var generatedStatements = marshaller.Generator.Generate(marshaller.TypeInfo, context);
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
                    if (returnsVoid)
                    {
                        invokeStatement = ExpressionStatement(invoke);
                    }
                    else
                    {
                        invokeStatement = ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(context.ReturnNativeIdentifier),
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
            if (!returnsVoid)
                statements.Add(ReturnStatement(IdentifierName(context.ReturnIdentifier)));

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

        private static (TypePositionInfo TypeInfo, IMarshallingGenerator Generator) GetMarshalInfo(TypePositionInfo info)
        {
            IMarshallingGenerator generator;
            if (!MarshallingGenerators.TryCreate(info, out generator))
            {
                // [TODO] Report warning
            }

            return (info, generator);
        }
    }
}
