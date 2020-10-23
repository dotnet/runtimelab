using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal static class MarshallerHelpers
    {
        public static ExpressionSyntax GetNumElementsExpressionFromMarshallingInfo(TypePositionInfo info, StubCodeContext context)
        {
            ExpressionSyntax numElementsExpression;
            if (info.MarshallingAttributeInfo is MarshalAsInfo { ArraySizeParamIndex: short index, ArraySizeConst: int constSize })
            {
                LiteralExpressionSyntax? constSizeExpression = constSize != MarshalAsInfo.UnspecifiedData
                    ? LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(constSize))
                    : null;
                ExpressionSyntax? sizeParamIndexExpression = null;
                if (index != MarshalAsInfo.UnspecifiedData)
                {
                    TypePositionInfo? paramIndexInfo = context.GetTypePositionInfoForManagedIndex(index);
                    if (paramIndexInfo is null)
                    {
                        throw new MarshallingNotSupportedException(info, context);
                    }
                    else if (!paramIndexInfo.ManagedType.IsIntegralType())
                    {
                        throw new MarshallingNotSupportedException(info, context);
                    }
                    else
                    {
                        sizeParamIndexExpression = CheckedExpression(SyntaxKind.CheckedExpression,
                            CastExpression(
                                PredefinedType(Token(SyntaxKind.IntKeyword)),
                                IdentifierName(context.GetIdentifiers(paramIndexInfo).native)));
                    }
                }
                numElementsExpression = (constSizeExpression, sizeParamIndexExpression) switch
                {
                    (null, null) => throw new MarshallingNotSupportedException(info, context),
                    (not null, null) => constSizeExpression!,
                    (null, not null) => sizeParamIndexExpression!,
                    (not null, not null) => BinaryExpression(SyntaxKind.AddExpression, constSizeExpression!, sizeParamIndexExpression!)
                };
            }
            else
            {
                throw new MarshallingNotSupportedException(info, context);
            }

            return numElementsExpression;
        }

        public static ForStatementSyntax GetForLoop(string collectionIdentifier, string indexerIdentifier)
        {
            return ForStatement(EmptyStatement())
            .WithDeclaration(
                VariableDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.IntKeyword)))
                .WithVariables(
                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                        VariableDeclarator(
                            Identifier(indexerIdentifier))
                        .WithInitializer(
                            EqualsValueClause(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(0)))))))
            .WithCondition(
                BinaryExpression(
                    SyntaxKind.LessThanExpression,
                    IdentifierName(indexerIdentifier),
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(collectionIdentifier),
                        IdentifierName("Length"))))
            .WithIncrementors(
                SingletonSeparatedList<ExpressionSyntax>(
                    PrefixUnaryExpression(
                        SyntaxKind.PreIncrementExpression,
                        IdentifierName(indexerIdentifier))));
        }
    }
}