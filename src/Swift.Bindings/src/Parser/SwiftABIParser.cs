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
    public record ABIRootNode
    {
        public required RootNode ABIRoot { get; set; }
    }

    /// <summary>
    /// Represents the root node of a module.
    /// </summary>
    public record RootNode
    {
        public required string Kind { get; set; }
        public required string Name { get; set; }
        public required string PrintedName { get; set; }
        public required IEnumerable<Node> Children { get; set; }
    }

    /// <summary>
    /// Represents a node.
    /// </summary>
    public record Node
    {
        public required string Kind { get; set; }
        public required string DeclKind { get; set; }
        public required string Name { get; set; }
        public required string MangledName { get; set; }
        public required string PrintedName { get; set; }
        public required string ModuleName { get; set; }
        public required string [] DeclAttributes { get; set; }
        public required bool? @static { get; set; }
        public required bool? IsInternal { get; set; }
        public required string? GenericSig { get; set; }
        public required IEnumerable<Node> Children { get; set; }
    }

    /// <summary>
    /// Represents a parser for Swift ABI.
    /// </summary>
    public sealed unsafe class SwiftABIParser : ISwiftParser
    {
        /// <summary>
        /// The set of operators.
        /// </summary>
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

        /// <summary>
        /// The ABI file path.
        /// </summary>
        private readonly string _filePath;

        /// <summary>
        /// The dylib path.
        /// </summary>
        private readonly string _dylibPath;

        /// <summary>
        /// The type database.
        /// </summary>
        private readonly TypeDatabase _typeDatabase;

        /// <summary>
        /// The verbosity level.
        /// </summary>
        private readonly int _verbose;

        /// <summary>
        /// The list of filters for the parser.
        /// Currently, only name filtering is supported.
        /// </summary>
        private List<string> _filters;

        /// <summary>
        /// The module root node.
        /// </summary>
        private readonly ABIRootNode _moduleRoot;

        public SwiftABIParser(string filePath, string dylibPath, TypeDatabase typeDatabase, int verbose = 0)
        {
            _filePath = filePath;
            _dylibPath = dylibPath;
            _typeDatabase = typeDatabase;
            _verbose = verbose;
            _filters = new List<string>();

            string jsonContent = File.ReadAllText(_filePath);
            _moduleRoot = JsonConvert.DeserializeObject<ABIRootNode>(jsonContent) ?? throw new InvalidOperationException("Invalid ABI structure.");
        }

        /// <summary>
        /// Sets the filter for the parser.
        /// </summary>
        public void SetFilter(List<string> filter)
        {
            _filters = filter;
        }

        /// <summary>
        /// Gets the module name.
        /// </summary>
        /// <returns>The module name.</returns>
        public string GetModuleName()
        {
            return _moduleRoot.ABIRoot.Children.FirstOrDefault()?.ModuleName ?? string.Empty;
        }

        /// <summary>
        /// Gets the module declaration from the ABI file.
        /// </summary>
        /// <returns>The module declaration.</returns>
        public ModuleDecl GetModuleDecl()
        {
            var decls = CollectDeclarations(_moduleRoot.ABIRoot.Children, _filters.Count == 0);

            var dependencies = new List<string>();
            var moduleName = GetModuleName();
            dependencies.AddRange(_typeDatabase.Registrar.GetDependencies(moduleName));
            dependencies.Remove(moduleName);

            return new ModuleDecl
            {
                Name = ExtractUniqueName(moduleName),
                Fields = decls.OfType<FieldDecl>().ToList(),
                Methods = decls.OfType<MethodDecl>().ToList(),
                Declarations = decls.Where(d => !(d is MethodDecl) && !(d is FieldDecl)).ToList(),
                Dependencies = dependencies
            };
        }

        /// <summary>
        /// Collects declarations from a list of nodes.
        /// </summary>
        /// <param name="node">The node representing a declaration.</param>
        /// <param name="collect">A flag indicating whether to collect declarations.</param>
        /// <returns>A list of declarations.</returns>
        private List<BaseDecl> CollectDeclarations(IEnumerable<Node> nodes, bool collect)
        {
            var declarations = new List<BaseDecl>();
            foreach (var node in nodes)
            {
                // Skip generic types
                if (node.GenericSig != null)
                {
                    if (_verbose > 1)
                        Console.WriteLine($"Generic type '{node.Name}' encountered. Skipping.");
                    continue;
                }

                if (!collect && _filters.Contains(node.Name))
                    collect = true;

                var nodeDeclaration = HandleNode(node, collect);
                if (nodeDeclaration != null)
                    declarations.Add(nodeDeclaration);

                // Reset collect to false if it was set true for a specific node only
                // TODO: Implement recursive filtering
                if (collect && _filters.Contains(node.Name))
                    collect = false;
            }
            return declarations;
        }

        /// <summary>
        /// Handles an ABI node and returns the corresponding declaration.
        /// </summary>
        /// <param name="node">The node representing a declaration.</param>
        /// <param name="collect">A flag indicating whether to collect declarations.</param>
        /// <returns>The declaration.</returns>
        private BaseDecl? HandleNode(Node node, bool collect)
        {
            if (!collect && !_filters.Contains(node.Name))
                return null;

            BaseDecl? result = null;
            try
            {
                switch (node.Kind)
                {
                    case "TypeDecl":
                        result = HandleTypeDecl(node, collect);
                        break;
                    case "Function":
                    case "Constructor":
                        // TODO: Implement operator overloading
                        result = IsOperator(node.Name) ? null : CreateMethodDecl(node);
                        break;
                    case "Var":
                        result = CreateFieldDecl(node);
                        break;
                    case "Import":
                        break;
                    default:
                        if (_verbose > 1)
                            Console.WriteLine($"Unsupported declaration '{node.DeclKind} {node.Name}' encountered.");
                        break;
                }
            }
            catch (NotImplementedException e)
            {
                if (_verbose > 1)
                    Console.WriteLine($"Not implemented '{node.Name}': {e.Message}"); 
            }
            catch (Exception e) 
            {
                if (_verbose > 0)
                    Console.WriteLine($"Error while processing node '{node.Name}': {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Handles a type declaration node and returns the corresponding declaration.
        /// </summary>
        /// <param name="node">The node representing a type declaration.</param>
        /// <param name="collect">A flag indicating whether to collect declarations.</param>
        /// <returns>The type declaration.</returns>
        private TypeDecl? HandleTypeDecl(Node node, bool collect)
        {
            if (_typeDatabase.IsTypeProcessed(node.ModuleName, node.Name))
            {
                if (_verbose > 1)
                    Console.WriteLine($"Type '{node.Name}' already processed. Skipping.");
                return null;
            }

            TypeDecl? decl = null;
            TypeRecord typeRecord = _typeDatabase.Registrar.RegisterType(node.ModuleName, node.Name);
            IntPtr metadataPtr;

            switch (node.DeclKind)
            {
                case "Struct":
                case "Enum":
                    metadataPtr = DynamicLibraryLoader.invoke(_dylibPath, GetMetadataAccessor(node));
                    var swiftTypeInfo = new SwiftTypeInfo { MetadataPtr = metadataPtr };

                    if (node.DeclAttributes != null && Array.IndexOf(node.DeclAttributes, "Frozen") != -1 && 
                        (!swiftTypeInfo.ValueWitnessTable->IsNonPOD || !swiftTypeInfo.ValueWitnessTable->IsNonBitwiseTakable))
                    {
                        decl = CreateStructDecl(node);
                    }
                    else
                    {
                        decl = CreateClassDecl(node);
                    }

                    typeRecord.SwiftTypeInfo = swiftTypeInfo;
                    break;

                case "Class":
                    decl = CreateClassDecl(node);
                    break;

                default:
                    if (_verbose > 1)
                        Console.WriteLine($"Unsupported declaration type '{node.DeclKind} {node.Name}' encountered.");
                    return null;
            }

            typeRecord.IsProcessed = true;

            if (node.Children != null && decl != null)
            {
                var childDecls = CollectDeclarations(node.Children, collect);
                decl.Fields.AddRange(childDecls.OfType<FieldDecl>());
                decl.Declarations.AddRange(childDecls.Where(d => !(d is FieldDecl)));
            }

            return decl;
        }

        /// <summary>
        /// Creates a struct declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the struct declaration.</param>
        /// <returns>The struct declaration.</returns>
        private StructDecl CreateStructDecl(Node node)
        {
            return new StructDecl
            {
                Name = ExtractUniqueName(node.Name),
                MangledName = node.MangledName,
                Fields = new List<FieldDecl>(),
                Declarations = new List<BaseDecl>()
            };
        }

        /// <summary>
        /// Creates a class declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the class declaration.</param>
        /// <returns>The class declaration.</returns>
        private ClassDecl CreateClassDecl(Node node)
        {
            return new ClassDecl
            {
                Name = ExtractUniqueName(node.Name),
                MangledName = node.MangledName,
                Fields = new List<FieldDecl>(),
                Declarations = new List<BaseDecl>()
            };
        }

        /// <summary>
        /// Creates a method declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the method declaration.</param>
        /// <returns>The method declaration.</returns>
        private MethodDecl CreateMethodDecl(Node node)
        {
            // Extract parameter names from the signature
            var paramNames = ExtractParameterNames(node.PrintedName);

            var methodDecl = new MethodDecl
            {
                Name = ExtractUniqueName(node.Name),
                // Constructors for structs are named with a trailing 'C' instead of 'c'
                // because a constructor wrapper is missing in the library.
                MangledName = node.Kind == "Constructor" ? PatchMangledName(node.MangledName) : node.MangledName,
                MethodType = node.@static ?? false ? MethodType.Static : MethodType.Instance,
                IsConstructor = node.Kind == "Constructor",
                Signature = new List<FieldDecl>(),
            };

            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count(); i++)
                {
                    methodDecl.Signature.Add(new FieldDecl
                    {
                        TypeIdentifier = CreateTypeDecl(node.Children.ElementAt(i)),
                        Name = paramNames[i],
                        Visibility = Visibility.Public
                    });
                }
            }

            return methodDecl;
        }

        /// <summary>
        /// Creates a field declaration from a given node.
        /// </summary>
        /// <param name="node">The node representing the field declaration.</param>
        /// <returns>The field declaration.</returns>
        private FieldDecl CreateFieldDecl(Node node)
        {
            return new FieldDecl
            {
                TypeIdentifier = CreateTypeDecl(node.Children.ElementAt(0)),
                Name = node.Name,
                Visibility = node.IsInternal ?? false ? Visibility.Private : Visibility.Public
            };
        }

        /// <summary>
        /// Creates a type declaration from a given node.
        /// </summary>
        /// <param name="node">The node representing the type declaration.</param>
        /// <returns>The type declaration.</returns>
        private TypeDecl CreateTypeDecl(Node node)
        {
            // Handle not supported types with a switch statement
            switch (node.Kind)
            {
                case "TypeNominal":
                    switch (node.Name)
                    {
                        case "Optional":
                        case "Dictionary":
                        case "Tuple":
                        case "Array":
                            throw new NotImplementedException($"{node.Name} types are not supported yet.");
                        default:
                            if (node.PrintedName.StartsWith("any ", StringComparison.InvariantCultureIgnoreCase))
                            {
                                throw new NotImplementedException("Protocols are not supported yet.");
                            }
                            break;
                    }
                    break;
                case "TypeFunc":
                    throw new NotImplementedException("Function types are not supported yet.");
            }

            var typeDecl = new TypeDecl
            {
                Name = string.Empty,
                MangledName = node.MangledName,
                Fields = new List<FieldDecl>(),
                Declarations = new List<BaseDecl>(),
            };

            TypeRecord typeRecord;
            string moduleName = node.PrintedName.IndexOf('.') > -1 ? node.PrintedName.Substring(0, node.PrintedName.IndexOf('.')) : string.Empty;
            // If the node has children, it is a generic type
            if (node.Children != null && node.Children.Any())
            {
                typeRecord = _typeDatabase.GetTypeMapping(moduleName, $"{node.Name}`{node.Children.Count()}");
                typeDecl.Name = typeRecord.TypeIdentifier.Replace($"`{node.Children.Count()}", "") + "<";

                for (int i = 0; i < node.Children.Count(); i++)
                {
                    var child = CreateTypeDecl(node.Children.ElementAt(i));
                    typeDecl.Declarations.Add(child);
                    if (i > 0)
                        typeDecl.Name += ", ";
                    typeDecl.Name += child.Name;
                }

                typeDecl.Name += ">";
            }
            // If the node has no children, it is a non-generic type
            else
            {
                var typeIdentifier = node.PrintedName.IndexOf('.') > -1 ? node.PrintedName.Substring(node.PrintedName.IndexOf('.') + 1) : node.PrintedName;
                typeRecord = _typeDatabase.GetTypeMapping(moduleName, typeIdentifier);
                typeDecl.Name = typeRecord.TypeIdentifier;
            }
            _typeDatabase.Registrar.UpdateDependencies(GetModuleName(), typeRecord.Namespace);

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
        /// <param name="name">The name to check.</param>
        /// <returns>The processed name.</returns>
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
        /// <param name="name">The name to check.</param>
        /// <returns>True if the name is an operator, false otherwise.</returns>
        private static bool IsOperator(string name)
        {
            return _operators.Contains(name);
        }

        /// <summary>
        /// Patches the mangled name of a constructor.
        /// </summary>
        /// <param name="mangledName">The mangled name to patch.</param>
        /// <returns>The patched mangled name.</returns>
        private string PatchMangledName(string mangledName)
        {
            if (mangledName.Last() == 'c')
            {
                return mangledName.Substring(0, mangledName.Length - 1) + "C";
            }
            return mangledName;
        }

        /// <summary>
        /// Gets the metadata accessor for a given node.
        /// </summary>
        /// <param name="node">The node to get the metadata accessor for.</param>
        /// <returns>The metadata accessor.</returns>
        private string GetMetadataAccessor(Node node)
        {
            if (node.GenericSig == null)
                return $"{node.MangledName}Ma";
            else
                return node.MangledName;
        }
    }
}
