// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Swift;
using Swift.Runtime;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BindingsGeneration.Tests;

public class SwiftOptionalTests : IClassFixture<SwiftOptionalTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public SwiftOptionalTests(TestFixture fixture)
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
    public static void TestIntOptionalSome()
    {
        var optional = SwiftOptional<int>.NewSome(42);
        Assert.Equal(SwiftOptionalCases.Some, optional.Case);
        Assert.Equal(42, optional.Some);
    }

    [Fact]
    public static void TestIntOptionalNone()
    {
        var optional = SwiftOptional<int>.NewNone();
        Assert.Equal(SwiftOptionalCases.None, optional.Case);
    }


    [Fact]
    public static void TestOptionIntOptionSome()
    {
        var opt0 = SwiftOptional<int>.NewSome(42);
        var opt1 = SwiftOptional<SwiftOptional<int>>.NewSome(opt0);
        Assert.Equal(SwiftOptionalCases.Some, opt1.Case);
        Assert.True(opt1.HasValue);
        var opt2 = opt1.Value;
        Assert.NotNull(opt2);
        Assert.Equal(SwiftOptionalCases.Some, opt2?.Case);
        Assert.Equal(42, opt2?.Some);
    }
}