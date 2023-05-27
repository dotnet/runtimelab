// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal unsafe partial class Program
{
    internal static bool Success = true;

    private static bool TestTryCatch()
    {
        // break out the individual tests to their own methods to make looking at the funclets easier
        TestTryCatchNoException();

        TestTryCatchThrowException(new Exception());

        TestTryCatchExceptionFromCall();

        TestCatchExceptionType();

        TestTryFinally();

        TestTryFinallyThrowException();

        TestTryFinallyCatchException();

        TestInnerTryFinallyOrder();

        TestTryCatchWithCallInIf();

        TestThrowInCatch();

        TestExceptionInGvmCall();

        TestCatchHandlerNeedsGenericContext();

        TestFilterHandlerNeedsGenericContext();

        TestFilter();

        TestFilterNested();

        TestIntraFrameFilterOrderBasic();

        TestIntraFrameFilterOrderDeep();

        TestDynamicStackAlloc();

        TestCatchAndThrow();

        TestRethrow();

        return Success;
    }

    private static void TestTryCatchNoException()
    {
        bool caught = false;
        StartTest("Catch not called when no exception test");
        try
        {
            new Exception();
        }
        catch (Exception)
        {
            caught = true;
        }
        EndTest(!caught);
    }

    // pass the exception to avoid a call/invoke for that ctor in this function
    private static void TestTryCatchThrowException(Exception e)
    {
        bool caught = false;
        StartTest("Catch called when exception thrown test");
        try
        {
            throw e;
        }
        catch (Exception)
        {
            PrintLine("caught");
            caught = true;
        }
        EndTest(caught);
    }

    private static void TestTryCatchExceptionFromCall()
    {
        bool caught = false;
        StartTest("Catch called when exception thrown from call");
        try
        {
            ThrowException(new Exception());
        }
        catch (Exception)
        {
            caught = true;
        }
        EndTest(caught);
    }

    private static void TestCatchExceptionType()
    {
        int i = 1;
        StartTest("Catch called for exception type and order");
        try
        {
            throw new NullReferenceException("test"); // the parameterless ctor is causing some unexplained memory corruption with the EHInfo pointers...
        }
        catch (ArgumentException)
        {
            i += 10;
        }
        catch (NullReferenceException e)
        {
            if (e.Message == "test")
            {
                i += 100;
            }
        }
        catch (Exception)
        {
            i += 1000;
        }
        EndTest(i == 101);
    }

    private static void TestTryFinally()
    {
        // Ensures all of the blocks of a try/finally function are hit when there aren't exceptions.
        StartTest("Try/Finally test");
        uint result = TryFinallyInner();
        if (result == 1111)
        {
            PassTest();
        }
        else
        {
            FailTest("Result: " + result.ToString());
        }
    }

    private static uint TryFinallyInner()
    {
        uint result = 1;
        try
        {
            result += 10;
        }
        finally
        {
            result += 100;
        }
        result += 1000;

        return result;
    }

    static string clauseExceution;
    private static void TestTryFinallyThrowException()
    {
        clauseExceution = "";
        StartTest("Try/Finally calls finally when exception thrown test");
        try
        {
            TryFinally();
        }
        catch (Exception)
        {
            clauseExceution += "COuter";
        }

        if (clauseExceution != "CInnerFCOuter")
        {
            FailTest("Expected CInnerFCOuter, but was " + clauseExceution);
        }
        else
        {
            PassTest();
        }
    }

    private static void TryFinally()
    {
        try
        {
            throw new Exception();
        }
        catch
        {
            clauseExceution += "CInner";
            throw;
        }
        finally
        {
            clauseExceution += "F";
        }
    }

    private static void TestTryFinallyCatchException()
    {
        clauseExceution = "";
        StartTest("Try/Finally calls finally once when exception thrown and caught test");

        TryFinallyWithCatch();

        if (clauseExceution != "CF")
        {
            FailTest("Expected CF, but was " + clauseExceution);
        }
        else
        {
            PassTest();
        }
    }

    private static void TryFinallyWithCatch()
    {
        try
        {
            throw new Exception();
        }
        catch
        {
            clauseExceution += "C";
        }
        finally
        {
            clauseExceution += "F";
        }
    }

    private static void TestInnerTryFinallyOrder()
    {
        clauseExceution = "";
        StartTest("Inner try finally called before outer catch");

        try
        {
            try
            {
                try
                {
                    throw new Exception();
                }
                finally
                {
                    clauseExceution += "F1";
                }
            }
            finally
            {
                clauseExceution += "F2";
            }

            // not reached
            try
            {
            }
            finally
            {
                clauseExceution += "F3";
            }
        }
        catch
        {
            clauseExceution += "C";
        }

        if (clauseExceution != "F1F2C")
        {
            FailTest("Expected F1F2C, but was " + clauseExceution);
        }
        else
        {
            PassTest();
        }
    }

    private static void TestTryCatchWithCallInIf()
    {
        int i = 1;
        bool caught = false;
        StartTest("Test invoke when last instruction in if block");
        try
        {
            if (i == 1)
            {
                PrintString("");
            }
        }
        catch
        {
            caught = true;
        }
        EndTest(!caught);
    }

    private static void TestThrowInCatch()
    {
        int i = 0;
        StartTest("Throw exception in catch");
        Exception outer = new Exception();
        Exception inner = new Exception();
        try
        {
            ThrowException(outer);
        }
        catch
        {
            i += 1;
            try
            {
                ThrowException(inner);
            }
            catch (Exception e)
            {
                if (object.ReferenceEquals(e, inner)) i += 10;
            }
        }
        EndTest(i == 11);
    }

    private static void TestExceptionInGvmCall()
    {
        StartTest("TestExceptionInGvmCall");

        var shouldBeFalse = CatchGvmThrownException(new GenBase<string>(), (string)null);
        var shouldBeTrue = CatchGvmThrownException(new DerivedThrows<string>(), (string)null);

        EndTest(shouldBeTrue && !shouldBeFalse);
    }

    private static bool CatchGvmThrownException<T>(GenBase<T> g, T p)
    {
        try
        {
            var i = 1;
            if (i == 1)
            {
                g.GMethod1(p, p);
            }
        }
        catch (Exception e)
        {
            return e.Message == "ToStringThrows"; // also testing here that we can return a value out of a catch
        }
        return false;
    }

    private static void TestCatchHandlerNeedsGenericContext()
    {
        StartTest("Catch handler can access generic context");
        DerivedCatches<object> c = new DerivedCatches<object>();
        EndTest(c.GvmInCatch<string>("a", "b") == "GenBase<System.Object>.GMethod1<System.String>(a,b)");
    }

    private static void TestFilterHandlerNeedsGenericContext()
    {
        StartTest("Filter funclet can access generic context");
        DerivedCatches<object> c = new DerivedCatches<object>();
        EndTest(c.GvmInFilter<string>("a", "b"));
    }

    class GenBase<A>
    {
        public virtual string GMethod1<T>(T t1, T t2) { return "GenBase<" + typeof(A) + ">.GMethod1<" + typeof(T) + ">(" + t1 + "," + t2 + ")"; }
    }

    class DerivedThrows<A> : GenBase<A>
    {
        public override string GMethod1<T>(T t1, T t2) { throw new Exception("ToStringThrows"); }
    }

    class DerivedCatches<A> : GenBase<A>
    {
        public string GvmInCatch<T>(T t1, T t2)
        {
            try
            {
                throw new Exception();
            }
            catch (Exception)
            {
                return GMethod1(t1, t2);
            }
        }

        public bool GvmInFilter<T>(T t1, T t2)
        {
            try
            {
                throw new Exception();
            }
            catch when (GMethod1(t1, t2) == "GenBase<System.Object>.GMethod1<System.String>(a,b)")
            {
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static unsafe void TestFilter()
    {
        StartTest("TestFilter");

        int counter = 0;
        try
        {
            counter++;
            throw new Exception("Testing filter");
        }
        catch (Exception e) when (e.Message == "Testing filter" && counter++ > 0)
        {
            if (e.Message == "Testing filter")
            {
                counter++;
            }
            counter++;
        }
        EndTest(counter == 4);
    }

    static string exceptionFlowSequence = "";
    private static void TestFilterNested()
    {
        StartTest("TestFilterNested");

        static bool Print(string s)
        {
            exceptionFlowSequence += $"Running {s} filter";
            return true;
        }

        foreach (var exception in new Exception[]
            {new ArgumentException(), new Exception(), new NullReferenceException()})
        {
            try
            {
                try
                {
                    try
                    {
                        throw exception;
                    }
                    catch (NullReferenceException) when (Print("inner"))
                    {
                        exceptionFlowSequence += "In inner catch";
                    }
                }
                catch (ArgumentException)
                {
                    exceptionFlowSequence += "In middle catch";
                }
            }
            catch (Exception) when (Print("outer"))
            {
                exceptionFlowSequence += "In outer catch";
            }
        }

        PrintLine(exceptionFlowSequence);
        EndTest(exceptionFlowSequence == @"In middle catchRunning outer filterIn outer catchRunning inner filterIn inner catch");
    }

    private static void TestIntraFrameFilterOrderBasic()
    {
        static bool CheckOrder(int turn, ref int counter)
        {
            if (++counter != turn)
            {
                counter = -1;
            }

            return turn == 2;
        }

        StartTest("TestIntraFrameFilterOrderBasic");

        int counter = 0;
        try
        {
            static void InnerFilterAndFinally(ref int counter)
            {
                try
                {
                    try
                    {
                        throw new Exception();
                    }
                    catch when (CheckOrder(1, ref counter))
                    {
                    }
                }
                finally
                {
                    CheckOrder(3, ref counter);
                }
            }

            InnerFilterAndFinally(ref counter);
        }
        catch when (CheckOrder(2, ref counter))
        {
            CheckOrder(4, ref counter);
        }

        EndTest(counter == 4);
    }

    private static void TestIntraFrameFilterOrderDeep()
    {
        static bool CheckOrder(int turn, ref int counter)
        {
            if (++counter != turn)
            {
                counter = -1;
            }

            return turn == 4;
        }

        StartTest("TestIntraFrameFilterOrderDeep");

        int counter = 0;
        try
        {
            static void InnerFilterAndFinally(ref int counter)
            {
                try
                {
                    try
                    {
                        try
                        {
                            static void InnerInnerFilterAndFinally(ref int counter)
                            {
                                try
                                {
                                    try
                                    {
                                        throw new Exception();
                                    }
                                    catch when (CheckOrder(1, ref counter))
                                    {
                                    }
                                }
                                finally
                                {
                                    CheckOrder(5, ref counter);
                                }
                            }

                            InnerInnerFilterAndFinally(ref counter);
                        }
                        catch when (CheckOrder(2, ref counter))
                        {
                        }
                    }
                    catch when (CheckOrder(3, ref counter))
                    {
                    }
                }
                finally
                {
                    CheckOrder(6, ref counter);
                }
            }

            InnerFilterAndFinally(ref counter);
        }
        catch when (CheckOrder(4, ref counter))
        {
            CheckOrder(7, ref counter);
        }

        EndTest(counter == 7);
    }

    private static void TestDynamicStackAlloc()
    {
        const int StkAllocSize = 999;
        bool result = false;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DoAlloc(out byte* addr, int size = 0)
        {
            if (size == 0)
            {
                size = StkAllocSize;
            }
            byte* stk = stackalloc byte[size];
            addr = stk;

            try
            {
                Volatile.Write(ref stk[size - 1], 1);
                if (Volatile.Read(ref stk[size - 1]) == 2)
                {
                    Volatile.Write(ref *(int*)null, 0);
                }
            }
            catch (NullReferenceException)
            {
                Volatile.Read(ref stk[size - 1]);
            }
        }

        StartTest("TestDynamicStackAlloc(release on return)");
        {
            DoAlloc(out byte* addrOne);
            DoAlloc(out byte* addrTwo);
            result = addrOne == addrTwo;
        }
        EndTest(result);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DoDoubleAlloc(bool* pReturnWithEH)
        {
            byte* stkOne = stackalloc byte[StkAllocSize];
            byte* stkTwo = stackalloc byte[StkAllocSize];

            try
            {
                Volatile.Write(ref stkOne[StkAllocSize - 1], 1);
                Volatile.Write(ref stkTwo[StkAllocSize - 1], 1);
                if (Volatile.Read(ref *pReturnWithEH))
                {
                    Volatile.Write(ref *(int*)null, 0);
                }
            }
            catch when (!Volatile.Read(ref *pReturnWithEH))
            {
                Volatile.Read(ref stkOne[StkAllocSize - 1]);
                Volatile.Read(ref stkTwo[StkAllocSize - 1]);
            }
        }

        StartTest("TestDynamicStackAlloc(double release on return)");
        {
            bool doReturnWithEH = false;
            DoAlloc(out byte* addrOne);
            DoDoubleAlloc(&doReturnWithEH);
            DoAlloc(out byte* addrTwo);
            result = addrOne == addrTwo;
        }
        EndTest(result);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DoAllocAndThrow(out byte* addr)
        {
            byte* stk = stackalloc byte[StkAllocSize];
            addr = stk;

            try
            {
                Volatile.Write(ref stk[StkAllocSize - 1], 1);
                Volatile.Write(ref *(int*)null, 0);
            }
            catch (DivideByZeroException)
            {
                Volatile.Read(ref stk[StkAllocSize - 1]);
            }
        }

        StartTest("TestDynamicStackAlloc(release on EH return)");
        {
            byte stkByte;
            byte* addrOne = null;
            byte* addrTwo = &stkByte;
            try
            {
                DoAllocAndThrow(out addrOne);
            }
            catch (NullReferenceException)
            {
            }
            try
            {
                DoAllocAndThrow(out addrTwo);
            }
            catch (NullReferenceException)
            {
            }

            result = addrOne == addrTwo;
        }
        EndTest(result);

        StartTest("TestDynamicStackAlloc(double release on EH return)");
        {
            DoAlloc(out byte* addrOne);
            try
            {
                bool doReturnWithEH = true;
                DoDoubleAlloc(&doReturnWithEH);
            }
            catch (NullReferenceException)
            {
            }
            DoAlloc(out byte* addrTwo);

            result = addrOne == addrTwo;
        }
        EndTest(result);

        StartTest("TestDynamicStackAlloc(release on EH return does not corrupt live state)");
        {
            byte* stkOne = stackalloc byte[StkAllocSize];
            Volatile.Write(ref stkOne[StkAllocSize - 1], 2);

            result = false;
            byte* stkTwo = null;
            byte* stkThree = null;
            try
            {
                DoAllocAndThrow(out stkTwo);
            }
            catch (NullReferenceException)
            {
                Volatile.Read(ref stkOne[StkAllocSize - 1]);
            }

            try
            {
                DoAlloc(out stkThree);
                Volatile.Write(ref stkThree[StkAllocSize - 1], 10);

                result = stkTwo == stkThree && stkOne != stkThree && Volatile.Read(ref stkOne[StkAllocSize - 1]) == 2;
                Volatile.Write(ref *(int*)null, 0);
            }
            catch (NullReferenceException)
            {
                Volatile.Read(ref stkThree[StkAllocSize - 1]);
            }
        }
        EndTest(result);

        StartTest("TestDynamicStackAlloc(release from an empty shadow frame does not release the parent's frame)");
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            void OuterMethodWithEmptyShadowStack(bool* pResult)
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                static void SideEffect(byte* pByte)
                {
                    if (Volatile.Read(ref *pByte) != 0)
                    {
                        throw new Exception();
                    }
                }

                [MethodImpl(MethodImplOptions.NoInlining)]
                static void InnerMethodWithEmptyShadowStack()
                {
                    try
                    {
                        byte* stk = stackalloc byte[StkAllocSize];
                        SideEffect(stk);
                        Console.WriteLine((int)stk);
                    }
                    catch (Exception)
                    {
                    }
                }

                try
                {
                    byte* stkOne = stackalloc byte[StkAllocSize];
                    Volatile.Write(ref stkOne[StkAllocSize - 1], 1);

                    InnerMethodWithEmptyShadowStack();

                    byte* stkTwo = stackalloc byte[StkAllocSize];
                    Volatile.Write(ref stkTwo[StkAllocSize - 1], 2);

                    *pResult = stkOne != stkTwo;
                }
                catch (Exception)
                {
                }
            }

            result = false;
            OuterMethodWithEmptyShadowStack(&result);
        }
        EndTest(result);

        StartTest("TestDynamicStackAlloc(EH-live state)");
        {
            static void InnerFinallyHandler(out bool result)
            {
                byte* stk = stackalloc byte[StkAllocSize];

                Volatile.Write(ref stk[0], 1);
                Volatile.Write(ref stk[StkAllocSize / 2], 2);
                Volatile.Write(ref stk[StkAllocSize - 1], 3);

                try
                {
                    throw new Exception();
                }
                finally // A second-pass handler.
                {
                    result = stk[0] == 1 && stk[StkAllocSize / 2] == 2 && stk[StkAllocSize - 1] == 3;
                }
            }

            static bool ClearNativeStack(byte* pFill)
            {
                byte* stk = stackalloc byte[StkAllocSize];

                Unsafe.InitBlock(stk, Volatile.Read(ref *pFill), StkAllocSize);

                return Volatile.Read(ref stk[0]) == Volatile.Read(ref *pFill) &&
                       Volatile.Read(ref stk[StkAllocSize / 2]) == Volatile.Read(ref *pFill) &&
                       Volatile.Read(ref stk[StkAllocSize - 1]) == Volatile.Read(ref *pFill);
            }

            result = false;
            byte fill = 0x17;
            try
            {
                InnerFinallyHandler(out result);
            }
            catch when (ClearNativeStack(&fill))
            {
            }

        }
        EndTest(result);

        StartTest("TestDynamicStackAlloc(alignment)");
        {
            DoAlloc(out byte* addr, 1);
            result = ((nuint)addr % 8) == 0;

            DoAlloc(out addr, 3);
            result &= ((nuint)addr % 8) == 0;

            DoAlloc(out addr, 17);
            result &= ((nuint)addr % 8) == 0;
        }
        EndTest(result);

        StartTest("TestDynamicStackAlloc(allocation patterns)");
        {
            static bool TestAllocs(ref byte* lastAddr, params int[] allocs)
            {
                bool TestAlloc(int index, out byte* stkOut)
                {
                    int allocSize = allocs[index];
                    byte* stk = stackalloc byte[allocSize];
                    stkOut = stk;

                    Volatile.Write(ref stk[allocSize - 1], 1);
                    try
                    {
                        if (Volatile.Read(ref stk[allocSize - 1]) == 2)
                        {
                            throw new Exception();
                        }
                    }
                    catch (Exception)
                    {
                        Volatile.Read(ref stk[allocSize - 1]);
                    }

                    int nextIndex = index + 1;
                    if (nextIndex < allocs.Length)
                    {
                        if (!TestAlloc(nextIndex, out byte* stkOne))
                        {
                            return false;
                        }

                        DoAlloc(out byte* stkTwo, allocs[nextIndex]);
                        return stkOne == stkTwo;
                    }

                    return true;
                }

                if (!TestAlloc(0, out _))
                {
                    return false;
                }

                DoAlloc(out byte* addr, 1);
                if (lastAddr != null && addr != lastAddr)
                {
                    return false;
                }

                lastAddr = addr;
                return true;
            }

            const int PageSize = 64 * 1024;
            const int LargeBlock = PageSize / 4;
            const int AverageBlock = LargeBlock / 4;
            const int SmallBlock = AverageBlock / 4;
            const int AlmostPageSize = PageSize - SmallBlock;

            int pageHeaderSize = 3 * sizeof(nint);
            byte* lastAddr = null;
            result = TestAllocs(ref lastAddr, SmallBlock / 2, AlmostPageSize, SmallBlock, PageSize);
            result &= TestAllocs(ref lastAddr, SmallBlock, SmallBlock);
            result &= TestAllocs(ref lastAddr, LargeBlock, LargeBlock, LargeBlock, LargeBlock - pageHeaderSize, SmallBlock);
            result &= TestAllocs(ref lastAddr, PageSize, 2 * PageSize, 4 * PageSize, SmallBlock, LargeBlock - pageHeaderSize, 8 * PageSize);
            result &= TestAllocs(ref lastAddr, 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53);
        }
        EndTest(result);
    }

    private static void TestCatchAndThrow()
    {
        StartTest("Test catch and throw different exception");
        int caught = 0;
        try
        {
            try
            {
                throw new Exception("first");
            }
            catch
            {
                caught += 1;
                throw new Exception("second");
            }
        }
        catch (Exception e)
        {
            if (e.Message == "second")
            {
                caught += 10;
            }
        }
        EndTest(caught == 11);
    }

    private static void TestRethrow()
    {
        StartTest("Test rethrow");
        int caught = 0;
        try
        {
            try
            {
                throw new Exception("first");
            }
            catch
            {
                caught++;
                throw;
            }
        }
        catch (Exception e)
        {
            if (e.Message == "first")
            {
                caught++;
            }
        }
        EndTest(caught == 2);
    }

    private static void ThrowException(Exception e) => throw e;

    public static void StartTest(string testDescription) => PrintString(testDescription + ": ");

    public static void EndTest(bool result, string failMessage = null)
    {
        if (result)
        {
            PassTest();
        }
        else
        {
            FailTest(failMessage);
        }
    }

    public static void PassTest() => PrintLine("Ok.");

    public static void FailTest(string failMessage = null)
    {
        Success = false;
        PrintLine("Failed.");
        if (failMessage != null) PrintLine(failMessage + "-");
    }

    public static void PrintString(string s)
    {
        [DllImport("*")]
        static extern int printf(byte* str, byte* unused);

        int length = s.Length;
        fixed (char* curChar = s)
        {
            for (int i = 0; i < length; i++)
            {
                TwoByteStr curCharStr = new TwoByteStr();
                curCharStr.first = (byte)curChar[i];
                printf((byte*)&curCharStr, null);
            }
        }
    }

    public static void PrintLine(string s)
    {
        PrintString(s);
        PrintString("\n");
    }
}

public struct TwoByteStr
{
    public byte first;
    public byte second;
}
