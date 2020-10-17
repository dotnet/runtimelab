using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal sealed class Utf8StringMarshaller : IMarshallingGenerator
    {
        private const int StackAllocBytesThreshold = 260;

        private static readonly TypeSyntax InteropServicesMarshalType = ParseTypeName(TypeNames.System_Runtime_InteropServices_Marshal);
        private static readonly TypeSyntax NativeType = PointerType(PredefinedType(Token(SyntaxKind.ByteKeyword)));
        private static readonly TypeSyntax UTF8EncodingType = ParseTypeName("System.Text.Encoding.UTF8");

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

            return Argument(IdentifierName(identifier));
        }

        public TypeSyntax AsNativeType(TypePositionInfo info) => NativeType;

        public ParameterSyntax AsParameter(TypePositionInfo info)
        {
            var type = info.IsByRef
                ? PointerType(AsNativeType(info))
                : AsNativeType(info);
            return Parameter(Identifier(info.InstanceIdentifier))
                .WithType(type);
        }

        public IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            (string managedIdentifier, string nativeIdentifier) = context.GetIdentifiers(info);
            string usedCoTaskMemIdentifier = $"{managedIdentifier}__usedCoTaskMem";
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
                        // <native> = (byte*)Marshal.StringToCoTaskMemUTF8(<managed>);
                        var coTaskMemAlloc = ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(nativeIdentifier),
                                CastExpression(
                                    NativeType,
                                    InvocationExpression(
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            InteropServicesMarshalType,
                                            IdentifierName("StringToCoTaskMemUTF8")),
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

                            // Code block for stackalloc if string is below threshold size
                            var marshalOnStack = Block(
                                // byte* <stackAllocPtr> = stackalloc byte[<byteLen>];
                                LocalDeclarationStatement(
                                    VariableDeclaration(
                                        PointerType(PredefinedType(Token(SyntaxKind.ByteKeyword))),
                                        SingletonSeparatedList(
                                            VariableDeclarator(stackAllocPtrIdentifier)
                                                .WithInitializer(EqualsValueClause(
                                                    StackAllocArrayCreationExpression(
                                                        ArrayType(
                                                            PredefinedType(Token(SyntaxKind.ByteKeyword)),
                                                            SingletonList<ArrayRankSpecifierSyntax>(
                                                                ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(
                                                                    IdentifierName(byteLenIdentifier))))))))))),
                                // fixed (char* <fixedChar> = <managedIdentifier>)
                                // {
                                //     <byteLen> = Encoding.UTF8.GetBytes(<fixedChar>, <managedIdentifier>.Length, <stackAllocPtr>, <byteLen>);
                                // }
                                FixedStatement(
                                    VariableDeclaration(
                                        PointerType(PredefinedType(Token(SyntaxKind.CharKeyword))),
                                        SingletonSeparatedList<VariableDeclaratorSyntax>(
                                            VariableDeclarator(fixedCharIdentifier)
                                            .WithInitializer(EqualsValueClause(IdentifierName(managedIdentifier))))),
                                    Block(
                                        ExpressionStatement(
                                            AssignmentExpression(
                                                SyntaxKind.SimpleAssignmentExpression,
                                                IdentifierName(byteLenIdentifier),
                                                InvocationExpression(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        UTF8EncodingType,
                                                        IdentifierName("GetBytes")),
                                                    ArgumentList(
                                                        SeparatedList(new ArgumentSyntax[] {
                                                            Argument(IdentifierName(fixedCharIdentifier)),
                                                            Argument(MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                IdentifierName(managedIdentifier),
                                                                IdentifierName("Length"))),
                                                            Argument(IdentifierName(stackAllocPtrIdentifier)),
                                                            Argument(IdentifierName(byteLenIdentifier))}))))))),
                                // <stackAllocPtr>[<byteLen>] = 0;
                                ExpressionStatement(
                                    AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        ElementAccessExpression(
                                            IdentifierName(stackAllocPtrIdentifier),
                                            BracketedArgumentList(
                                                SingletonSeparatedList<ArgumentSyntax>(
                                                    Argument(IdentifierName(byteLenIdentifier))))),
                                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)))),
                                // <nativeIdentifier> = <stackAllocPtr>;
                                ExpressionStatement(
                                    AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        IdentifierName(nativeIdentifier),
                                        CastExpression(
                                            NativeType,
                                            IdentifierName(stackAllocPtrIdentifier)))));

                            // if (<managed> == null)
                            // {
                            //     <native> = null;
                            // }
                            // else
                            // {
                            //     int <byteLen> = (<managed>.Length + 1) * 3 + 1;
                            //     if (<byteLen> > <StackAllocBytesThreshold>)
                            //     {
                            //         <native> = (byte*)Marshal.StringToCoTaskMemUTF8(<managed>);
                            //     }
                            //     else
                            //     {
                            //         byte* <stackAllocPtr> = stackalloc byte[<byteLen>];
                            //         fixed (char* <fixedChar> = <managed>)
                            //         {
                            //             <byteLen> = Encoding.UTF8.GetBytes(<fixedChar>, <managed>.Length, <stackAllocPtr>, <byteLen>);
                            //         }
                            //         <stackAllocPtr>[<byteLen>] = 0;
                            //         <native> = (byte*)<stackAllocPtr>;
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
                                                                SyntaxKind.AddExpression,
                                                                BinaryExpression(
                                                                    SyntaxKind.MultiplyExpression,
                                                                    ParenthesizedExpression(
                                                                        BinaryExpression(
                                                                            SyntaxKind.AddExpression,
                                                                            MemberAccessExpression(
                                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                                IdentifierName(managedIdentifier),
                                                                                IdentifierName("Length")),
                                                                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)))),
                                                                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(3))),
                                                                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)))))))),
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
                        // <managedIdentifier> = Marshal.PtrToStringUTF8((IntPtr)<nativeIdentifier>);
                        yield return ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(managedIdentifier),
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        InteropServicesMarshalType,
                                        IdentifierName("PtrToStringUTF8")),
                                    ArgumentList(SingletonSeparatedList<ArgumentSyntax>(
                                        Argument(
                                            CastExpression(
                                                ParseTypeName("System.IntPtr"),
                                                IdentifierName(nativeIdentifier))))))));
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
                            ArgumentList(SingletonSeparatedList<ArgumentSyntax>(
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
