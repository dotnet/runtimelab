

using Swift.Runtime;

namespace BindingsGeneration
{
    public static class MarshallingExtensions // TODO: Find better place for those
    {
        public static bool RequiresIndirectResult(this MethodDecl methodDecl, BaseDecl parentDecl, TypeDatabase typeDatabase)
        {
            if (methodDecl.IsConstructor && !(parentDecl is StructDecl structDecl && structDecl.IsMarshalledAsStruct())) return true;
            var returnType = methodDecl.CSSignature.First();

            if (returnType.Name == "") return false; // TODO: Void should be handled differently

            var returnTypeRecord = typeDatabase.Registrar.GetType(returnType);
            if (!(returnTypeRecord.IsFrozen && returnTypeRecord.IsBlittable)) return true;
            return false;
        }

        public static bool RequiresSwiftSelf(this MethodDecl methodDecl, BaseDecl parentDecl)
        {
            if (parentDecl is ModuleDecl) return false; // global funcs
            if (methodDecl.MethodType == MethodType.Static) return false;
            if (methodDecl.IsConstructor) return false;

            return true;
        }

        public static bool IsMarshalledAsStruct(this StructDecl decl)
        {
            return decl is StructDecl structDecl && structDecl.IsFrozen && structDecl.IsBlittable;
        }

        public static TypeRecord GetType(this TypeRegistrar typeRegistrar, ArgumentDecl argumentDecl)
        {
            if (argumentDecl.SwiftTypeSpec is not NamedTypeSpec swiftTypeSpec)
            {
                throw new NotImplementedException($"{argumentDecl} is not a NamedTypeSpec");
            }

            var typeRecord = typeRegistrar.GetType(swiftTypeSpec.Module, swiftTypeSpec.NameWithoutModule) ?? throw new NotImplementedException($"Type {swiftTypeSpec.Name} not found in type database");
            return typeRecord;
        }
    }
}
