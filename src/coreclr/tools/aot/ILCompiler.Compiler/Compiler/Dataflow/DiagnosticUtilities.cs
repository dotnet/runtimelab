// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.Dataflow
{
    static class DiagnosticUtilities
    {
        internal static Origin GetMethodParameterFromIndex(MethodDesc method, int parameterIndex)
        {
            int declaredParameterIndex;
            if (!method.Signature.IsStatic)
            {
                if (parameterIndex == 0)
                    return new MethodOrigin(method);

                declaredParameterIndex = parameterIndex - 1;
            }
            else
                declaredParameterIndex = parameterIndex;

            return new ParameterOrigin(method, declaredParameterIndex);
        }

        internal static string GetParameterNameForErrorMessage(ParameterOrigin origin)
        {
            return $"#{origin.Index}";
        }

        internal static string GetMethodSignatureDisplayName(MethodDesc method)
        {
            return method.GetDisplayName();
        }

        internal static string GetGenericParameterDeclaringMemberDisplayName(GenericParameterOrigin origin)
        {
            var param = (EcmaGenericParameter)origin.GenericParameter;
            var parent = param.Module.GetObject(param.MetadataReader.GetGenericParameter(param.Handle).Parent);
            if (parent is MethodDesc m)
                return m.GetDisplayName();
            else
                return ((TypeDesc)parent).GetDisplayName();
        }
    }
}
