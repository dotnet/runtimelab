// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibrary
{
    public class ClassLibrary
    {
        [UnmanagedCallersOnly(EntryPoint = "ReturnsPrimitiveInt", CallConvs = new Type[] { typeof(CallConvStdcall) })]
        public static int ReturnsPrimitiveInt()
        {
            return 10;
        }

        [UnmanagedCallersOnly(EntryPoint = "returns-primitive-int")]
        public static unsafe int wasmExportReturnsPrimitiveInt()
        {
            return 10;
        }

        [UnmanagedCallersOnly(EntryPoint = "ReturnsPrimitiveBool", CallConvs = new Type[] { typeof(CallConvStdcall) })]
        public static bool ReturnsPrimitiveBool()
        {
            return true;
        }

        [UnmanagedCallersOnly(EntryPoint = "returns-primitive-bool")]
        public static unsafe int wasmExportReturnsPrimitiveBool()
        {
            return 1;
        }

        [UnmanagedCallersOnly(EntryPoint = "ReturnsPrimitiveChar", CallConvs = new Type[] { typeof(CallConvStdcall) })]
        public static char ReturnsPrimitiveChar()
        {
            return 'a';
        }

        [UnmanagedCallersOnly(EntryPoint = "returns-primitive-char")]
        public static unsafe int wasmExportReturnsPrimitiveChar()
        {
            return (int)'a';
        }

        [UnmanagedCallersOnly(EntryPoint = "EnsureManagedClassLoaders", CallConvs = new Type[] { typeof(CallConvStdcall) })]
        public static void EnsureManagedClassLoaders()
        {
            Random random = new Random();
            random.Next();
        }

        [UnmanagedCallersOnly(EntryPoint = "ensure-managed-class-loaders")]
        public static unsafe void wasmExportEnsureManagedClassLoaders()
        {
            Random random = new Random();
            random.Next();
        }

        [UnmanagedCallersOnly(EntryPoint = "CheckSimpleExceptionHandling", CallConvs = new Type[] { typeof(CallConvStdcall) })]
        public static int CheckSimpleExceptionHandling()
        {
            return DoCheckSimpleExceptionHandling();
        }

        [UnmanagedCallersOnly(EntryPoint = "check-simple-exception-handling")]
        public static unsafe int wasmExportCheckSimpleExceptionHandling()
        {
            return DoCheckSimpleExceptionHandling();
        }

        public static int DoCheckSimpleExceptionHandling()
        {
            int result = 10;

            try
            {
                Console.WriteLine("Throwing exception");
                throw new Exception();
            }
            catch when (result == 10)
            {
                result += 20;
            }
            finally
            {
                result += 70;
            }

            return result;
        }

        private static bool s_collected;

        class ClassWithFinalizer
        {
            ~ClassWithFinalizer() { s_collected = true; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MakeGarbage()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            object[] arr = new object[1024 * 1024];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = new object();

            new ClassWithFinalizer();
        }

        [UnmanagedCallersOnly(EntryPoint = "CheckSimpleGCCollect", CallConvs = new Type[] { typeof(CallConvStdcall) })]
        public static int CheckSimpleGCCollect()
        {
            return DoCheckSimpleGCCollect();
        }

        [UnmanagedCallersOnly(EntryPoint = "check-simple-gc-collect")]
        public static unsafe int wasmExportCheckSimpleGcCollect()
        {
            return DoCheckSimpleGCCollect();
        }

        public static int DoCheckSimpleGCCollect()
        {
            string myString = string.Format("Hello {0}", "world");

            MakeGarbage();

            Console.WriteLine("Triggering GC");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return s_collected ? (myString == "Hello world" ? 100 : 1) : 2;
        }

        [UnmanagedCallersOnly(EntryPoint = "test-http")]
        public static unsafe void TestHttp(int port)
        {
            [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "PollWasiEventLoopUntilResolvedVoid")]
            static extern void PollWasiEventLoopUntilResolvedVoid(Thread t, Task task);

            Stopwatch stopwatch = Stopwatch.StartNew();
            PollWasiEventLoopUntilResolvedVoid(null, TestHttpAsync((ushort)port));
            stopwatch.Stop();

            // Verify that `PollWasiEventLoopUntilResolvedVoid` returned
            // promptly once the main task finished, even if there were other
            // tasks (e.g. the default 100 second HttpClient timeout) still in
            // progress.
            Trace.Assert(stopwatch.ElapsedMilliseconds < 10000);
        }

        private static async Task TestHttpAsync(ushort port)
        {
            using var client = new HttpClient();
            var urlBase = $"http://127.0.0.1:{port}";

            {
                var response = await client.GetAsync($"{urlBase}/hello");
                response.EnsureSuccessStatusCode();
                Trace.Assert(
                    4 == response.Content.Headers.ContentLength,
                    $"unexpected content length: {response.Content.Headers.ContentLength}"
                );
                Trace.Assert(
                    "text/plain".Equals(response.Content.Headers.ContentType.ToString()),
                    $"unexpected content type: \"{response.Content.Headers.ContentType}\""
                );
                var content = await response.Content.ReadAsStringAsync();
                Trace.Assert("hola".Equals(content), $"unexpected content: \"{content}\"");
            }

            {
                var length = 10 * 1024 * 1024;
                var body = new byte[length];
                new Random().NextBytes(body);

                var content = new StreamContent(new MemoryStream(body));
                var type = "application/octet-stream";
                content.Headers.ContentType = new MediaTypeHeaderValue(type);

                var response = await client.PostAsync($"{urlBase}/echo", content);
                response.EnsureSuccessStatusCode();
                Trace.Assert(
                    length == response.Content.Headers.ContentLength,
                    $"unexpected content length: {response.Content.Headers.ContentLength}"
                );
                Trace.Assert(
                    type.Equals(response.Content.Headers.ContentType.ToString()),
                    $"unexpected content type: \"{response.Content.Headers.ContentType}\""
                );
                var received = await response.Content.ReadAsByteArrayAsync();
                Trace.Assert(body.SequenceEqual(received), "unexpected content");
            }

            using var impatientClient = new HttpClient();
            impatientClient.Timeout = TimeSpan.FromMilliseconds(100);
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                await impatientClient.GetAsync($"{urlBase}/slow-hello");
                throw new Exception("request to /slow-hello endpoint should have timed out");
            }
            catch (TaskCanceledException _)
            {
                // The /slow-hello endpoint takes 10 seconds to return a
                // response, whereas we've set a 100ms timeout, so this is
                // expected.
            }
            stopwatch.Stop();
            Trace.Assert(stopwatch.ElapsedMilliseconds >= 100);
            Trace.Assert(stopwatch.ElapsedMilliseconds < 1000);
        }
    }
}
