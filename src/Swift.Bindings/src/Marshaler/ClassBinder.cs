namespace BindingsGeneration;

public class ClassHandlerFactory : IFactory<BaseDecl, ITypeHandler>
{
    public bool Handles(BaseDecl decl)
    {
        return decl is ClassDecl;
    }

    public ITypeHandler Construct ()
    {
        return new ClassHandler ();
    }
}

public class ClassHandler : BaseTypeHandler
{
    public ClassHandler ()
        : base("class")
    {
    }
}