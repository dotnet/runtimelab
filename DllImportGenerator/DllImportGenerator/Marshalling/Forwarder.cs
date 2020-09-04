using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    class Forwarder : IMarshallingGenerator
    {
        public TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return ParseTypeName(info.TypeSymbol.ToString(), 0, true);
        }

        public ArgumentSyntax AsArgument(TypePositionInfo info)
        {
            return Argument(IdentifierName(info.InstanceIdentifier))
                .WithRefKindKeyword(Token(info.RefKindSyntax));
        }

        public ParameterSyntax AsParameter(TypePositionInfo info)
        {
            return Parameter(Identifier(info.InstanceIdentifier))
                .WithModifiers(TokenList(Token(info.RefKindSyntax)))
                .WithType(info.ManagedType);
        }

        public IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context)
        {
            return Array.Empty<StatementSyntax>();
        }
    }
}
