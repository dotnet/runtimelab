namespace BindingsGeneration;
public class TypeEnv {
    List<PInvoke>? pinvokes = null;
    HashSet<string>? usings = null;
    string? pinvokeName = null;
    public TypeEnv (Conductor conductor, TypeDecl typeDecl, TypeEnv? parentEnv, string entityType)
    {
        this.TypeDecl = typeDecl;
        this.Conductor = conductor;
        this.parentEnv = parentEnv;
        this.EntityType = entityType;;
        if (parentEnv is null) {
            usings = new HashSet<string>();
            pinvokes = new List<PInvoke>();
            string pinvokeName = $"PinvokesFor{typeDecl.Name}"; // FIXME - needs to be a sanitized name
        }
    }

    protected TypeEnv TopMost {
        get {
            var env = this;
            while (env.parentEnv is not null)
                env = env.parentEnv;
            return env;
        }
    }
    public bool IsTopMost { get => parentEnv is null;}

    protected TypeEnv? parentEnv;
    public TypeDecl TypeDecl {get; private set;}
    public Conductor Conductor {get; private set;}

    public void AddUsing(string @namespace)
    {
        var useItem = UsingNamespaces;
        if (!useItem.Contains(@namespace))
            useItem.Add(@namespace);
    }

    public void GenerateDecl(string visibility, bool isStatic, bool isUnsafe, string name, string? generics=null, string? inherits=null, string? where=null)
    {
        Declaration = $"{visibility} {(isStatic ? "static " : "")}{(isUnsafe ? "unsafe " : "")}{EntityType} {name}{(generics is not null ? $"<{generics}>" : "")}{(inherits is not null ? $": {inherits}" : "")}{(where is not null ? where : "")}";
    }

    public string EntityType {get; private set;}
    public string Declaration { get; private set; } = "";

    public List<string> Fields {get; private set; } = new List<string>();
    public List<Method> Methods {get; private set;} = new List<Method>();

    public string PInvokeName => TopMost.pinvokeName!;
    public List<PInvoke> PInvokes => TopMost.pinvokes!;
    public List<Tuple<ITypeHandler, TypeEnv>> InnerTypes {get; private set;} = new List<Tuple<ITypeHandler, TypeEnv>>();
    public HashSet<string> UsingNamespaces => TopMost.usings!;
}