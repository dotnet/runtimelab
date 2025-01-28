// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Swift.Runtime;
using System.Reflection;

namespace BindingsGeneration.Tests;

public class KnownMetadataTests : IClassFixture<KnownMetadataTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public KnownMetadataTests(TestFixture fixture)
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
    public static void HasPrimatives()
    {
        Assert.True(TypeMetadata.TryGetTypeMetadata<bool>(out var md0));
        Assert.True(TypeMetadata.TryGetTypeMetadata<sbyte>(out var md1));
        Assert.True(TypeMetadata.TryGetTypeMetadata<byte>(out var md2));
        Assert.True(TypeMetadata.TryGetTypeMetadata<short>(out var md3));
        Assert.True(TypeMetadata.TryGetTypeMetadata<ushort>(out var md4));
        Assert.True(TypeMetadata.TryGetTypeMetadata<int>(out var md5));
        Assert.True(TypeMetadata.TryGetTypeMetadata<uint>(out var md6));
        Assert.True(TypeMetadata.TryGetTypeMetadata<long>(out var md7));
        Assert.True(TypeMetadata.TryGetTypeMetadata<ulong>(out var md8));
        Assert.True(TypeMetadata.TryGetTypeMetadata<nint>(out var md9));
        Assert.True(TypeMetadata.TryGetTypeMetadata<nuint>(out var md10));
        Assert.True(TypeMetadata.TryGetTypeMetadata<float>(out var md11));
        Assert.True(TypeMetadata.TryGetTypeMetadata<double>(out var md12));
    }
}
