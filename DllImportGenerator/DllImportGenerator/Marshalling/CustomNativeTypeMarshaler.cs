using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

#nullable enable

namespace Microsoft.Interop
{
    class CustomNativeTypeMarshaler : IMarshallingGenerator
    {
        private const string MarshalerLocalSuffix = "__marshaler";
        private readonly TypeSyntax _nativeTypeSyntax;
        private readonly TypeSyntax _nativeLocalTypeSyntax;
        private readonly SupportedMarshallingMethods _marshallingMethods;
        private bool _hasFreeNative;
        private bool _useValueProperty;
        public CustomNativeTypeMarshaler(NativeMarshallingAttributeInfo marshallingInfo)
        {
            ITypeSymbol nativeType = marshallingInfo.ValuePropertyType ?? marshallingInfo.NativeMarshallingType;
            _nativeTypeSyntax = ParseTypeName(nativeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            _nativeLocalTypeSyntax = ParseTypeName(marshallingInfo.NativeMarshallingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            _marshallingMethods = marshallingInfo.MarshallingMethods;
            _hasFreeNative = ManualTypeMarshallingHelper.HasFreeNativeMethod(marshallingInfo.NativeMarshallingType);
            _useValueProperty = marshallingInfo.ValuePropertyType != null;
        }

        public CustomNativeTypeMarshaler(GeneratedNativeMarshallingAttributeInfo marshallingInfo)
        {
            _nativeTypeSyntax = _nativeLocalTypeSyntax = ParseTypeName(marshallingInfo.NativeMarshallingFullyQualifiedTypeName);
            _marshallingMethods = SupportedMarshallingMethods.ManagedToNative | SupportedMarshallingMethods.NativeToManaged;
            _hasFreeNative = true;
            _useValueProperty = false;
        }

        public TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return _nativeTypeSyntax;
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

            if (context.PinningSupported && (_marshallingMethods & SupportedMarshallingMethods.Pinning) != 0)
            {
                return Argument(CastExpression(AsNativeType(info), IdentifierName(identifier)));
            }

            return Argument(IdentifierName(identifier));
        }

        public IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            (string managedIdentifier, string nativeIdentifier) = context.GetIdentifiers(info);
            string marshalerIdentifier = _useValueProperty ? nativeIdentifier + MarshalerLocalSuffix  : nativeIdentifier;
            if (!info.IsManagedReturnPosition &&
                !info.IsByRef &&
                context.PinningSupported &&
                (_marshallingMethods & SupportedMarshallingMethods.Pinning) != 0)
            {
                if (context.CurrentStage == StubCodeContext.Stage.Pin)
                {
                    yield return FixedStatement(
                        VariableDeclaration(
                            PointerType(PredefinedType(Token(SyntaxKind.VoidKeyword))),
                            SingletonSeparatedList(
                                VariableDeclarator(Identifier(nativeIdentifier))
                                    .WithInitializer(EqualsValueClause(
                                        IdentifierName(managedIdentifier)
                                    ))
                            )
                        ),
                        EmptyStatement()
                    );
                }
                yield break;
            }
            switch (context.CurrentStage)
            {
                case StubCodeContext.Stage.Setup:
                    yield return LocalDeclarationStatement(
                        VariableDeclaration(
                            _nativeTypeSyntax,
                            SingletonSeparatedList(
                                VariableDeclarator(nativeIdentifier))));
                    if (_useValueProperty)
                    {
                        yield return LocalDeclarationStatement(
                            VariableDeclaration(
                                _nativeLocalTypeSyntax,
                                SingletonSeparatedList(
                                    VariableDeclarator(marshalerIdentifier)
                                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.DefaultLiteralExpression))))));
                    }
                    break;
                case StubCodeContext.Stage.Marshal:
                    if (info.RefKind != RefKind.Out)
                    {
                        yield return ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(marshalerIdentifier),
                                ObjectCreationExpression(_nativeLocalTypeSyntax)
                                    .WithArgumentList(ArgumentList(
                                        context.StackSpaceUsable && (_marshallingMethods & SupportedMarshallingMethods.ManagedToNativeStackalloc) != 0
                                        ? SeparatedList(
                                            new [] 
                                            {
                                            Argument(IdentifierName(managedIdentifier)),
                                            Argument(
                                                StackAllocArrayCreationExpression(
                                                    ArrayType(
                                                        PredefinedType(Token(SyntaxKind.ByteKeyword)),
                                                        SingletonList(ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(
                                                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                                                _nativeLocalTypeSyntax,
                                                                IdentifierName(ManualTypeMarshallingHelper.StackBufferSizeFieldName))
                                                        ))))))
                                            })
                                        : SingletonSeparatedList(Argument(IdentifierName(managedIdentifier)))
                                    )
                                )
                            )
                        );

                        if (_useValueProperty)
                        {
                            yield return ExpressionStatement(
                                AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    IdentifierName(nativeIdentifier),
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(marshalerIdentifier),
                                        IdentifierName(ManualTypeMarshallingHelper.ValuePropertyName))
                                )
                            );
                        }
                    }
                    break;
                case StubCodeContext.Stage.Unmarshal:
                    if (info.IsManagedReturnPosition || (info.IsByRef && info.RefKind != RefKind.In))
                    {
                        if (_useValueProperty)
                        {
                            yield return ExpressionStatement(
                                AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(marshalerIdentifier),
                                        IdentifierName(ManualTypeMarshallingHelper.ValuePropertyName)),
                                    IdentifierName(nativeIdentifier)
                                )
                            );
                        }
                        yield return ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(managedIdentifier),
                                InvocationExpression(
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(marshalerIdentifier),
                                        IdentifierName(ManualTypeMarshallingHelper.ToManagedMethodName)))));
                    }
                    break;
                case StubCodeContext.Stage.Cleanup:
                    if (info.RefKind != RefKind.Out && _hasFreeNative)
                    {
                        yield return ExpressionStatement(
                            InvocationExpression(
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(marshalerIdentifier),
                                        IdentifierName(ManualTypeMarshallingHelper.FreeNativeMethodName)))
                        );
                    }
                    break;
                // TODO: Determine how to keep alive delegates that are in struct fields.
                default:
                    break;
            }
        }

        public bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return info.IsManagedReturnPosition || !(context.PinningSupported && (_marshallingMethods & SupportedMarshallingMethods.Pinning) != 0);
        }
    }
}
