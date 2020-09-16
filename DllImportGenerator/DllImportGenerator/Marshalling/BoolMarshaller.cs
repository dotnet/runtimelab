using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal abstract class BoolMarshallerBase : IMarshallingGenerator
    {
        private readonly PredefinedTypeSyntax _nativeType;
        private readonly int _trueValue;
        private readonly int _falseValue;

        protected BoolMarshallerBase(PredefinedTypeSyntax nativeType, int trueValue, int falseValue)
        {
            _nativeType = nativeType;
            _trueValue = trueValue;
            _falseValue = falseValue;
        }

        public TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return _nativeType;
        }

        public ParameterSyntax AsParameter(TypePositionInfo info)
        {
            var type = info.IsByRef
                ? PointerType(AsNativeType(info))
                : AsNativeType(info);
            return Parameter(Identifier(info.InstanceIdentifier))
                .WithType(type);
        }

        public ArgumentSyntax AsArgument(TypePositionInfo info, StubCodeContext context)
        {
            string identifier = context.GetIdentifiers(info).native;
            if (info.IsByRef)
            {
                return Argument(
                    PrefixUnaryExpression(
                        SyntaxKind.AddressOfExpression,
                        IdentifierName(identifier)));
            }

            return Argument(IdentifierName(identifier));
        }

        public IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            (string managedIdentifier, string nativeIdentifier) = context.GetIdentifiers(info);
            switch (context.CurrentStage)
            {
                case StubCodeContext.Stage.Setup:
                    if (info.IsManagedReturnPosition)
                        nativeIdentifier = context.GenerateReturnNativeIdentifier();

                    yield return LocalDeclarationStatement(
                        VariableDeclaration(
                            AsNativeType(info),
                            SingletonSeparatedList(VariableDeclarator(nativeIdentifier))));

                    break;
                case StubCodeContext.Stage.Marshal:
                    // <nativeIdentifier> = (<nativeType>)(<managedIdentifier> ? _trueValue : _falseValue);
                    if (info.RefKind != RefKind.Out)
                    {
                        yield return ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(nativeIdentifier),
                                CastExpression(
                                    AsNativeType(info),
                                    ParenthesizedExpression(
                                        ConditionalExpression(IdentifierName(managedIdentifier),
                                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(_trueValue)),
                                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(_falseValue)))))));
                    }

                    break;
                case StubCodeContext.Stage.Unmarshal:
                    if (info.IsManagedReturnPosition || info.IsByRef)
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

    internal class CBoolMarshaller : BoolMarshallerBase
    {
        public CBoolMarshaller()
            : base(PredefinedType(Token(SyntaxKind.ByteKeyword)), 0, 1)
        {
        }
    }

    internal class WinBoolMarshaller : BoolMarshallerBase
    {
        public WinBoolMarshaller()
            : base(PredefinedType(Token(SyntaxKind.IntKeyword)), 0, 1)
        {
        }
    }
    
    internal class VariantBoolMarshaller : BoolMarshallerBase
    {
        public VariantBoolMarshaller()
            : base(PredefinedType(Token(SyntaxKind.ShortKeyword)), -1, 0)
        {
        }
    }
}
