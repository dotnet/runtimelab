using System.CodeDom.Compiler;

namespace BindingsGeneration;
public interface ITypeHandler
{
    TypeEnv MakeTypeEnv(Conductor conductor, TypeDecl baseDecl, TypeEnv? parentEnv);
    void BeginType(TypeEnv typeEnv);
    void EndType(TypeEnv typeEnv, IndentedTextWriter writer);
}