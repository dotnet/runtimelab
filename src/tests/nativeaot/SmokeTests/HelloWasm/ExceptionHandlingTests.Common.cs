// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Assignment in conditional expression is always constant; did you mean to use == instead of = ? (No we did not).
#pragma warning disable CS0665

internal unsafe partial class Program
{
    internal static bool ExitOnFirstTestFailure = Environment.GetCommandLineArgs().AsSpan().Contains("-exit");
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

        TestUnconditionalThrowInCatch();

        TestThrowInMutuallyProtectingHandlers();

        TestDisjointMutuallyProtectingHandlers();

        TestExceptionInGvmCall();

        TestCatchHandlerNeedsGenericContext();

        TestFilterHandlerNeedsGenericContext();

        TestFilter();

        TestFilterNested();

        TestIntraFrameFilterOrderBasic();

        TestIntraFrameFilterOrderDeep();

        TestCatchAndThrow();

        TestRethrow();

        TestCatchUnreachableViaFilter();

        TestVirtualUnwindIndexSetForkedFlow();

        TestVirtualUnwindStackPopOnThrow();
        TestVirtualUnwindStackNoPopOnThrow();
        TestVirtualUnwindStackPopSelfOnUnwindingCatch();
        TestVirtualUnwindStackPopOnUnwindingCatch();
        TestVirtualUnwindStackNoPopOnUnwindingCatch();
        TestVirtualUnwindStackNoPopOnNestedUnwindingCatch();
        TestVirtualUnwindStackNoPopOnMutuallyProtectingUnwindingCatch();
        TestVirtualUnwindStackPopSelfOnUnwindingFault();
        TestVirtualUnwindStackPopSelfOnNestedUnwindingFault();
        TestVirtualUnwindStackPopOnUnwindingFault();
        TestVirtualUnwindStackNoPopOnUnwindingFault();
        TestVirtualUnwindStackNoPopOnNestedUnwindingFault();

        TestContainedNestedDispatchSingleFrame();
        TestContainedNestedDispatchIntraFrame();
        TestDeepContainedNestedDispatchSingleFrame();
        TestDeepContainedNestedDispatchIntraFrame();
        TestExactUncontainedNestedDispatchSingleFrame();
        TestClippingUncontainedNestedDispatchSingleFrame();
        TestExpandingUncontainedNestedDispatchSingleFrame();
        TestExactUncontainedNestedDispatchIntraFrame();
        TestClippingUncontainedNestedDispatchIntraFrame();
        TestExpandingUncontainedNestedDispatchIntraFrame();
        TestDeepUncontainedNestedDispatchSingleFrame();
        TestDeepUncontainedNestedDispatchIntraFrame();

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

    private static void TestUnconditionalThrowInCatch()
    {
        StartTest("TestUnconditionalThrowInCatch");

        bool pass = true;
        try
        {
            try
            {
                ThrowException(new ArgumentException());
            }
            catch (IndexOutOfRangeException)
            {
                pass = false;
                throw new ArgumentException();
            }
        }
        catch (ArgumentException)
        {
        }

        EndTest(pass);
    }

    private static void TestThrowInMutuallyProtectingHandlers()
    {
        StartTest("Test throws in mutually protecting catch handlers");

        Exception[] exceptions = new Exception[] { new ArgumentNullException(), new ArgumentException(), new Exception() };
        for (int i = 0; i < exceptions.Length; i++)
        {
            int catchIndex = -1;
            try
            {
                try
                {
                    throw exceptions[i];
                }
                catch (ArgumentNullException)
                {
                    catchIndex = 0;
                    throw;
                }
                catch (ArgumentException)
                {
                    catchIndex = 1;
                    throw;
                }
                catch (Exception)
                {
                    catchIndex = 2;
                    throw;
                }
            }
            catch
            {
                if (catchIndex != i)
                {
                    FailTest();
                    return;
                }
            }
        }

        PassTest();
    }

