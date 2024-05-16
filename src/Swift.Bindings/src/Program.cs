// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using Swift.Runtime;
using System.IO;
using System.Diagnostics;
using System.CodeDom.Compiler;

namespace BindingsGeneration
{
    /// <summary>
    /// Command-line tool for generating C# bindings from Swift ABI files.
    /// </summary>
    public class BindingsGenerator
    {
        /// <summary>
        /// Platform for the bindings generator tool.
        /// </summary>
        public static string platform = "MacOSX";

        /// <summary>
        /// SDK version for the bindings generator tool.
        /// </summary>
        public static string sdk = "14.4";

        /// <summary>
        /// Architecture for the bindings generator tool.
        /// </summary>
        public static string arch = "arm64e";

        /// <summary>
        /// Target for the bindings generator tool.
        /// </summary>
        public static string target = "apple-macos";

        /// <summary>
        /// Main entry point of the bindings generator tool.
        /// </summary>
        public static void Main(string[] args)
        {
            Option<IEnumerable<string>> swiftAbiOption = new(aliases: new[] { "-a", "--swiftabi", "-f", "--framework" }, "Path to the Swift ABI file or framework.") { AllowMultipleArgumentsPerToken = true, IsRequired = true };
            Option<string> outputDirectoryOption = new(aliases: new[] { "-o", "--output" }, "Output directory for generated bindings.") { IsRequired = true };
            Option<string> platformOption = new(aliases: new[] { "--platform" }, "Platform, e.g., MacOSX.");
            Option<string> sdkOption = new(aliases: new[] { "-sdk", "--sdk" }, "SDK version, e.g., 14.4.");
            Option<string> archOption = new(aliases: new[] { "-arch", "--architecture" }, "Architecture, e.g., arm64e.");
            Option<string> targetOption = new(aliases: new[] { "--target" }, "Target, e.g., apple-macos.");
            Option<int> verboseOption = new(aliases: new[] { "-v", "--verbose" }, "Prints information about work in process.");
            Option<bool> helpOption = new(aliases: new[] { "-h", "--help" }, "Display a help message.");

            RootCommand rootCommand = new(description: "Swift bindings generator.")
            {
                swiftAbiOption,
                outputDirectoryOption,
                platformOption,
                sdkOption,
                archOption,
                targetOption,
                verboseOption,
                helpOption,
            };
            rootCommand.SetHandler((IEnumerable<string> swiftAbiPaths, string outputDirectory, string platform, string sdk, string arch, string target, int verbose, bool help) =>
            {
                if (help)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  -a, --swiftabi, -f, --framework     Required. Path to the Swift ABI file or framework.");
                    Console.WriteLine("  -o, --output                        Required. Output directory for generated bindings.");
                    Console.WriteLine("  --platform                          Platform, e.g., MacOSX.");
                    Console.WriteLine("  -sdk, --sdk                         SDK version, e.g., 14.4.");
                    Console.WriteLine("  -arch, --architecture               Architecture, e.g., arm64e.");
                    Console.WriteLine("  --target                            Target, e.g., apple-macos.");
                    Console.WriteLine("  -v, --verbose                       Information about work in process.");
                    return;
                }

                if (outputDirectory == string.Empty)
                {
                    Console.Error.WriteLine("Error: Missing required argument(s).");
                    return;
                }

                if (!string.IsNullOrEmpty(platform))
                    BindingsGenerator.platform = platform;

                if (!string.IsNullOrEmpty(sdk))
                    BindingsGenerator.sdk = sdk;

                if (!string.IsNullOrEmpty(arch))
                    BindingsGenerator.arch = arch;

                if (!string.IsNullOrEmpty(target))
                    BindingsGenerator.target = target;

                Queue<string> queueList = new Queue<string>(swiftAbiPaths);
                GenerateBindings(queueList, outputDirectory, verbose);
            },
            swiftAbiOption,
            outputDirectoryOption,
            platformOption,
            sdkOption,
            archOption,
            targetOption,
            verboseOption,
            helpOption
            );

            rootCommand.Invoke(args);
        }

        /// <summary>
        /// Simplified entry point for generating C# bindings from a single Swift ABI file.
        /// </summary>
        /// <param name="path">Path to the Swift ABI file.</param>
        /// <param name="outputDirectory">Output directory for generated bindings.</param>
        public static void GenerateBindings(string path, string outputDirectory)
        {
            GenerateBindings(new Queue<string>(new string[] { path }), outputDirectory);    
        }

