using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal class Utf16StringMarshaller : IMarshallingGenerator
    {
        private static readonly TypeSyntax InteropServicesMarshalType = ParseTypeName(TypeNames.System_Runtime_InteropServices_Marshal);
        private static readonly TypeSyntax NativeType = PointerType(PredefinedType(Token(SyntaxKind.UShortKeyword)));
        private static readonly int StackAllocBytesThreshold = 260 * sizeof(ushort);

        public ArgumentSyntax AsArgument(TypePositionInfo info, StubCodeContext context)
        {
            string identifier = context.GetIdentifiers(info).native;
            if (info.IsByRef)
            {
                // &<nativeIdentifier>
                return Argument(
                    PrefixUnaryExpression(
                        SyntaxKind.AddressOfExpression,
                        IdentifierName(identifier)));
            }
            else if (context.PinningSupported)
            {
                // (ushort*)<nativeIdentifier>
                return Argument(
                    CastExpression(
                        AsNativeType(info),
                        IdentifierName(identifier)));
            }

            // <nativeIdentifier>
            return Argument(IdentifierName(identifier));
        }

        public TypeSyntax AsNativeType(TypePositionInfo info)
        {
            // ushort*
            return NativeType;
        }

        public ParameterSyntax AsParameter(TypePositionInfo info)
        {
            // ushort**
            // or
            // ushort*
            var type = info.IsByRef
                ? PointerType(AsNativeType(info))
                : AsNativeType(info);
            return Parameter(Identifier(info.InstanceIdentifier))
                .WithType(type);
        }

        public IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            (string managedIdentifier, string nativeIdentifier) = context.GetIdentifiers(info);
            if (context.PinningSupported && !info.IsByRef && !info.IsManagedReturnPosition)
            {
                if (context.CurrentStage == StubCodeContext.Stage.Pin)
                {
                    // fixed (char* <nativeIdentifier> = <managedIdentifier>)
                    yield return FixedStatement(
                        VariableDeclaration(
                            PointerType(PredefinedType(Token(SyntaxKind.CharKeyword))),
                            SingletonSeparatedList(
                                VariableDeclarator(Identifier(nativeIdentifier))
                                    .WithInitializer(EqualsValueClause(IdentifierName(managedIdentifier))))),
                        EmptyStatement());
                }

                yield break;
            }

            string usedCoTaskMemIdentifier = $"{managedIdentifier}__usedCoTaskMem";
            switch (context.CurrentStage)
            {
                case StubCodeContext.Stage.Setup:
                    // ushort* <nativeIdentifier>
                    yield return LocalDeclarationStatement(
                        VariableDeclaration(
                            AsNativeType(info),
                            SingletonSeparatedList(VariableDeclarator(nativeIdentifier))));
                    break;
                case StubCodeContext.Stage.Marshal:
                    if (info.RefKind != RefKind.Out)
                    {
                        // <nativeIdentifier> = (ushort*)Marshal.StringToCoTaskMemUni(<managedIdentifier>)
                        var coTaskMemAlloc = ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(nativeIdentifier),
                                CastExpression(
                                    AsNativeType(info),
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            InteropServicesMarshalType,
                                            IdentifierName("StringToCoTaskMemUni")),
                                        ArgumentList(
                                            SingletonSeparatedList<ArgumentSyntax>(
                                                Argument(IdentifierName(managedIdentifier))))))));
                        if (info.IsByRef && info.RefKind != RefKind.In)
                        {
                            yield return coTaskMemAlloc;
                        }
                        else
                        {
                            // <usedCoTaskMemIdentifier> = false;
                            yield return LocalDeclarationStatement(
                                VariableDeclaration(
                                    PredefinedType(Token(SyntaxKind.BoolKeyword)),
                                    SingletonSeparatedList(
                                        VariableDeclarator(usedCoTaskMemIdentifier)
                                            .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression))))));

                            string stackAllocPtrIdentifier = $"{managedIdentifier}__stackalloc";
                            string fixedCharIdentifier = $"{managedIdentifier}__fixedChar";
                            string byteLenIdentifier = $"{managedIdentifier}__byteLen";

                            // <managedIdentifier>.Length + 1
                            ExpressionSyntax lengthWithNullTerminator = BinaryExpression(
                                SyntaxKind.AddExpression,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(managedIdentifier),
                                    IdentifierName("Length")),
                                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));

                            // Code block for stackalloc if string is below threshold size
                            var marshalOnStack = Block(
                                // ushort* <stackAllocIdentifier> = stackalloc ushort[<managedIdentifier>.Length + 1];
                                LocalDeclarationStatement(
                                    VariableDeclaration(
                                        PointerType(PredefinedType(Token(SyntaxKind.UShortKeyword))),
                                        SingletonSeparatedList(
                                            VariableDeclarator(stackAllocPtrIdentifier)
                                                .WithInitializer(EqualsValueClause(
                                                    StackAllocArrayCreationExpression(
                                                        ArrayType(
                                                            PredefinedType(Token(SyntaxKind.UShortKeyword)),
                                                            SingletonList<ArrayRankSpecifierSyntax>(
                                                                ArrayRankSpecifier(SingletonSeparatedList(lengthWithNullTerminator)))))))))),
                                // fixed (char* <fixedCharIdentifier> = <managedIdentifier>)
                                // {
                                //     Buffer.MemoryCopy(<fixedCharIdentifier>, <stackAllocIdentifier>, <byteLenIdentifier>, <byteLenIdentifier>);
                                // }
                                FixedStatement(
                                    VariableDeclaration(
                                        PointerType(PredefinedType(Token(SyntaxKind.CharKeyword))),
                                        SingletonSeparatedList<VariableDeclaratorSyntax>(
                                            VariableDeclarator(fixedCharIdentifier)
                                            .WithInitializer(EqualsValueClause(IdentifierName(managedIdentifier))))),
                                    Block(
                                        ExpressionStatement(
                                            InvocationExpression(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    ParseTypeName("System.Buffer"),
                                                    IdentifierName("MemoryCopy")),
                                                ArgumentList(
                                                    SeparatedList(new ArgumentSyntax[] {
                                                        Argument(IdentifierName(fixedCharIdentifier)),
                                                        Argument(IdentifierName(stackAllocPtrIdentifier)),
                                                        Argument(IdentifierName(byteLenIdentifier)),
                                                        Argument(IdentifierName(byteLenIdentifier))})))))),
                                // <nativeIdentifier> = <stackAllocIdentifier>;
                                ExpressionStatement(
                                    AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        IdentifierName(nativeIdentifier),
                                        IdentifierName(stackAllocPtrIdentifier))));

                            // if (<managedIdentifier> == null)
                            // {
                            //     <nativeIdentifier> = null;
                            // }
                            // else
                            // {
                            //     int <byteLenIdentifier> = (<managedIdentifier>.Length + 1) * sizeof(ushort);
                            //     if (<byteLenIdentifier> > <StackAllocBytesThreshold>)
                            //     {
                            //         <nativeIdentifier> = (ushort*)Marshal.StringToCoTaskMemUni(<managedIdentifier>);
                            //         <usedCoTaskMemIdentifier> = true;
                            //     }
                            //     else
                            //     {
                            //         ushort* <stackAllocIdentifier> = stackalloc ushort[<managedIdentifier>.Length + 1];
                            //         fixed (char* <fixedCharIdentifier> = <managedIdentifier>)
                            //         {
                            //             Buffer.MemoryCopy(<fixedCharIdentifier>, <stackAllocIdentifier>, <byteLenIdentifier>, <byteLenIdentifier>);
                            //         }
                            //         <nativeIdentifier> = <stackAllocIdentifier>;
                            //     }
                            // }
                            yield return IfStatement(
                                BinaryExpression(
                                    SyntaxKind.EqualsExpression,
                                    IdentifierName(managedIdentifier),
                                    LiteralExpression(SyntaxKind.NullLiteralExpression)),
                                Block(
                                    ExpressionStatement(
                                        AssignmentExpression(
                                            SyntaxKind.SimpleAssignmentExpression,
                                            IdentifierName(nativeIdentifier),
                                            LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                                ElseClause(
                                    Block(
                                        LocalDeclarationStatement(
                                            VariableDeclaration(
                                                PredefinedType(Token(SyntaxKind.IntKeyword)),
                                                SingletonSeparatedList<VariableDeclaratorSyntax>(
                                                    VariableDeclarator(Identifier(byteLenIdentifier))
                                                        .WithInitializer(EqualsValueClause(
                                                            BinaryExpression(
                                                                SyntaxKind.MultiplyExpression,
                                                                ParenthesizedExpression(lengthWithNullTerminator),
                                                                SizeOfExpression(PredefinedType(Token(SyntaxKind.UShortKeyword))))))))),
                                        IfStatement(
                                            BinaryExpression(
                                                SyntaxKind.GreaterThanExpression,
                                                IdentifierName(byteLenIdentifier),
                                                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(StackAllocBytesThreshold))),
                                            Block(
                                                coTaskMemAlloc,
                                                ExpressionStatement(
                                                    AssignmentExpression(
                                                        SyntaxKind.SimpleAssignmentExpression,
                                                        IdentifierName(usedCoTaskMemIdentifier),
                                                        LiteralExpression(SyntaxKind.TrueLiteralExpression)))),
                                            ElseClause(marshalOnStack)))));
                        }
                    }
                    break;
                case StubCodeContext.Stage.Unmarshal:
                    if (info.IsManagedReturnPosition || (info.IsByRef && info.RefKind != RefKind.In))
                    {
                        // <managedIdentifier> = <nativeIdentifier> == null ? null : new string((char*)<nativeIdentifier>);
                        yield return ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(managedIdentifier),
                                ConditionalExpression(
                                    BinaryExpression(
                                        SyntaxKind.EqualsExpression,
                                        IdentifierName(nativeIdentifier),
                                        LiteralExpression(SyntaxKind.NullLiteralExpression)),
                                    LiteralExpression(SyntaxKind.NullLiteralExpression),
                                    ObjectCreationExpression(
                                        PredefinedType(Token(SyntaxKind.StringKeyword)),
                                        ArgumentList(SingletonSeparatedList<ArgumentSyntax>(
                                            Argument(
                                                CastExpression(
                                                    PointerType(PredefinedType(Token(SyntaxKind.CharKeyword))),
                                                    IdentifierName(nativeIdentifier))))),
                                        initializer: null))));
                    }
                    break;
                case StubCodeContext.Stage.Cleanup:
                    // Marshal.FreeCoTaskMem((IntPtr)<nativeIdentifier>)
                    var freeCoTaskMem = ExpressionStatement(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                InteropServicesMarshalType,
                                IdentifierName("FreeCoTaskMem")),
                            ArgumentList(
                                SingletonSeparatedList<ArgumentSyntax>(
                                    Argument(
                                        CastExpression(
                                            ParseTypeName("System.IntPtr"),
                                            IdentifierName(nativeIdentifier)))))));

                    if (info.IsByRef && info.RefKind != RefKind.In)
                    {
                        yield return freeCoTaskMem;
                    }
                    else
                    {
                        // if (<usedCoTaskMemIdentifier>)
                        // {
                        //     Marshal.FreeCoTaskMem((IntPtr)<nativeIdentifier>)
                        // }
                        yield return IfStatement(
                            IdentifierName(usedCoTaskMemIdentifier),
                            Block(freeCoTaskMem));
                    }

                    break;
            }
        }

        public bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context) => true;
    }
}
