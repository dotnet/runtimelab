// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Swift;
using Swift.Runtime;
using Xunit;

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
    static void SmokeTest()
    {
        var metadata = TypeMetadata.GetTypeMetadataOrThrow<SwiftArray<int>>();
        // sizeof(ArrayBuffer)
        Assert.Equal((nuint)8, metadata.Size);

        var array = new SwiftArray<int>();
        Assert.Equal(0, array.Count);
    }

    [Fact]
    static void Append()
    {
        var array = new SwiftArray<int>();
        array.Append(42);
        array.Append(17);
        var c = array.Count;
        Assert.Equal(2, c);
        Assert.Equal(17, array[1]);
    }

    [Fact]
    static void Insert()
    {
        var array = new SwiftArray<int>();
        array.Append(42);
        array.Append(17);
        var c = array.Count;
        Assert.Equal(2, c);
        array.Insert(1, 99);
        c = array.Count;
        Assert.Equal(3, c);
        Assert.Equal(17, array[2]);
    }

    [Fact]
    static void Replace()
    {
        var array = new SwiftArray<int>();
        array.Append(42);
        array.Append(17);
        var c = array.Count;
        Assert.Equal(2, c);
        Assert.Equal(17, array[1]);
        array[1] = 99;
        Assert.Equal(99, array[1]);
    }

    [Fact]
    static void Remove()
    {
        var array = new SwiftArray<int>();
        array.Append(42);
        array.Append(17);
        var c = array.Count;
        Assert.Equal(2, c);
        array.Remove(0);
        c = array.Count;
        Assert.Equal(1, c);
        Assert.Equal(17, array[0]);
    }

    [Fact]
    static void Clear()
    {
        var array = new SwiftArray<int>();
        array.Append(42);
        array.Append(17);
        var c = array.Count;
        Assert.Equal(2, c);
        array.RemoveAll();
        c = array.Count;
        Assert.Equal(0, c);
    }

    [Fact]
    public void LargeArray()
    {
        var array = new SwiftArray<int>();
        const int count = 1000000;
        for (int i = 0; i < count; i++)
        {
            array.Append(i);
        }
        Assert.Equal(count, array.Count);

        Assert.Equal(0, array[0]);
        Assert.Equal(1, array[1]);
        Assert.Equal(999999, array[count - 1]);
    }

    [Fact(Skip = "https://github.com/dotnet/runtimelab/issues/2851")]
    public unsafe void ArrayDispose()
    {
        var array = new SwiftArray<int>();
        var payload = *(IntPtr*)array.Payload.storage;

        // Retain the payload to ensure it stays alive after the dispose
        Arc.Retain(payload);
        var count = Arc.RetainCount(payload);

        array.Dispose();

        Assert.Equal(count - 1, Arc.RetainCount(payload));
        // Release the payload after the assertion
        Arc.Release(payload);
    }

    private static void PrimitiveArrayTest<T>(T value1, T value2, T overwriteValue)
    {
        var metadata = TypeMetadata.GetTypeMetadataOrThrow<SwiftArray<T>>();
        Assert.True(metadata.Size > 0);

        var array = new SwiftArray<T>();
        Assert.Equal(0, array.Count);

        array.Append(value1);
        Assert.Equal(1, array.Count);
        Assert.Equal(value1, array[0]);

        array.Append(value2);
        Assert.Equal(2, array.Count);
        Assert.Equal(value2, array[1]);

        array[0] = overwriteValue;
        Assert.Equal(overwriteValue, array[0]);

        array.Insert(1, value1);
        Assert.Equal(3, array.Count);

        array.Remove(1);
        Assert.Equal(2, array.Count);

        Assert.Equal(overwriteValue, array[0]);
        Assert.Equal(value2, array[1]);

        array.RemoveAll();
        Assert.Equal(0, array.Count);
    }

    [Fact] public void ArrayTestSByte() => PrimitiveArrayTest<sbyte>(42, 17, 100);
    [Fact] public void ArrayTestByte() => PrimitiveArrayTest<byte>(42, 17, 100);
    [Fact] public void ArrayTestShort() => PrimitiveArrayTest<short>(42, 17, 100);
    [Fact] public void ArrayTestUShort() => PrimitiveArrayTest<ushort>(42, 17, 100);
    [Fact] public void ArrayTestInt() => PrimitiveArrayTest<int>(42, 17, 100);
    [Fact] public void ArrayTestUInt() => PrimitiveArrayTest<uint>(42, 17, 100);
    [Fact] public void ArrayTestLong() => PrimitiveArrayTest<long>(42, 17, 100);
    [Fact] public void ArrayTestULong() => PrimitiveArrayTest<ulong>(42, 17, 100);
    [Fact] public void ArrayTestFloat() => PrimitiveArrayTest<float>(4.2f, 1.7f, 10.0f);
    [Fact] public void ArrayTestDouble() => PrimitiveArrayTest<double>(4.2, 1.7, 10.0);
    [Fact] public void ArrayTestBool() => PrimitiveArrayTest<bool>(true, false, true);
    [Fact] public void ArrayTestString() => PrimitiveArrayTest<SwiftString>(new SwiftString("Hello"), new SwiftString("World"), new SwiftString("String"));
}
