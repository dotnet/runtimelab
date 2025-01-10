// Copyright (c) Microsoft Corporation.  
// Licensed under the MIT License. 

using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Performs post-processing of types collected from the Swift ABI before generating bindings.
    /// Calculates properties on types which are not directly available from the ABI.
    /// Generates type database entries for structs, enums, and classes.
    /// </summary>
    internal class ModuleProcessor
    {
        private readonly string _module;
        private readonly string _dylibPath;
        private readonly TypeDatabase _typeDatabase;
        private readonly Dictionary<NamedTypeSpec, TypeDecl> _typeDecls;
        private readonly int _verbosity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleProcessor"/> class.
        /// </summary>
        /// <param name="module">The name of the Swift module being processed.</param>
        /// <param name="dylibPath">File path to the Swift dynamic library.</param>
        /// <param name="typeDatabase">The global type database tracking processed types.</param>
        /// <param name="typeDecls">Dictionary of Swift type specs to their declarations.</param>
        /// <param name="verbosity">Verbosity level for logging.</param>
        public ModuleProcessor(string module, string dylibPath,
            Dictionary<NamedTypeSpec, TypeDecl> typeDecls, TypeDatabase typeDatabase, int verbosity)
        {
            _module = module;
            _dylibPath = dylibPath;
            _typeDatabase = typeDatabase;
            _typeDecls = typeDecls;
            _verbosity = verbosity;
        }

        /// <summary>
        /// Executes the post-processing workflow for all unprocessed types in this module.
        /// Produces type database entries for structs, enums, and classes.
        /// </summary>
        public void FinalizeTypeProcessing()
        {
            foreach (var (typeSpec, typeDecl) in _typeDecls)
            {
                if (!_typeDatabase.IsTypeProcessed(typeSpec.Module, typeSpec.NameWithoutModule))
                {
                    ProcessTypeRecursively(typeSpec, typeDecl);
                }
            }
        }

        /// <summary>
        /// Recursively processes a type. If the type is a struct, enum, or class,
        /// calls into specialized handlers.
        /// </summary>
        /// <param name="namedTypeSpec">The Swift type specification.</param>
        /// <param name="typeDecl">The associated type declaration.</param>
        private void ProcessTypeRecursively(NamedTypeSpec namedTypeSpec, TypeDecl typeDecl)
        {
            if (_typeDatabase.IsTypeProcessed(namedTypeSpec.Module, namedTypeSpec.NameWithoutModule))
                return;

            switch (typeDecl)
            {
                case StructDecl structDecl:
                    ProcessStruct(namedTypeSpec, structDecl);
                    break;

                case EnumDecl enumDecl:
                    ProcessEnum(namedTypeSpec, enumDecl);
                    break;

                case ClassDecl classDecl:
                    ProcessClass(namedTypeSpec, classDecl);
                    break;

                default:
                    if (_verbosity > 1)
                        Console.WriteLine($"Skipping unknown type declaration '{typeDecl.GetType().Name}'.");
                    return;
            }
        }

        /// <summary>
        /// Processes a Swift struct declaration to determine final properties such as blittability
        /// and frozenness, then registers it into the type database.
        /// </summary>
        /// <param name="namedTypeSpec">Swift type specification for the struct.</param>
        /// <param name="structDecl">The struct declaration.</param>
        private void ProcessStruct(NamedTypeSpec namedTypeSpec, StructDecl structDecl)
        {
            // Ensure all fields are processed or known in the database.
            ProcessStructFields(structDecl);

            // TODO: Remove loading dylib
            IntPtr metadataPtr = DynamicLibraryLoader.invoke(_dylibPath, $"{structDecl.MangledName}Ma");
            SwiftTypeInfo swiftTypeInfo = new SwiftTypeInfo { MetadataPtr = metadataPtr };


            bool isFrozen = EvaluateFrozenness(structDecl);
            bool isBlittable = EvaluateBlittability(swiftTypeInfo);
            RegisterStructType(namedTypeSpec, structDecl, swiftTypeInfo, isFrozen, isBlittable);

            // Update the struct declaration in memory so future passes see these fields.
            structDecl.IsBlittable = isBlittable;
            structDecl.IsFrozen = isFrozen;
        }

        /// <summary>
        /// Ensures that each field in the struct is either from an already processed type
        /// or is recursively processed in this module.
        /// </summary>
        /// <param name="structDecl">Struct being validated.</param>
        /// <exception cref="Exception">Throws if a field type cannot be found or processed.</exception>
        private void ProcessStructFields(StructDecl structDecl)
        {
            foreach (var fieldDecl in structDecl.Fields)
            {
                if (fieldDecl.SwiftTypeSpec is not NamedTypeSpec namedFieldType)
                    continue;

                if (fieldDecl.IsStatic)
                    continue;

                // If the field is from a different module, ensure that type is already processed.
                if (namedFieldType.Module != _module)
                {
                    if (!_typeDatabase.IsTypeProcessed(namedFieldType.Module, namedFieldType.NameWithoutModule))
                    {
                        if (_verbosity > 1)
                            Console.WriteLine($"Skipping field '{fieldDecl.Name}' of type '{namedFieldType.NameWithoutModule}' from module '{namedFieldType.Module}'. Type should have been processed in a previous module, but was not found.");
                        continue;
                    }
                }
                // If the field is in the same module, process it recursively.
                else
                {
                    if (!_typeDecls.TryGetValue(namedFieldType, out var nestedDecl))
                    {
                        if (_verbosity > 1)
                            Console.WriteLine($"Skipping field '{fieldDecl.Name}' of type '{namedFieldType.NameWithoutModule}' from module '{namedFieldType.Module}'. Not found in type declarations.");
                        continue;
                    }
                    ProcessTypeRecursively(namedFieldType, nestedDecl);
                }
            }
        }

        /// <summary>
        /// Determines whether a struct is truly frozen. The struct itself must be marked frozen
        /// and all of its fields must also be from frozen types.
        /// </summary>
        /// <param name="structDecl">The struct declaration.</param>
        /// <returns>True if the struct is frozen, false otherwise.</returns>
        private bool EvaluateFrozenness(StructDecl structDecl)
        {
            if (!structDecl.IsFrozen)
                return false;

            // If any field is not frozen, the struct cannot be frozen.
            foreach (var fieldDecl in structDecl.Fields)
            {
                if (fieldDecl.SwiftTypeSpec is not NamedTypeSpec namedFieldType)
                    continue;

                var fieldDatabaseRecord = _typeDatabase.Registrar.GetType(namedFieldType.Module, namedFieldType.NameWithoutModule)
                    ?? throw new Exception($"Type not found in the database: {namedFieldType.NameWithoutModule}");

                if (!fieldDatabaseRecord.IsFrozen)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Determines whether the struct can be treated as blittable under .NET's rules.
        /// For now, uses Swift's ValueWitnessTable flags to detect non-POD or non-bitwise-takable.
        /// </summary>
        /// <param name="swiftTypeInfo">Struct's Swift type info containing the ValueWitnessTable pointer.</param>
        /// <returns>True if blittable, false otherwise.</returns>
        private unsafe bool EvaluateBlittability(SwiftTypeInfo swiftTypeInfo)
        {
            // TODO: Replace with a manual approach.
            bool isNonPod = swiftTypeInfo.ValueWitnessTable->IsNonPOD;
            bool isNonBitwiseTakable = swiftTypeInfo.ValueWitnessTable->IsNonBitwiseTakable;
            return (!isNonPod && !isNonBitwiseTakable);
        }

        /// <summary>
        /// Inserts a struct's details (e.g. name, metadata accessor, frozenness, blittability) into the type database.
        /// </summary>
        /// <param name="namedTypeSpec">Spec for the struct's name, module, etc.</param>
        /// <param name="structDecl">The struct declaration node.</param>
        /// <param name="swiftTypeInfo">Pointer plus ValueWitnessTable for the struct.</param>
        /// <param name="isFrozen">Whether the struct is effectively frozen.</param>
        /// <param name="isBlittable">Whether the struct is effectively blittable.</param>
        private void RegisterStructType(NamedTypeSpec namedTypeSpec, StructDecl structDecl,
            SwiftTypeInfo swiftTypeInfo, bool isFrozen, bool isBlittable)
        {
            _typeDatabase.Registrar.RegisterType(namedTypeSpec.Module,
                                                 namedTypeSpec.NameWithoutModule,
                                                 new TypeRecord
                                                 {
                                                     Namespace = namedTypeSpec.Module,
                                                     TypeIdentifier = namedTypeSpec.NameWithoutModule,
                                                     SwiftTypeInfo = swiftTypeInfo,
                                                     MetadataAccessor = $"{structDecl.MangledName}Ma",
                                                     IsProcessed = true,
                                                     IsBlittable = isBlittable,
                                                     IsFrozen = isFrozen
                                                 });
        }

        /// <summary>
        /// Processes an enum declaration. Currently unimplemented.
        /// </summary>
        /// <param name="namedTypeSpec">Spec for the enum's name, module, etc.</param>
        /// <param name="enumDecl">The enum declaration node.</param>
        private void ProcessEnum(NamedTypeSpec namedTypeSpec, EnumDecl enumDecl)
        {
            return;
        }

        /// <summary>
        /// Processes a class declaration. Currently unimplemented.
        /// </summary>
        /// <param name="namedTypeSpec">Spec for the class's name, module, etc.</param>
        /// <param name="classDecl">The class declaration node.</param>
        private void ProcessClass(NamedTypeSpec namedTypeSpec, ClassDecl classDecl)
        {
            return;
        }
    }
}
