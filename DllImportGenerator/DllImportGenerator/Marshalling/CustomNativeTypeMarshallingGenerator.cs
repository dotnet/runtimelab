using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    /// <summary>
    /// Implements generating code for an <see cref="ICustomNativeTypeMarshallingStrategy"/> instance.
    /// </summary>
    internal sealed class CustomNativeTypeMarshallingGenerator : IMarshallingGenerator
    {
        private readonly ICustomNativeTypeMarshallingStrategy nativeTypeMarshaller;

        public CustomNativeTypeMarshallingGenerator(ICustomNativeTypeMarshallingStrategy nativeTypeMarshaller)
        {
            this.nativeTypeMarshaller = nativeTypeMarshaller;
        }

        public ArgumentSyntax AsArgument(TypePositionInfo info, StubCodeContext context)
        {
            return nativeTypeMarshaller.AsArgument(info, context);
        }

        public TypeSyntax AsNativeType(TypePositionInfo info)
        {
            return nativeTypeMarshaller.AsNativeType(info);
        }

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
            switch (context.CurrentStage)
            {
                case StubCodeContext.Stage.Setup:
                    return nativeTypeMarshaller.GenerateSetupStatements(info, context);
                case StubCodeContext.Stage.Marshal:
                    if (!info.IsManagedReturnPosition && info.RefKind != RefKind.Out)
                    {
                        return nativeTypeMarshaller.GenerateMarshalStatements(info, context, nativeTypeMarshaller.GetNativeTypeConstructorArguments(info, context));
                    }
                    break;
                case StubCodeContext.Stage.Pin:
                    if (!info.IsByRef || info.RefKind == RefKind.In)
                    {
                        return nativeTypeMarshaller.GeneratePinStatements(info, context);
                    }
                    break;
                case StubCodeContext.Stage.Unmarshal:
                    if (info.IsManagedReturnPosition || (info.IsByRef && info.RefKind != RefKind.In))
                    {
                        return nativeTypeMarshaller.GenerateUnmarshalStatements(info, context);
                    }
                    break;
                case StubCodeContext.Stage.Cleanup:
                    return nativeTypeMarshaller.GenerateCleanupStatements(info, context);
                default:
                    break;
            }

            return Array.Empty<StatementSyntax>();
        }

        public bool SupportsByValueMarshalKind(ByValueContentsMarshalKind marshalKind, StubCodeContext context)
        {
            return false;
        }

        public bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return nativeTypeMarshaller.UsesNativeIdentifier(info, context);
        }
    }
}
