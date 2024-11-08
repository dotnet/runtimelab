// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
public class Async2SharedGeneric
{
    [Fact]
    public static void TestEntryPoint()
    {
        Async1EntryPoint<int>(typeof(int), 42).Wait();
        Async1EntryPoint<string>(typeof(string), "abc").Wait();
        Async1EntryPoint<object>(typeof(object), "def").Wait();

        Async2EntryPoint<int>(typeof(int), 142).Wait();
        Async2EntryPoint<string>(typeof(string), "ghi").Wait();
        Async2EntryPoint<object>(typeof(object), "jkl").Wait();
    }

    private static async Task Async1EntryPoint<T>(Type t, T value)
    {
        await new GenericClass<T>().InstanceMethod(t);
        await GenericClass<T>.StaticMethod(t);
        await GenericClass<T>.StaticMethod<T>(t, t);
        Assert.Equal(value, await GenericClass<T>.StaticReturnClassType(value));
        Assert.Equal(value, await GenericClass<T>.StaticReturnMethodType<T>(value));
    }

    private static async2 Task Async2EntryPoint<T>(Type t, T value)
    {
        await new GenericClass<T>().InstanceMethod(t);
        await GenericClass<T>.StaticMethod(t);
        await GenericClass<T>.StaticMethod<T>(t, t);
        Assert.Equal(value, await GenericClass<T>.StaticReturnClassType(value));
        Assert.Equal(value, await GenericClass<T>.StaticReturnMethodType<T>(value));
    }
}

public class GenericClass<T>
{
    // 'this' is context
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async2 Task InstanceMethod(Type t)
    {
        Assert.Equal(typeof(T), t);
        await Task.Yield();
        Assert.Equal(typeof(T), t);
    }

    // Class context
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async2 Task StaticMethod(Type t)
    {
        Assert.Equal(typeof(T), t);
        await Task.Yield();
        Assert.Equal(typeof(T), t);
    }

    // Method context
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async2 Task StaticMethod<TM>(Type t, Type tm)
    {
        Assert.Equal(typeof(T), t);
        Assert.Equal(typeof(TM), tm);
        await Task.Yield();
        Assert.Equal(typeof(T), t);
        Assert.Equal(typeof(TM), tm);
    }

    public static async2 Task<T> StaticReturnClassType(T value)
    {
        await Task.Yield();
        return value;
    }

    public static async2 Task<TM> StaticReturnMethodType<TM>(TM value)
    {
        await Task.Yield();
        return value;
    }
}
