// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Swift;
using Swift.Runtime;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BindingsGeneration.Tests;

public class SwiftArrayTests : IClassFixture<SwiftArrayTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public SwiftArrayTests(TestFixture fixture)
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
    static void ArraySizeExpected()
    {
        var metadata = TypeMetadata.GetTypeMetadataOrThrow<SwiftArray<int>>();
        Assert.Equal((nuint)8, metadata.Size);
    }

    [Fact]
    static void SmokeTest()
    {
        var array = new SwiftArray<int>();
        Assert.Empty(array);
    }

    [Fact]
    static void AddsElements()
    {
        var array = new SwiftArray<int>();
        array.Add(42);
        array.Add(17);
        var c = array.Count;
        Assert.Equal(2, c);
        Assert.Equal(17, array[1]);
    }

    [Fact]
    static void ReplacesElement()
    {
        var array = new SwiftArray<int>();
        array.Add(42);
        array.Add(17);
        var c = array.Count;
        Assert.Equal(2, c);
        Assert.Equal(17, array[1]);
        array[1] = 99;
        Assert.Equal(99, array[1]);
    }

    [Fact]
    static void Clears()
    {
        var array = new SwiftArray<int>();
        array.Add(42);
        array.Add(17);
        var c = array.Count;
        Assert.Equal(2, c);
        array.Clear();
        c = array.Count;
        Assert.Equal(0, c);
    }

    [Fact]
    static void RemovesElement()
    {
        var array = new SwiftArray<int>();
        array.Add(42);
        array.Add(17);
        var c = array.Count;
        Assert.Equal(2, c);
        array.RemoveAt(0);
        c = array.Count;
        Assert.Equal(1, c);
        Assert.Equal(17, array[0]);
    }

    [Fact]
    static void InsertsElement()
    {
        var array = new SwiftArray<int>();
        array.Add(42);
        array.Add(17);
        var c = array.Count;
        Assert.Equal(2, c);
        array.Insert(1, 99);
        c = array.Count;
        Assert.Equal(3, c);
        Assert.Equal(17, array[2]);
    }
}
