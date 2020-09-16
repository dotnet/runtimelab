using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal class BlittableMarshaller : IMarshallingGenerator
    {
        public TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return info.ManagedType.AsTypeSyntax();
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
            if (info.IsByRef)
            {
                if (!context.PinningSupported)
                {
                    return Argument(
                        PrefixUnaryExpression(
                            SyntaxKind.AddressOfExpression,
                            IdentifierName(context.GetIdentifiers(info).native)));
                }
                else
                {
                    return Argument(IdentifierName(context.GetIdentifiers(info).native));
                }
            }

            return Argument(IdentifierName(info.InstanceIdentifier));
        }

        public IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            if (!info.IsByRef || info.IsManagedReturnPosition)
                yield break;

            (string managedIdentifier, string nativeIdentifier) = context.GetIdentifiers(info);

            if (!context.PinningSupported)
            {
                switch (context.CurrentStage)
                {
                    case StubCodeContext.Stage.Setup:
                        yield return LocalDeclarationStatement(
                            VariableDeclaration(
                                AsNativeType(info),
                                SingletonSeparatedList(
                                    VariableDeclarator(nativeIdentifier))));
                        break;
                    case StubCodeContext.Stage.Marshal:
                        if (info.RefKind == RefKind.Ref)
                        {
                            yield return ExpressionStatement(
                                AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    IdentifierName(nativeIdentifier),
                                    IdentifierName(managedIdentifier)));
                        }

                        break;
                    case StubCodeContext.Stage.Unmarshal:
                        yield return ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(managedIdentifier),
                                IdentifierName(nativeIdentifier)));
                        break;
                    default:
                        break;
                }
            }
            else
            {
                if (context.CurrentStage == StubCodeContext.Stage.Pin)
                {
                    yield return FixedStatement(
                        VariableDeclaration(
                            PointerType(AsNativeType(info)),
                            SingletonSeparatedList(
                                VariableDeclarator(Identifier(nativeIdentifier))
                                    .WithInitializer(EqualsValueClause(
                                        PrefixUnaryExpression(SyntaxKind.AddressOfExpression,
                                            IdentifierName(managedIdentifier))
                                    ))
                            )
                        ),
                        EmptyStatement()
                    );
                }
            }
        }
    }

}
