using System.CodeDom.Compiler;
namespace BindingsGeneration;

public class Method {
    public Method (string declarationLine)
    {
        DeclarationLine = declarationLine;
    }
    public string DeclarationLine {get; private set;}
    public List<string> BodyContents {get; private set;} = new List<string>();
    public void Write (IndentedTextWriter writer)
    {
        writer.WriteLine (DeclarationLine);
        writer.WriteLine ("{");
        writer.Indent++;
        foreach (var bodyLine in BodyContents) {
            writer.WriteLine (bodyLine);
        }
        writer.Indent--;
        writer.WriteLine ("}");
    }
}