using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal class NonBlittableArrayMarshaller : IMarshallingGenerator
    {
        private IMarshallingGenerator _elementMarshaller;
        public NonBlittableArrayMarshaller(IMarshallingGenerator elementMarshaller)
        {
            _elementMarshaller = elementMarshaller;
        }

        public TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return PointerType(_elementMarshaller.AsNativeType(TypePositionInfo.CreateForType(((IArrayTypeSymbol)info.ManagedType).ElementType, null)));
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
            return info.IsByRef ?
                Argument(IdentifierName(context.GetIdentifiers(info).native))
                : Argument(
                    PrefixUnaryExpression(
                        SyntaxKind.AddressOfExpression,
                        IdentifierName(context.GetIdentifiers(info).native)));
        }

        public IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            yield break;
        }

        public bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return (info.IsByRef && !info.IsManagedReturnPosition) || !context.PinningSupported;
        }
    }

}
