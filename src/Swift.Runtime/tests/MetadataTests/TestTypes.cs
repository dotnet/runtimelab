using Swift;
using Swift.Runtime;

namespace BindingsGeneration.Tests;

struct TypeNotImplementingAnyProtocols { }

struct SwiftIntMock : ISwiftObject
{
    public int Value { get; set; }

    public SwiftIntMock(int value)
    {
        Value = value;
    }

    static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>() where TProtocol : class
    {
        var dic = new Dictionary<Type, string>
            {
                { typeof(ISwiftHashable), "$sSiSHsMc"}, // protocol conformance descriptor for Swift.Int : Swift.Hashable in Swift
                { typeof(IEquatable<SwiftIntMock>), "$sSiSQsMc"}, // protocol conformance descriptor for Swift.Int : Swift.Equatable in Swift
            };

        if (!dic.ContainsKey(typeof(TProtocol)))
        {
            throw new SwiftRuntimeException("Protocol conformance not found");
        }

        return ProtocolConformanceDescriptor.LoadFromSymbol("/usr/lib/swift/libswiftCore.dylib", dic[typeof(TProtocol)]);
    }

    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.GetTypeMetadataOrThrow<nint>();
    }

    static ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle payload)
    {
        return new SwiftIntMock((int)payload.Handle);
    }

    nint ISwiftObject.MarshalToSwift(nint swiftDest)
    {
        unsafe
        {
            *(int*)swiftDest = Value;
        }
        return swiftDest;
    }
}

struct AnyTypeMock : ISwiftObject
{
    static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>() where TProtocol : class
    {
        return ProtocolConformanceDescriptor.Zero;
    }

    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Zero;
    }

    static ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle payload)
    {
        throw new NotImplementedException();
    }

    nint ISwiftObject.MarshalToSwift(nint swiftDest)
    {
        throw new NotImplementedException();
    }
}
