// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// This is a manual test. It is meant to be compiled and then stepped through with the debugger,
// verifiying the assertions provided in comments. Note as well that at this time, Debugger.Break
// does not work right (an upstream issue), so breakpoints have to be set manually.
//
using System;
using System.Diagnostics;

unsafe class Program
{
    public static int Main()
    {
        SimpleStruct simpleStruct = new() { IntField = 1, FloatField = 2.0f };
        SimpleClass simpleClass = new() { IntField = 1, FloatField = 2.0f };

        TestBasicTypesDisplay(true, false, 'a', 1, -1, 2, -2, 3, -3, 4, -4, 5, -5, 1.0f, 2.0);
        TestEnumDisplay(DayOfWeek.Monday);
        TestByRefDisplay(ref simpleStruct, ref simpleClass);
        TestPointerDisplay(&simpleStruct);
        TestStringDisplay("Basic string");
        TestBasicArrayDisplay(new int[] { 1, 2, 3 }, new double[] { 1, 2, 3 });
        TestComplexArrayDisplay(new[] { simpleClass }, new[] { simpleStruct });
        TestBasicMultiDimensionalArrayDisplay(new int[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        TestSimpleStructDisplay(simpleStruct);
        TestSimpleClassDisplay(simpleClass);
        TestDerivedClassDisplay(new() { IntField = 1, FloatField = 2.0f, LongField = 3 });
        TestRecursiveClassDisplay(new() { Value = 1, Next = new() { Value = 2 } });

        simpleStruct.TestStructInstanceMethod();
        simpleClass.TestClassInstanceMethod();
        TestVariables(5, simpleStruct, simpleClass);

        // TODO-LLVM-DI: debugging and EH.
        return 100;
    }

    private static void TestBasicTypesDisplay(
        bool boolTrue, bool boolFalse,
        char charA,
        byte i1, sbyte iM1, ushort i2, short iM2, uint i3, int iM3, ulong i4, long iM4, nuint i5, nint iM5,
        float f1, double d2)
    {
        Debugger.Break(); // All of the parameters should be inspectable and have their designated values.
    }

    private static void TestEnumDisplay(DayOfWeek dayOfWeek)
    {
        Debugger.Break(); // The "dayOfWeek" parameter should be inspectable and equal to "DayOfWeek.Monday".
    }

    private static void TestByRefDisplay(ref SimpleStruct p1, ref SimpleClass p2)
    {
        Debugger.Break(); // Both "p1" and "p2" parameters should be inspectable and equal to "{ 1, 2.0 }".
    }

    private static void TestPointerDisplay(SimpleStruct* s)
    {
        Debugger.Break(); // The "*s" value should be inspectable and equal to "{ 1, 2.0 }".
    }

    private static void TestStringDisplay(string s)
    {
        Debugger.Break(); // The "s" parameter should be inspectable and equal to "Basic string".
    }

    private static void TestBasicArrayDisplay(int[] p1, double[] p2)
    {
        Debugger.Break(); // Both "p1" and "p2" parameters should be inspectable and equal to "{ 1, 2, 3 }".
    }

    private static void TestComplexArrayDisplay(SimpleClass[] p1, SimpleStruct[] p2)
    {
        Debugger.Break(); // Both "p1" and "p2" parameters should be inspectable and equal to "{ { 1, 2.0 } }".
    }

    private static void TestBasicMultiDimensionalArrayDisplay(int[,] s)
    {
        Debugger.Break(); // The "s" parameter should be inspectable and equal to "{ { 1, 2, 3 }, { 4, 5, 6 }".
    }

    private static void TestSimpleStructDisplay(SimpleStruct s)
    {
        Debugger.Break(); // The "s" parameter should be inspectable and equal to "{ 1, 2.0 }".
    }

    private static void TestSimpleClassDisplay(SimpleClass s)
    {
        Debugger.Break(); // The "s" parameter should be inspectable and equal to "{ 1, 2.0 }".
    }

    private static void TestDerivedClassDisplay(DerivedClass s)
    {
        Debugger.Break(); // The "s" parameter should be inspectable and equal to "{ 1, 2.0, 3 }".
    }

    private static void TestRecursiveClassDisplay(RecursiveClass s)
    {
        Debugger.Break(); // The "s" parameter should be inspectable and equal to "{ 1, Next = { 2 } }".
    }

    private static void TestVariables(int p1, SimpleStruct p2, SimpleClass p3)
    {
        int unusedBasicLocal = 1;
        Debugger.Break(); // "unusedBasicLocal" should be equal to "1".

        SimpleClass unusedClassLocal = new() { IntField = 1, FloatField = 2.0f };
        Debugger.Break(); // "unusedClassLocal" should be equal to "{ 1, 2.0 }".

        int index;
        Debugger.Break(); // "index" should be to "0".
        for (index = 0; index < 10; index++) { }
        Debugger.Break(); // "index" should be to "10".

        SimpleClass classLocal = new() { IntField = 1 };
        Debugger.Break(); // "classLocal.IntField" should be equal to "1".
        classLocal.IntField = 2;
        Debugger.Break(); // "classLocal.IntField" should be equal to "2".

        SimpleStruct structLocal = new() { IntField = 1 };
        Debugger.Break(); // "structLocal.IntField" should be equal to "1".
        structLocal.IntField = 2;
        Debugger.Break(); // "structLocal.IntField" should be equal to "2".

        p2.IntField = p1;
        p3.IntField = p1;
        Debugger.Break(); // "p2.IntField" and "p3.IntField" should be equal to "p1".

        structLocal = p2;
        Debugger.Break(); // "structLocal" should be equal to "p2".
        classLocal = p3;
        Debugger.Break(); // "classLocal" should be equal to "p3".        
    }
}

struct SimpleStruct
{
    public int IntField;
    public float FloatField;

    public void TestStructInstanceMethod()
    {
        Debugger.Break(); // "this" should be equal to "{ 1, 2.0 }".
    }
}

class SimpleClass
{
    public int IntField;
    public float FloatField;

    public void TestClassInstanceMethod()
    {
        Debugger.Break(); // "this" should be equal to "{ 1, 2.0 }".
    }
}

class DerivedClass : SimpleClass
{
    public long LongField;
}

class RecursiveClass
{
    public int Value;
    public RecursiveClass Next;
}
