namespace BindingsGeneration;
public interface IArgumentHandler {
    ArgumentEnv MakeArgumentEnv(Conductor conductor, MethodDecl baseDecl, MethodEnv parentEnv);
    void Begin(ArgumentEnv argumentEnv);
    void End(ArgumentEnv argumentEnv);
}