
namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    class GeneratedMarshallingAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Struct)]
    public class BlittableTypeAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
    public class NativeMarshallingAttribute : Attribute
    {
        public NativeMarshallingAttribute(Type nativeType)
        {
            NativeType = nativeType;
        }

        public Type NativeType { get; }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Field)]
    public class MarshalUsingAttribute : Attribute
    {
        public MarshalUsingAttribute()
        {
            CountElementName = null!;
        }

        public MarshalUsingAttribute(Type nativeType)
            :this()
        {
            NativeType = nativeType;
        }

        public Type? NativeType { get; }

        public string CountElementName { get; set; }

        public int ConstantElementCount { get; set; }

        public int ElementIndirectionLevel { get; set; }

        public const string ReturnsCountValue = "return-value";
    }

    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
    public sealed class GenericContiguousCollectionMarshallerAttribute : Attribute
    {
        public GenericContiguousCollectionMarshallerAttribute()
        {
        }
    }
}