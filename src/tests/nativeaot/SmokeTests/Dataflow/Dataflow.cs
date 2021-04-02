// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable 649 // 'blah' is never assgined to

class Program
{
    static int Main()
    {
        TestReturnValue.Run();
        TestGetMethodEventFieldPropertyConstructor.Run();
        TestInGenericCode.Run();
        TestAttributeDataflow.Run();
        TestGenericDataflow.Run();
        TestArrayDataflow.Run();
        TestAllDataflow.Run();
        TestDynamicDependency.Run();
        TestDynamicDependencyWithGenerics.Run();

        return 100;
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

    class TestInGenericCode
    {
        class MyGenericType<T>
        {
            public static void MyGenericMethod<U>(T param1, U param2)
            {

            }
        }

        static void GenericMethod<T, U>()
        {
            // Ensure this method body is looked at by dataflow analysis
            Assert.NotNull(typeof(TestType).GetConstructor(new Type[] { typeof(double) }));

            // Regression test for a bug where we would try to resolve !1 (the U parameter)
            // within the signature of MyGenericMethod (that doesn't have a second generic parameter
            // and would cause an out-of-bounds array access at analysis time)
            MyGenericType<U>.MyGenericMethod<U>(default, default);
        }

        class TestType
        {
            public TestType(double c) { }
        }

        public static void Run()
        {
            GenericMethod<object, object>();
        }
    }

    class TestAttributeDataflow
    {
        class RequiresNonPublicMethods : Attribute
        {
            public RequiresNonPublicMethods([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)] Type needed)
            {
            }

            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicMethods)]
            public Type AlsoNeeded { get; set; }

            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
            public Type AndAlsoNeeded;
        }

        static class Type1WithNonPublicKept
        {
            private static void KeptMethod() { }
            public static void RemovedMethod() { }
        }

        static class Type2WithAllKept
        {
            private static void KeptMethod() { }
            public static void AlsoKeptMethod() { }
        }

        static class Type3WithPublicKept
        {
            public static void KeptMethod() { }
            private static void RemovedMethod() { }
        }

        [RequiresNonPublicMethods(typeof(Type1WithNonPublicKept), AlsoNeeded = typeof(Type2WithAllKept), AndAlsoNeeded = typeof(Type3WithPublicKept))]
        public static void Run()
        {
            Assert.Equal(0, typeof(Type1WithNonPublicKept).CountPublicMethods());
            Assert.Equal(1, typeof(Type1WithNonPublicKept).CountMethods());

            Assert.Equal(2, typeof(Type2WithAllKept).CountMethods());

            Assert.Equal(1, typeof(Type3WithPublicKept).CountPublicMethods());
            Assert.Equal(1, typeof(Type3WithPublicKept).CountMethods());
        }
    }

    class TestGenericDataflow
    {
        class Type1WithNonPublicKept
        {
            private static void KeptMethod() { }
            private static void AlsoKeptMethod() { }
            public static void RemovedMethod() { }
        }

        class Type2WithPublicKept
        {
            public static void KeptMethod() { }
            public static void AlsoKeptMethod() { }
            private static void RemovedMethod() { }
        }

        class Type3WithPublicKept
        {
            public static void KeptMethod() { }
            public static void AlsoKeptMethod() { }
            private static void RemovedMethod() { }
        }

        struct Struct1WithPublicKept
        {
            public static void KeptMethod() { }
            public static void AlsoKeptMethod() { }
            private static void RemovedMethod() { }
        }


        class KeepsNonPublic<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)] T>
        {
            public KeepsNonPublic()
            {
                Assert.NotNull(typeof(T).GetMethod("KeptMethod", BindingFlags.NonPublic | BindingFlags.Static));
            }
        }

        class KeepsNonPublic : KeepsNonPublic<Type1WithNonPublicKept>
        {
        }

        static void KeepPublic<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>()
        {
            Assert.NotNull(typeof(T).GetMethod("KeptMethod", BindingFlags.Public | BindingFlags.Static));
        }

        class KeepsPublic<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>
        {
            public static void Keep<U>()
            {
                Assert.NotNull(typeof(T).GetMethod("KeptMethod", BindingFlags.Public | BindingFlags.Static));
            }
        }

        public static void Run()
        {
            new KeepsNonPublic();
            Assert.Equal(2, typeof(Type1WithNonPublicKept).CountMethods());
            Assert.Equal(0, typeof(Type1WithNonPublicKept).CountPublicMethods());

            KeepPublic<Type2WithPublicKept>();
            Assert.Equal(2, typeof(Type2WithPublicKept).CountMethods());
            Assert.Equal(2, typeof(Type2WithPublicKept).CountPublicMethods());

            KeepPublic<Struct1WithPublicKept>();
            Assert.Equal(2, typeof(Struct1WithPublicKept).CountMethods());
            Assert.Equal(2, typeof(Struct1WithPublicKept).CountPublicMethods());

            KeepsPublic<Type3WithPublicKept>.Keep<object>();
            Assert.Equal(2, typeof(Type3WithPublicKept).CountMethods());
            Assert.Equal(2, typeof(Type3WithPublicKept).CountPublicMethods());
        }
    }

    class TestArrayDataflow
    {
        public static void Run()
        {
            // System.Array has 7 public properties
            // This test might be a bit fragile, but we want to make sure accessing properties
            // on an array triggers same as accessing properties on System.Array.
            Assert.Equal(7, typeof(int[]).GetProperties().Length);

            // Regression test for when dataflow analysis was trying to generate method bodies for these
            Assert.Equal(1, typeof(int[]).GetConstructors().Length);
        }
    }

    class TestAllDataflow
    {
        class Base
        {
            private static int GetNumber() => 42;
        }

        class Derived : Base
        {
        }

        private static MethodInfo GetPrivateMethod([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type t)
        {
            return t.BaseType.GetMethod("GetNumber", BindingFlags.Static | BindingFlags.NonPublic);
        }

        public static void Run()
        {
            if ((int)GetPrivateMethod(typeof(Derived)).Invoke(null, Array.Empty<object>()) != 42)
                throw new Exception();
        }
    }

    class TestDynamicDependency
    {
        class TypeWithPublicMethodsKept
        {
            public int Method1() => throw null;
            protected int Method2() => throw null;
        }

        class TypeWithAllMethodsKept
        {
            public int Method1() => throw null;
            protected int Method2() => throw null;
        }

        class TypeWithSpecificMethodKept
        {
            public int Method1() => throw null;
            public int Method2() => throw null;
            public int Method3() => throw null;
        }

        class TypeWithSpecificOverloadKept
        {
            public int Method(int x, int y) => throw null;
            public int Method(int x, char y) => throw null;
        }

        class TypeWithAllOverloadsKept
        {
            public int Method(int x, int y) => throw null;
            public int Method(int x, char y) => throw null;
        }

        class TypeWithPublicPropertiesKept
        {
            public int Property1 { get; set; }
            private int Property2 { get; set; }
        }

        public static void DependentMethod() => throw null;
        public static void UnreachedMethod() => throw null;

        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(TypeWithPublicMethodsKept))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods, typeof(TypeWithAllMethodsKept))]
        [DynamicDependency("Method2", typeof(TypeWithSpecificMethodKept))]
        [DynamicDependency("Method(System.Int32,System.Int32)", typeof(TypeWithSpecificOverloadKept))]
        [DynamicDependency("Method", typeof(TypeWithAllOverloadsKept))]
        [DynamicDependency(nameof(DependentMethod))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(TypeWithPublicPropertiesKept))]
        public static void Run()
        {
            Assert.Equal(1, typeof(TypeWithPublicMethodsKept).CountMethods());
            Assert.Equal(2, typeof(TypeWithAllMethodsKept).CountMethods());
            Assert.Equal(1, typeof(TypeWithSpecificMethodKept).CountMethods());
            Assert.Equal(1, typeof(TypeWithSpecificOverloadKept).CountMethods());
            Assert.Equal(2, typeof(TypeWithAllOverloadsKept).CountMethods());
            Assert.Equal(2, typeof(TestDynamicDependency).CountMethods());
            Assert.Equal(1, typeof(TypeWithPublicPropertiesKept).CountProperties());
        }
    }

    class TestDynamicDependencyWithGenerics
    {
        class TypeWithPublicMethodsKept<T>
        {
            public int Method1() => throw null;
            protected int Method2() => throw null;
        }

        class TypeWithAllMethodsKept<T>
        {
            public int Method1() => throw null;
            protected int Method2() => throw null;
        }

        class TypeWithSpecificMethodKept<T>
        {
            public int Method1() => throw null;
            public int Method2() => throw null;
            public int Method3() => throw null;
        }

        class TypeWithSpecificOverloadKept<T>
        {
            public int Method(int x, int y) => throw null;
            public int Method(int x, char y) => throw null;
        }

        class TypeWithAllOverloadsKept<T>
        {
            public int Method(int x, int y) => throw null;
            public int Method(int x, char y) => throw null;
        }

        class TypeWithPublicPropertiesKept<T>
        {
            public int Property1 { get; set; }
            private int Property2 { get; set; }
        }

        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(TypeWithPublicMethodsKept<>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods, typeof(TypeWithAllMethodsKept<>))]
        [DynamicDependency("Method2", typeof(TypeWithSpecificMethodKept<>))]
        [DynamicDependency("Method(System.Int32,System.Int32)", typeof(TypeWithSpecificOverloadKept<>))]
        [DynamicDependency("Method", typeof(TypeWithAllOverloadsKept<>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(TypeWithPublicPropertiesKept<>))]
        public static void Run()
        {
            Assert.Equal(1, typeof(TypeWithPublicMethodsKept<>).CountMethods());
            Assert.Equal(2, typeof(TypeWithAllMethodsKept<>).CountMethods());
            Assert.Equal(1, typeof(TypeWithSpecificMethodKept<>).CountMethods());
            Assert.Equal(1, typeof(TypeWithSpecificOverloadKept<>).CountMethods());
            Assert.Equal(2, typeof(TypeWithAllOverloadsKept<>).CountMethods());
            Assert.Equal(1, typeof(TypeWithPublicPropertiesKept<>).CountProperties());
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
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
        Justification = "That's the point")]
    public static int CountConstructors(this Type t)
        => t.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Length;
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
        Justification = "That's the point")]
    public static int CountPublicConstructors(this Type t)
        => t.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public).Length;
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
        Justification = "That's the point")]
    public static int CountMethods(this Type t)
        => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length;
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
        Justification = "That's the point")]
    public static int CountPublicMethods(this Type t)
        => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly).Length;
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
        Justification = "That's the point")]
    public static int CountFields(this Type t)
        => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length;
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
        Justification = "That's the point")]
    public static int CountProperties(this Type t)
        => t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length;
}
