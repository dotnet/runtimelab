using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DllImportGenerator.Tools
{
    public enum OutputFormat
    {
        Csv,
        Html,
        Text
    }

    public class Options
    {
        public FileInfo Assembly { get; init; }
        public DirectoryInfo Directory { get; init; }
        public FileInfo OutputFile { get; init; }
        public string[] Exclude { get; init; }
        public OutputFormat OutputFormat { get; init; }
        public bool Quiet { get; init; }
    }

    class Program
    {
        private readonly Options options;
        private readonly PInvokeDump dump = new PInvokeDump();

        private Program (Options options)
        {
            this.options = options;
        }

        public void Run()
        {
            if (options.Assembly != null)
            {
                Console.WriteLine($"Processing assembly '{options.Assembly}'...");
                ReadAssembly(options.Assembly);
            }

            if (options.Directory != null)
            {
                Console.WriteLine($"Processing directory '{options.Directory}'...");
                foreach (var file in options.Directory.GetFiles("*.dll"))
                {
                    ReadAssembly(file);
                }
            }

            Console.WriteLine($"Total: {dump.Count}");
            Print();
        }

        private void ReadAssembly(FileInfo assemblyFile)
        {
            if (options.Exclude != null && options.Exclude.Contains(assemblyFile.Name, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  Skipping '{assemblyFile.Name}'");
                return;
            }

            (bool hasMetadata, int count) = dump.Process(assemblyFile);
            if (hasMetadata && !options.Quiet)
                Console.WriteLine($"  {assemblyFile.Name} :\t{count} P/Invoke method(s)");
        }

        private void Print()
        {
            string output = options.OutputFormat switch
            {
                OutputFormat.Csv => Reporting.Csv.Generate(dump),
                OutputFormat.Html => Reporting.Html.Generate(dump, "P/Invokes", ((FileSystemInfo)options.Assembly ?? options.Directory).ToString()),
                _ => Reporting.Text.Generate(dump)
            };

            if (options.OutputFile != null)
            {
                FileInfo outputFile = options.OutputFile;

                // Delete any pre-existing file
                if (outputFile.Exists)
                    outputFile.Delete();

                // Write info to the output file.
                using var outputFileStream = new StreamWriter(outputFile.OpenWrite());
                outputFileStream.Write(output);
            }
            else
            {
                if (!options.Quiet)
                    Console.WriteLine(output);
            }
        }

        public static Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<FileInfo>(
                    new string[] { "--assembly", "-a" },
                    "Assembly to inspect"),
                new Option<DirectoryInfo>(
                    new string[] {  "--directory", "-d" },
                    "Directory with assemblies to inspect"),
                new Option<FileInfo>(
                    new string[] { "--output-file", "-o" },
                    "Output file"),
                new Option<OutputFormat>(
                    new string[] { "--output-format", "-fmt" },
                    getDefaultValue: () => OutputFormat.Text,
                    "Output format"),
                new Option<string>(
                    new string[] { "--exclude", "-x"},
                    "Name of file to exclude")
                    {
                        Argument = new Argument<string>() { Arity = ArgumentArity.ZeroOrMore }
                    },
                new Option<bool>(
                    new string[] { "--quiet", "-q" },
                    getDefaultValue: () => false,
                    "Reduced console output")
            };

            rootCommand.Description = "Read P/Invoke information from assembly metadata";
            rootCommand.AddValidator(result =>
            {
                if (result.Children.Contains("--assembly") &&
                    result.Children.Contains("--directory"))
                {
                    return "Options '--assembly' and '--directory' cannot be used together.";
                }

                if (!result.Children.Contains("--assembly") &&
                    !result.Children.Contains("--directory"))
                {
                    return "Either '--assembly' or '--directory' must be specified.";
                }

                return null;
            });

            rootCommand.Handler = CommandHandler.Create<Options>((Options o) => new Program(o).Run());
            return rootCommand.InvokeAsync(args);
        }
    }
}
