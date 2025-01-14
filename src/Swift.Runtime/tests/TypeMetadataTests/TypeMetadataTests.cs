// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Swift.Runtime;
using System.Reflection;

namespace BindingsGeneration.Tests;

public class TypeMetadataTests : IClassFixture<TypeMetadataTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public TypeMetadataTests(TestFixture fixture)
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

    static TypeMetadata MakePhonyMetadata(int value)
    {
        IntPtr p = new IntPtr(value);

        var t = typeof(TypeMetadata);
        var ci = t.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { typeof(IntPtr) })!;

        return (TypeMetadata)(ci.Invoke(new object[] { p }));
    }

    [Fact]
    public static void CacheWorks()
    {
        var fakeMeta = MakePhonyMetadata(42);
        TypeMetadata.Cache.GetOrAdd(typeof(System.Convert), (t) =>
        {
            return fakeMeta;
        });
        Assert.True(TypeMetadata.Cache.TryGet(typeof(System.Convert), out var result));
    }

    [Fact]
    public static void TryGetFail()
    {
        var contains = TypeMetadata.Cache.TryGet(typeof(System.EventArgs), out var result);
        Assert.False(contains);
    }

    [Fact]
    public static void TryGetSucceed()
    {
        var fakeMeta = MakePhonyMetadata(43);
        TypeMetadata.Cache.GetOrAdd(typeof(System.Random), (t) =>
        {
            return fakeMeta;
        });
        var contains = TypeMetadata.Cache.TryGet(typeof(System.Random), out var result);
        Assert.True(contains);
    }

    public struct ThisOnlyGetsUsedHere : ISwiftObject {
        static TypeMetadata ISwiftObject.GetTypeMetadata()
        {
            if (TypeMetadata.TryGetTypeMetadata<int>(out var fakeMd)) {
                return TypeMetadata.Cache.GetOrAdd(typeof(ThisOnlyGetsUsedHere), (type) => fakeMd.Value);
            }
            return TypeMetadata.Zero;
        }

        IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
        {
            return swiftDest;
        }

        static ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle payload)
        {
            return new ThisOnlyGetsUsedHere();
        }
    }

    [Fact]
    public static void CanTryGetMetadata()
    {
        Assert.True(TypeMetadata.TryGetTypeMetadata<ThisOnlyGetsUsedHere>(out var md));
    }

    [Fact]
    public static void CannotTryGetMetadata()
    {
        Assert.False(TypeMetadata.TryGetTypeMetadata<TypeMetadataTests>(out var md));
    }

    [Fact]
    public static void TryGetWillThrow()
    {
        Assert.Throws<SwiftRuntimeException>(() => {
            TypeMetadata.GetTypeMetadataOrThrow<TypeMetadataTests>();
        });
    }

    [Fact]
    public static void CanTryGetOnInstance()
    {
        Assert.True(TypeMetadata.TryGetTypeMetadata<ThisOnlyGetsUsedHere>(out var md));
    }

    [Fact]
    public static void CannotGetOnUnknownInstance()
    {
        Assert.False(TypeMetadata.TryGetTypeMetadata<object>(out var md));
    }
}
