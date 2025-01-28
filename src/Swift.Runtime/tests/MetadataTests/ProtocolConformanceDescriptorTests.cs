// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Swift.Runtime;

namespace BindingsGeneration.Tests;

public class ProtocolConformanceDescriptorTests : IClassFixture<ProtocolConformanceDescriptorTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public ProtocolConformanceDescriptorTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestFixture
    {
        static TestFixture()
        {
        }

        private static void InitializeResources()
        {
        }
    }

    struct SomeType { }

    interface ISwiftHashableMock
    { }

    struct SwiftIntMock : ISwiftObject
    {
        static ProtocolConformanceDescriptor ISwiftObject.GetProtocolConformanceDescriptor<TProtocol>() where TProtocol : class
        {
            var dic = new Dictionary<Type, string>
            {
                { typeof(ISwiftHashableMock), "$sSiSHsMc"} // protocol conformance descriptor for Swift.Int : Swift.Hashable in Swift
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
            throw new NotImplementedException();
        }

        nint ISwiftObject.MarshalToSwift(nint swiftDest)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public static void RetrievesExistingProtocolConformanceDescriptor()
    {
        var descriptor = ProtocolConformanceDescriptor.LoadFromSymbol("/usr/lib/swift/libswiftCore.dylib", "$sSiSHsMc");
        Assert.True(descriptor.IsValid);
    }

    [Fact]
    public static void FailsToRetrieveNonExistentProtocolConformanceDescriptor()
    {
        Assert.Throws<SwiftRuntimeException>(() => ProtocolConformanceDescriptor.LoadFromSymbol("/usr/lib/swift/libswiftCore.dylib", "nonExistentSymbol"));
    }

    [Fact]
    public static void FailsToRetrieveFromNonExistentLibrary()
    {
        Assert.Throws<SwiftRuntimeException>(() => ProtocolConformanceDescriptor.LoadFromSymbol("nonExistentLibrary", "nonExistentSymbol"));
    }

    [Fact]
    public static void RetrievesProtocolConformanceDescriptorUsingStaticAccessor()
    {
        var result = ProtocolConformanceDescriptor.TryGet<SwiftIntMock, ISwiftHashableMock>(out var descriptor);
        Assert.True(result);
        Assert.NotNull(descriptor);
        Assert.True(descriptor!.Value.IsValid);
    }

    [Fact]
    public static void FailsToRetrieveProtocolConformanceDescriptorUsingStaticAccessorWhenTypeDoesNotConformToProtocol()
    {
        var result = ProtocolConformanceDescriptor.TryGet<SomeType, ISwiftHashableMock>(out var _);
        Assert.False(result);
    }
}
