namespace BindingsGeneration;

public interface IMethodHandler
{
    MethodEnv MakeMethodEnv(Conductor conductor, MethodDecl baseDecl, TypeEnv? parentEnv);
    void Begin(MethodEnv methodEnv);
    Method End(MethodEnv methodEnv);
}