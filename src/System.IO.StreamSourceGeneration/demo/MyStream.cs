using System.IO.StreamSourceGeneration;

namespace Demo;

[GenerateStreamBoilerplate]
public partial class MyStream : Stream
{
    public override void Flush()
    {
        throw new NotImplementedException();
    }
}