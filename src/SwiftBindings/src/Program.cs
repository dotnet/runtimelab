// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommandLine;
using SwiftReflector;

namespace SwiftBindings
{
    public class Options
    {
        [Option('d', "dylibs", Required = true, Separator = ',', HelpText = "Paths to the dynamic libraries (dylibs), separated by commas.")]
        public IEnumerable<string> DylibPaths { get; set; }

        [Option('s', "swiftinterfaces", Required = true, Separator = ',', HelpText = "Paths to the Swift interface files, separated by commas.")]
        public IEnumerable<string> SwiftInterfacePaths { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output directory for generated bindings.")]
        public string OutputDirectory { get; set; }

        [Option('h', "help", HelpText = "Display this help message.")]
        public bool Help { get; set; }
    }

    public class BindingsTool
    {
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(options =>
            {
                if (options.Help)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  -d, --dylibs            Paths to the dynamic libraries (dylibs), separated by commas.");
                    Console.WriteLine("  -s, --swiftinterfaces   Paths to the Swift interface files, separated by commas.");
                    Console.WriteLine("  -o, --output            Output directory for generated bindings.");
                    return;
                }

                if (options.DylibPaths == null || options.SwiftInterfacePaths == null || options.OutputDirectory == null)
                {
                    Console.WriteLine("Error: Missing required argument(s).");
                    return;
                }

                if (options.DylibPaths.Count() != options.SwiftInterfacePaths.Count())
                {
                    Console.WriteLine("Error: Number of dylibs, interface files, and modules must match.");
                    return;
                }

                if (!Directory.Exists(options.OutputDirectory))
                {
                    Console.WriteLine($"Error: Directory '{options.OutputDirectory}' doesn't exist.");
                    return;
                }

                for (int i = 0; i < options.DylibPaths.Count(); i++)
                {
                    string dylibPath = options.DylibPaths.ElementAt(i);
                    string swiftInterfacePath = options.SwiftInterfacePaths.ElementAt(i);

                    if (!File.Exists(dylibPath))
                    {
                        Console.WriteLine($"Error: Dynamic library not found at path '{dylibPath}'.");
                        return;
                    }

                    if (!File.Exists(swiftInterfacePath))
                    {
                        Console.WriteLine($"Error: Swift interface file not found at path '{swiftInterfacePath}'.");
                        return;
                    }

                    GenerateBindings(dylibPath, swiftInterfacePath, options.OutputDirectory);
                }
            });
        }

        public static void GenerateBindings(string dylibPath, string swiftInterfacePath, string outputDirectory)
        {
            BindingsCompiler bindingsCompiler = new BindingsCompiler();
            var errors = new ErrorHandling ();
			var moduleInventory = bindingsCompiler.GetModuleInventory(dylibPath, errors);
			var moduleDeclarations = bindingsCompiler.GetModuleDeclarations(swiftInterfacePath);
			bindingsCompiler.CompileModules(moduleDeclarations, moduleInventory, dylibPath, outputDirectory, errors);
        }
    }
}
