// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using SwiftRuntimeLibrary;

namespace BindingsGeneration
{
    /// <summary>
    /// Command-line tool for generating C# bindings from Swift ABI files.
    /// </summary>
    public class BindingsGenerator
    {
        /// <summary>
        /// Main entry point of the bindings generator tool.
        /// </summary>
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
                    Console.WriteLine("  -v, --verbose      Information about work in process.");
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

        /// <summary>
        /// Generates C# bindings from a Swift ABI file.
        /// </summary>
        /// <param name="swiftAbiPath">Path to the Swift ABI file.</param>
        /// <param name="outputDirectory">Output directory for generated bindings.</param>
        /// <param name="verbose">Verbosity level for logging information.</param>
        public static void GenerateBindings(string swiftAbiPath, string outputDirectory, int verbose = 0)
        {
            if (verbose > 0)
                Console.WriteLine("Starting bindings generation...");

            ISwiftParser swiftParser = new SwiftABIParser(swiftAbiPath, verbose);
            var decl = swiftParser.GetModuleDecl();

            if (verbose > 1)
                Console.WriteLine("Parsed Swift ABI file successfully.");


            TypeDatabase typeDatabase = new TypeDatabase(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TypeDatabase.xml"));

            ICSharpEmitter csharpEmitter = new StringCSharpEmitter(outputDirectory, typeDatabase, verbose);
            csharpEmitter.EmitModule(decl);

            if (verbose > 0)
                Console.WriteLine("Bindings generation completed.");
        }
    }
}
