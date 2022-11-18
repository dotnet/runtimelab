// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_delay {

    public static int Main() {

        var t = Task.RunAsGreenThread(() =>
        {
            Deep(2000);
        });

        t.Wait();
        return 100;
    }

    static int printedout = 0;
    class TempObj
    {
        static int tempObjs;
        public TempObj()
        {
            o = Interlocked.Increment(ref tempObjs);
        }
        public object o;
    }

    static TempObj tmpObjStore;

    static void Deep(int moredeep)
    {
        TempObj tmp = new TempObj();
        tmpObjStore = tmp;
        object objLocal = tmp.o;
        int localNumVal = (int)objLocal;

        if (moredeep <= 0)
        {
            printedout++;
            Console.WriteLine("SuperDeep");
            GC.Collect();
            Task.Delay(2000).Wait();
            Console.WriteLine("SuperDeepResumed");
            return;
        }

        Deep(moredeep - 1);
        if (printedout < 10)
            Deep(moredeep - 1);

        if (localNumVal != (int)objLocal)
            Console.WriteLine("GC fail");

        if (!Object.ReferenceEquals(objLocal, tmp.o))
            Console.WriteLine("GC fail2");
    }
}
