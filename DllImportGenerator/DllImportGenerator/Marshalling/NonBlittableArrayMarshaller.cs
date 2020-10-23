using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal class NonBlittableArrayMarshaller : ConditionalStackallocMarshallingGenerator
    {
        private const int StackAllocBytesThreshold = 0x200;

        private IMarshallingGenerator _elementMarshaller;
        public NonBlittableArrayMarshaller(IMarshallingGenerator elementMarshaller)
        {
            _elementMarshaller = elementMarshaller;
        }

        private TypeSyntax GetNativeElementTypeSyntax(TypePositionInfo info)
        {
            return _elementMarshaller.AsNativeType(TypePositionInfo.CreateForType(((IArrayTypeSymbol)info.ManagedType).ElementType, null));
        }

        public override TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return PointerType(GetNativeElementTypeSyntax(info));
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

                        string indexerIdentifier = "i";
                        var arraySubContext = new ArrayMarshallingCodeContext(context.CurrentStage, indexerIdentifier);
                        yield return MarshallerHelpers.GetForLoop(managedIdentifer, indexerIdentifier)
                            .WithStatement(Block(List(_elementMarshaller.Generate(info, arraySubContext))));
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
                                ArrayType(((IArrayTypeSymbol)info.ManagedType).ElementType.AsTypeSyntax(),
                                    SingletonList(ArrayRankSpecifier(
                                        SingletonSeparatedList(
                                            MarshallerHelpers.GetNumElementsExpressionFromMarshallingInfo(info, context))))))));

                        string indexerIdentifier = "i";
                        var arraySubContext = new ArrayMarshallingCodeContext(context.CurrentStage, indexerIdentifier);
                        yield return MarshallerHelpers.GetForLoop(managedIdentifer, indexerIdentifier)
                            .WithStatement(Block(List(_elementMarshaller.Generate(info, arraySubContext))));
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
            // (<nativeType>*)Marshal.AllocCoTaskMem(<byteLengthIdentifier>)
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
                SizeOfExpression(GetNativeElementTypeSyntax(info)),
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
