// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Swift.Runtime;
using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents the root node of the ABI.
    /// </summary>
    public sealed class ABIRootNode
    {
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
        public required string DeclKind { get; set; }
        public required string Name { get; set; }
        public required string MangledName { get; set; }
        public required string PrintedName { get; set; }
        public required string ModuleName { get; set; }
        public required string [] DeclAttributes { get; set; }
        public required bool? @static { get; set; }
        public required IEnumerable<Node> Children { get; set; }
    }

    /// <summary>
    /// Represents a parser for Swift ABI.
    /// </summary>
    public sealed class SwiftABIParser : ISwiftParser
    {
        private static readonly HashSet<string> _operators = new HashSet<string>
        {
            // Arithmetic
            "+", "-", "*", "/", "%",
            // Relational
            "<", ">", "<=", ">=", "==", "!=",
            // Logical
            "&&", "||", "!",
            // Bitwise
            "&", "|", "^", "~", "<<", ">>",
            // Assignment
            "=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=",
            // Other
            "??", "?.", "=>"
        };
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
            if (abiRoot?.ABIRoot?.Children == null)
            {
                throw new InvalidOperationException("Invalid ABI structure.");
            }

            var moduleName = abiRoot.ABIRoot.Children.FirstOrDefault()?.ModuleName ?? string.Empty;
            var dependencies = new List<string>
            {
                "System",
                "System.Runtime.InteropServices",
                "System.Runtime.CompilerServices",
                "System.Runtime.InteropServices.Swift"
            };

            var declarations = CollectDeclarations(abiRoot.ABIRoot.Children, dependencies);

            return new ModuleDecl
            {
                Name = ExtractUniqueName(moduleName),
                Declarations = declarations,
                Dependencies = dependencies
            };
        }

        /// <summary>
        /// Collects declarations from a list of nodes.
        /// </summary>
        /// <param name="node">The node representing a declaration.</param>
        /// <param name="dependencies">A list of dependencies for the declaration.</param>
        private List<BaseDecl> CollectDeclarations(IEnumerable<Node> nodes, List<string> dependencies)
        {
            var declarations = new List<BaseDecl>();
            foreach (var node in nodes)
            {
                var nodeDeclaration = HandleNode(node, dependencies);
                if (nodeDeclaration != null)
                    declarations.Add(nodeDeclaration);
            }
            return declarations;
        }

        /// <summary>
        /// Handles an ABI node and returns the corresponding declaration.
        /// </summary>
        /// <param name="node">The node representing a declaration.</param>
        /// <param name="dependencies">A list of dependencies for the declaration.</param>
        private BaseDecl? HandleNode(Node node, List<string> dependencies)
        {
            BaseDecl? result = null;
            try 
            {
                switch (node.Kind)
                {
                    case "TypeDecl":
                        result = HandleTypeDecl(node, dependencies);
                        break;
                    case "Function":
                        // TODO: Implement operator overloading
                        result = IsOperator(node.Name) ? null : CreateMethodDecl(node, dependencies);
                        break;
                    default:
                        if (_verbose > 1)
                            Console.WriteLine($"Unsupported declaration '{node.DeclKind} {node.Name}' encountered.");
                        break;
                }
            } catch (Exception e) 
            {
                Console.WriteLine($"Error while processing node '{node.Name}': {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Handles a type declaration node and returns the corresponding declaration.
        /// </summary>
        /// <param name="node">The node representing a type declaration.</param>
        /// <param name="dependencies">A list of dependencies for the declaration.</param>
        private BaseDecl? HandleTypeDecl(Node node, List<string> dependencies)
        {
            BaseDecl? decl = null;
            switch (node.DeclKind)
            {
                case "Struct":
                case "Enum":
                    if (Array.IndexOf(node.DeclAttributes, "Frozen") != -1) 
                    {
                        decl = CreateStructDecl(node, dependencies);
                    } else {
                        decl = CreateClassDecl(node, dependencies);
                    }

                    if (node.Children != null)
                    {
                        var childDeclarations = CollectDeclarations(node.Children, dependencies);
                        decl.Declarations.AddRange(childDeclarations);
                    }
                    break;
                case "Class":
                    decl = CreateStructDecl(node, dependencies);

                    if (node.Children != null)
                    {
                        var childDeclarations = CollectDeclarations(node.Children, dependencies);
                        decl.Declarations.AddRange(childDeclarations);
                    }
                    break;
                default:
                    if (_verbose > 1)
                        Console.WriteLine($"Unsupported declaration type '{node.DeclKind} {node.Name}' encountered.");
                    break;
            }

            return decl;
        }

        /// <summary>
        /// Creates a struct declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the struct declaration.</param>
        /// <param name="dependencies">A list of dependencies for the struct declaration.</param>
        /// <returns>The struct declaration.</returns>
        private StructDecl CreateStructDecl(Node node, List<string> dependencies)
        {
            return new StructDecl
            {
                Name = ExtractUniqueName(node.Name),
                MangledName = node.MangledName,
                Declarations = new List<BaseDecl>()
            };
        }

        /// <summary>
        /// Creates a class declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the class declaration.</param>
        /// <param name="dependencies">A list of dependencies for the class declaration.</param>
        /// <returns>The class declaration.</returns>
        private ClassDecl CreateClassDecl(Node node, List<string> dependencies)
        {
            return new ClassDecl
            {
                Name = ExtractUniqueName(node.Name),
                MangledName = node.MangledName,
                Declarations = new List<BaseDecl>()
            };
        }

        /// <summary>
        /// Creates a method declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the method declaration.</param>
        /// <param name="dependencies">A list of dependencies for the method declaration.</param>
        /// <returns>The method declaration.</returns>
        private MethodDecl CreateMethodDecl(Node node, List<string> dependencies)
        {
            // Extract parameter names from the signature
            var paramNames = ExtractParameterNames(node.PrintedName);

            var methodDecl = new MethodDecl
            {
                Name = ExtractUniqueName(node.Name),
                MangledName = node.MangledName,
                RequireMarshalling = false,
                IsStatic = node.@static ?? false,
                Signature = new List<TypeDecl>(),
                Declarations = new List<BaseDecl>()
            };

            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count(); i++)
                {
                    var typeDecl = CreateTypeDecl(node.Children.ElementAt(i), dependencies);
                    typeDecl.Name = paramNames[i];
                    methodDecl.Signature.Add(typeDecl);
                }
            }

            return methodDecl;
        }

        /// <summary>
        /// Creates a type declaration from a given node.
        /// </summary>
        /// <param name="node">The node representing the type declaration.</param>
        /// <param name="dependencies">A list of dependencies for the type declaration.</param>
        /// <returns>The type declaration.</returns>
        private TypeDecl CreateTypeDecl(Node node, List<string> dependencies)
        {
            var typeDecl = new TypeDecl
            {
                Name = string.Empty,
                TypeIdentifier = string.Empty,
                Generics = new List<TypeDecl>(),
                Declarations = new List<BaseDecl>()
            };

            string[] csharpTypeName;
            // If the node has children, it is a generic type
            if (node.Children != null && node.Children.Count() > 0)
            {
                string baseTypeName = $"{node.Name}`{node.Children.Count()}";
                csharpTypeName = _typeDatabase.GetCSharpTypeName(baseTypeName);
                typeDecl.TypeIdentifier = csharpTypeName[1].Replace($"`{node.Children.Count()}", "") + "<";

                for (int i = 0; i < node.Children.Count(); i++)
                {
                    var child = CreateTypeDecl(node.Children.ElementAt(i), dependencies);
                    typeDecl.Generics.Add(child);
                    if (i > 0)
                        typeDecl.TypeIdentifier += ", ";
                    typeDecl.TypeIdentifier += child.TypeIdentifier;
                }

                typeDecl.TypeIdentifier += ">";
            }
            // If the node has no children, it is a non-generic type
            else
            {
                csharpTypeName = _typeDatabase.GetCSharpTypeName(node.Name);
                typeDecl.TypeIdentifier = csharpTypeName[1];
            }

            if (!dependencies.Contains(csharpTypeName[0]))
            {
                dependencies.Add(csharpTypeName[0]);
            }

            return typeDecl;
        }

        /// <summary>
        /// Extracts and processes parameter names from a method signature.
        /// </summary>
        /// <param name="signature">The method signature string.</param>
        /// <returns>A list of processed parameter names.</returns>
        private List<string> ExtractParameterNames(string signature)
        {
            // Split the signature to get parameter names part and process it.
            var paramNames = signature.Split('(', ')')[1]
                                    .Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries)
                                    .ToList();

            for (int i = 0; i < paramNames.Count; i++)
            {
                paramNames[i] = ExtractUniqueName(paramNames[i]);
                // If the parameter name is just "_", generate a unique generic name
                if (paramNames[i] == "_")
                {
                    paramNames[i] = $"arg{i}";
                }
            }

            // Return type is the first element in the signature
            paramNames.Insert(0, string.Empty);

            return paramNames;
        }

        /// <summary>
        /// Check if the name is a keyword and prefix it with "_".
        /// </summary>
        private static string ExtractUniqueName(string name)
        {
            if (SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
            {
                return $"_{name}";
            }

            return name;
        }

        /// <summary>
        /// Check if the name is an operator.
        /// </summary>
        private static bool IsOperator(string name)
        {
            return _operators.Contains(name);
        }
    }
}
