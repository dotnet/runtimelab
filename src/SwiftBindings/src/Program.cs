using System;
using System.Collections.Generic;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using SwiftReflector;

namespace SwiftBindings
{
    public class BindingsTool
    {
        public static void Main(string[] args)
        {
            Option<IEnumerable<string>> dylibOption = new(aliases: new [] {"-d", "--dylib"}, description: "Path to the dynamic library.") {AllowMultipleArgumentsPerToken = true, IsRequired = true};
            Option<IEnumerable<string>> swiftinterfaceOption = new(aliases: new [] {"-s", "--swiftinterface"}, "Path to the Swift interface file.") {AllowMultipleArgumentsPerToken = true, IsRequired = true};
            Option<string> outputDirectoryOption = new(aliases: new [] {"-o", "--output"}, "Output directory for generated bindings.") {IsRequired = true};
            Option<int> verbosityOption = new(aliases: new [] {"-v", "--verbosity"}, "Prints information about work in process.");
            Option<bool> helpOption = new(aliases: new [] {"-h", "--help"}, "Display a help message.");

            RootCommand rootCommand = new(description: "Swift bindings generator.")
            {
                dylibOption,
                swiftinterfaceOption,
                outputDirectoryOption,
                verbosityOption,
                helpOption,
            };
            rootCommand.SetHandler((IEnumerable<string> dylibPaths, IEnumerable<string> swiftinterfacePaths, string outputDirectory, int verbosity, bool help) =>
                {
                    if (help)
                    {
                        Console.WriteLine("Usage:");
                        Console.WriteLine("  -d, --dylib             Required. Path to the dynamic library.");
                        Console.WriteLine("  -s, --swiftinterface    Required. Path to the Swift interface file.");
                        Console.WriteLine("  -o, --output            Required. Output directory for generated bindings.");
                        Console.WriteLine("  -v, --verbosity         Information about work in process.");
                        return;
                    }
                    
                    if (ValidateOptions(dylibPaths, swiftinterfacePaths, outputDirectory))
                    {
                        for (int i = 0; i < dylibPaths.Count(); i++)
                        {
                            string dylibPath = dylibPaths.ElementAt(i);
                            string swiftInterfacePath = swiftinterfacePaths.ElementAt(i);

                            if (!File.Exists(dylibPath))
                            {
                                Console.Error.WriteLine($"Error: Dynamic library not found at path '{dylibPath}'.");
                                return;
                            }

                            if (!File.Exists(swiftInterfacePath))
                            {
                                Console.Error.WriteLine($"Error: Swift interface file not found at path '{swiftInterfacePath}'.");
                                return;
                            }

                            GenerateBindings(dylibPath, swiftInterfacePath, outputDirectory, verbosity);
                        }
                    }

                },
                dylibOption,
                swiftinterfaceOption,
                outputDirectoryOption,
                verbosityOption,
                helpOption
            );

            rootCommand.Invoke(args);
        }

        private static bool ValidateOptions(IEnumerable<string> dylibPaths, IEnumerable<string> swiftinterfacePaths, string outputDirectory)
        {
            if (dylibPaths == null || swiftinterfacePaths == null || outputDirectory == string.Empty)
            {
                Console.Error.WriteLine("Error: Missing required argument(s).");
                return false;
            }

            if (dylibPaths.Count() != swiftinterfacePaths.Count())
            {
                Console.Error.WriteLine("Error: Number of dylib and interface files must match.");
                return false;
            }

            if (!Directory.Exists(outputDirectory))
            {
                Console.Error.WriteLine($"Error: Directory '{outputDirectory}' doesn't exist.");
                return false;
            }

            return true;
        }

        public static void GenerateBindings(string dylibPath, string swiftInterfacePath, string outputDirectory, int verbositry = 0)
        {
            BindingsCompiler bindingsCompiler = new BindingsCompiler();
            var errors = new ErrorHandling ();
			var moduleInventory = bindingsCompiler.GetModuleInventory(dylibPath, errors);
			var moduleDeclarations = bindingsCompiler.GetModuleDeclarations(swiftInterfacePath);
			bindingsCompiler.CompileModules(moduleDeclarations, moduleInventory, dylibPath, outputDirectory, errors);
        }
    }
}
