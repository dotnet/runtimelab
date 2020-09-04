using System.Collections.Generic;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Interop
{
    interface IMarshallingGenerator
    {
        TypeSyntax AsNativeType(TypePositionInfo info);
        ArgumentSyntax AsArgument(TypePositionInfo info);
        ParameterSyntax AsParameter(TypePositionInfo info);

        IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context);
    }

    class MarshallingGenerator
    {
        public static bool TryCreate(TypePositionInfo info, out IMarshallingGenerator generator)
        {
            generator = new Forwarder();
            return true;
        }
    }
}
