// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents the root node of the ABI.
    /// </summary>
    public sealed class ABIRootNode
    {
        [JsonProperty("ABIRoot")]
        public required RootNode ABIRoot { get; set; }
    }

    /// <summary>
    /// Represents the root node of a module.
    /// </summary>
    public sealed class RootNode
    {
        public required string Kind { get; set; }
        public required string Name { get; set; }
        public required string PrintedName { get; set; }
        public required IEnumerable<Node> Children { get; set; }
    }

    /// <summary>
    /// Represents a node.
    /// </summary>
    public sealed class Node
    {
        public required string Kind { get; set; }
        public required string Name { get; set; }
        public required string MangledName { get; set; }
        public required string ModuleName { get; set; }
        public required string PrintedName { get; set; }
        public required IEnumerable<Node> Children { get; set; }
    }

    /// <summary>
    /// Represents a parser for Swift ABI.
    /// </summary>
    public sealed class SwiftABIParser : ISwiftParser
    {
        private readonly string _filePath;
        private readonly int _verbose;
        private readonly TypeDatabase _typeDatabase;

        public SwiftABIParser(string filePath, TypeDatabase typeDatabase, int verbose = 0)
        {
            _filePath = filePath;
            _verbose = verbose;
            _typeDatabase = typeDatabase;
        }

        /// <summary>
        /// Gets the module declaration from the ABI file.
        /// </summary>
        /// <returns>The module declaration.</returns>
        public ModuleDecl GetModuleDecl()
        {
            string jsonContent = File.ReadAllText(_filePath);
            var abiRoot = JsonConvert.DeserializeObject<ABIRootNode>(jsonContent);

            var moduleDecl = new ModuleDecl
            {
                Name = Path.GetFileNameWithoutExtension(_filePath).Replace(".abi", ""),
                Methods = new List<MethodDecl>(),
                Dependencies = new List<string>() { "System", "System.Runtime.InteropServices", "System.Runtime.CompilerServices" }
            };

            if (abiRoot?.ABIRoot?.Children == null) return moduleDecl;

            var stack = new Stack<Node>(abiRoot.ABIRoot.Children);
            while (stack.Count > 0)
            {
                var node = stack.Pop();

                switch (node.Kind)
                {
                    case "Function":
                        try
                        {
                            moduleDecl.Methods.Add(CreateMethodDecl(node, moduleDecl.Dependencies));
                        }
                        catch (Exception e)
                        {
                            if (_verbose > 1)
                                Console.WriteLine($"Warning: Unsupported Swift type: {e.Message}");
                        }
                        break;
                    case "TypeNominal":
                        break;
                    default:
                        if (_verbose > 1)
                            Console.WriteLine($"Warning: Unsupported node type '{node.Kind}' encountered during parsing.");
                        break;
                }

                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        stack.Push(child);
                    }
                }
            }

            return moduleDecl;
        }

        /// <summary>
        /// Creates a method declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the method declaration.</param>
        /// <returns>The method declaration.</returns>
        public MethodDecl CreateMethodDecl(Node node, List<string> dependencies)
        {
            // Read the parameter names from the signature
            var paramNames = new string[] { "" }.Concat(node.PrintedName.Split("(")[1].Split(")")[0].Split(":", StringSplitOptions.RemoveEmptyEntries)).ToList();
            var methodDecl = new MethodDecl
            {
                Name = node.Name,
                MangledName = node.MangledName,
                Signature = new List<TypeDecl>(),
                RequireMarshalling = false
            };

            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count(); i++)
                {
                    var child = node.Children.ElementAt(i);
                    string[] csharpTypeName = _typeDatabase.GetCSharpTypeName(child.Name);
                    if (!dependencies.Contains(csharpTypeName[0]))
                        dependencies.Add(csharpTypeName[0]);

                    methodDecl.Signature.Add(new TypeDecl
                    {
                        Name = paramNames[i],
                        FullyQualifiedName = csharpTypeName[1],
                        TypeKind = TypeKind.Named
                    });
                }
            }

            return methodDecl;
        }
    }
}
