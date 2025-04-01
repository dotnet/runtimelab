// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using Microsoft.Extensions.Logging;

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
            Option<string> tbdOption = new(aliases: new[] { "-t", "--tbd" }, "Path to the TBD file.") { IsRequired = true };
            Option<string> outputDirectoryOption = new(aliases: new[] { "-o", "--output" }, "Output directory for generated bindings.") { IsRequired = true };
            Option<int> verboseOption = new(
                aliases: new[] { "-v", "--verbose" },
                description: "Verbosity level. 0 = No logging, 1 = General information, 2 = Debugging information. (default: 1)",
                getDefaultValue: () => 1);
            Option<bool> helpOption = new(aliases: new[] { "-h", "--help" }, "Display a help message.");

            RootCommand rootCommand = new(description: "Swift bindings generator.")
            {
                swiftAbiOption,
                dylibOption,
                tbdOption,
                outputDirectoryOption,
                verboseOption,
                helpOption,
            };
            rootCommand.SetHandler((string swiftAbiPath, string dylibPath, string tbdPath, string outputDirectory, int verbose, bool help) =>
            {
                if (help)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  -a, --swiftabi     Required. Path to the Swift ABI file.");
                    Console.WriteLine("  -d, --dylib        Required. Path to the dynamic library.");
                    Console.WriteLine("  -t, --tbd          Required. Path to the TBD file.");
                    Console.WriteLine("  -o, --output       Required. Output directory for generated bindings.");
                    Console.WriteLine("  -v, --verbose      Verbosity level. 0 = No logging, 1 = General information, 2 = Debugging information. (default: 1)");
                    return;
                }

                ILoggerFactory loggerFactory = CreateLoggerFactory(verbose);
                ILogger logger = loggerFactory.CreateLogger<BindingsGenerator>();

                if (string.IsNullOrWhiteSpace(swiftAbiPath) || !File.Exists(swiftAbiPath))
                {
                    logger.LogError("Error: Valid Swift ABI file is required.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(dylibPath) || !File.Exists(dylibPath))
                {
                    logger.LogError("Error: Valid dynamic library is required.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(tbdPath) || !File.Exists(tbdPath))
                {
                    logger.LogError("Error: Valid TBD file is required.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
                {
                    logger.LogError("Error: Valid output directory is required.");
                    return;
                }

                GenerateBindings(swiftAbiPath, dylibPath, tbdPath, outputDirectory, logger, loggerFactory);
            },
            swiftAbiOption,
            dylibOption,
            tbdOption,
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
        /// <param name="logger">ILogger instance.</param>
        public static void GenerateBindings(string swiftAbiPath, string dylibPath, string tbdPath, string outputDirectory, ILogger logger, ILoggerFactory loggerFactory)
        {
            var typeDatabase = new TypeDatabase();
            string[] moduleDatabases = { "FoundationDatabase.xml", "SwiftDatabase.xml" };
            foreach (var database in moduleDatabases)
            {
                typeDatabase.LoadModuleDatabaseFromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Swift", database)).Wait();
            }

            logger.LogInformation("Starting bindings generation for {SwiftAbiPath}...", swiftAbiPath);

            // Parse the TBD file
            Demangling.DemanglingResults demangledTbdFile = Demangling.DemanglingResults.FromTbd(tbdPath, loggerFactory);

            // Initialize the Swift ABI parser
            var swiftParser = new SwiftABIParser(swiftAbiPath, typeDatabase, demangledTbdFile, loggerFactory.CreateLogger<SwiftABIParser>());
            var moduleName = swiftParser.GetModuleName();

            // Skip if the module has already been processed
            // Modules will have to be processed in topological order
            if (!typeDatabase.IsModuleProcessed(moduleName))
            {
                // Parse the Swift ABI file and generate declarations
                var (decl, moduleTypes) = swiftParser.ParseModule();

                var moduleProcessor = new ModuleProcessor(moduleName, dylibPath, moduleTypes, typeDatabase, loggerFactory.CreateLogger<ModuleProcessor>());
                var moduleDatabase = moduleProcessor.FinalizeTypeProcessingAndCreateModuleDatabase().ModuleDatabase;
                typeDatabase.AddModuleDatabase(moduleDatabase);

                logger.LogDebug("Parsed Swift ABI file successfully.");

                // Emit the C# bindings
                var stringEmitter = new StringEmitter(outputDirectory, typeDatabase, loggerFactory);
                stringEmitter.EmitModule(decl);

                logger.LogInformation("Bindings generation completed for {SwiftAbiPath}.", swiftAbiPath);

            }
            else
                logger.LogWarning("Bindings generation already completed for {SwiftAbiPath}.", swiftAbiPath);

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

        /// <summary>
        /// Creates and configures a logger factory based on the verbosity level.
        /// </summary>
        /// <param name="verbosity">Verbosity level (0 = No logging, 1 = General information, 2 = Debugging information).</param>
        static ILoggerFactory CreateLoggerFactory(int verbosity)
        {
            return LoggerFactory.Create(builder =>
            {
                builder.AddConsole();

                builder.SetMinimumLevel(verbosity switch
                {
                    0 => LogLevel.None,  // No logging
                    1 => LogLevel.Information, // Info and above
                    2 => LogLevel.Debug,    // Debug and above
                    _ => throw new ArgumentOutOfRangeException(nameof(verbosity), "Invalid verbosity level.")
                });
            });
        }
    }
}
