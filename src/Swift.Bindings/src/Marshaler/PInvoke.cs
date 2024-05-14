using System.CodeDom.Compiler;
namespace BindingsGeneration;

public class PInvoke
{
    public string Attribute { get; set; } = "";
    public string Declaration { get; set; } = "";

    public void Write (IndentedTextWriter writer)
    {
        if (!string.IsNullOrEmpty (Attribute))
            writer.WriteLine (Attribute);
        if (!string.IsNullOrEmpty (Declaration))
            writer.WriteLine (Declaration);
    }
}