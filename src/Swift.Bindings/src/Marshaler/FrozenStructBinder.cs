namespace BindingsGeneration;

public class FrozenStructHandlerFactory : IFactory<BaseDecl, ITypeHandler>
{
    public bool Handles(BaseDecl decl)
    {
        return decl is StructDecl stdecl && StructIsFrozen(stdecl);
    }

    public ITypeHandler Construct ()
    {
        return new FrozenStructHandler ();
    }

    bool StructIsFrozen(StructDecl stdecl) => false;
}

public class FrozenStructHandler : BaseTypeHandler
{
    public FrozenStructHandler ()
        : base("struct")
    {
    }
}