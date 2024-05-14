namespace BindingsGeneration;

public class ArgumentEnv
{
    public ArgumentEnv (Conductor conductor, MethodEnv parentEnv)
    {
        this.Conductor = conductor;
        this.parentEnv = parentEnv;
    }
    protected MethodEnv parentEnv;
    public Conductor Conductor {get; private set;}
}