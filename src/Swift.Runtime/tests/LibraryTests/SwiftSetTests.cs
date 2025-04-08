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
    public void SmokeTest()
    {
        var metadata = TypeMetadata.GetTypeMetadataOrThrow<SwiftSet<SwiftIntMock>>();
        // sizeof (Variant)
        Assert.True(metadata.Size == 8);

        var set = new SwiftSet<SwiftIntMock>();
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public unsafe void SetDispose()
    {
        var set = new SwiftSet<SwiftIntMock>();
        Assert.Equal(0, set.Count);
        // An empty array is singleton and it's count doesn't change with new instances
        // https://github.com/swiftlang/swift/blob/50a98d3055e5a636d80c376a99b4eea35387cd0d/stdlib/public/SwiftShims/swift/shims/GlobalObjects.h#L44
        Assert.True(Arc.RetainCount(*(IntPtr*)set.Payload.DangerousGetHandle()) > 1);
    }
}
