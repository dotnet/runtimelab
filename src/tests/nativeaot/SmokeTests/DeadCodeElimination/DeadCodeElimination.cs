// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

class Program
{
    static int Main()
    {
        SanityTest.Run();
        TestInstanceMethodOptimization.Run();
        TestAbstractTypeVirtualsOptimization.Run();

        return 100;
    }

    class SanityTest
    {
        class PresentType { }

        class NotPresentType { }

        public static void Run()
        {
            typeof(PresentType).ToString();

            if (!IsTypePresent(typeof(SanityTest), nameof(PresentType)))
                throw new Exception();

            ThrowIfPresent(typeof(SanityTest), nameof(NotPresentType));
        }
    }

    class TestInstanceMethodOptimization
    {
        class UnreferencedType { }

        class NeverAllocatedType
        {
            public Type DoSomething() => typeof(UnreferencedType);
        }

        static object s_instance = new object[10];

        public static void Run()
        {
            Console.WriteLine("Testing instance methods on unallocated types");

            if (s_instance is NeverAllocatedType never)
                never.DoSomething();

            ThrowIfPresent(typeof(TestInstanceMethodOptimization), nameof(UnreferencedType));
        }
    }

    class TestAbstractTypeVirtualsOptimization
    {
        class UnreferencedType1 { }
        class UnreferencedType2 { }
        class ReferencedType1 { }

        abstract class Base
        {
            public virtual Type GetTheType() => typeof(UnreferencedType1);
            public virtual Type GetOtherType() => typeof(ReferencedType1);
        }

        abstract class Mid : Base
        {
            public override Type GetTheType() => typeof(UnreferencedType2);
        }

        class Derived : Mid
        {
            public override Type GetTheType() => null;
        }

        static Base s_instance = Activator.CreateInstance<Derived>();

        public static void Run()
        {
            Console.WriteLine("Testing virtual methods on abstract types");

            s_instance.GetTheType();
            s_instance.GetOtherType();

            ThrowIfPresent(typeof(TestAbstractTypeVirtualsOptimization), nameof(UnreferencedType1));
            ThrowIfPresent(typeof(TestAbstractTypeVirtualsOptimization), nameof(UnreferencedType2));
        }
    }

    private static bool IsTypePresent(Type testType, string typeName) => testType.GetNestedType(typeName, BindingFlags.NonPublic | BindingFlags.Public) != null;

    private static void ThrowIfPresent(Type testType, string typeName)
    {
        if (IsTypePresent(testType, typeName))
        {
            throw new Exception(typeName);
        }
    }
}
