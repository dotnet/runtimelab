using System;
using System.Runtime.InteropServices.JavaScript;

namespace DotnetJsApp;

partial class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Hello, DotnetJs!");
        Console.WriteLine($"Args {String.Join(", ", args)}");

        if (args.Length != 3 || args[0] != "A" || args[1] != "B" || args[2] != "C")
            return 1;
            
        var mathResult = Interop.Math(1, 2, 3);
        Console.WriteLine($"Math result is '{mathResult}'");
        if (mathResult != 7)
            return 2;

        return 100;
    }

    static partial class Interop
    {
        [JSImport("interop.math", "main.js")]
        internal static partial int Math(int a, int b, int c);

        [JSExport]
        internal static int Square(int x)
        {
            Console.WriteLine($"Compting square of {x}");
            return x^2;
        }
    }
}
