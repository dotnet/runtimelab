// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Text;

internal unsafe partial class Program
{
    const int Pass = 100;
    const int Fail = -1;

    volatile int myField;
    volatile Object myObjectField;

    public Program()
    {
        myField = 1;
    }

    static Program g = null;

    static int finallyCounter = 0;

    public static void Main()
    {
        if (string.Empty.Length > 0)
        {
            // Just something to make sure we generate reflection metadata for the type
            new Program().ToString();
        }

        if (!TestTryCatch())
        {
            return;
        }

        TestGenericExceptions();

        int counter = 0;
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionEventHandler;

        try
        {
            try
            {
                throw new Exception("My exception");
            }
            catch (OutOfMemoryException)
            {
                Console.WriteLine("Unexpected exception caught");
                return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception caught!");
            if (e.Message != "My exception")
            {
                Console.WriteLine("Unexpected exception message!");
                return;
            }

            string stackTrace = e.StackTrace;
            if (!stackTrace.Contains("Program.Main"))
            {
                Console.WriteLine("Unexpected stack trace: " + stackTrace);
                return;
            }
            counter++;
        }

        try
        {
            g.myObjectField = new Object();
        }
        catch (NullReferenceException)
        {
            Console.WriteLine("Null reference exception in write barrier caught!");
            counter++;
        }

        try
        {
            try
            {
                g.myField++;
            }
            finally
            {
                counter++;
            }
        }
        catch (NullReferenceException)
        {
            Console.WriteLine("Null reference exception caught!");
            counter++;
        }

        try
        {
            throw new Exception("Testing filter");
        }
        catch (Exception e) when (FilterWithStackTrace(e) && counter++ > 0)
        {
            Console.WriteLine("Exception caught via filter!");
            if (e.Message != "Testing filter")
            {
                Console.WriteLine("Unexpected exception message!");
                return;
            }
            counter++;
        }

        // test interaction of filters and finally clauses with GC
        try
        {
            ThrowExcThroughMethodsWithFinalizers1("Main");
        }
        catch (Exception e) when (FilterWithGC() && counter++ > 0)
        {
            Console.WriteLine(e.Message);
            if (e.Message != "ThrowExcThroughMethodsWithFinalizers2")
            {
                Console.WriteLine("Unexpected exception message!");
                return;
            }
            if (finallyCounter != 2)
            {
                Console.WriteLine("Finalizers didn't execute!");
                return;
            }
            counter++;
        }

        try
        {
            try
            {
                throw new Exception("Hello");
            }
            catch
            {
                counter++;
                throw;
            }
        }
        catch (Exception ex)
        {
            if (ex.Message != "Hello")
                return;
            counter++;
        }

        if (counter != 10)
        {
            Console.WriteLine("Unexpected counter value");
            return;
        }

        throw new Exception("UnhandledException");
    }

    static void UnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs e)
    {
        Console.WriteLine("Exception triggered UnhandledExceptionHandler");
        if (e.ExceptionObject is Exception ex && ex.Message == "UnhandledException")
        {
            Environment.Exit(Pass);
        }

        Console.WriteLine("Unexpected exception!");

        Environment.Exit(Fail);
    }

    static void CreateSomeGarbage()
    {
        for (int i = 0; i < 100; i++)
        {
            string s = new string('.', 100);
        }
    }

    static void ThrowExcThroughMethodsWithFinalizers1(string caller)
    {
        CreateSomeGarbage();
        string s = caller + " + ThrowExcThroughMethodsWithFinalizers1";
        CreateSomeGarbage();
        try
        {
            ThrowExcThroughMethodsWithFinalizers2(s);
        }
        finally
        {
            Console.WriteLine("Executing finally in {0}", s);
            finallyCounter++;
        }
    }

    static void ThrowExcThroughMethodsWithFinalizers2(string caller)
    {
        CreateSomeGarbage();
        string s = caller + " + ThrowExcThroughMethodsWithFinalizers2";
        CreateSomeGarbage();
        try
        {
            throw new Exception("ThrowExcThroughMethodsWithFinalizers2");
        }
        finally
        {
            Console.WriteLine("Executing finally in {0}", s);
            finallyCounter++;
        }
    }

    static void TestGenericExceptions()
    {
        if (CatchGenericException<DivideByZeroException>(100, 0) != 42)
        {
            Environment.Exit(Fail);
        }

        try
        {
            CatchGenericException<NotSupportedException>(100, 0);
        }
        catch (DivideByZeroException)
        {
            return;
        }
        Environment.Exit(Fail);
    }

    static int CatchGenericException<T>(int a, int b) where T : Exception
    {
        try
        {
            return a / b;
        }
        catch (T)
        {
            return 42;
        }
    }

    static bool FilterWithStackTrace(Exception e)
    {
        var stackTrace = new StackTrace(0, true);
        Console.WriteLine("Test Stacktrace with exception on stack:");
        Console.WriteLine(stackTrace);
        return e.Message == "Testing filter";
    }

    static bool FilterWithGC()
    {
        CreateSomeGarbage();
        GC.Collect();
        CreateSomeGarbage();
        return true;
    }
}

