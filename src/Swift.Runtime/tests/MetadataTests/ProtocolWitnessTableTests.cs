// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Swift;
using Swift.Runtime;
using Xunit;

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

    [Fact]
    public static void TryGetRetrievesProtocolWitnessTable()
    {
        var result = ProtocolWitnessTable.TryGet<SwiftIntMock, ISwiftHashable>(out var protocolWitnessTable);
        Assert.True(result);
        Assert.NotNull(protocolWitnessTable);
        Assert.True(protocolWitnessTable!.Value.IsValid);
    }

    [Fact]
    public static void TryGetFailsToRetrieveProtocolWitnessTableWhenTypeDoesNotConformToProtocol()
    {
        var result = ProtocolWitnessTable.TryGet<TypeNotImplementingAnyProtocols, ISwiftHashable>(out var protocolWitnessTable);
        Assert.False(result);
        Assert.Null(protocolWitnessTable);
    }

    [Fact]
    public static void GetOrThrowRetrievesProtocolWitnessTable()
    {
        var protocolWitnessTable = ProtocolWitnessTable.GetOrThrow<SwiftIntMock, ISwiftHashable>();
        Assert.True(protocolWitnessTable.IsValid);
    }

    [Fact]
    public static void GetOrThrowThrowsWhenTypeDoesNotConformToProtocol()
    {
        Assert.Throws<SwiftRuntimeException>(() => ProtocolWitnessTable.GetOrThrow<TypeNotImplementingAnyProtocols, ISwiftHashable>());
    }

    [Fact]
    public static void FailsToRetrieveProtocolWitnessTableWhenProtocolConformanceDescriptorInvalid()
    {
        var result = ProtocolWitnessTable.TryGet<AnyTypeMock, ISwiftHashable>(out var protocolWitnessTable);
        Assert.False(result);
        Assert.Null(protocolWitnessTable);
    }
}