        /// <summary>
        /// Generates C# bindings from Swift ABI files.
        /// </summary>
        /// <param name="paths">Queue of paths to Swift ABI files.</param>
        /// <param name="outputDirectory">Output directory for generated bindings.</param>
        /// <param name="verbose">Verbosity level for logging information.</param>
        public static void GenerateBindings(Queue<string> paths, string outputDirectory, int verbose = 2)
        {
            TypeDatabase typeDatabase = new TypeDatabase(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TypeDatabase.xml"));

            while (paths.Count > 0)
            {
                string path = paths.Dequeue();
                string dylib;

                if (verbose > 0)
                    Console.WriteLine($"Starting bindings generation for {path}...");

                if (!File.Exists(path))
                {
                    // In this case, the path is assumed to be a framework name
                    dylib = $"/System/Library/Frameworks/{path}.framework/{path}";
                    path = ResolveABIPath(path, outputDirectory);
                }
                else
                {
                    // Resolve the dylib path from the ABI file
                    dylib = $"{Path.GetDirectoryName(path)}/lib{Path.GetFileName(path).Replace(".abi.json", "")}.dylib";
                }

                if (!Path.Exists(path) || !Path.Exists(dylib))
                {
                    Console.WriteLine($"Error: Swift ABI file or dylib not found at path '{path}' or '{dylib}'.");
                    continue;
                }

                // Initialize the Swift ABI parser
                var swiftParser = new SwiftABIParser(path, dylib, typeDatabase, verbose);
                var moduleName = swiftParser.GetModuleName();
                
                // Skip if the module has already been processed
                if (typeDatabase.IsModuleProcessed(moduleName))
                {
                    if (verbose > 0)
                        Console.WriteLine($"Bindings generation already completed for {path}.");
                    continue;
                }
                
                // Register the module and set the filter
                var moduleRecord = typeDatabase.Registrar.RegisterModule(moduleName);
                moduleRecord.Path = dylib;
                var filters = typeDatabase.GetUnprocessedTypes(moduleName);
                swiftParser.SetFilter(filters);

                // Parse the Swift ABI file and generate declarations
                var decl = swiftParser.GetModuleDecl();

                if (verbose > 1)
                    Console.WriteLine("Parsed Swift ABI file successfully.");

                // Emit the C# bindings
                ICSharpEmitter csharpEmitter = new StringCSharpEmitter(outputDirectory, typeDatabase, verbose);
                csharpEmitter.EmitModule(decl);

                moduleRecord.IsProcessed = true;
                if (verbose > 0)
                    Console.WriteLine($"Bindings generation completed for {path}.");

                // Add any unprocessed modules to the queue
                foreach (var type in typeDatabase.GetUnprocessedModules())
                    paths.Enqueue(type);
            }

            // Copy the Swift.Runtime library to the output directory
            string[] fileEntries = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library"));
            foreach (string filePath in fileEntries)
            {
                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(outputDirectory, fileName);
                File.Copy(filePath, destFilePath, true);
            }
        }

        /// <summary>
        /// Resolves the ABI path for a given framework.
        /// </summary>
        /// <param name="framework">Name of the framework.</param>
        /// <param name="outputDirectory">Output directory for the ABI file.</param>
        /// <returns>Path to the ABI file.</returns>
        private static string ResolveABIPath(string framework, string outputDirectory)
        {
            string outputPath = Path.Combine(outputDirectory, $"{framework}.abi.json");
            string sdkPathCommand = $"xcrun -sdk {platform.ToLower()} --show-sdk-path";
            string swiftInterfacePath = $"/Applications/Xcode.app/Contents/Developer/Platforms/{platform}.platform/Developer/SDKs/{platform}{sdk}.sdk/System/Library/Frameworks/{framework}.framework/Versions/Current/Modules/{framework}.swiftmodule/{arch}-{target}.swiftinterface";
            string command = $"xcrun swift-frontend -compile-module-from-interface {swiftInterfacePath} -module-name {framework} -sdk `{sdkPathCommand}` -emit-abi-descriptor-path {outputPath}";
            
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (Process? process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Console.Error.WriteLine("Error: Failed to start process. Command: " + command);
                }
            }

            return outputPath;
        }
    }
}
