using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal class ArrayMarshaller : CustomNativeTypeMarshaller
    {
        private CustomNativeTypeMarshaller innerCollectionMarshaller;

        private bool blittable;

        public ArrayMarshaller(
            ContiguousCollectionBlittableElementsMarshallingGenerator innerCollectionMarshaller,
            NativeContiguousCollectionMarshallingInfo marshallingInfo)
            : base(marshallingInfo)
        {
            this.innerCollectionMarshaller = innerCollectionMarshaller;
            blittable = true;
        }

        public ArrayMarshaller(
            ContiguousCollectionNonBlittableElementsMarshallingGenerator innerCollectionMarshaller,
            NativeContiguousCollectionMarshallingInfo marshallingInfo)
            : base(marshallingInfo)
        {
            this.innerCollectionMarshaller = innerCollectionMarshaller;
            blittable = true;
        }

        private bool UseCustomPinningPath(TypePositionInfo info, StubCodeContext context)
        {
            return blittable && !info.IsByRef && !info.IsManagedReturnPosition && context.PinningSupported;
        }

        public override IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            if (UseCustomPinningPath(info, context))
            {
                return GenerateCustomPinning();
            }

            if (context.CurrentStage == StubCodeContext.Stage.Unmarshal
                && info.ByValueContentsMarshalKind.HasFlag(ByValueContentsMarshalKind.Out))
            {
                return GenerateByValueOutUnmarshalling();
            }

            return innerCollectionMarshaller.Generate(info, context);

            IEnumerable<StatementSyntax> GenerateCustomPinning()
            {
                var (managedIdentifer, nativeIdentifier) = context.GetIdentifiers(info);
                string byRefIdentifier = $"__byref_{managedIdentifer}";
                TypeSyntax arrayElementType = ((IArrayTypeSymbol)info.ManagedType).ElementType.AsTypeSyntax();
                if (context.CurrentStage == StubCodeContext.Stage.Marshal)
                {
                    // [COMPAT] We use explicit byref calculations here instead of just using a fixed statement 
                    // since a fixed statement converts a zero-length array to a null pointer.
                    // Many native APIs, such as GDI+, ICU, etc. validate that an array parameter is non-null
                    // even when the passed in array length is zero. To avoid breaking customers that want to move
                    // to source-generated interop in subtle ways, we explicitly pass a reference to the 0-th element
                    // of an array as long as it is non-null, matching the behavior of the built-in interop system
                    // for single-dimensional zero-based arrays.

                    // ref <elementType> <byRefIdentifier> = <managedIdentifer> == null ? ref *(<elementType*)0 : ref MemoryMarshal.GetArrayDataReference(<managedIdentifer>);
                    var nullRef =
                        PrefixUnaryExpression(SyntaxKind.PointerIndirectionExpression,
                            CastExpression(
                                PointerType(arrayElementType),
                                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))));

                    var getArrayDataReference =
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName(TypeNames.System_Runtime_InteropServices_MemoryMarshal),
                                IdentifierName("GetArrayDataReference")),
                            ArgumentList(SingletonSeparatedList(
                                Argument(IdentifierName(managedIdentifer)))));

                    yield return LocalDeclarationStatement(
                        VariableDeclaration(
                            RefType(arrayElementType))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(byRefIdentifier))
                            .WithInitializer(EqualsValueClause(
                                RefExpression(ParenthesizedExpression(
                                    ConditionalExpression(
                                        BinaryExpression(
                                            SyntaxKind.EqualsExpression,
                                            IdentifierName(managedIdentifer),
                                            LiteralExpression(
                                                SyntaxKind.NullLiteralExpression)),
                                        RefExpression(nullRef),
                                        RefExpression(getArrayDataReference)))))))));
                }
                if (context.CurrentStage == StubCodeContext.Stage.Pin)
                {
                    // fixed (<nativeType> <nativeIdentifier> = &<byrefIdentifier>)
                    yield return FixedStatement(
                        VariableDeclaration(AsNativeType(info), SingletonSeparatedList(
                            VariableDeclarator(nativeIdentifier)
                                .WithInitializer(EqualsValueClause(
                                    PrefixUnaryExpression(SyntaxKind.AddressOfExpression,
                                        IdentifierName(byRefIdentifier)))))),
                        EmptyStatement());
                }
                yield break;
            }
            
            IEnumerable<StatementSyntax> GenerateByValueOutUnmarshalling()
            {
                var (managedIdentifer, nativeIdentifier) = context.GetIdentifiers(info);
                // For [Out] by value unmarshalling, we emit custom code that only assigns the
                // Value property and copy the elements.
                // We do not call SetUnmarshalledCollectionLength since that creates a new
                // array, and we want to fill the original one.
                string marshalerIdentifier = innerCollectionMarshaller.GetMarshallerIdentifier(info, context);
                // <marshalerIdentifier>.Value = <nativeIdentifier>;
                yield return ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(marshalerIdentifier),
                            IdentifierName(ManualTypeMarshallingHelper.ValuePropertyName)),
                        IdentifierName(nativeIdentifier)));

                foreach (var statement in innerCollectionMarshaller.GenerateIntermediateUnmarshallingStatements(info, context))
                {
                    yield return statement;
                }
            }
        }

        public override IEnumerable<ArgumentSyntax> GenerateAdditionalNativeTypeConstructorArguments(TypePositionInfo info, StubCodeContext context)
        {
            return innerCollectionMarshaller.GenerateAdditionalNativeTypeConstructorArguments(info, context);
        }

        public override IEnumerable<StatementSyntax> GenerateIntermediateMarshallingStatements(TypePositionInfo info, StubCodeContext context)
        {
            if (info.ByValueContentsMarshalKind == ByValueContentsMarshalKind.Out)
            {
                // Don't marshal contents of an array when it is marshalled by value [Out].
                return Array.Empty<StatementSyntax>();
            }
            return innerCollectionMarshaller.GenerateIntermediateMarshallingStatements(info, context);
        }

        public override IEnumerable<StatementSyntax> GeneratePreUnmarshallingStatements(TypePositionInfo info, StubCodeContext context)
        {
            return innerCollectionMarshaller.GeneratePreUnmarshallingStatements(info, context);
        }

        public override IEnumerable<StatementSyntax> GenerateIntermediateUnmarshallingStatements(TypePositionInfo info, StubCodeContext context)
        {
            return innerCollectionMarshaller.GenerateIntermediateUnmarshallingStatements(info, context);
        }

        public override bool SupportsByValueMarshalKind(ByValueContentsMarshalKind marshalKind, StubCodeContext context)
        {
            return !(blittable && context.PinningSupported) && marshalKind.HasFlag(ByValueContentsMarshalKind.Out);
        }

        public override bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return !UseCustomPinningPath(info, context) && innerCollectionMarshaller.UsesNativeIdentifier(info, context);
        }
    }
}