// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;

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
            Option<string> swiftAbiOption = new(aliases: new[] { "-a", "--swiftabi" }, "Path to the Swift ABI file.") { IsRequired = true };
            Option<string> dylibOption = new(aliases: new[] { "-d", "--dylib" }, "Path to the dynamic library.") { IsRequired = true };
            Option<string> outputDirectoryOption = new(aliases: new[] { "-o", "--output" }, "Output directory for generated bindings.") { IsRequired = true };
            Option<int> verboseOption = new(aliases: new[] { "-v", "--verbose" }, "Verbosity level.");
            Option<bool> helpOption = new(aliases: new[] { "-h", "--help" }, "Display a help message.");

            RootCommand rootCommand = new(description: "Swift bindings generator.")
            {
                swiftAbiOption,
                dylibOption,
                outputDirectoryOption,
                verboseOption,
                helpOption,
            };
            rootCommand.SetHandler((string swiftAbiPath, string dylibPath, string outputDirectory, int verbose, bool help) =>
            {
                if (help)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  -a, --swiftabi     Required. Path to the Swift ABI file.");
                    Console.WriteLine("  -d, --dylib        Required. Path to the dynamic library.");
                    Console.WriteLine("  -o, --output       Required. Output directory for generated bindings.");
                    Console.WriteLine("  -v, --verbose      Verbosity level.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(swiftAbiPath) || !File.Exists(swiftAbiPath))
                {
                    Console.Error.WriteLine("Error: Valid Swift ABI file is required.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(dylibPath) || !File.Exists(dylibPath))
                {
                    Console.Error.WriteLine("Error: Valid dynamic library is required.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
                {
                    Console.Error.WriteLine("Error: Valid output directory is required.");
                    return;
                }

                GenerateBindings(swiftAbiPath, dylibPath, outputDirectory, verbose);
            },
            swiftAbiOption,
            dylibOption,
            outputDirectoryOption,
            verboseOption,
            helpOption
            );

            rootCommand.Invoke(args);
        }

        /// <summary>
        /// Generates C# bindings from Swift ABI files.
        /// </summary>
        /// <param name="swiftAbiPath">Path to the Swift ABI file.</param>
        /// <param name="dylibPath">Path to the dynamic library.</param>
        /// <param name="outputDirectory">Output directory for generated bindings.</param>
        /// <param name="verbose">Verbosity level.</param>
        public static void GenerateBindings(string swiftAbiPath, string dylibPath, string outputDirectory, int verbose = 2)
        {
            TypeDatabase typeDatabase = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Swift", "TypeDatabase.xml"));

            if (verbose > 0)
                Console.WriteLine($"Starting bindings generation for {swiftAbiPath}...");

            // Initialize the Swift ABI parser
            var swiftParser = new SwiftABIParser(swiftAbiPath, dylibPath, typeDatabase, verbose);
            var moduleName = swiftParser.GetModuleName();

            // Skip if the module has already been processed
            // Modules will have to be processed in topological order
            if (!typeDatabase.IsModuleProcessed(moduleName))
            {

                // Register the module and set the filter
                var moduleRecord = typeDatabase.Registrar.RegisterModule(moduleName);
                moduleRecord.Path = dylibPath;

                // Parse the Swift ABI file and generate declarations
                var (decl, moduleTypes) = swiftParser.ParseModule();

                var moduleProcessor = new ModuleProcessor(moduleName, dylibPath, moduleTypes, typeDatabase, verbose);
                moduleProcessor.FinalizeTypeProcessing();

                if (verbose > 1)
                    Console.WriteLine("Parsed Swift ABI file successfully.");

                // Emit the C# bindings
                var csharpEmitter = new StringCSharpEmitter(outputDirectory, typeDatabase, verbose);
                csharpEmitter.EmitModule(decl);

                moduleRecord.IsProcessed = true;
                if (verbose > 0)
                    Console.WriteLine($"Bindings generation completed for {swiftAbiPath}.");

            }
            else if (verbose > 0)
                Console.WriteLine($"Bindings generation already completed for {swiftAbiPath}.");

            // Copy the Swift library to the output directory
            CopyDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Swift"), Path.Combine(outputDirectory, "Swift"), true);
        }

        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}
