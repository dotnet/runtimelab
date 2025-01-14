// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents the result of parsing a module.
    /// </summary>
    /// <param name="ModuleDecl">The module declaration.</param>
    /// <param name="TypeDecls">The type declarations.</param>
    /// <param name="BoundGenericTypes">The bound generic types.</param>
    public sealed record ModuleParsingResult(
    ModuleDecl ModuleDecl,
    Dictionary<NamedTypeSpec, TypeDecl> TypeDecls,
    List<NamedTypeSpec> BoundGenericTypes);

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
        public required IEnumerable<Node> Children { get; set; } = Enumerable.Empty<Node>();
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
        public required string[] DeclAttributes { get; set; }
        public required bool? @static { get; set; }
        public required bool? IsInternal { get; set; }
        public required string? GenericSig { get; set; }
        public required string? sugared_genericSig { get; set; }
        public required IEnumerable<Node> Children { get; set; } = Enumerable.Empty<Node>();
    }

    /// <summary>
    /// Represents a parser for Swift ABI.
    /// </summary>
    public sealed unsafe class SwiftABIParser : ISwiftParser
    {
        const string kNominal = "TypeNominal";
        const string kFunc = "TypeFunc";
        const string kTuple = "Tuple";

        /// <summary>
        /// The set of operators.
        /// </summary>
        private static readonly HashSet<string> _operators = new()
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
        /// The type database.
        /// </summary>
        private readonly ITypeDatabase _typeDatabase;

        /// <summary>
        /// The verbosity level.
        /// </summary>
        private readonly int _verbose;

        /// <summary>
        /// The module root node.
        /// </summary>
        private readonly ABIRootNode _moduleRoot;

        /// <summary>
        /// Types declared in the module.
        /// </summary>
        private readonly Dictionary<NamedTypeSpec, TypeDecl> _moduleTypes = new();

        /// <summary>
        /// Closed generic types encountered during parsing method signatures.
        /// </summary>
        private readonly List<NamedTypeSpec> _boundGenericTypes = new();

        public SwiftABIParser(string filePath, ITypeDatabase typeDatabase, int verbose = 0)
        {
            _filePath = filePath;
            _typeDatabase = typeDatabase;
            _verbose = verbose;

            string jsonContent = File.ReadAllText(_filePath);
            _moduleRoot = JsonConvert.DeserializeObject<ABIRootNode>(jsonContent) ?? throw new InvalidOperationException("Invalid ABI structure.");
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
        /// Processes the module ABI. Processes all declarations and builds the ModuleDecl.
        /// </summary>
        /// <returns>The module ABI processing result.</returns>
        public ModuleParsingResult ParseModule()
        {
            var dependencies = new List<string>();
            var moduleName = GetModuleName();
            var moduleDecl = new ModuleDecl
            {
                Name = ExtractUniqueName(moduleName),
                FullyQualifiedName = ExtractUniqueName(moduleName),
                Fields = new List<FieldDecl>(),
                Methods = new List<MethodDecl>(),
                Types = new List<TypeDecl>(),
                Dependencies = dependencies,
                ParentDecl = null,
                ModuleDecl = null
            };

            var decls = CollectDeclarations(_moduleRoot.ABIRoot.Children, moduleDecl, moduleDecl);

            dependencies.Remove(moduleName);

            moduleDecl.Fields = decls.OfType<FieldDecl>().ToList();
            moduleDecl.Methods = decls.OfType<MethodDecl>().ToList();
            moduleDecl.Types = decls.OfType<TypeDecl>().ToList();
            moduleDecl.Dependencies = dependencies;

            foreach (var type in moduleDecl.Types)
            {
                _moduleTypes.Add(new NamedTypeSpec(type.FullyQualifiedName), type);
            }

            return new ModuleParsingResult(moduleDecl, _moduleTypes, _boundGenericTypes);
        }

        /// <summary>
        /// Collects declarations from a list of nodes.
        /// </summary>
        /// <param name="nodes">The list of nodes to collect declarations from.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The list of collected declarations.</returns>
        private List<BaseDecl> CollectDeclarations(IEnumerable<Node> nodes, BaseDecl parentDecl, BaseDecl moduleDecl)
        {
            var declarations = new List<BaseDecl>();
            foreach (var node in nodes)
            {
                var nodeDeclaration = HandleNode(node, parentDecl, moduleDecl);
                if (nodeDeclaration is not null)
                    declarations.Add(nodeDeclaration);
            }
            return declarations;
        }

        /// <summary>
        /// Handles an ABI node and returns the corresponding declaration.
        /// </summary>
        /// <param name="node">The node representing a declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The declaration.</returns>
        private BaseDecl? HandleNode(Node node, BaseDecl parentDecl, BaseDecl moduleDecl)
        {
            BaseDecl? result = null;
            try
            {
                switch (node.Kind)
                {
                    case "TypeDecl":
                        result = HandleTypeDecl(node, parentDecl, moduleDecl);
                        break;
                    case "Function":
                    case "Constructor":
                        // TODO: Implement operator overloading
                        result = IsOperator(node.Name) ? null : CreateMethodDecl(node, parentDecl, moduleDecl);
                        break;
                    case "Var":
                        // TODO: Implement computed properties
                        if (node.DeclAttributes is not null && Array.IndexOf(node.DeclAttributes, "HasStorage") != -1)
                            result = CreateFieldDecl(node, parentDecl, moduleDecl);
                        break;
                    case "Import":
                        break;
                    default:
                        throw new NotImplementedException($"Unsupported node kind '{node.Kind}' encountered.");
                }
            }
            catch (NotImplementedException e)
            {
                if (_verbose > 1)
                    Console.WriteLine($"Not implemented '{node.Name}' ({node.MangledName}): {e.Message}");
            }
            catch (Exception e)
            {
                if (_verbose > 0)
                    Console.WriteLine($"Error while processing node '{node.Name} ({node.MangledName})': {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Handles a type declaration node and returns the corresponding declaration.
        /// </summary>
        /// <param name="node">The node representing a type declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The type declaration.</returns>
        private TypeDecl? HandleTypeDecl(Node node, BaseDecl parentDecl, BaseDecl moduleDecl)
        {
            if (_typeDatabase.IsTypeProcessed(node.ModuleName, ExtractFullyQualifiedName(parentDecl.FullyQualifiedName, node.Name)))
            {
                if (_verbose > 1)
                    Console.WriteLine($"Type '{node.Name}' already processed. Skipping.");
                return null;
            }

            if (string.IsNullOrEmpty(node.MangledName))
            {
                if (_verbose > 1)
                    Console.WriteLine($"Type '{node.Name}' has no mangled name. Skipping.");
                return null;
            }

            TypeDecl? decl;

            if (node.GenericSig is not null)
            {
                if (_verbose > 1)
                    Console.WriteLine($"Generic type '{node.Name}' not supported. Skipping.");
                return null;
            }

            switch (node.DeclKind)
            {
                case "Struct":
                case "Enum":
                    var hasFrozenAttribute = node.DeclAttributes is not null && Array.IndexOf(node.DeclAttributes, "Frozen") != -1;
                    decl = CreateStructDecl(node, parentDecl, moduleDecl, hasFrozenAttribute);
                    break;

                case "Class":
                    decl = CreateClassDecl(node, parentDecl, moduleDecl);
                    break;

                default:
                    if (_verbose > 1)
                        Console.WriteLine($"Unsupported declaration type '{node.DeclKind} {node.Name}' encountered.");
                    return null;
            }

            if (decl is not null)
            {
                var childDecls = CollectDeclarations(node.Children, decl, moduleDecl);
                decl.Fields.AddRange(childDecls.OfType<FieldDecl>());
                decl.Methods.AddRange(childDecls.OfType<MethodDecl>());
                decl.Types.AddRange(childDecls.OfType<TypeDecl>());

                foreach (var type in decl.Types)
                {
                    _moduleTypes.Add(new NamedTypeSpec(type.FullyQualifiedName), type);
                }
            }

            return decl;
        }

        /// <summary>
        /// Creates a struct declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the struct declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The struct declaration.</returns>
        private StructDecl CreateStructDecl(Node node, BaseDecl parentDecl, BaseDecl moduleDecl, bool hasFrozenAttribute)
        {
            return new StructDecl
            {
                Name = ExtractUniqueName(node.Name),
                FullyQualifiedName = ExtractFullyQualifiedName(parentDecl.FullyQualifiedName, node.Name),
                MangledName = node.MangledName,
                Fields = new List<FieldDecl>(),
                Methods = new List<MethodDecl>(),
                Types = new List<TypeDecl>(),
                ParentDecl = parentDecl,
                ModuleDecl = moduleDecl,
                IsFrozen = hasFrozenAttribute,
                IsBlittable = false,
            };
        }

        /// <summary>
        /// Creates a class declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the class declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The class declaration.</returns>
        private ClassDecl CreateClassDecl(Node node, BaseDecl parentDecl, BaseDecl moduleDecl)
        {
            return new ClassDecl
            {
                Name = ExtractUniqueName(node.Name),
                FullyQualifiedName = ExtractFullyQualifiedName(parentDecl.FullyQualifiedName, node.Name),
                MangledName = node.MangledName,
                Fields = new List<FieldDecl>(),
                Methods = new List<MethodDecl>(),
                Types = new List<TypeDecl>(),
                ParentDecl = parentDecl,
                ModuleDecl = moduleDecl
            };
        }

        /// <summary>
        /// Creates a method declaration from a node.
        /// </summary>
        /// <param name="node">The node representing the method declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The method declaration.</returns>
        private MethodDecl CreateMethodDecl(Node node, BaseDecl parentDecl, BaseDecl moduleDecl)
        {
            // Extract parameter names from the signature
            var paramNames = ExtractParameterNames(node.PrintedName);

            var methodDecl = new MethodDecl
            {
                Name = ExtractUniqueName(node.Name),
                FullyQualifiedName = parentDecl.FullyQualifiedName,
                // Constructors for structs are named with a trailing 'C' instead of 'c'
                // because a constructor wrapper is missing in the library.
                MangledName = node.Kind == "Constructor" ? PatchMangledName(node.MangledName) : node.MangledName,
                MethodType = node.@static ?? false ? MethodType.Static : MethodType.Instance,
                IsConstructor = node.Kind == "Constructor",
                CSSignature = new List<ArgumentDecl>(),
                ParentDecl = parentDecl,
                ModuleDecl = moduleDecl
            };

            for (int i = 0; i < node.Children.Count(); i++)
            {
                var typeSpec = CreateTypeSpec(node.Children.ElementAt(i));

                //TODO: Make sure that the generics are actually bound
                if (!_typeDatabase.IsTypeProcessed(typeSpec) && typeSpec is NamedTypeSpec namedTypeSpec && namedTypeSpec.GenericParameters.Count > 0)
                {
                    _boundGenericTypes.Add(namedTypeSpec);
                }

                methodDecl.CSSignature.Add(new ArgumentDecl
                {
                    SwiftTypeSpec = typeSpec,
                    Name = paramNames[i],
                    FullyQualifiedName = string.Empty,
                    PrivateName = string.Empty,
                    IsInOut = false,
                    ParentDecl = methodDecl,
                    ModuleDecl = moduleDecl
                });
            }

            return methodDecl;
        }

        /// <summary>
        /// Creates a field declaration from a given node.
        /// </summary>
        /// <param name="node">The node representing the field declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The field declaration.</returns>
        private FieldDecl CreateFieldDecl(Node node, BaseDecl parentDecl, BaseDecl moduleDecl)
        {
            var typeSpec = CreateTypeSpec(node.Children.ElementAt(0));
            var fieldDecl = new FieldDecl
            {
                SwiftTypeSpec = typeSpec,
                Name = node.Name,
                FullyQualifiedName = ExtractFullyQualifiedName(parentDecl.FullyQualifiedName, node.Name),
                Visibility = node.IsInternal ?? false ? Visibility.Private : Visibility.Public,
                ParentDecl = parentDecl,
                ModuleDecl = moduleDecl,
                IsStatic = node.@static ?? false
            };

            return fieldDecl;
        }

        /// <summary>
        /// Creates a type spec from a given node parsing the printed name
        /// </summary>
        TypeSpec CreateTypeSpec(Node node)
        {
            switch (node.Kind)
            {
                case kNominal:
                case kFunc:
                    var spec = TypeSpecParser.Parse(node.PrintedName);
                    if (spec is null)
                    {
                        throw new Exception($"Error parsing type from \"{node.PrintedName}\"");
                    }
                    return spec;
                default:
                    throw new NotImplementedException($"Can't handle node type {node.Kind} yet.");
            }
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

        private static string ExtractFullyQualifiedName(string parentName, string name)
        {
            return string.IsNullOrEmpty(parentName) ? ExtractUniqueName(name) : $"{parentName}.{ExtractUniqueName(name)}";
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
    }
}
