using System.CodeDom.Compiler;

namespace BindingsGeneration;
public abstract class BaseTypeHandler : ITypeHandler
{
    string entityType;
    protected BaseTypeHandler (string entityType)
    {
        this.entityType = entityType;
    }
    public TypeEnv MakeTypeEnv(Conductor conductor, TypeDecl typeDecl, TypeEnv? parentEnv)
    {
        return new TypeEnv (conductor, typeDecl, parentEnv, entityType);
    }
    public void BeginType(TypeEnv typeEnv)
    {
        // FIXME - values for static and unsafe are wrong
        typeEnv.GenerateDecl("public", false, false, typeEnv.TypeDecl.Name);
        HandleInnerTypes(typeEnv);
        HandleFields(typeEnv);
        HandleProperties(typeEnv);
        HandleIndexers(typeEnv);
        HandleMethods(typeEnv);
    }

    void HandleInnerTypes(TypeEnv typeEnv)
    {
        foreach (var inner in typeEnv.TypeDecl.Declarations.OfType<TypeDecl>())
        {
            if (typeEnv.Conductor.TryGetTypeHandler(inner, out var innerHandler))
            {
                var innerEnv = innerHandler.MakeTypeEnv(typeEnv.Conductor, inner, typeEnv);
                innerHandler.BeginType(innerEnv);
                typeEnv.InnerTypes.Add(new Tuple<ITypeHandler, TypeEnv>(innerHandler, innerEnv));
            }
        }
    }

    void HandleFields(TypeEnv typeEnv)
    {

    }

    void HandleProperties(TypeEnv env)
    {

    }

    void HandleIndexers(TypeEnv env)
    {

    }

    void HandleMethods(TypeEnv env)
    {
        foreach (var method in env.TypeDecl.Declarations.OfType<MethodDecl>())
        {
            if (env.Conductor.TryGetMethodHandler(method, out var methodHandler))
            {
                var methodEnv =  methodHandler.MakeMethodEnv(env.Conductor, method, env);
                methodHandler.Begin(methodEnv);
                env.Methods.Add(methodHandler.End(methodEnv));
            }
        }
    }

    public void EndType(TypeEnv typeEnv, IndentedTextWriter writer)
    {
        if (typeEnv.IsTopMost) {
            foreach (var use in typeEnv.UsingNamespaces)
            {
                writer.WriteLine($"using use");
            }
            // FIXME add namespace declaration
        }
        writer.WriteLine(typeEnv.Declaration);
        
        writer.WriteLine("{");
        writer.Indent++;

        WriteInnerTypes(typeEnv, writer);
        WriteFields(typeEnv, writer);
        WriteProperties(typeEnv, writer);
        WriteIndexers(typeEnv, writer);
        WriteMethods(typeEnv, writer);

        writer.Indent--;
        writer.WriteLine("}");

        if (typeEnv.IsTopMost) 
            WritePInvokes(typeEnv, writer);
    }

    void WriteInnerTypes(TypeEnv typeEnv, IndentedTextWriter writer)
    {
        foreach(var (inner, innerEnv) in typeEnv.InnerTypes)
        {
            inner.EndType(innerEnv, writer);
        }
    }

    void WriteFields(TypeEnv typeEnv, IndentedTextWriter writer)
    {
        foreach (var field in typeEnv.Fields)
        {
            writer.WriteLine(field);
        }

    }

    void WriteProperties(TypeEnv typeEnv, IndentedTextWriter writer)
    {

    }

    void WriteIndexers(TypeEnv typeEnv, IndentedTextWriter writer)
    {

    }

    void WriteMethods(TypeEnv typeEnv, IndentedTextWriter writer)
    {
        foreach (var method in typeEnv.Methods)
        {
            method.Write(writer);
        }
    }

    void WritePInvokes(TypeEnv typeEnv, IndentedTextWriter writer)
    {
        writer.WriteLine();
        writer.WriteLine($"internal static class {typeEnv.PInvokeName}");
        writer.WriteLine("{");
        writer.Indent++;
        foreach (var pi in typeEnv.PInvokes)
        {
            pi.Write(writer);
        }
        writer.Indent--;
        writer.WriteLine("}");
    }
}