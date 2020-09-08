using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    class BoolMarshaller : IMarshallingGenerator
    {
        public TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return info.NativeType.AsTypeSyntax();
        }

        public ArgumentSyntax AsArgument(TypePositionInfo info)
        {
            string identifier = StubCodeContext.ToNativeIdentifer(info.InstanceIdentifier);
            if (info.IsByRef)
            {
                return Argument(
                    PrefixUnaryExpression(
                        SyntaxKind.AddressOfExpression,
                        IdentifierName(identifier)));
            }

            return Argument(IdentifierName(identifier));
        }

        public ParameterSyntax AsParameter(TypePositionInfo info)
        {
            var type = info.IsByRef
                ? PointerType(info.NativeType.AsTypeSyntax())
                : info.NativeType.AsTypeSyntax();
            return Parameter(Identifier(info.InstanceIdentifier))
                .WithType(type);
        }

        public IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            (string managedIdentifier, string nativeIdentifier) = context.GetIdentifiers(info);
            switch (context.CurrentStage)
            {
                case StubCodeContext.Stage.Setup:
                    if (info.IsReturnType)
                        nativeIdentifier = context.GenerateReturnNativeIdentifier();

                    yield return LocalDeclarationStatement(
                        VariableDeclaration(
                            info.NativeType.AsTypeSyntax(),
                            SingletonSeparatedList(VariableDeclarator(nativeIdentifier))));

                    break;
                case StubCodeContext.Stage.Marshal:
                    // <nativeIdentifier> = (<nativeType>)(<managedIdentifier> ? 1 : 0);
                    if (info.RefKind != RefKind.Out)
                    {
                        yield return ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(nativeIdentifier),
                                CastExpression(
                                    info.NativeType.AsTypeSyntax(),
                                    ParenthesizedExpression(
                                        ConditionalExpression(IdentifierName(managedIdentifier),
                                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)),
                                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)))))));
                    }

                    break;
                case StubCodeContext.Stage.Unmarshal:
                    if (info.IsReturnType || info.IsByRef)
                    {
                        // <managedIdentifier> = <nativeIdentifier> != 0;
                        yield return ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(managedIdentifier),
                                BinaryExpression(
                                    SyntaxKind.NotEqualsExpression,
                                    IdentifierName(nativeIdentifier),
                                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)))));
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
