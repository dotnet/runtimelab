namespace BindingsGeneration;

public class MethodEnv
{
    public MethodEnv (Conductor conductor, MethodDecl methodDecl, TypeEnv? parentEnv)
    {
        this.Conductor = conductor;
        this.methodDecl = methodDecl;
        this.parentEnv = parentEnv;
    }
    protected TypeEnv? parentEnv;
    protected MethodDecl methodDecl;
    public Conductor Conductor {get; private set;}
}