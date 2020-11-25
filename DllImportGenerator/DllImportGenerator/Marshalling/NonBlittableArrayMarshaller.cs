using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal class NonBlittableArrayMarshaller : ConditionalStackallocMarshallingGenerator
    {
        /// <summary>
        /// Stack-alloc threshold set to 256 bytes to enable small arrays to be passed on the stack.
        /// Number kept small to ensure that P/Invokes with a lot of small array parameters doesn't
        /// blow the stack since this is a new optimization in the code-generated interop.
        /// </summary>
        private const int StackAllocBytesThreshold = 0x200;

        private const string IndexerIdentifier = "__i";

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
            return _elementMarshaller.AsNativeType(TypePositionInfo.CreateForType(GetElementTypeSymbol(info), NoMarshallingInfo.Instance));
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
                    if (TryGenerateSetupSyntax(info, context, out StatementSyntax conditionalAllocSetup))
                        yield return conditionalAllocSetup;

                    break;
                case StubCodeContext.Stage.Marshal:
                    if (info.RefKind != RefKind.Out)
                    {
                        foreach (var statement in GenerateConditionalAllocationSyntax(
                            info,
                            context,
                            StackAllocBytesThreshold))
                        {
                            yield return statement;
                        }

                        BlockSyntax marshalBlock = Block();

                        string managedLocal = managedIdentifer;

                        if (info.IsByRef)
                        {
                            // Copy the managed object to a local to avoid the caller modifing the managed value on another thread
                            // while we marshal the contents.
                            managedLocal = managedIdentifer + ArrayMarshallingCodeContext.LocalManagedIdentifierSuffix;
                            marshalBlock = marshalBlock.AddStatements(LocalDeclarationStatement(
                                VariableDeclaration(
                                    info.ManagedType.AsTypeSyntax(),
                                    SingletonSeparatedList(
                                        VariableDeclarator(managedLocal)
                                            .WithInitializer(EqualsValueClause(
                                                IdentifierName(managedIdentifer)))))));
                        }

                        // Iterate through the elements of the array to marshal them
                        var arraySubContext = new ArrayMarshallingCodeContext(context.CurrentStage, IndexerIdentifier, context, stripLocalManagedIdentifierSuffix: info.IsByRef);
                        marshalBlock = marshalBlock.AddStatements(IfStatement(BinaryExpression(SyntaxKind.NotEqualsExpression,
                            IdentifierName(managedLocal),
                            LiteralExpression(SyntaxKind.NullLiteralExpression)),
                            MarshallerHelpers.GetForLoop(managedLocal, IndexerIdentifier)
                                .WithStatement(Block(
                                    List(_elementMarshaller.Generate(
                                        info with { ManagedType = GetElementTypeSymbol(info), InstanceIdentifier = managedLocal },
                                        arraySubContext))))));
                        yield return marshalBlock.Statements.Count == 1 ? marshalBlock.Statements[0] : marshalBlock;
                    }
                    break;
                case StubCodeContext.Stage.Unmarshal:
                    if (info.IsManagedReturnPosition || (info.IsByRef && info.RefKind != RefKind.In))
                    {
                        BlockSyntax unmarshalBlock = Block();

                        string managedLocal = managedIdentifer;

                        if (info.IsByRef)
                        {
                            // Unmarshal the values to a local to avoid the caller modifing the managed value on another thread
                            // while we unmarshal the contents.
                            managedLocal = managedIdentifer + ArrayMarshallingCodeContext.LocalManagedIdentifierSuffix;
                            unmarshalBlock = unmarshalBlock.AddStatements(LocalDeclarationStatement(
                                VariableDeclaration(
                                    info.ManagedType.AsTypeSyntax(),
                                    SingletonSeparatedList(
                                        VariableDeclarator(managedLocal)))));
                        }

                        var arraySubContext = new ArrayMarshallingCodeContext(context.CurrentStage, IndexerIdentifier, context, stripLocalManagedIdentifierSuffix: info.IsByRef);
                        
                        unmarshalBlock = unmarshalBlock.AddStatements(IfStatement(
                            BinaryExpression(SyntaxKind.NotEqualsExpression,
                            IdentifierName(nativeIdentifier),
                            LiteralExpression(SyntaxKind.NullLiteralExpression)),
                            Block(
                                // <managedLocal> = new <managedElementType>[<numElementsExpression>];
                                ExpressionStatement(
                                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                        IdentifierName(managedLocal),
                                        ArrayCreationExpression(
                                        ArrayType(GetElementTypeSymbol(info).AsTypeSyntax(),
                                            SingletonList(ArrayRankSpecifier(
                                                SingletonSeparatedList(_numElementsExpr))))))),
                                // Iterate through the elements of the native array to unmarshal them
                                MarshallerHelpers.GetForLoop(managedLocal, IndexerIdentifier)
                                    .WithStatement(Block(
                                        List(_elementMarshaller.Generate(
                                            info with { ManagedType = GetElementTypeSymbol(info), InstanceIdentifier = managedLocal },
                                            arraySubContext))))),
                            ElseClause(
                                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                    IdentifierName(managedLocal),
                                    LiteralExpression(SyntaxKind.NullLiteralExpression))))));

                        if (managedLocal != managedIdentifer)
                        {
                            unmarshalBlock = unmarshalBlock.AddStatements(ExpressionStatement(
                                AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                    IdentifierName(managedIdentifer),
                                    IdentifierName(managedLocal))
                            ));
                        }

                        yield return unmarshalBlock.Statements.Count == 1 ? unmarshalBlock.Statements[0] : unmarshalBlock;
                    }
                    break;
                case StubCodeContext.Stage.Cleanup:
                    {
                        var arraySubContext = new ArrayMarshallingCodeContext(context.CurrentStage, IndexerIdentifier, context, stripLocalManagedIdentifierSuffix: false);
                        var elementCleanup = List(_elementMarshaller.Generate(info with { ManagedType = GetElementTypeSymbol(info) }, arraySubContext));
                        if (elementCleanup.Count != 0)
                        {
                            // Iterate through the elements of the native array to clean up any unmanaged resources.
                            yield return IfStatement(
                                BinaryExpression(SyntaxKind.NotEqualsExpression,
                                    IdentifierName(managedIdentifer),
                                    LiteralExpression(SyntaxKind.NullLiteralExpression)),
                                MarshallerHelpers.GetForLoop(managedIdentifer, IndexerIdentifier)
                                    .WithStatement(Block(elementCleanup)));
                        }
                        yield return GenerateConditionalAllocationFreeSyntax(info, context);
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
            // checked(sizeof(<nativeElementType>) * <managedIdentifier>.Length)
            return CheckedExpression(SyntaxKind.CheckedExpression,
                BinaryExpression(SyntaxKind.MultiplyExpression,
                    SizeOfExpression(GetNativeElementTypeSyntax(info)),
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(context.GetIdentifiers(info).managed),
                        IdentifierName("Length"))));
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
