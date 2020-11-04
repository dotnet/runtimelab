// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using MethodAttributes = System.Reflection.MethodAttributes;
using FieldAttributes = System.Reflection.FieldAttributes;
using TypeAttributes = System.Reflection.TypeAttributes;

namespace ILCompiler.Dataflow
{
    static class EcmaExtensions
    {
        public static bool IsPublic(this MethodDesc method)
        {
            return method.GetTypicalMethodDefinition() is EcmaMethod ecmaMethod
                && (ecmaMethod.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
        }

        public static bool IsPublic(this FieldDesc field)
        {
            return field.GetTypicalFieldDefinition() is EcmaField ecmaField
                && (ecmaField.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;  
        }

        public static bool IsPrivate(this MethodDesc method)
        {
            return method.GetTypicalMethodDefinition() is EcmaMethod ecmaMethod
                && (ecmaMethod.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;
        }

        public static bool IsPrivate(this FieldDesc field)
        {
            return field.GetTypicalFieldDefinition() is EcmaField ecmaField
                && (ecmaField.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
        }

        public static bool IsNestedPublic(this MetadataType mdType)
        {
            return mdType.GetTypeDefinition() is EcmaType ecmaType
                && (ecmaType.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic;
        }
    }
}
