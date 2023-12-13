using System;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Hello, DotnetJs!");
        Console.WriteLine($"Args {String.Join(", ", args)}");

        if (args.Length != 3 || args[0] != "A" || args[1] != "B" || args[2] != "C")
            return 1;

        return 100;
    }
}