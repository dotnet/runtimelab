using System.Collections.Generic;

using Microsoft.CodeAnalysis;
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

    class MarshallingGenerators
    {
        public static readonly BoolMarshaller Bool = new BoolMarshaller();
        public static readonly Forwarder Forwarder = new Forwarder();
        public static readonly NumericMarshaller Numeric = new NumericMarshaller();

        public static bool TryCreate(TypePositionInfo info, out IMarshallingGenerator generator)
        {
            switch (info.ManagedType.SpecialType)
            {
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                    generator = MarshallingGenerators.Numeric;
                    return true;

                case SpecialType.System_Boolean:
                    generator = MarshallingGenerators.Bool;
                    return true;
                default:
                    generator = MarshallingGenerators.Forwarder;
                    return false;
            }
        }
    }
}
