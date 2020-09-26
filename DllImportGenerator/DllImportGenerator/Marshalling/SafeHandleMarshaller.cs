using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    class SafeHandleMarshaller : IMarshallingGenerator
    {
        public TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return ParseTypeName("global::System.IntPtr");
        }

        public ParameterSyntax AsParameter(TypePositionInfo info)
        {
            var type = info.IsByRef
                ? PointerType(AsNativeType(info))
                : AsNativeType(info);
            return Parameter(Identifier(info.InstanceIdentifier))
                .WithType(type);
        }

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

        public IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            // [TODO] Handle byrefs in a more common place?
            // This pattern will become very common (arrays and strings will also use it)
            (string managedIdentifier, string nativeIdentifier) = context.GetIdentifiers(info);
            string addRefdIdentifier = $"{managedIdentifier}__addRefd";
            string newHandleObjectIdentifier = info.IsManagedReturnPosition
                ? managedIdentifier
                : $"{managedIdentifier}__newHandle";
            string handleValueBackupIdentifier = $"{nativeIdentifier}__original";
            switch (context.CurrentStage)
            {
                case StubCodeContext.Stage.Setup:
                    yield return LocalDeclarationStatement(
                        VariableDeclaration(
                            AsNativeType(info),
                            SingletonSeparatedList(
                                VariableDeclarator(nativeIdentifier))));
                    yield return LocalDeclarationStatement(
                        VariableDeclaration(
                            PredefinedType(Token(SyntaxKind.BoolKeyword)),
                            SingletonSeparatedList(
                                VariableDeclarator(addRefdIdentifier)
                                .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression))))));
                    if (info.IsByRef && info.RefKind != RefKind.In)
                    {
                        // We create the new handle in the Setup phase
                        // so we reduce the possible failure points during unmarshalling, where we would
                        // leak the handle if we failed to create the handle.
                        yield return LocalDeclarationStatement(
                            VariableDeclaration(
                                info.ManagedType.AsTypeSyntax(),
                                SingletonSeparatedList(
                                    VariableDeclarator(newHandleObjectIdentifier)
                                    .WithInitializer(EqualsValueClause(
                                        InvocationExpression(
                                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                                ParseName(TypeNames.System_Runtime_InteropServices_MarshalEx),
                                                GenericName(Identifier("CreateSafeHandle"),
                                                    TypeArgumentList(SingletonSeparatedList(info.ManagedType.AsTypeSyntax())))),
                                            ArgumentList()))))));     
                        yield return LocalDeclarationStatement(
                            VariableDeclaration(
                                AsNativeType(info),
                                SingletonSeparatedList(
                                    VariableDeclarator(handleValueBackupIdentifier)
                                    .WithInitializer(EqualsValueClause(
                                        InvocationExpression(
                                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName(newHandleObjectIdentifier),
                                                IdentifierName("DangerousGetHandle")),
                                            ArgumentList()))))));
                    }
                    break;
                case StubCodeContext.Stage.Marshal:
                    if (info.RefKind != RefKind.Out)
                    {
                        yield return ParseStatement($"{managedIdentifier}.DangerousAddRef(ref {addRefdIdentifier}");
                        if (info.IsByRef && info.RefKind != RefKind.In)
                        {
                            yield return ParseStatement($"{handleValueBackupIdentifier} = {nativeIdentifier} = {managedIdentifier}.DangerousGetHandle();");
                        }
                    }
                    break;
                case StubCodeContext.Stage.LeakSafeUnmarshal:
                    StatementSyntax unmarshalStatement = 
                        ParseStatement($"{TypeNames.System_Runtime_InteropServices_MarshalEx}.SetHandle({newHandleObjectIdentifier}, {nativeIdentifier});");

                    if(info.IsManagedReturnPosition)
                    {
                        yield return unmarshalStatement;
                    }
                    else if (info.RefKind == RefKind.Out)
                    {
                        yield return unmarshalStatement;
                        yield return ExpressionStatement(
                            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(managedIdentifier),
                                IdentifierName(newHandleObjectIdentifier)));
                    }
                    else if (info.RefKind == RefKind.Ref)
                    {
                        // Decrement refcount on original SafeHandle if we addrefd
                        yield return IfStatement(
                            IdentifierName(addRefdIdentifier),
                            ExpressionStatement(
                                InvocationExpression(
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(managedIdentifier),
                                        IdentifierName("DangerousRelease")),
                                    ArgumentList())));
                                
                        // Do not unmarshal the handle if the value didn't change.
                        yield return IfStatement(
                            ParseExpression($"{handleValueBackupIdentifier} != {nativeIdentifier}"),
                            Block(
                                unmarshalStatement,
                                ExpressionStatement(
                                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                        IdentifierName(managedIdentifier),
                                        IdentifierName(newHandleObjectIdentifier)))));
                    }
                    break;
                case StubCodeContext.Stage.Cleanup:
                    if (!info.IsByRef || info.RefKind == RefKind.In)
                    {
                        yield return IfStatement(
                            IdentifierName(addRefdIdentifier),
                            ExpressionStatement(
                                InvocationExpression(
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(managedIdentifier),
                                    IdentifierName("DangerousRelease")),
                                    ArgumentList())));
                    }
                    break;
                default:
                    break;
            }
        }

        public bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context) => true;
    }
}
