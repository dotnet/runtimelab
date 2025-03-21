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
    sealed record ModuleProcessingResult(ModuleTypeDatabase ModuleDatabase);

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
        private readonly int _verbosity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleProcessor"/> class.
        /// </summary>
        /// <param name="module">The name of the Swift module being processed.</param>
        /// <param name="dylibPath">The file path to the Swift dynamic library.</param>
        /// <param name="typeDecls">A dictionary mapping Swift type specs to their declarations.</param>
        /// <param name="typeDatabase">The global type database tracking processed types.</param>
        /// <param name="verbosity">The verbosity level for logging.</param>
        public ModuleProcessor(
            string module,
            string dylibPath,
            Dictionary<NamedTypeSpec, TypeDecl> typeDecls,
            ITypeDatabase typeDatabase,
            int verbosity)
        {
            _module = module;
            _dylibPath = dylibPath;
            _typeDatabase = typeDatabase;
            _moduleDatabase = new ModuleTypeDatabase(module, dylibPath);
            _typeDecls = typeDecls;
            _verbosity = verbosity;
        }

        /// <summary>
        /// Tries to retrieve a <see cref="TypeRecord"/> for the specified Swift type.
        /// </summary>
        /// <param name="swiftTypeSpec">The Swift type specification.</param>
        /// <param name="record">
        /// When this method returns, contains the <see cref="TypeRecord"/> if found; otherwise, <c>null</c>.
        /// </param>
        /// <returns><c>true</c> if the type was found; otherwise, <c>false</c>.</returns>
        private bool TryGetTypeRecord(NamedTypeSpec swiftTypeSpec, [NotNullWhen(true)] out TypeRecord? record)
        {
            var swiftTypeName = SwiftTypeName.FromTypeSpec(swiftTypeSpec);

            // First, check if this module is the one being processed.
            if (swiftTypeName.Module == _module)
            {
                return _moduleDatabase.TryGetTypeRecord(swiftTypeName, out record);
            }

            // Otherwise, fall back to checking the global type database.
            return _typeDatabase.TryGetTypeRecord(swiftTypeName, out record);
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

            return new ModuleProcessingResult(_moduleDatabase);
        }

        /// <summary>
        /// Recursively processes a type. If the type is a struct, enum, or class,
        /// calls into specialized handlers.
        /// </summary>
        /// <param name="namedTypeSpec">The Swift type specification.</param>
        /// <param name="typeDecl">The associated type declaration.</param>
        private void ProcessTypeRecursively(NamedTypeSpec namedTypeSpec, TypeDecl typeDecl)
        {
            if (_moduleDatabase.IsTypeProcessed(SwiftTypeName.FromTypeSpec(namedTypeSpec)))
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

                case ProtocolDecl protocolDecl:
                    ProcessProtocol(namedTypeSpec, protocolDecl);
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
            // Ensure that all properties are processed or known in the database.
            ProcessStructProperties(structDecl);


            // TODO: Remove loading dylib
            IntPtr metadataPtr = DynamicLibraryLoader.invoke(_dylibPath, structDecl.MetadataAccessor);
            var swiftTypeInfo = new SwiftTypeInfo { MetadataPtr = metadataPtr };

            TypeRecordFlags flags = CacluateFlags(structDecl);

            RegisterStructType(namedTypeSpec, structDecl, swiftTypeInfo, flags);

            // Update the struct declaration in memory so future passes see these properties.
            structDecl.IsFrozen = (flags & TypeRecordFlags.Frozen) != 0;
        }

        /// <summary>
        /// Ensures that each property in the struct is either from an already processed type
        /// or is recursively processed in this module.
        /// </summary>
        /// <param name="structDecl">The struct declaration.</param>
        /// <exception cref="Exception">
        /// Thrown if a property type cannot be found or processed.
        /// </exception>
        private void ProcessStructProperties(StructDecl structDecl)
        {
            foreach (var propertyDecl in structDecl.Properties)
            {
                if (propertyDecl.SwiftTypeSpec is not NamedTypeSpec namedPropertyType || propertyDecl.IsStatic)
                    continue;

                // If the property is from a different module, ensure that type is already processed.
                if (namedPropertyType.Module != _module)
                {
                    if (!_typeDatabase.IsTypeProcessed(namedPropertyType))
                    {
                        if (_verbosity > 1)
                        {
                            Console.WriteLine(
                                $"Skipping property '{propertyDecl.Name}' of type '{namedPropertyType.NameWithoutModule}' " +
                                $"from module '{namedPropertyType.Module}'. Type should have been processed " +
                                "in a previous module but was not found.");
                        }
                        continue;
                    }
                }
                // If the property is in the same module, process it recursively.
                else
                {
                    if (!_typeDecls.TryGetValue(namedPropertyType, out var nestedDecl))
                    {
                        if (_verbosity > 1)
                        {
                            Console.WriteLine(
                                $"Skipping property '{propertyDecl.Name}' of type '{namedPropertyType.NameWithoutModule}' " +
                                $"from module '{namedPropertyType.Module}'. Not found in type declarations.");
                        }
                        continue;
                    }
                    ProcessTypeRecursively(namedPropertyType, nestedDecl);
                }
            }
        }

        /// <summary>
        /// Determines whether a struct is truly frozen. The struct itself must be marked frozen
        /// and all of its properties must also be from frozen types.
        /// </summary>
        /// <param name="structDecl">The struct declaration.</param>
        /// <returns><c>true</c> if the struct is frozen; otherwise, <c>false</c>.</returns>
        private TypeRecordFlags CacluateFlags(StructDecl structDecl)
        {
            TypeRecordFlags flags = TypeRecordFlags.None;

            if (!structDecl.IsFrozen)
                return TypeRecordFlags.None;

            if (structDecl.IsFrozen)
                flags |= TypeRecordFlags.Frozen;

            foreach (var propertyDecl in structDecl.Properties)
            {
                if (propertyDecl.SwiftTypeSpec is not NamedTypeSpec namedPropertyType)
                    continue;

                if (propertyDecl.IsStatic)
                    continue;

                if (!TryGetTypeRecord(namedPropertyType, out var propertyRecord))
                    throw new Exception($"Type not found in the database: {namedPropertyType}");

                // If any property is not frozen struct, remove the frozen flag
                if (propertyRecord.Kind == TypeRecordKind.Struct && (propertyRecord.Flags & TypeRecordFlags.Frozen) == 0)
                    flags &= ~TypeRecordFlags.Frozen;

                // If any property is heap-allocated, set the RequiresMemoryManagement flag
                if ((propertyRecord.Flags & TypeRecordFlags.RequiresMemoryManagement) != 0 || propertyRecord.Kind == TypeRecordKind.Class)
                    flags |= TypeRecordFlags.RequiresMemoryManagement;
            }

            return flags;
        }

        /// <summary>
        /// Inserts a struct's details (e.g. name, metadata accessor, frozenness, blittability) into the type database.
        /// </summary>
        /// <param name="namedTypeSpec">The Swift type specification, including module name.</param>
        /// <param name="structDecl">The struct declaration node.</param>
        /// <param name="swiftTypeInfo">Pointer to the Swift metadata plus ValueWitnessTable.</param>
        /// <param name="isFrozen">Indicates whether the struct is effectively frozen.</param>
        private void RegisterStructType(
            NamedTypeSpec namedTypeSpec,
            StructDecl structDecl,
            SwiftTypeInfo swiftTypeInfo,
            TypeRecordFlags flags)
        {
            var @namespace = $"Swift.{namedTypeSpec.Module}"; // TODO: Correctly map to a .NET namespace
            // TODO: Remove this logic once correct csharp type names are used
            var csharpTypeIdentifier = structDecl.SwiftTypeName.Module == "" ? structDecl.SwiftTypeName.Name : structDecl.SwiftTypeName.ModuleQualifiedName.Substring(structDecl.SwiftTypeName.ModuleQualifiedName.IndexOf(".") + 1);
            var typeRecord = new TypeRecord
            {
                SwiftTypeName = structDecl.SwiftTypeName,
                CSharpTypeName = CSharpTypeName.FromNamespaceAndName(@namespace, csharpTypeIdentifier),
                SwiftTypeInfo = swiftTypeInfo,
                MetadataAccessor = structDecl.MetadataAccessor,
                Flags = flags,
                Kind = TypeRecordKind.Struct,
            };

            _moduleDatabase.RegisterType(structDecl.SwiftTypeName, typeRecord);
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
        /// Inserts a class's details into the type database.
        /// </summary>
        /// <param name="namedTypeSpec">The Swift type specification, including module name.</param>
        /// <param name="classDecl">The class declaration node.</param>
        /// <param name="swiftTypeInfo">Pointer to the Swift metadata plus ValueWitnessTable.</param>
        private void RegisterClassType(
            NamedTypeSpec namedTypeSpec,
            ClassDecl classDecl,
            SwiftTypeInfo swiftTypeInfo)
        {
            TypeRecordFlags flags = TypeRecordFlags.RequiresMemoryManagement;
            var @namespace = $"Swift.{namedTypeSpec.Module}"; // TODO: Correctly map to a .NET namespace
            // TODO: Remove this logic once correct csharp type names are used
            var csharpTypeIdentifier = classDecl.SwiftTypeName.Module == "" ? classDecl.SwiftTypeName.Name : classDecl.SwiftTypeName.ModuleQualifiedName.Substring(classDecl.SwiftTypeName.ModuleQualifiedName.IndexOf(".") + 1);
            var typeRecord = new TypeRecord
            {
                SwiftTypeName = classDecl.SwiftTypeName,
                CSharpTypeName = CSharpTypeName.FromNamespaceAndName(@namespace, csharpTypeIdentifier),
                SwiftTypeInfo = swiftTypeInfo,
                MetadataAccessor = $"{classDecl.MangledName}Ma",
                Flags = flags,
                Kind = TypeRecordKind.Class,
            };

            _moduleDatabase.RegisterType(classDecl.SwiftTypeName, typeRecord);
        }

        /// <summary>
        /// Processes a class declaration. Currently unimplemented.
        /// </summary>
        /// <param name="namedTypeSpec">Spec for the class's name, module, etc.</param>
        /// <param name="classDecl">The class declaration node.</param>
        private void ProcessClass(NamedTypeSpec namedTypeSpec, ClassDecl classDecl)
        {
            IntPtr metadataPtr = DynamicLibraryLoader.invoke(_dylibPath, $"{classDecl.MangledName}Ma");
            var swiftTypeInfo = new SwiftTypeInfo { MetadataPtr = metadataPtr };

            RegisterClassType(namedTypeSpec, classDecl, swiftTypeInfo);
        }

        /// <summary>
        /// Processes a protocol declaration. Currently unimplemented.
        /// </summary>
        /// <param name="namedTypeSpec">Spec for the protocol's name, module, etc.</param>
        /// <param name="protocolDecl">The protocol declaration node.</param>
        /// <returns><c>true</c> if the protocol was processed successfully; otherwise, <c>false</c>.</returns>
        private bool ProcessProtocol(NamedTypeSpec namedTypeSpec, ProtocolDecl protocolDecl)
        {
            return true;
        }
    }
}
