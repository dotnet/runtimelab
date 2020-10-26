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
        private readonly ExpressionSyntax _numElementsExpr;

        public NonBlittableArrayMarshaller(IMarshallingGenerator elementMarshaller, ExpressionSyntax numElementsExpr)
        {
            _elementMarshaller = elementMarshaller;
            _numElementsExpr = numElementsExpr;
        }

        private ITypeSymbol GetElementTypeSymbol(TypePositionInfo info)
        {
            return ((IArrayTypeSymbol)info.ManagedType).ElementType;
        }

        private TypeSyntax GetNativeElementTypeSyntax(TypePositionInfo info)
        {
            return _elementMarshaller.AsNativeType(TypePositionInfo.CreateForType(GetElementTypeSymbol(info), null));
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
            return info.IsByRef
                ? Argument(
                    PrefixUnaryExpression(
                        SyntaxKind.AddressOfExpression,
                        IdentifierName(context.GetIdentifiers(info).native)))
                : Argument(IdentifierName(context.GetIdentifiers(info).native));
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
                        var arraySubContext = new ArrayMarshallingCodeContext(context.CurrentStage, indexerIdentifier, context);
                        yield return MarshallerHelpers.GetForLoop(managedIdentifer, indexerIdentifier)
                            .WithStatement(Block(List(_elementMarshaller.Generate(info with { ManagedType = GetElementTypeSymbol(info) }, arraySubContext))));
                    }
                    break;
                case StubCodeContext.Stage.Unmarshal:
                    if (info.IsManagedReturnPosition || (info.IsByRef && info.RefKind != RefKind.In))
                    {
                        // <managedIdentifier> = new <managedElementType>[<numElementsExpression>];
                        yield return ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(managedIdentifer),
                                ArrayCreationExpression(
                                ArrayType(GetElementTypeSymbol(info).AsTypeSyntax(),
                                    SingletonList(ArrayRankSpecifier(
                                        SingletonSeparatedList(_numElementsExpr)))))));

                        string indexerIdentifier = "i";
                        var arraySubContext = new ArrayMarshallingCodeContext(context.CurrentStage, indexerIdentifier, context);
                        yield return MarshallerHelpers.GetForLoop(managedIdentifer, indexerIdentifier)
                            .WithStatement(Block(List(_elementMarshaller.Generate(info with { ManagedType = GetElementTypeSymbol(info) }, arraySubContext))));
                    }
                    break;
                case StubCodeContext.Stage.Cleanup:
                    {
                        yield return GenerateConditionalAllocationFreeSyntax(info, context);
                        string indexerIdentifier = "i";
                        var arraySubContext = new ArrayMarshallingCodeContext(context.CurrentStage, indexerIdentifier, context);
                        var elementCleanup = List(_elementMarshaller.Generate(info with { ManagedType = GetElementTypeSymbol(info) }, arraySubContext));
                        if (elementCleanup.Count != 0)
                        {
                            yield return MarshallerHelpers.GetForLoop(managedIdentifer, indexerIdentifier)
                                .WithStatement(Block(elementCleanup));
                        }
                    }
                    break;
            }
        }

        public override bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return true;
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
