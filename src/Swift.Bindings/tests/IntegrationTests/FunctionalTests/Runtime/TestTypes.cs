// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Swift;
using Swift.Runtime;

namespace BindingsGeneration.Tests;

struct SwiftIntMock : ISwiftObject
{
    static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>() where TProtocol : class
    {
        var dic = new Dictionary<Type, string>
            {
                { typeof(ISwiftHashable), "$sSiSHsMc"} // protocol conformance descriptor for Swift.Int : Swift.Hashable in Swift
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

    static ISwiftObject ISwiftObject.NewFromPayload(IntPtr payload)
    {
        throw new NotImplementedException();
    }

    int ISwiftObject.MarshalToSwift(ref Span<byte> swiftDestSpan)
    {
        throw new NotImplementedException();
    }
}
