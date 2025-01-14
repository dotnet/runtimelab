// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents the result of processing a Swift module.
    /// </summary>
    /// <param name="ModuleDatabase">The module database containing type records.</param>
    /// <param name="OutOfModuleTypeRecords"> Type records which look as if they belong to another module, e.g. closed generics.</param>
    sealed record ModuleProcessingResult(ModuleTypeDatabase ModuleDatabase, IEnumerable<(string, TypeRecord)> OutOfModuleTypeRecords);

    /// <summary>
    /// Performs post-processing of types collected from the Swift ABI before generating bindings.
    /// Calculates properties on types which are not directly available from the ABI.
    /// Generates type database entries for structs, enums, and classes.
    /// </summary>
    internal class ModuleProcessor
    {
        private readonly string _module;
        private readonly string _dylibPath;
        private readonly ITypeDatabase _typeDatabase;
        private readonly ModuleTypeDatabase _moduleDatabase;
        private readonly Dictionary<NamedTypeSpec, TypeDecl> _typeDecls;
        private readonly List<NamedTypeSpec> _boundGenericTypes; // TODO: Temporary solution for closed generics. Revise.
        private readonly Dictionary<string, TypeRecord> _outOfModuleTypeRecords; // E.g. closed generics from other modules.
        private readonly int _verbosity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleProcessor"/> class.
        /// </summary>
        /// <param name="module">The name of the Swift module being processed.</param>
        /// <param name="dylibPath">The file path to the Swift dynamic library.</param>
        /// <param name="typeDecls">A dictionary mapping Swift type specs to their declarations.</param>
        /// <param name="boundGenericTypes">A list of closed generic types encountered during method signature parsing.</param>
        /// <param name="typeDatabase">The global type database tracking processed types.</param>
        /// <param name="verbosity">The verbosity level for logging.</param>
        public ModuleProcessor(
            string module,
            string dylibPath,
            Dictionary<NamedTypeSpec, TypeDecl> typeDecls,
            List<NamedTypeSpec> boundGenericTypes,
            ITypeDatabase typeDatabase,
            int verbosity)
        {
            _module = module;
            _dylibPath = dylibPath;
            _typeDatabase = typeDatabase;
            _moduleDatabase = new ModuleTypeDatabase(module, dylibPath);
            _boundGenericTypes = boundGenericTypes;
            _outOfModuleTypeRecords = new Dictionary<string, TypeRecord>();
            _typeDecls = typeDecls;
            _verbosity = verbosity;
        }

        /// <summary>
        /// Tries to retrieve a <see cref="TypeRecord"/> for the specified Swift type.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <param name="typeIdentifier">The Swift type identifier.</param>
        /// <param name="record">
        /// When this method returns, contains the <see cref="TypeRecord"/> if found; otherwise, <c>null</c>.
        /// </param>
        /// <returns><c>true</c> if the type was found; otherwise, <c>false</c>.</returns>
        private bool TryGetTypeRecord(string moduleName, string typeIdentifier, [NotNullWhen(true)] out TypeRecord? record)
        {
            // First, check if this module is the one being processed.
            if (moduleName == _module)
            {
                return _moduleDatabase.TryGetTypeRecord(typeIdentifier, out record);
            }

            // Next, check if the type is an out-of-module type (e.g., a closed generic).
            if (_outOfModuleTypeRecords.TryGetValue($"{moduleName}.{typeIdentifier}", out record))
            {
                return true;
            }

            // Otherwise, fall back to checking the global type database.
            return _typeDatabase.TryGetTypeRecord(moduleName, typeIdentifier, out record);
        }

        /// <summary>
        /// Executes the post-processing workflow for all unprocessed types in the current module.
        /// Produces type database entries for structs, enums, and classes.
        /// </summary>
        /// <returns>A <see cref="ModuleProcessingResult"/>The module database and out-of-module type records.</returns>
        public ModuleProcessingResult FinalizeTypeProcessingAndCreateModuleDatabase()
        {
            foreach (var (typeSpec, typeDecl) in _typeDecls)
            {
                ProcessTypeRecursively(typeSpec, typeDecl);
            }

            // Placeholder for handling closed generics.
            foreach (var closedGenericType in _boundGenericTypes)
            {
                ProcessGenericTypeSpec(closedGenericType);
            }

            return new ModuleProcessingResult(_moduleDatabase, _outOfModuleTypeRecords.Select(kv => (kv.Key, kv.Value)));
        }

        /// <summary>
        /// Recursively processes a type. If the type is a struct, enum, or class,
        /// calls into specialized handlers.
        /// </summary>
        /// <param name="namedTypeSpec">The Swift type specification.</param>
        /// <param name="typeDecl">The associated type declaration.</param>
        private void ProcessTypeRecursively(NamedTypeSpec namedTypeSpec, TypeDecl typeDecl)
        {
            if (_moduleDatabase.IsTypeProcessed(namedTypeSpec.NameWithoutModule))
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
                    {
                        Console.WriteLine($"Skipping unknown type declaration '{typeDecl.GetType().Name}'.");
                    }
                    break;
            }
        }

        /// <summary>
        /// Processes a Swift struct declaration to determine final properties such as blittability
        /// and frozenness, then registers it into the type database.
        /// </summary>
        /// <param name="namedTypeSpec">The Swift type specification for the struct.</param>
        /// <param name="structDecl">The struct declaration.</param>
        private void ProcessStruct(NamedTypeSpec namedTypeSpec, StructDecl structDecl)
        {
            // Ensure that all fields are processed or known in the database.
            ProcessStructFields(structDecl);

            // TODO: Remove loading dylib
            IntPtr metadataPtr = DynamicLibraryLoader.invoke(_dylibPath, $"{structDecl.MangledName}Ma");
            var swiftTypeInfo = new SwiftTypeInfo { MetadataPtr = metadataPtr };

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
        /// <param name="structDecl">The struct declaration.</param>
        /// <exception cref="Exception">
        /// Thrown if a field type cannot be found or processed.
        /// </exception>
        private void ProcessStructFields(StructDecl structDecl)
        {
            foreach (var fieldDecl in structDecl.Fields)
            {
                if (fieldDecl.SwiftTypeSpec is not NamedTypeSpec namedFieldType || fieldDecl.IsStatic)
                    continue;

                // If the field is from a different module, ensure that type is already processed.
                if (namedFieldType.Module != _module)
                {
                    if (!_typeDatabase.IsTypeProcessed(namedFieldType.Module, namedFieldType.NameWithoutModule))
                    {
                        if (_verbosity > 1)
                        {
                            Console.WriteLine(
                                $"Skipping field '{fieldDecl.Name}' of type '{namedFieldType.NameWithoutModule}' " +
                                $"from module '{namedFieldType.Module}'. Type should have been processed " +
                                "in a previous module but was not found.");
                        }
                        continue;
                    }
                }
                // If the field is in the same module, process it recursively.
                else
                {
                    if (!_typeDecls.TryGetValue(namedFieldType, out var nestedDecl))
                    {
                        if (_verbosity > 1)
                        {
                            Console.WriteLine(
                                $"Skipping field '{fieldDecl.Name}' of type '{namedFieldType.NameWithoutModule}' " +
                                $"from module '{namedFieldType.Module}'. Not found in type declarations.");
                        }
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
        /// <returns><c>true</c> if the struct is frozen; otherwise, <c>false</c>.</returns>
        private bool EvaluateFrozenness(StructDecl structDecl)
        {
            if (!structDecl.IsFrozen)
                return false;

            // If any field is not frozen, the struct cannot be frozen.
            foreach (var fieldDecl in structDecl.Fields)
            {
                if (fieldDecl.SwiftTypeSpec is not NamedTypeSpec namedFieldType)
                    continue;

                if (!TryGetTypeRecord(namedFieldType.Module, namedFieldType.NameWithoutModule, out var fieldRecord))
                {
                    throw new Exception($"Type not found in the database: {namedFieldType}");
                }

                if (!fieldRecord.IsFrozen)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the struct can be treated as blittable under .NET's rules.
        /// For now, uses Swift's ValueWitnessTable flags to detect non-POD or non-bitwise-takable.
        /// </summary>
        /// <param name="swiftTypeInfo">Swift type info containing a pointer to the ValueWitnessTable.</param>
        /// <returns><c>true</c> if the struct is blittable; otherwise, <c>false</c>.</returns>
        private unsafe bool EvaluateBlittability(SwiftTypeInfo swiftTypeInfo)
        {
            // TODO: Replace with a manual approach.
            bool isNonPod = swiftTypeInfo.ValueWitnessTable->IsNonPOD;
            bool isNonBitwiseTakable = swiftTypeInfo.ValueWitnessTable->IsNonBitwiseTakable;
            return !isNonPod && !isNonBitwiseTakable;
        }

        /// <summary>
        /// Inserts a struct's details (e.g. name, metadata accessor, frozenness, blittability) into the type database.
        /// </summary>
        /// <param name="namedTypeSpec">The Swift type specification, including module name.</param>
        /// <param name="structDecl">The struct declaration node.</param>
        /// <param name="swiftTypeInfo">Pointer to the Swift metadata plus ValueWitnessTable.</param>
        /// <param name="isFrozen">Indicates whether the struct is effectively frozen.</param>
        /// <param name="isBlittable">Indicates whether the struct is effectively blittable.</param>
        private void RegisterStructType(
            NamedTypeSpec namedTypeSpec,
            StructDecl structDecl,
            SwiftTypeInfo swiftTypeInfo,
            bool isFrozen,
            bool isBlittable)
        {
            var typeRecord = new TypeRecord
            {
                ModuleName = namedTypeSpec.Module,
                Namespace = namedTypeSpec.Module, // TODO: Correctly map to a .NET namespace
                SwiftTypeIdentifier = namedTypeSpec.NameWithoutModule,
                CSTypeIdentifier = structDecl.FullyQualifiedNameWithoutModule,
                SwiftTypeInfo = swiftTypeInfo,
                MetadataAccessor = $"{structDecl.MangledName}Ma",
                IsBlittable = isBlittable,
                IsFrozen = isFrozen
            };

            _moduleDatabase.RegisterType(namedTypeSpec.NameWithoutModule, typeRecord);
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

        /// <summary>
        /// Processes a closed generic type specification by mapping open generic definitions
        /// to specific instantiations (e.g., <c>MyGenericClass&lt;SomeType&gt;</c>).
        /// </summary>
        /// <param name="namedTypeSpec">The Swift type specification representing the closed generic.</param>
        private void ProcessGenericTypeSpec(NamedTypeSpec namedTypeSpec)
        {
            // Attempt to retrieve the open generic type record (e.g., MyGenericClass`1).
            if (!TryGetTypeRecord(namedTypeSpec.Module, $"{namedTypeSpec.NameWithoutModule}`{namedTypeSpec.GenericParameters.Count}", out var genericTypeRecord))
            {
                if (_verbosity > 1)
                {
                    Console.WriteLine($"Skipping generic '{namedTypeSpec}' from module '{namedTypeSpec.Module}'. " +
                                      "Could not find open generic's type record.");
                }
                return;
            }

            // Build a C#-style generic type name based on the parameters.
            var nameBuilder = new StringBuilder(genericTypeRecord.CSTypeIdentifier).Append("<");

            foreach (var genericParameter in namedTypeSpec.GenericParameters)
            {
                if (genericParameter is not NamedTypeSpec namedGenericParameter)
                    continue;

                if (!TryGetTypeRecord(namedGenericParameter.Module, namedGenericParameter.NameWithoutModule, out var genericParameterRecord))
                {
                    if (_verbosity > 1)
                    {
                        Console.WriteLine($"Skipping generic '{namedTypeSpec}' from module '{namedTypeSpec.Module}'.");
                    }
                    return;
                }
                nameBuilder.Append(genericParameterRecord.CSTypeIdentifier);
            }

            nameBuilder.Append(">");

            var typeRecord = new TypeRecord
            {
                ModuleName = namedTypeSpec.Module,
                Namespace = namedTypeSpec.Module, // TODO: Correctly map to a .NET namespace
                SwiftTypeIdentifier = namedTypeSpec.NameWithoutModuleWithGenericParameters,
                CSTypeIdentifier = nameBuilder.ToString(),
                MetadataAccessor = string.Empty, // TODO: Implement metadata accessor for closed generics
                IsBlittable = true,
                IsFrozen = true
            };

            // If the closed generic belongs to a different module, store it in out-of-module records.
            if (namedTypeSpec.Module != _module)
            {
                _outOfModuleTypeRecords[$"{namedTypeSpec.Module}.{namedTypeSpec.NameWithoutModuleWithGenericParameters}"] = typeRecord;
            }
            // Otherwise, register it in the current module.
            else
            {
                _moduleDatabase.RegisterType(namedTypeSpec.NameWithoutModuleWithGenericParameters, typeRecord);
            }
        }
    }
}
