using System.IO.StreamSourceGeneration;

namespace ClassLibrary1;

[GenerateStreamBoilerplate]
public partial class MyStream : Stream
{
    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return 0;
    }
}