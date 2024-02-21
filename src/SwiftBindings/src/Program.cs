// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using SwiftReflector;
using SwiftReflector.Parser;

namespace SwiftBindings
{
    public class BindingsTool
    {
        public static void Main(string[] args)
        {
            Option<IEnumerable<string>> swiftAbiOption = new(aliases: new[] { "-a", "--swiftabi" }, "Path to the Swift ABI file.") { AllowMultipleArgumentsPerToken = true, IsRequired = true };
            Option<string> outputDirectoryOption = new(aliases: new[] { "-o", "--output" }, "Output directory for generated bindings.") { IsRequired = true };
            Option<int> verboseOption = new(aliases: new[] { "-v", "--verbose" }, "Prints information about work in process.");
            Option<bool> helpOption = new(aliases: new[] { "-h", "--help" }, "Display a help message.");

            RootCommand rootCommand = new(description: "Swift bindings generator.")
            {
                swiftAbiOption,
                outputDirectoryOption,
                verboseOption,
                helpOption,
            };
            rootCommand.SetHandler((IEnumerable<string> swiftAbiPaths, string outputDirectory, int verbose, bool help) =>
            {
                if (help)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  -a, --swiftabi     Required. Path to the Swift ABI file.");
                    Console.WriteLine("  -o, --output       Required. Output directory for generated bindings.");
                    Console.WriteLine("  -v, --verbose      nformation about work in process.");
                    return;
                }

                if (outputDirectory == string.Empty)
                {
                    Console.Error.WriteLine("Error: Missing required argument(s).");
                    return;
                }

                for (int i = 0; i < swiftAbiPaths.Count(); i++)
                {
                    string swiftAbiPath = swiftAbiPaths.ElementAt(i);

                    if (!File.Exists(swiftAbiPath))
                    {
                        Console.Error.WriteLine($"Error: Swift ABI file not found at path '{swiftAbiPath}'.");
                        return;
                    }

                    if (verbose > 0)
                        Console.WriteLine($"Processing Swift ABI file: {swiftAbiPath}");

                    GenerateBindings(swiftAbiPath, outputDirectory, verbose);
                }
            },
            swiftAbiOption,
            outputDirectoryOption,
            verboseOption,
            helpOption
            );

            rootCommand.Invoke(args);
        }

        public static void GenerateBindings(string swiftAbiPath, string outputDirectory, int verbose = 0)
        {
            if (verbose > 0)
                Console.WriteLine("Starting bindings generation...");

            BindingsCompiler bindingsCompiler = new BindingsCompiler();
            ISwiftParser swiftParser = new SwiftABIParser();
            var errors = new ErrorHandling();
            var decl = swiftParser.GetModuleDeclaration(swiftAbiPath, errors);

            if (verbose > 1)
                Console.WriteLine("Parsed Swift ABI file successfully.");

            bindingsCompiler.CompileModule(decl, outputDirectory, errors);

            if (verbose > 0)
                Console.WriteLine("Bindings generation completed.");
        }
    }
}
