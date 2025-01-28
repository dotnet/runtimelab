// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Swift.Runtime;

namespace BindingsGeneration.Tests;

public class ProtocolWitnessTableTests : IClassFixture<ProtocolWitnessTableTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public ProtocolWitnessTableTests(TestFixture fixture)
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

    [Fact]
    public static void TryGetRetrievesProtocolWitnessTable()
    {
        var result = ProtocolWitnessTable.TryGet<SwiftIntMock, ISwiftHashableMock>(out var protocolWitnessTable);
        Assert.True(result);
        Assert.NotNull(protocolWitnessTable);
        Assert.True(protocolWitnessTable!.Value.IsValid);
    }

    [Fact]
    public static void TryGetFailsToRetrieveProtocolWitnessTableWhenTypeDoesNotConformToProtocol()
    {
        var result = ProtocolWitnessTable.TryGet<SomeType, ISwiftHashableMock>(out var protocolWitnessTable);
        Assert.False(result);
        Assert.Null(protocolWitnessTable);
    }

    [Fact]
    public static void GetOrThrowRetrievesProtocolWitnessTable()
    {
        var protocolWitnessTable = ProtocolWitnessTable.GetOrThrow<SwiftIntMock, ISwiftHashableMock>();
        Assert.True(protocolWitnessTable.IsValid);
    }

    [Fact]
    public static void GetOrThrowThrowsWhenTypeDoesNotConformToProtocol()
    {
        Assert.Throws<SwiftRuntimeException>(() => ProtocolWitnessTable.GetOrThrow<SomeType, ISwiftHashableMock>());
    }

    [Fact]
    public static void FailsToRetrieveProtocolWitnessTableWhenProtocolConformanceDescriptorInvalid()
    {
        var result = ProtocolWitnessTable.TryGet<AnyTypeMock, ISwiftHashableMock>(out var protocolWitnessTable);
        Assert.False(result);
        Assert.Null(protocolWitnessTable);
    }
}
