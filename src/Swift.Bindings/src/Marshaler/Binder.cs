using System.CodeDom.Compiler;

namespace BindingsGeneration;

public class Binder {
    public void Bind(IEnumerable<BaseDecl> declarations)
    {
        var conductor = new Conductor();

        foreach (var decl in declarations.OfType<TypeDecl>()) {
            if (conductor.TryGetTypeHandler(decl, out var declHandler)) {
                var env = declHandler.MakeTypeEnv(conductor, decl, null);
                declHandler.BeginType(env);
                var fileName = $"{decl.Name}.cs";
                using var stm = new StreamWriter (fileName); 
                using var writer = new IndentedTextWriter (stm);
                declHandler.EndType(env, writer);
            } else {
                // error/warning here
            }
        }
    }
}