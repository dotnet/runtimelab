
using Mono.Cecil;

namespace MstatDump;

public class TypeStats
{
    public TypeReference Type { get; set; }
    public int Size { get; set; }
}

public class MethodStats
{
    public MethodReference Method { get; set; }
    public int Size { get; set; }
    public int GcInfoSize { get; set; }
    public int EhInfoSize { get; set; }
}

public class BlobStats
{
    public string Name { get; set; }
    public int Size { get; set; }
}