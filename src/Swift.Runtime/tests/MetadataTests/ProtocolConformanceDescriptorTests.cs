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
        var result = ProtocolConformanceDescriptor.TryGet<TypeNotImplementingAnyProtocols, ISwiftHashableMock>(out var _);
        Assert.False(result);
    }
}
