// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    public static class MarshallingHelpers // TODO: Find better place for those
    {
        public static bool MethodRequiresIndirectResult(MethodEnvironment env)
        {
            if (env.MethodDecl.IsConstructor && !(env.ParentDecl is StructDecl structDecl && StructIsMarshalledAsCSStruct(structDecl))) return true;
            var returnType = env.MethodDecl.CSSignature.First();

            if (returnType.IsGeneric) return true;
            if (returnType.SwiftTypeSpec.IsEmptyTuple) return false;

            if (!ArgumentIsMarshalledAsCSStruct(returnType, env.TypeDatabase)) return true;
            return false;
        }

        public static bool MethodRequiresSwiftSelf(MethodEnvironment env)
        {
            if (env.ParentDecl is ModuleDecl) return false; // global funcs
            if (env.MethodDecl.MethodType == MethodType.Static) return false;
            if (env.MethodDecl.IsConstructor) return false;

            return true;
        }

        public static bool StructIsMarshalledAsCSStruct(StructDecl decl)
        {
            return decl is StructDecl structDecl && structDecl.IsFrozen && structDecl.IsBlittable;
        }

        public static bool ArgumentIsMarshalledAsCSStruct(ArgumentDecl argumentDecl, ITypeDatabase typeDatabase)
        {
            var typeRecord = typeDatabase.GetTypeRecordOrThrow(argumentDecl.SwiftTypeSpec);
            return typeRecord.IsFrozen && typeRecord.IsBlittable;
        }
    }
}
