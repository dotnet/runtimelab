// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable 1998

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

public class Async2StructMutate
{
    [Fact]
    public static void TestEntryPoint()
    {
        Async().Wait();
    }

    private static async2 Task Async()
    {
        S s = new S(100);
        await s.Test();
        Assert.Equal(100, s.Value);
    }

    private struct S
    {
        public int Value;

        public S(int value) => Value = value;

        public async2 Task Test()
        {
            S copy = this; // should be done by Roslyn for backwards compat
            Assert.Equal(100, copy.Value);
            copy.Value++;
            await copy.InstanceCall();
            Assert.Equal(101, copy.Value);

            await copy.TaskButNotAsync();
            Assert.Equal(102, copy.Value);
        }

        private async2 Task InstanceCall()
        {
            S copy = this; // should be done by Roslyn for backwards compat
            Assert.Equal(101, copy.Value);
            copy.Value++;
            Assert.Equal(102, copy.Value);
            await Task.Yield();
            Assert.Equal(102, copy.Value);
        }

        private Task TaskButNotAsync()
        {
            Assert.Equal(101, Value);
            Value++;
            return Task.CompletedTask;
        }
    }
}