    private static void TestDisjointMutuallyProtectingHandlers()
    {
        int index = 0;
        bool result = true;
        [MethodImpl(MethodImplOptions.NoInlining)] void At(int expected) => result &= index++ == expected;

        StartTest("TestDisjointMutuallyProtectingHandlers");
        try
        {
            At(0);
            throw new InvalidOperationException();
        }
        catch (NotSupportedException)
        {
            At(-1);
        }
        catch (InvalidOperationException)
        {
            try
            {
                At(1);
                throw new NullReferenceException();
            }
            catch (NotSupportedException)
            {
                At(-1);
            }
            catch (NullReferenceException)
            {
                try
                {
                    At(2);
                    throw new Exception();
                }
                catch
                {
                    At(3);
                }
                finally
                {
                    At(4);
                }
            }
            catch
            {
                At(-1);
            }
        }
        catch (Exception)
        {
            At(-1);
        }
        finally
        {
            At(5);
        }

        EndTest(result);
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

    private static void TestCatchUnreachableViaFilter()
    {
        StartTest("Test catch unreachable because of the filter");

        int counter = 0;

        // Make sure that even if the catch handler is statically unreachable, we pop the virtual unwind frame.
        void TestCatchUnreachableViaFilter_Inner()
        {
            int one = 1;
            try
            {
                ThrowException(new Exception());
            }
            catch when (++counter == 0 || one == 1 ? throw new Exception() : true)
            {
            }
        }

        try
        {
            TestCatchUnreachableViaFilter_Inner();
        }
        catch // An inconsistent virtual unwind stack here would result in the inner filter running twice.
        {
            try
            {
                throw new Exception();
            }
            catch { }
        }

        EndTest(counter == 1);
    }

    private static void TestVirtualUnwindIndexSetForkedFlow()
    {
        StartTest("Test the the virtual unwind index is set on forked flow");

        // The flowgraph here is akin to the following:
        // [ZR] --> [ZR] -> [T0] -> [ZR]
        //      \-----------------/
        // Make sure we do not fail to set the unwind index to NOT_IN_TRY (ZR) on exit.
        //
        [MethodImpl(MethodImplOptions.NoInlining)]
        void TestVirtualUnwindIndexSetForkedFlow_Test(bool doEnterTry, ref bool result)
        {
            DoNotThrowException();
            if (doEnterTry)
            {
                DoNotThrowException();
                try
                {
                    DoNotThrowException();
                }
                catch when (result = false)
                {
                }
            }

            ThrowNewException();
        }

        bool result = true;
        try
        {
            TestVirtualUnwindIndexSetForkedFlow_Test(doEnterTry: true, ref result);
        }
        catch { }

        EndTest(result);
    }

    private static void TestVirtualUnwindStackPopOnThrow()
    {
        StartTest("Test that the NOT_IN_TRY virtual unwind frames are unlinked on throw");

        void TestVirtualUnwindStackPopOnThrow_NotInTry()
        {
            try { DoNotThrowException(); } catch { }
            ThrowNewException();
            try { DoNotThrowException(); } catch { }
        }

        try
        {
            TestVirtualUnwindStackPopOnThrow_NotInTry();
        }
        catch
        {
            VerifyVirtualUnwindStack();
        }
        PassTest();
    }

    private static void TestVirtualUnwindStackNoPopOnThrow()
    {
        StartTest("Test that the NOT_IN_TRY_CATCH virtual unwind frames are NOT unlinked on throw");

        static void TestVirtualUnwindStackNoPopOnThrow_NotInTryCatch(ref bool result)
        {
            try { DoNotThrowException(); } catch { }
            try
            {
                ThrowNewException();
            }
            finally
            {
                // Check that we haven't popped the frame corresponding to this function.
                try
                {
                    ThrowNewException();
                }
                catch when (result = true) { }
            }
            try { DoNotThrowException(); } catch { }
        }

        bool result = false;
        try
        {
            TestVirtualUnwindStackNoPopOnThrow_NotInTryCatch(ref result);
        }
        catch { }
        EndTest(result);
    }

    private static void TestVirtualUnwindStackPopSelfOnUnwindingCatch()
    {
        StartTest("Test that the virtual unwind frame is unlinked by an unwinding catch");

        void TestVirtualUnwindStackPopSelfOnUnwindingCatch_Catch()
        {
            try
            {
                ThrowNewException();
            }
            catch (NullReferenceException) { }
        }

        try
        {
            TestVirtualUnwindStackPopSelfOnUnwindingCatch_Catch();
        }
        catch
        {
            VerifyVirtualUnwindStack();
        }

        PassTest();
    }

    private static void TestVirtualUnwindStackPopOnUnwindingCatch()
    {
        StartTest("Test that the NOT_IN_TRY virtual unwind frames are unlinked by an unwinding catch");

        void TestVirtualUnwindStackPopOnUnwindingCatch_Catch()
        {
            try
            {
                ThrowNewException();
            }
            catch (NullReferenceException) { }
        }

        void TestVirtualUnwindStackPopOnUnwindingCatch_NotInTry()
        {
            try { DoNotThrowException(); } catch { }
            TestVirtualUnwindStackPopOnUnwindingCatch_Catch();
            try { DoNotThrowException(); } catch { }
        }

        try
        {
            TestVirtualUnwindStackPopOnUnwindingCatch_NotInTry();
        }
        catch
        {
            VerifyVirtualUnwindStack();
        }

        PassTest();
    }

    private static void TestVirtualUnwindStackNoPopOnUnwindingCatch()
    {
        StartTest("Test that the NOT_IN_TRY_CATCH virtual unwind frames are NOT unlinked by an unwinding catch");

        void TestVirtualUnwindStackNoPopOnUnwindingCatch_Catch()
        {
            try
            {
                ThrowNewException();
            }
            catch (NullReferenceException) { }
        }

        void TestVirtualUnwindStackNoPopOnUnwindingCatch_NotInTryCatch(ref bool result)
        {
            try
            {
                TestVirtualUnwindStackNoPopOnUnwindingCatch_Catch();
            }
            finally
            {
                // Check that we haven't popped the frame corresponding to this function.
                try
                {
                    ThrowNewException();
                }
                catch when (result = true) { }
            }
        }

        bool result = false;
        try
        {
            TestVirtualUnwindStackNoPopOnUnwindingCatch_NotInTryCatch(ref result);
        }
        catch { }

        EndTest(result);
    }

    private static void TestVirtualUnwindStackNoPopOnNestedUnwindingCatch()
    {
        StartTest("Test that the virtual unwind frame is not unlinked by a nested unwinding catch");

        bool result = false;
        try
        {
            try
            {
                try
                {
                    ThrowNewException();
                }
                catch (DivideByZeroException) { }
            }
            finally
            {
                // Check that we haven't popped the frame corresponding to this function.
                try
                {
                    ThrowNewException();
                }
                catch when (result = true) { }
            }
        }
        catch { }

        EndTest(result);
    }

    private static void TestVirtualUnwindStackNoPopOnMutuallyProtectingUnwindingCatch()
    {
        StartTest("Test that the virtual unwind frame is not unlinked by a nested unwinding mutually protecting catch");

        try
        {
            ThrowNewException();
        }
        catch (NullReferenceException) { }
        catch
        {
            VerifyVirtualUnwindStack();
        }

        PassTest();
    }

    private static void TestVirtualUnwindStackPopSelfOnUnwindingFault()
    {
        StartTest("Test that the virtual unwind frame is unlinked by an unwinding fault");

        void TestVirtualUnwindStackPopSelfOnUnwindingFault_Fault()
        {
            try
            {
                ThrowNewException();
            }
            finally
            {
                DoNotThrowException();
            }
        }

        try
        {
            TestVirtualUnwindStackPopSelfOnUnwindingFault_Fault();
        }
        catch
        {
            VerifyVirtualUnwindStack();
        }

        PassTest();
    }

    private static void TestVirtualUnwindStackPopOnUnwindingFault()
    {
        StartTest("Test that the NOT_IN_TRY virtual unwind frames are unlinked by an unwinding fault");

        void TestVirtualUnwindStackPopOnUnwindingFault_Fault()
        {
            try
            {
                ThrowNewException();
            }
            finally
            {
                DoNotThrowException();
            }
        }

        void TestVirtualUnwindStackPopOnUnwindingFault_NotInTry()
        {
            try { DoNotThrowException(); } catch { }
            TestVirtualUnwindStackPopOnUnwindingFault_Fault();
            try { DoNotThrowException(); } catch { }
        }

        try
        {
            TestVirtualUnwindStackPopOnUnwindingFault_NotInTry();
        }
        catch
        {
            VerifyVirtualUnwindStack();
        }

        PassTest();
    }

    private static void TestVirtualUnwindStackPopSelfOnNestedUnwindingFault()
    {
        StartTest("Test that the virtual unwind frame is unlinked by a nested unwinding fault");

        void TestVirtualUnwindStackPopSelfOnNestedUnwindingFault_Faults()
        {
            try
            {
                try
                {
                    ThrowNewException();
                }
                finally
                {
                    // This fault should release the virtual unwind frame.
                    try
                    {
                        ThrowNewException();
                    }
                    catch
                    {
                        DoNotThrowException();
                    }
                }
            }
            finally
            {
                DoNotThrowException();
            }
        }

        try
        {
            TestVirtualUnwindStackPopSelfOnNestedUnwindingFault_Faults();
        }
        catch
        {
            VerifyVirtualUnwindStack();
        }

        PassTest();
    }

    private static void TestVirtualUnwindStackNoPopOnUnwindingFault()
    {
        StartTest("Test that the NOT_IN_TRY_CATCH virtual unwind frames are NOT unlinked by an unwinding fault");

        void TestVirtualUnwindStackNoPopOnUnwindingFault_Fault()
        {
            try
            {
                ThrowNewException();
            }
            finally
            {
                DoNotThrowException();
            }
        }

        void TestVirtualUnwindStackNoPopOnUnwindingFault_NotInTryCatch(ref bool result)
        {
            try
            {
                TestVirtualUnwindStackNoPopOnUnwindingFault_Fault();
            }
            finally
            {
                // Check that we haven't popped the frame corresponding to this function.
                try
                {
                    ThrowNewException();
                }
                catch when (result = true) { }
            }
        }

        bool result = false;
        try
        {
            TestVirtualUnwindStackNoPopOnUnwindingFault_NotInTryCatch(ref result);
        }
        catch { }

        EndTest(result);
    }

    private static void TestVirtualUnwindStackNoPopOnNestedUnwindingFault()
    {
        StartTest("Test that the virtual unwind frame is not unlinked by a nested unwinding fault");

        bool result = false;
        try
        {
            try
            {
                try
                {
                    ThrowNewException();
                }
                finally
                {
                    DoNotThrowException();
                }
            }
            finally
            {
                // Check that we haven't popped the frame corresponding to this function.
                try
                {
                    ThrowNewException();
                }
                catch when (result = true) { }
            }
        }
        catch { }

        EndTest(result);
    }

    private static void TestContainedNestedDispatchSingleFrame()
    {
        int index = 0;
        bool result = true;
        void At(int expected) => result &= index++ == expected;

        StartTest("Test contained nested dispatch in a single frame");

        try
        {
            try
            {
                At(0);
                ThrowNewException();
            }
            finally
            {
                try
                {
                    At(1);
                    ThrowNewException();
                }
                catch
                {
                    At(2);
                }
            }
        }
        catch
        {
            try
            {
                At(3);
                ThrowNewException();
            }
            catch
            {
                At(4);
            }
        }

        EndTest(result);
    }

    private static void TestContainedNestedDispatchIntraFrame()
    {
        int index = 0;
        bool result = true;
        void At(int expected) => result &= index++ == expected;

        StartTest("Test contained nested dispatch in nested frames");

        void TestContainedNestedDispatchIntraFrame_ThrowAndCatch(int index)
        {
            try
            {
                At(index);
                ThrowNewException();
            }
            catch
            {
                At(index + 1);
            }
        }

        try
        {
            try
            {
                At(0);
                ThrowNewException();
            }
            finally
            {
                TestContainedNestedDispatchIntraFrame_ThrowAndCatch(1);
            }
        }
        catch
        {
            TestContainedNestedDispatchIntraFrame_ThrowAndCatch(3);
        }

        EndTest(result);
    }

    private static void TestDeepContainedNestedDispatchSingleFrame()
    {
        int index = 0;
        bool result = true;
        void At(int expected) => result &= index++ == expected;

        StartTest("Test deep contained nested dispatch in a single frame");

        try
        {
            try
            {
                At(0);
                ThrowNewException();
            }
            finally
            {
                try
                {
                    try
                    {
                        try
                        {
                            At(1);
                            ThrowException(new DivideByZeroException());
                        }
                        finally
                        {
                            At(2);
                        }
                    }
                    finally
                    {
                        try
                        {
                            try
                            {
                                try
                                {
                                    At(3);
                                    ThrowException(new ArgumentException());
                                }
                                finally
                                {
                                    At(4);
                                }
                            }
                            catch (ArgumentNullException) { }
                        }
                        catch
                        {
                            At(5);
                        }
                    }
                }
                catch
                {
                    try
                    {
                        try
                        {
                            At(6);
                            ThrowException(new IndexOutOfRangeException());
                        }
                        finally
                        {
                            At(7);
                        }
                    }
                    catch
                    {
                        At(8);
                    }
                }
            }
        }
        catch
        {
            At(9);
        }

        EndTest(result);
    }

    private static void TestDeepContainedNestedDispatchIntraFrame()
    {
        int index = 0;
        bool result = true;
        void At(int expected) => result &= index++ == expected;

        StartTest("Test deep contained nested dispatch in nested frames");

        try
        {
            void TestDeepContainedNestedDispatchSingleFrame_TryOne()
            {
                try
                {
                    At(0);
                    ThrowNewException();
                }
                finally
                {
                    void TestDeepContainedNestedDispatchSingleFrame_TryTwo()
                    {
                        try
                        {
                            void TestDeepContainedNestedDispatchSingleFrame_TryThree()
                            {
                                try
                                {
                                    void TestDeepContainedNestedDispatchSingleFrame_TryFive()
                                    {
                                        try
                                        {
                                            At(1);
                                            ThrowException(new DivideByZeroException());
                                        }
                                        finally
                                        {
                                            At(2);
                                        }
                                    }

                                    TestDeepContainedNestedDispatchSingleFrame_TryFive();
                                }
                                finally
                                {
                                    try
                                    {
                                        void TestDeepContainedNestedDispatchSingleFrame_TrySix()
                                        {
                                            try
                                            {
                                                try
                                                {
                                                    At(3);
                                                    ThrowException(new ArgumentException());
                                                }
                                                finally
                                                {
                                                    At(4);
                                                }
                                            }
                                            catch (ArgumentNullException) { }

                                        }

                                        TestDeepContainedNestedDispatchSingleFrame_TrySix();
                                    }
                                    catch
                                    {
                                        At(5);
                                    }
                                }
                            }

                            TestDeepContainedNestedDispatchSingleFrame_TryThree();
                        }
                        catch
                        {
                            void TestDeepContainedNestedDispatchSingleFrame_TryFour()
                            {
                                try
                                {
                                    try
                                    {
                                        At(6);
                                        ThrowException(new IndexOutOfRangeException());
                                    }
                                    finally
                                    {
                                        At(7);
                                    }
                                }
                                catch
                                {
                                    At(8);
                                }
                            }

                            TestDeepContainedNestedDispatchSingleFrame_TryFour();
                        }
                    }

                    TestDeepContainedNestedDispatchSingleFrame_TryTwo();
                }
            }

            TestDeepContainedNestedDispatchSingleFrame_TryOne();
        }
        catch
        {
            At(9);
        }

        EndTest(result);
    }

    private static void TestExactUncontainedNestedDispatchSingleFrame()
    {
        StartTest("Test exact uncontained nested dispatch in a single frame");

        Exception exception = null;
        bool result = false;
        try
        {
            try
            {
                try
                {
                    ThrowNewException();
                }
                catch (NullReferenceException)
                {
                    // Make sure second pass updates the next catch on the original exception correctly.
                }
            }
            finally
            {
                try
                {
                    // The target for this nested exception is exactly the same as for the original.
                    exception = new Exception();
                    throw exception;
                }
                catch (NullReferenceException)
                {
                    // Make sure second pass updates the next catch on the original exception correctly.
                }
            }
        }
        catch (Exception e)
        {
            result = exception == e;
        }

        EndTest(result);
    }

    private static void TestClippingUncontainedNestedDispatchSingleFrame()
    {
        StartTest("Test clipping uncontained nested dispatch in a single frame");

        Exception exception = null;
        bool result = false;
        bool didReachNormalFlow = false;
        try
        {
            try
            {
                try
                {
                    try
                    {
                        ThrowNewException();
                    }
                    catch (NullReferenceException)
                    {
                        // Make sure second pass updates the next catch on the original exception correctly.
                    }
                }
                finally
                {
                    try
                    {
                        // The target for this nested exception is below that of the original.
                        exception = new IndexOutOfRangeException();
                        ThrowException(exception);
                    }
                    catch (NullReferenceException)
                    {
                        // Make sure second pass updates the next catch on the original exception correctly.
                    }
                }
            }
            catch (IndexOutOfRangeException e)
            {
                result = exception == e;
            }

            // This test demonstrates that nested exceptions allow EH flow to "return" below the original catch.
            didReachNormalFlow = true;
        }
        catch
        {
            // We should not reach here.
            result = false;
        }

        EndTest(didReachNormalFlow && result);
    }

    private static void TestExpandingUncontainedNestedDispatchSingleFrame()
    {
        StartTest("Test expanding uncontained nested dispatch in a single frame");

        Exception exception = null;
        bool result = false;
        try
        {
            try
            {
                try
                {
                    try
                    {
                        ThrowException(new IndexOutOfRangeException());
                    }
                    catch (NullReferenceException)
                    {
                        // Make sure second pass updates the next catch on the original exception correctly.
                    }
                }
                finally
                {
                    try
                    {
                        // The target for this nested exception is below that of the original.
                        exception = new Exception();
                        ThrowException(exception);
                    }
                    catch (NullReferenceException)
                    {
                        // Make sure second pass updates the next catch on the original exception correctly.
                    }
                }
            }
            catch (IndexOutOfRangeException e)
            {
                // We should not reach here.
                result = false;
            }
        }
        catch (Exception e)
        {
            result = exception == e;
        }

        EndTest(result);
    }

    private static void TestExactUncontainedNestedDispatchIntraFrame()
    {
        StartTest("Test exact uncontained nested dispatch in nested frames");

        Exception exception = null;
        bool result = false;
        try
        {
            void TestExactUncontainedNestedDispatchIntraFrame_Throw()
            {
                try
                {
                    ThrowNewException();
                }
                catch (NullReferenceException)
                {
                    // Make sure second pass updates the next catch on the original exception correctly.
                }
            }

            void TestExactUncontainedNestedDispatchIntraFrame_Fault()
            {
                try
                {
                    try
                    {
                        TestExactUncontainedNestedDispatchIntraFrame_Throw();
                    }
                    catch (NullReferenceException)
                    {
                        // Make sure second pass updates the next catch on the original exception correctly.
                    }
                }
                finally
                {
                    void TestExactUncontainedNestedDispatchIntraFrame_NestedThrow()
                    {
                        // The target for this nested exception is exactly the same as for the original.
                        exception = new Exception();
                        throw exception;
                    }

                    try
                    {
                        TestExactUncontainedNestedDispatchIntraFrame_NestedThrow();
                    }
                    catch (NullReferenceException)
                    {
                        // Make sure second pass updates the next catch on the nested exception correctly.
                    }
                }
            }

            TestExactUncontainedNestedDispatchIntraFrame_Fault();
        }
        catch (Exception e)
        {
            result = exception == e;
        }

        EndTest(result);
    }

    private static void TestClippingUncontainedNestedDispatchIntraFrame()
    {
        StartTest("Test clipping uncontained nested dispatch in nested frames");

        Exception exception = null;
        bool result = false;
        bool didReachNormalFlow = false;
        try
        {
            void TestClippingUncontainedNestedDispatchIntrarame_NestedCatch()
            {
                try
                {
                    void TestClippingUncontainedNestedDispatchIntrarame_NestedThrow()
                    {
                        try
                        {
                            try
                            {
                                ThrowNewException();
                            }
                            catch (NullReferenceException)
                            {
                                // Make sure second pass updates the next catch on the original exception correctly.
                            }
                        }
                        finally
                        {
                            try
                            {
                                // The target for this nested exception is below that of the original.
                                exception = new IndexOutOfRangeException();
                                ThrowException(exception);
                            }
                            catch (NullReferenceException)
                            {
                                // Make sure second pass updates the next catch on the original exception correctly.
                            }
                        }
                    }

                    TestClippingUncontainedNestedDispatchIntrarame_NestedThrow();
                }
                catch (IndexOutOfRangeException e)
                {
                    result = exception == e;
                }

                // This test demonstrates that nested exceptions allow EH flow to "return" below the original catch,
                // even in a different frame. This means that even state which is not live in/out of handlers must be
                // accessible in the second.
                didReachNormalFlow = true;
            }

            TestClippingUncontainedNestedDispatchIntrarame_NestedCatch();
        }
        catch
        {
            // We should not reach here.
            result = false;
        }

        EndTest(didReachNormalFlow && result);
    }

    private static void TestExpandingUncontainedNestedDispatchIntraFrame()
    {
        StartTest("Test expanding uncontained nested dispatch in nested frames");

        Exception exception = null;
        bool result = false;
        try
        {
            void TestExpandingUncontainedNestedDispatchIntraFrame_OriginalCatch()
            {
                try
                {
                    void TestExpandingUncontainedNestedDispatchIntraFrame_NestedThrow()
                    {
                        try
                        {
                            try
                            {
                                ThrowException(new IndexOutOfRangeException());
                            }
                            catch (NullReferenceException)
                            {
                                // Make sure second pass updates the next catch on the original exception correctly.
                            }
                        }
                        finally
                        {
                            try
                            {
                                // The target for this nested exception is below that of the original.
                                exception = new Exception();
                                ThrowException(exception);
                            }
                            catch (NullReferenceException)
                            {
                                // Make sure second pass updates the next catch on the original exception correctly.
                            }
                        }
                    }

                    TestExpandingUncontainedNestedDispatchIntraFrame_NestedThrow();
                }
                catch (IndexOutOfRangeException e)
                {
                    // We should not reach here.
                    result = false;
                }
            }

            TestExpandingUncontainedNestedDispatchIntraFrame_OriginalCatch();
        }
        catch (Exception e)
        {
            result = exception == e;
        }

        EndTest(result);
    }

    private static void TestDeepUncontainedNestedDispatchSingleFrame()
    {
        int index = 0;
        bool result = true;
        void At(int expected) => result &= index++ == expected;

        try
        {
            try
            {
                At(0);
                ThrowNewException();
            }
            finally
            {
                try
                {
                    try
                    {
                        try
                        {
                            At(1);
                            ThrowException(new IndexOutOfRangeException());
                        }
                        finally
                        {
                            try
                            {
                                At(2);
                                ThrowException(new Exception());
                            }
                            catch (ArgumentNullException)
                            {
                                result = false; // Unreachable.
                            }
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        result = false; // Unreachable.
                    }
                }
                finally
                {
                    At(3);
                }
            }
        }
        catch
        {
            At(4);
        }

        EndTest(result);
    }

    private static void TestDeepUncontainedNestedDispatchIntraFrame()
    {
        int index = 0;
        bool result = true;
        void At(int expected) => result &= index++ == expected;

        try
        {
            void TestDeepUncontainedNestedDispatchIntraFrame_TopCatch()
            {
                try
                {
                    At(0);
                    ThrowNewException();
                }
                finally
                {
                    void TestDeepUncontainedNestedDispatchIntraFrame_TopFault()
                    {
                        try
                        {
                            void TestDeepUncontainedNestedDispatchIntraFrame_MiddleFault()
                            {
                                try
                                {
                                    void TestDeepUncontainedNestedDispatchIntraFrame_MiddleThrow()
                                    {
                                        try
                                        {
                                            At(1);
                                            ThrowException(new IndexOutOfRangeException());
                                        }
                                        finally
                                        {
                                            void TestDeepUncontainedNestedDispatchIntraFrame_BottomThrow()
                                            {
                                                try
                                                {
                                                    At(2);
                                                    ThrowException(new Exception());
                                                }
                                                catch (ArgumentNullException)
                                                {
                                                    result = false; // Unreachable.
                                                }
                                            }

                                            TestDeepUncontainedNestedDispatchIntraFrame_BottomThrow();
                                        }
                                    }

                                    TestDeepUncontainedNestedDispatchIntraFrame_MiddleThrow();
                                }
                                catch (IndexOutOfRangeException)
                                {
                                    result = false; // Unreachable.
                                }
                            }

                            TestDeepUncontainedNestedDispatchIntraFrame_MiddleFault();
                        }
                        finally
                        {
                            At(3);
                        }
                    }

                    TestDeepUncontainedNestedDispatchIntraFrame_TopFault();
                }
            }

            TestDeepUncontainedNestedDispatchIntraFrame_TopCatch();
        }
        catch
        {
            At(4);
        }

        EndTest(result);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowException(Exception e) => throw e;

    private static void ThrowNewException() => ThrowException(new Exception());

    private static int s_alwaysZero = 0;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void DoNotThrowException()
    {
        if (Volatile.Read(ref s_alwaysZero) == 1)
        {
            ThrowNewException();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void VerifyVirtualUnwindStack()
    {
        // If the frame chain is corrupt, this new frame will link to itself, causing stack overflow in the first pass.
        try
        {
            ThrowNewException();
        }
        catch { }
    }

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
        if (failMessage != null)
            PrintLine(failMessage + "-");

        if (ExitOnFirstTestFailure)
        {
            Environment.Exit(-1);
        }
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
