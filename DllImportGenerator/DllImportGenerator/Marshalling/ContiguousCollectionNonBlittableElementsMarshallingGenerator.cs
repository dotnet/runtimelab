using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    class ContiguousCollectionNonBlittableElementsMarshallingGenerator : CustomNativeTypeMarshaller
    {
        private const string IndexerIdentifier = "__i";
        private readonly IMarshallingGenerator elementMarshaller;
        private readonly TypePositionInfo elementInfo;
        private readonly ExpressionSyntax numElementsExpression;

        public ContiguousCollectionNonBlittableElementsMarshallingGenerator(
            NativeContiguousCollectionMarshallingInfo marshallingInfo,
            IMarshallingGenerator elementMarshaller,
            TypePositionInfo elementInfo,
            ExpressionSyntax numElementsExpression)
            :base(marshallingInfo)
        {
            this.elementMarshaller = elementMarshaller;
            this.elementInfo = elementInfo;
            this.numElementsExpression = numElementsExpression;
        }

        private ExpressionSyntax GenerateSizeOfElementExpression()
        {
            return SizeOfExpression(elementMarshaller.AsNativeType(elementInfo));
        }

        public override IEnumerable<ArgumentSyntax> GenerateAdditionalNativeTypeConstructorArguments(TypePositionInfo info, StubCodeContext context)
        {
            yield return Argument(GenerateSizeOfElementExpression());
        }

        private string GetNativeSpanIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return context.GetIdentifiers(info).managed + "__nativeSpan";
        }

        private LocalDeclarationStatementSyntax GenerateNativeSpanDeclaration(TypePositionInfo info, StubCodeContext context)
        {            
            string nativeSpanIdentifier = GetNativeSpanIdentifier(info, context);
            return LocalDeclarationStatement(VariableDeclaration(
                GenericName(
                    Identifier(TypeNames.System_Span),
                    TypeArgumentList(
                        SingletonSeparatedList(elementMarshaller.AsNativeType(elementInfo).GetCompatibleGenericTypeParameterSyntax()))
                ),
                SingletonSeparatedList(
                    VariableDeclarator(Identifier(nativeSpanIdentifier))
                    .WithInitializer(EqualsValueClause(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName(TypeNames.System_Runtime_InteropServices_MemoryMarshal),
                                GenericName(
                                    Identifier("Cast"))
                                .WithTypeArgumentList(
                                    TypeArgumentList(
                                        SeparatedList(
                                            new []
                                            {
                                                PredefinedType(Token(SyntaxKind.ByteKeyword)),
                                                elementMarshaller.AsNativeType(elementInfo).GetCompatibleGenericTypeParameterSyntax()
                                            })))))
                        .AddArgumentListArguments(
                            Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(GetMarshallerIdentifier(info, context)),
                                IdentifierName("NativeValueStorage")))))))));
        }

        private StatementSyntax GenerateContentsMarshallingStatement(TypePositionInfo info, StubCodeContext context, bool useManagedSpanForLength)
        {
            string marshalerIdentifier = GetMarshallerIdentifier(info, context);
            string nativeSpanIdentifier = GetNativeSpanIdentifier(info, context);
            var elementSubContext = new ContiguousCollectionElementMarshallingCodeContext(
                context.CurrentStage,
                IndexerIdentifier,
                nativeSpanIdentifier,
                context);

            string collectionIdentifierForLength = useManagedSpanForLength
                ? $"{marshalerIdentifier}.ManagedValues"
                : nativeSpanIdentifier;

            TypePositionInfo localElementInfo = elementInfo with
            {
                InstanceIdentifier = info.InstanceIdentifier,
                RefKind = info.IsByRef ? info.RefKind : info.ByValueContentsMarshalKind.GetRefKindForByValueContentsKind(),
                ManagedIndex = info.ManagedIndex,
                NativeIndex = info.NativeIndex
            };

            StatementSyntax marshallingStatement = Block(
                List(elementMarshaller.Generate(
                    localElementInfo,
                    elementSubContext)));

            if (elementMarshaller.AsNativeType(elementInfo) is PointerTypeSyntax)
            {
                PointerNativeTypeAssignmentRewriter rewriter = new(elementSubContext.GetIdentifiers(localElementInfo).native);
                marshallingStatement = (StatementSyntax)rewriter.Visit(marshallingStatement);
            }

            // Iterate through the elements of the native collection to unmarshal them
            return Block(
                GenerateNativeSpanDeclaration(info, context),
                MarshallerHelpers.GetForLoop(collectionIdentifierForLength, IndexerIdentifier)
                                .WithStatement(marshallingStatement));
        }

        public override IEnumerable<StatementSyntax> GenerateIntermediateMarshallingStatements(TypePositionInfo info, StubCodeContext context)
        {
            yield return GenerateContentsMarshallingStatement(info, context, useManagedSpanForLength: true);
        }

        public override IEnumerable<StatementSyntax> GeneratePreUnmarshallingStatements(TypePositionInfo info, StubCodeContext context)
        {
            string marshalerIdentifier = GetMarshallerIdentifier(info, context);
            if (info.RefKind == RefKind.Out || info.IsManagedReturnPosition)
            {
                yield return ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(marshalerIdentifier),
                    ImplicitObjectCreationExpression().AddArgumentListArguments(Argument(GenerateSizeOfElementExpression()))));
            }
            yield return ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(marshalerIdentifier),
                        IdentifierName("SetUnmarshalledCollectionLength")))
                .AddArgumentListArguments(Argument(numElementsExpression)));
        }

        public override IEnumerable<StatementSyntax> GenerateIntermediateUnmarshallingStatements(TypePositionInfo info, StubCodeContext context)
        {
            yield return GenerateContentsMarshallingStatement(info, context, useManagedSpanForLength: false);
        }

        public override IEnumerable<StatementSyntax> GenerateIntermediateCleanupStatements(TypePositionInfo info, StubCodeContext context)
        {
            yield return GenerateContentsMarshallingStatement(info, context, useManagedSpanForLength: false);
        }

        /// <summary>
        /// Rewrite assignment expressions to the native identifier to cast to IntPtr.
        /// This handles the case where the native type of a non-blittable managed type is a pointer,
        /// which are unsupported in generic type parameters.
        /// </summary>
        private class PointerNativeTypeAssignmentRewriter : CSharpSyntaxRewriter
        {
            private readonly string nativeIdentifier;

            public PointerNativeTypeAssignmentRewriter(string nativeIdentifier)
            {
                this.nativeIdentifier = nativeIdentifier;
            }

            public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
            {
                if (node.Left.ToString() == nativeIdentifier)
                {
                    return node.WithRight(
                        CastExpression(ParseTypeName("System.IntPtr"), node.Right));
                }

                return node;
            }
        }
    }
}