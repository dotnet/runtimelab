using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal abstract class ConditionalStackallocMarshallingGenerator : IMarshallingGenerator
    {
        private static string GetAllocationMarkerIdentifier(string managedIdentifier) => $"{managedIdentifier}__allocated";

        private static string GetByteLengthIdentifier(string managedIdentifier) => $"{managedIdentifier}__bytelen";

        private static string GetStackAllocIdentifier(string managedIdentifier) => $"{managedIdentifier}__stackptr";

        protected IEnumerable<StatementSyntax> GenerateConditionalAllocationSyntax(
            TypePositionInfo info, 
            StubCodeContext context,
            int stackallocMaxSize,
            bool checkForNull)
        {
            (string managedIdentifier, string nativeIdentifier) = context.GetIdentifiers(info);
            
            string allocationMarkerIdentifier = GetAllocationMarkerIdentifier(managedIdentifier);
            string byteLenIdentifier = GetByteLengthIdentifier(managedIdentifier);
            string stackAllocPtrIdentifier = GetStackAllocIdentifier(managedIdentifier);
            // <native> = <allocationExpression>;
            var allocationStatement = ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(nativeIdentifier),
                    GenerateAllocationExpression(info, context)));
            if (!context.CanUseAdditionalTemporaryState || (info.IsByRef && info.RefKind != RefKind.In))
            {
                yield return allocationStatement;
            }
            else
            {
                // <allocationMarkerIdentifier> = false;
                yield return LocalDeclarationStatement(
                    VariableDeclaration(
                        PredefinedType(Token(SyntaxKind.BoolKeyword)),
                        SingletonSeparatedList(
                            VariableDeclarator(allocationMarkerIdentifier)
                                .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression))))));

                // Code block for stackalloc if string is below threshold size
                var marshalOnStack = Block(
                    // byte* <stackAllocPtr> = stackalloc byte[<byteLen>];
                    LocalDeclarationStatement(
                        VariableDeclaration(
                            PointerType(PredefinedType(Token(SyntaxKind.ByteKeyword))),
                            SingletonSeparatedList(
                                VariableDeclarator(stackAllocPtrIdentifier)
                                    .WithInitializer(EqualsValueClause(
                                        StackAllocArrayCreationExpression(
                                            ArrayType(
                                                PredefinedType(Token(SyntaxKind.ByteKeyword)),
                                                SingletonList<ArrayRankSpecifierSyntax>(
                                                    ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(
                                                        IdentifierName(byteLenIdentifier))))))))))),
                    GenerateStackallocOnlyValueMarshalling(info, context, Identifier(byteLenIdentifier), Identifier(stackAllocPtrIdentifier)),
                    // <nativeIdentifier> = <stackAllocPtr>;
                    ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(nativeIdentifier),
                            CastExpression(
                                AsNativeType(info),
                                IdentifierName(stackAllocPtrIdentifier)))));

                //   int <byteLenIdentifier> = <byteLengthExpression>;
                yield return LocalDeclarationStatement(
                        VariableDeclaration(
                            PredefinedType(Token(SyntaxKind.IntKeyword)),
                            SingletonSeparatedList<VariableDeclaratorSyntax>(
                                VariableDeclarator(byteLenIdentifier)
                                    .WithInitializer(EqualsValueClause(
                                        GenerateByteLengthCalculationExpression(info, context))))));

                //   if (<byteLen> > <StackAllocBytesThreshold>)
                //   {
                //       <allocationStatement>;
                //   }
                //   else
                //   {
                //       byte* <stackAllocPtr> = stackalloc byte[<byteLen>];
                //       <marshalValueOnStackStatement>;
                //       <native> = (<nativeType>)<stackAllocPtr>;
                //   }
                var allocBlock = IfStatement(
                        BinaryExpression(
                            SyntaxKind.GreaterThanExpression,
                            IdentifierName(byteLenIdentifier),
                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(stackallocMaxSize))),
                        Block(
                            allocationStatement,
                            ExpressionStatement(
                                AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    IdentifierName(allocationMarkerIdentifier),
                                    LiteralExpression(SyntaxKind.TrueLiteralExpression)))),
                        ElseClause(marshalOnStack));

                if (checkForNull)
                {
                    yield return IfStatement(
                                BinaryExpression(
                                    SyntaxKind.EqualsExpression,
                                    IdentifierName(managedIdentifier),
                                    LiteralExpression(SyntaxKind.NullLiteralExpression)),
                                Block(
                                    ExpressionStatement(
                                        AssignmentExpression(
                                            SyntaxKind.SimpleAssignmentExpression,
                                            IdentifierName(nativeIdentifier),
                                            LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                                ElseClause(Block(allocBlock)));
                }
                else
                {
                    yield return allocBlock;
                }
            }
        }

        protected StatementSyntax GenerateConditionalAllocationFreeSyntax(
            TypePositionInfo info,
            StubCodeContext context)
        {
            (string managedIdentifier, string nativeIdentifier) = context.GetIdentifiers(info);
            string allocationMarkerIdentifier = GetAllocationMarkerIdentifier(managedIdentifier);
            if (!context.CanUseAdditionalTemporaryState || (info.IsByRef && info.RefKind != RefKind.In))
            {
                return ExpressionStatement(GenerateFreeExpression(info, context));
            }
            else
            {
                // if (<allocationMarkerIdentifier>)
                // {
                //     <freeExpression>;
                // }
                return IfStatement(
                    IdentifierName(allocationMarkerIdentifier),
                    Block(ExpressionStatement(GenerateFreeExpression(info, context))));
            }
        }

        protected abstract ExpressionSyntax GenerateAllocationExpression(
            TypePositionInfo info,
            StubCodeContext context);

        protected abstract ExpressionSyntax GenerateByteLengthCalculationExpression(
            TypePositionInfo info,
            StubCodeContext context);

        protected abstract StatementSyntax GenerateStackallocOnlyValueMarshalling(
            TypePositionInfo info,
            StubCodeContext context,
            SyntaxToken byteLengthIdentifier,
            SyntaxToken stackAllocPtrIdentifier);

        protected abstract ExpressionSyntax GenerateFreeExpression(
            TypePositionInfo info,
            StubCodeContext context);

        public abstract TypeSyntax AsNativeType(TypePositionInfo info);

        public abstract ParameterSyntax AsParameter(TypePositionInfo info);

        public abstract ArgumentSyntax AsArgument(TypePositionInfo info, StubCodeContext context);

        public abstract IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context);

        public abstract bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context);
    }
}