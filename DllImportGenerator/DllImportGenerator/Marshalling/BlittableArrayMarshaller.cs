using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal class BlittableArrayMarshaller : ConditionalStackallocMarshallingGenerator
    {
        private const int StackAllocBytesThreshold = 0x200;

        private TypeSyntax GetElementTypeSyntax(TypePositionInfo info)
        {
            return ((IArrayTypeSymbol)info.ManagedType).ElementType.AsTypeSyntax();
        }

        public override TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return PointerType(GetElementTypeSyntax(info));
        }

        public override ParameterSyntax AsParameter(TypePositionInfo info)
        {
            var type = info.IsByRef
                ? PointerType(AsNativeType(info))
                : AsNativeType(info);
            return Parameter(Identifier(info.InstanceIdentifier))
                .WithType(type);
        }

        public override ArgumentSyntax AsArgument(TypePositionInfo info, StubCodeContext context)
        {
            return info.IsByRef ?
                Argument(IdentifierName(context.GetIdentifiers(info).native))
                : Argument(
                    PrefixUnaryExpression(
                        SyntaxKind.AddressOfExpression,
                        IdentifierName(context.GetIdentifiers(info).native)));
        }

        public override IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            var (managedIdentifer, nativeIdentifier) = context.GetIdentifiers(info);
            if (!info.IsByRef && context.PinningSupported)
            {
                if (context.CurrentStage == StubCodeContext.Stage.Pin)
                {
                    // fixed (<nativeType> <nativeIdentifier> = &MemoryMarshal.GetArrayDataReference(<managedIdentifer>))
                    yield return FixedStatement(
                        VariableDeclaration(AsNativeType(info), SingletonSeparatedList(
                            VariableDeclarator(nativeIdentifier)
                                .WithInitializer(EqualsValueClause(
                                    PrefixUnaryExpression(SyntaxKind.AddressOfExpression,
                                        InvocationExpression(
                                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                                ParseTypeName(TypeNames.System_Runtime_InteropServices_MemoryMarshal),
                                                IdentifierName("GetArrayDataReference")),
                                                ArgumentList(
                                                    SingletonSeparatedList(Argument(IdentifierName(managedIdentifer)))
                                                ))))))),
                        EmptyStatement());
                }
                yield break;
            }

            switch (context.CurrentStage)
            {
                case StubCodeContext.Stage.Setup:
                    yield return LocalDeclarationStatement(
                        VariableDeclaration(
                            AsNativeType(info),
                            SingletonSeparatedList(VariableDeclarator(nativeIdentifier))));
                    break;
                case StubCodeContext.Stage.Marshal:
                    if (info.RefKind != RefKind.Out)
                    {
                        foreach (var statement in GenerateConditionalAllocationSyntax(
                            info,
                            context,
                            StackAllocBytesThreshold,
                            checkForNull: true))
                        {
                            yield return statement;
                        }

                        // managedIdentifier.AsSpan().CopyTo(new Span<T>(nativeIdentifier, managedIdentifier.Length));
                        yield return ExpressionStatement(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName(managedIdentifer),
                                            IdentifierName("AsSpan"))),
                                    IdentifierName("CopyTo")))
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            ObjectCreationExpression(
                                                GenericName(TypeNames.System_Span)
                                                .WithTypeArgumentList(
                                                    TypeArgumentList(
                                                        SingletonSeparatedList(
                                                            GetElementTypeSyntax(info)))))
                                            .WithArgumentList(
                                                ArgumentList(
                                                    SeparatedList(
                                                        new []{
                                                            Argument(
                                                                IdentifierName(nativeIdentifier)),
                                                            Argument(
                                                                MemberAccessExpression(
                                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                                    IdentifierName(managedIdentifer),
                                                                    IdentifierName("Length")))}))))))));
                    }
                    break;
                case StubCodeContext.Stage.Unmarshal:
                    if (info.IsManagedReturnPosition || (info.IsByRef && info.RefKind != RefKind.In))
                    {
                        // <managedIdentifier> = new <managedElementType>[<numElementsExpression>];
                        yield return ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(managedIdentifer),
                                ArrayCreationExpression(
                                ArrayType(GetElementTypeSyntax(info),
                                    SingletonList(ArrayRankSpecifier(
                                        SingletonSeparatedList(
                                            MarshallerHelpers.GetNumElementsExpressionFromMarshallingInfo(info, context))))))));

                        // new Span<T>(nativeIdentifier, managedIdentifier.Length).CopyTo(managedIdentifier);
                        yield return ExpressionStatement(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ObjectCreationExpression(
                                                GenericName(TypeNames.System_Span)
                                                .WithTypeArgumentList(
                                                    TypeArgumentList(
                                                        SingletonSeparatedList(
                                                            GetElementTypeSyntax(info)))))
                                            .WithArgumentList(
                                                ArgumentList(
                                                    SeparatedList(
                                                        new[]{
                                                            Argument(
                                                                IdentifierName(nativeIdentifier)),
                                                            Argument(
                                                                MemberAccessExpression(
                                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                                    IdentifierName(managedIdentifer),
                                                                    IdentifierName("Length")))}))),
                                    IdentifierName("CopyTo")))
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(IdentifierName(managedIdentifer))))));
                    }
                    break;
                case StubCodeContext.Stage.Cleanup:
                    yield return GenerateConditionalAllocationFreeSyntax(info, context);
                    break;
            }
        }

        public override bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return (info.IsByRef && !info.IsManagedReturnPosition) || !context.PinningSupported;
        }

        protected override ExpressionSyntax GenerateAllocationExpression(TypePositionInfo info, StubCodeContext context, SyntaxToken byteLengthIdentifier, out bool allocationRequiresByteLength)
        {
            allocationRequiresByteLength = true;
            // (<nativeType>)Marshal.AllocCoTaskMem(<byteLengthIdentifier>)
            return CastExpression(AsNativeType(info),
                InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        ParseTypeName(TypeNames.System_Runtime_InteropServices_Marshal),
                        IdentifierName("AllocCoTaskMem")),
                    ArgumentList(SingletonSeparatedList(Argument(IdentifierName(byteLengthIdentifier))))));
        }

        protected override ExpressionSyntax GenerateByteLengthCalculationExpression(TypePositionInfo info, StubCodeContext context)
        {
            // sizeof(<nativeElementType>) * <managedIdentifier>.Length
            return BinaryExpression(SyntaxKind.MultiplyExpression,
                SizeOfExpression(GetElementTypeSyntax(info)),
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(context.GetIdentifiers(info).managed),
                    IdentifierName("Length")
                ));
        }

        protected override StatementSyntax GenerateStackallocOnlyValueMarshalling(TypePositionInfo info, StubCodeContext context, SyntaxToken byteLengthIdentifier, SyntaxToken stackAllocPtrIdentifier)
        {
            return EmptyStatement();
        }

        protected override ExpressionSyntax GenerateFreeExpression(TypePositionInfo info, StubCodeContext context)
        {
            // Marshal.FreeCoTaskMem((IntPtr)<nativeIdentifier>)
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ParseTypeName(TypeNames.System_Runtime_InteropServices_Marshal),
                    IdentifierName("FreeCoTaskMem")),
                ArgumentList(SingletonSeparatedList(
                    Argument(
                        CastExpression(
                            ParseTypeName("System.IntPtr"),
                            IdentifierName(context.GetIdentifiers(info).native))))));
        }
    }

}
