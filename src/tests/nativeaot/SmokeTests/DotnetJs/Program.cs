using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DotnetJsApp;

partial class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Hello, DotnetJs!");
        Console.WriteLine($"Args {String.Join(", ", args)}");

        if (args.Length != 3 || args[0] != "A" || args[1] != "B" || args[2] != "C")
            return 11;
            
        var mathResult = Interop.Math(1, 2, 3);
        Console.WriteLine($"Math result is '{mathResult}'");
        if (mathResult != 7)
            return 12;

        return 100;
    }

    static partial class Interop
    {
        [JSImport("interop.math", "main.js")]
        internal static partial int Math(int a, int b, int c);

        [JSExport]
        internal static int Square(int x)
        {
            var result = x * x;
            Console.WriteLine($"Computing square of '{x}' with result '{result}'");
            return result;
        }

        [JSExport]
        internal static string Concat(string a, string b)
        {
            var result = a + b;
            Console.WriteLine($"Concatenating '{a}' and '{b}' with result '{result}'");
            return result;
        }

        [JSExport]
        internal static void Throw() => throw new Exception("This is a test exception");

        [JSExport]
        internal static async Task<int> Async(Task task, bool throwAfterAwait)
        {
            Console.WriteLine($"Async method started");
            await task;
            if (throwAfterAwait)
                throw new Exception("Async throwAfterAwait");

            Console.WriteLine("Async method completed");
            return 87;
        }

        [JSExport]
        internal static async Task<int> AsyncWithCancel()
        {
            Console.WriteLine("Fire HTTP");
            using var cts = new CancellationTokenSource();
            using var http = new HttpClient();
            var responseTask = http.GetStringAsync("https://github.com", cts.Token);

            Console.WriteLine("Wait");
            await Task.Delay(10);
            Console.WriteLine("Cancel");
            cts.Cancel();

            try
            {
                await responseTask;
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine($"Expected Exception with message '{e.Message}'");
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected Exception of type '{e.GetType().FullName}' with message '{e.Message}'");
                return 1;
            }

            return 2;
        }
        
        [JSExport]
        internal static bool JSObject(JSObject jsObject)
        {
            Console.WriteLine($"Got JSObject '{jsObject}'");
            var x = jsObject.GetPropertyAsInt32("x");
            Console.WriteLine($"Property x is '{x}'");
            jsObject.SetProperty("y", x + 1);
            return x == 42;
        }
    }
}
