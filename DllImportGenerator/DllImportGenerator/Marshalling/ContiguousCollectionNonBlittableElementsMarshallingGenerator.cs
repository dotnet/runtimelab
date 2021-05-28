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

        public override IEnumerable<ArgumentSyntax> GenerateAdditionalNativeTypeConstructorArguments(TypePositionInfo info, StubCodeContext context)
        {
            yield return Argument(SizeOfExpression(elementMarshaller.AsNativeType(elementInfo)));
        }

        private string GetNativeSpanIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return context.GetIdentifiers(info).managed + "__nativeSpan";
        }

        private LocalDeclarationStatementSyntax GenerateNativeSpanDeclaration(string nativeSpanIdentifier)
        {
            return LocalDeclarationStatement(VariableDeclaration(
                GenericName(
                    Identifier(TypeNames.System_Span),
                    TypeArgumentList(
                        SingletonSeparatedList<TypeSyntax>(PredefinedType(Token(SyntaxKind.ByteKeyword))))
                ),
                SingletonSeparatedList(
                    VariableDeclarator(Identifier(nativeSpanIdentifier))
                    .WithInitializer(EqualsValueClause(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("MemoryMarshal"),
                                GenericName(
                                    Identifier("Cast"))
                                .WithTypeArgumentList(
                                    TypeArgumentList(
                                        SeparatedList(
                                            new []
                                            {
                                                PredefinedType(Token(SyntaxKind.ByteKeyword)),
                                                elementMarshaller.AsNativeType(elementInfo)
                                            }))))))))));
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

            // Iterate through the elements of the native collection to unmarshal them
            return Block(
                GenerateNativeSpanDeclaration(GetNativeSpanIdentifier(info, context)),
                MarshallerHelpers.GetForLoop(collectionIdentifierForLength, IndexerIdentifier)
                                .WithStatement(Block(
                                    List(elementMarshaller.Generate(
                                        elementInfo,
                                        elementSubContext)))));
        }

        public override IEnumerable<StatementSyntax> GenerateIntermediateMarshallingStatements(TypePositionInfo info, StubCodeContext context)
        {
            yield return GenerateContentsMarshallingStatement(info, context, useManagedSpanForLength: true);
        }

        public override IEnumerable<StatementSyntax> GeneratePreUnmarshallingStatements(TypePositionInfo info, StubCodeContext context)
        {
            string marshalerIdentifier = GetMarshallerIdentifier(info, context);
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
    }
}