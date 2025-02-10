// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Swift;
using Swift.Runtime;
using Xunit;

namespace BindingsGeneration.Tests;

public class SwiftSetTests : IClassFixture<SwiftSetTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public SwiftSetTests(TestFixture fixture)
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
    static void SmokeTest()
    {
        var metadata = TypeMetadata.GetTypeMetadataOrThrow<SwiftSet<SwiftIntMock>>();
        // sizeof (Variant)
        Assert.True(metadata.Size == 8);

        var set = new SwiftSet<SwiftIntMock>();
        Assert.Equal(0, set.Count);
    }

    [Fact(Skip = "https://github.com/dotnet/runtimelab/issues/2851")]
    public unsafe void SetDispose()
    {
        var array = new SwiftSet<SwiftIntMock>();
        var payload = *(IntPtr*)array.Payload.rawValue;

        // Retain the payload to ensure it stays alive after the dispose
        Arc.Retain(payload);
        var count = Arc.RetainCount(payload);

        array.Dispose();

        Assert.Equal(count - 1, Arc.RetainCount(payload));
        // Release the payload after the assertion
        Arc.Release(payload);
    }
}
