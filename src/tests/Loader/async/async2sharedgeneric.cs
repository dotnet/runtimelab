// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
public class Async2SharedGeneric
{
    public static Type Type;
    [Fact]
    public static void TestEntryPoint()
    {
        Async1EntryPoint<int>(typeof(int)).Wait();
        Async1EntryPoint<string>(typeof(string)).Wait();
        Async1EntryPoint<object>(typeof(object)).Wait();

        Async2EntryPoint<int>(typeof(int)).Wait();
        Async2EntryPoint<string>(typeof(string)).Wait();
        Async2EntryPoint<object>(typeof(object)).Wait();
    }

    private static async Task Async1EntryPoint<T>(Type t)
    {
        await new GenericClass<T>().InstanceMethod(t);
        await GenericClass<T>.StaticMethod(t);
        await GenericClass<T>.StaticMethod<T>(t, t);
    }

    private static async2 Task Async2EntryPoint<T>(Type t)
    {
        await new GenericClass<T>().InstanceMethod(t);
        await GenericClass<T>.StaticMethod(t);
        await GenericClass<T>.StaticMethod<T>(t, t);
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
}
