// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Swift.Runtime;

namespace BindingsGeneration
{
    public static class MarshallingHelpers // TODO: Find better place for those
    {
        public static bool MethodRequiresIndirectResult(MethodDecl methodDecl, BaseDecl parentDecl, ITypeDatabase typeDatabase)
        {
            if (methodDecl.IsConstructor && !(parentDecl is StructDecl structDecl && StructIsMarshalledAsCSStruct(structDecl))) return true;
            var returnType = methodDecl.CSSignature.First();

            if (returnType.SwiftTypeSpec.IsEmptyTuple) return false;

            if (!ArgumentIsMarshalledAsCSStruct(returnType, typeDatabase)) return true;
            return false;
        }

        public static bool MethodRequiresSwiftSelf(MethodDecl methodDecl, BaseDecl parentDecl)
        {
            if (parentDecl is ModuleDecl) return false; // global funcs
            if (methodDecl.MethodType == MethodType.Static) return false;
            if (methodDecl.IsConstructor) return false;

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
