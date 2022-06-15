// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace System.Reflection.Emit.Experimental
{

    public class AssemblyBuilder: System.Reflection.Assembly
    {
        private static readonly Guid s_guid = Guid.NewGuid();//Some random ID, Need to look into how these should be generated.
        private static readonly BlobContentId s_contentId = new BlobContentId(s_guid, 0x04030201);
        private BlobBuilder _emptyBlob = new BlobBuilder();
        public override string? FullName { get; }
        private MetadataBuilder _metadata = new MetadataBuilder();
        private IDictionary<string,ModuleBuilder> _moduleStorage = new Dictionary<string, ModuleBuilder>();
        public AssemblyBuilder() { }
        private AssemblyBuilder(string name) 
        {
            FullName = name;
        }

        public void Save(string assemblyFileName)
        {
            if (assemblyFileName == null)
            {
                throw new ArgumentNullException();
            }
            if (FullName == null)
            {
                throw new ArgumentNullException();
            }
            //Add assembly metadata
            _metadata.AddAssembly(//Metadata is added for the new assembly - Current design - metdata generated only when Save method is called.
               _metadata.GetOrAddString(value: FullName),//FullName, CultureName?
               version: new Version(1, 0, 0, 0),
               culture: default(StringHandle),
               publicKey: default(BlobHandle),
               flags: 0,
               hashAlgorithm: AssemblyHashAlgorithm.None);
            //Add each module's medata
            foreach (KeyValuePair<string, ModuleBuilder> entry in _moduleStorage)
            {
                entry.Value.AppendMetadata(_metadata);
            }
            using var peStream = new FileStream(assemblyFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var ilBuilder = new BlobBuilder();
            WritePEImage(peStream, _metadata, ilBuilder);
            peStream.Dispose();//close or dispose?
        }

        public static System.Reflection.Emit.Experimental.AssemblyBuilder DefineDynamicAssembly(System.Reflection.AssemblyName name, System.Reflection.Emit.AssemblyBuilderAccess access)
        {
            if (name == null || name.Name == null)
            {
                throw new ArgumentNullException();
            }
            //AssemblyBuilderAccess affects runtime managment only and is not relevant for saving to disk.
            AssemblyBuilder currentAssembly = new AssemblyBuilder(name.Name);
            //We need to create module becaue even a blank assembly has one module.
            currentAssembly.DefineDynamicModule(name.Name);
            return currentAssembly;
        }

        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Defining a dynamic assembly requires dynamic code.")]
        public static System.Reflection.Emit.AssemblyBuilder DefineDynamicAssembly(System.Reflection.AssemblyName name, System.Reflection.Emit.AssemblyBuilderAccess access, System.Collections.Generic.IEnumerable<System.Reflection.Emit.CustomAttributeBuilder>? assemblyAttributes) 
        { 
            throw new NotImplementedException(); 
        }

        public System.Reflection.Emit.Experimental.ModuleBuilder DefineDynamicModule(string name) 
        {
            ModuleBuilder moduleBuilder = new ModuleBuilder(name,this);
            _moduleStorage.Add(name, moduleBuilder);
            return moduleBuilder;
        }

        public System.Reflection.Emit.Experimental.ModuleBuilder? GetDynamicModule(string name) 
        {
            return _moduleStorage[name];
        }

        private static void WritePEImage(Stream peStream, MetadataBuilder metadataBuilder, BlobBuilder ilBuilder) // MethodDefinitionHandle entryPointHandle when we have main method.
        {
            // Create executable with the managed metadata from the specified MetadataBuilder.
            var peHeaderBuilder = new PEHeaderBuilder(
                imageCharacteristics: Characteristics.Dll //Start off with a simple DLL
                );

            var peBuilder = new ManagedPEBuilder(
                peHeaderBuilder,
                new MetadataRootBuilder(metadataBuilder),
                ilBuilder,
                flags: CorFlags.ILOnly,
                deterministicIdProvider: content => s_contentId);

            // Write executable into the specified stream.
            var peBlob = new BlobBuilder();
            BlobContentId contentId = peBuilder.Serialize(peBlob);
            peBlob.WriteContentTo(peStream);
        }
    }
}

