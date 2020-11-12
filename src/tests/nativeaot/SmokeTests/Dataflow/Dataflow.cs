// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable 649 // 'blah' is never assgined to

class Program
{
    static void Main()
    {
        TestReturnValue.Run();
        TestGetMethodEventFieldPropertyConstructor.Run();
    }

    class TestReturnValue
    {
        class PublicOnly
        {
            public PublicOnly(int x) { }
            private PublicOnly(double x) { }
        }

        class PublicAndPrivate
        {
            public PublicAndPrivate(int x) { }
            private PublicAndPrivate(double x) { }
        }

        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        static Type GiveMePublic() => typeof(PublicOnly);

        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
        static Type GiveMePublicAndPrivate() => typeof(PublicAndPrivate);

        public static void Run()
        {
            GiveMePublic();
            Assert.Equal(1, typeof(PublicOnly).CountConstructors());

            GiveMePublicAndPrivate();
            Assert.Equal(2, typeof(PublicAndPrivate).CountConstructors());
        }
    }

    class TestGetMethodEventFieldPropertyConstructor
    {
        static class TestType1
        {
            public static void TestMethod() => throw null;
            public static void UnreferencedMethod() => throw null;
            public static int TestField;
            public static int UnreferencedField;
        }

        static class TestType2
        {
            public static int TestProperty { get; set; }
            public static int UnreferencedProperty { get; set; }
        }

        class TestType3
        {
            public TestType3(int val) { }
            private TestType3(double val) { }
        }

        public static void Run()
        {
            Assert.NotNull(typeof(TestType1).GetMethod(nameof(TestType1.TestMethod)));
            Assert.Equal(1, typeof(TestType1).CountMethods());

            //Assert.NotNull(typeof(TestType1).GetField(nameof(TestType1.TestField)));
            //Assert.Equal(1, typeof(TestType1).CountFields());

            Assert.NotNull(typeof(TestType2).GetProperty(nameof(TestType2.TestProperty)));
            Assert.NotNull(typeof(TestType2).GetProperty(nameof(TestType2.TestProperty)).GetGetMethod());
            Assert.NotNull(typeof(TestType2).GetProperty(nameof(TestType2.TestProperty)).GetSetMethod());
            Assert.Equal(1, typeof(TestType2).CountProperties());
            Assert.Equal(2, typeof(TestType2).CountMethods());

            Assert.NotNull(typeof(TestType3).GetConstructor(new Type[] { typeof(int) }));
            Assert.Equal(1, typeof(TestType3).CountConstructors());
        }
    }
}

static class Assert
{
    public static void Equal(int expected, int actual)
    {
        if (expected != actual)
            throw new Exception($"{expected} != {actual}");
    }

    public static void NotNull(object o)
    {
        if (o is null)
            throw new Exception();
    }
}

static class Helpers
{
    public static int CountConstructors(this Type t)
        => t.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Length;
    public static int CountPublicConstructors(this Type t)
        => t.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public).Length;
    public static int CountMethods(this Type t)
        => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length;
    public static int CountPublicMethods(this Type t)
        => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly).Length;
    public static int CountFields(this Type t)
        => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length;
    public static int CountProperties(this Type t)
        => t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length;
}
