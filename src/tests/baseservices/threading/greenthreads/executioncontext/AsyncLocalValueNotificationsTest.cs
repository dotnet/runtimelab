// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_executioncontext_AsyncLocalValueNotificationsTest
{
    public static int Main()
    {
        Console.WriteLine("Test AsyncLocal value change notifications");

        int result = 100;
        var valueChanges = new List<ValueChangeInfo>();
        var asyncLocal = new AsyncLocal<int>(valueChangedArgs =>
        {
            lock (valueChanges)
            {
                valueChanges.Add(new ValueChangeInfo(valueChangedArgs));
            }
        });

        Console.WriteLine("  Before setting AsyncLocal value");
        asyncLocal.Value = 1;
        VerifyChanges(new ValueChangeInfo(false, 0, 1));

        Action taskAction = () =>
        {
            VerifyChanges(new ValueChangeInfo(true, 0, 1));
            Console.WriteLine("  Before yield");
            Task.Delay(1000).Wait();
            VerifyChanges(new ValueChangeInfo(true, 1, 0), new ValueChangeInfo(true, 0, 1));
            Console.WriteLine("  Before exiting green thread");
        };

        Console.WriteLine("  Before creating green thread");
        Task.RunAsGreenThread(taskAction).Wait();
        Thread.Sleep(500); // wait for green thread to fully exit
        VerifyChanges(new ValueChangeInfo(true, 1, 0));
        Console.WriteLine("  After exiting green thread");
        return result;

        void VerifyChanges(params ValueChangeInfo[] expectedValueChanges)
        {
            lock (valueChanges)
            {
                try
                {
                    if (valueChanges.Count != expectedValueChanges.Length)
                    {
                        Fail();
                        return;
                    }

                    for (int i = 0; i < expectedValueChanges.Length; i++)
                    {
                        if (valueChanges[i].onGreenThread != expectedValueChanges[i].onGreenThread ||
                            valueChanges[i].previousValue != expectedValueChanges[i].previousValue ||
                            valueChanges[i].newValue != expectedValueChanges[i].newValue)
                        {
                            Fail();
                            return;
                        }
                    }
                }
                finally
                {
                    valueChanges.Clear();
                }
            }
        }

        void Fail()
        {
            Console.WriteLine("      Failed");
            result = 101;
        }
    }

    private readonly struct ValueChangeInfo
    {
        public readonly bool onGreenThread;
        public readonly int previousValue;
        public readonly int newValue;

        public ValueChangeInfo(bool onGreenThread, int previousValue, int newValue)
        {
            this.onGreenThread = onGreenThread;
            this.previousValue = previousValue;
            this.newValue = newValue;
        }

        public ValueChangeInfo(AsyncLocalValueChangedArgs<int> valueChangedArgs)
        {
            onGreenThread = Thread.IsGreenThread;
            previousValue = valueChangedArgs.PreviousValue;
            newValue = valueChangedArgs.CurrentValue;
            Console.WriteLine($"    AsyncLocal value changed - on green thread: {onGreenThread} | previous value: {previousValue} | new value: {newValue}");
        }
    }
}
