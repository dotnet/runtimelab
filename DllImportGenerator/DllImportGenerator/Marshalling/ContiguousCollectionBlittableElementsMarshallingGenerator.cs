using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    class ContiguousCollectionBlittableElementsMarshallingGenerator : CustomNativeTypeMarshaller
    {
        private readonly ITypeSymbol elementType;
        private readonly ExpressionSyntax numElementsExpression;

        public ContiguousCollectionBlittableElementsMarshallingGenerator(
            NativeContiguousCollectionMarshallingInfo marshallingInfo,
            ExpressionSyntax numElementsExpression)
            :base(marshallingInfo)
        {
            this.elementType = marshallingInfo.ElementType;
            this.numElementsExpression = numElementsExpression;
        }

        public override IEnumerable<ArgumentSyntax> GenerateAdditionalNativeTypeConstructorArguments(TypePositionInfo info, StubCodeContext context)
        {
            yield return Argument(SizeOfExpression(elementType.AsTypeSyntax()));
        }

        public override IEnumerable<StatementSyntax> GenerateIntermediateMarshallingStatements(TypePositionInfo info, StubCodeContext context)
        {
            string marshalerIdentifier = GetMarshallerIdentifier(info, context);
            // <marshalerIdentifier>.ManagedValues.CopyTo(MemoryMarshal.Cast<byte, <elementType>>(<marshalerIdentifier.NativeValueStorage));
            yield return ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(marshalerIdentifier),
                            IdentifierName("ManagedValues")),
                        IdentifierName("CopyTo")))
                .AddArgumentListArguments(
                    Argument(
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
                                                elementType.AsTypeSyntax()
                                            })))))
                        .AddArgumentListArguments(
                            Argument(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(marshalerIdentifier),
                                    IdentifierName("NativeValueStorage")))))));
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
            string marshalerIdentifier = GetMarshallerIdentifier(info, context);
            // MemoryMarshal.Cast<byte, <elementType>>(<marshalerIdentifier.NativeValueStorage).CopyTo(<marshalerIdentifier>.ManagedValues);
            yield return ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
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
                                                    elementType.AsTypeSyntax()
                                                })))))
                            .AddArgumentListArguments(
                                Argument(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(marshalerIdentifier),
                                        IdentifierName("NativeValueStorage")))),
                        IdentifierName("CopyTo")))
                .AddArgumentListArguments(
                    Argument(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(marshalerIdentifier),
                            IdentifierName("ManagedValues")))));
        }
    }
}