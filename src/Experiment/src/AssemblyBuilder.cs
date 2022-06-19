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
        private readonly Guid _guid = Guid.NewGuid();
        private AssemblyName? _assemblyName;
        private MetadataBuilder _metadata = new MetadataBuilder();
        private IDictionary<string,ModuleBuilder> _moduleStorage = new Dictionary<string, ModuleBuilder>();

        private AssemblyBuilder(AssemblyName name) 
        {
            _assemblyName = name;
        }

        public void Save(string assemblyFileName)
        {
            if (assemblyFileName == null)
            {
                throw new ArgumentNullException(nameof(assemblyFileName));
            }
            if (_assemblyName==null||_assemblyName.Name==null)
            {
                throw new ArgumentNullException(nameof(_assemblyName));
            }
            if(_moduleStorage.Count==0)
            {
                throw new InvalidOperationException("Assembly needs at least one module defined");
            }
            //Add assembly metadata
            _metadata.AddAssembly(//Metadata is added for the new assembly - Current design - metdata generated only when Save method is called.
               _metadata.GetOrAddString(value: _assemblyName.Name),
               version: _assemblyName.Version ?? new Version(0, 0, 0, 0),
               culture: (_assemblyName.CultureName==null) ?  default : _metadata.GetOrAddString(value: _assemblyName.CultureName),
               publicKey: (_assemblyName.GetPublicKey() is byte[] publicKey) ? _metadata.GetOrAddBlob(value: publicKey) : default,
               flags: (AssemblyFlags) _assemblyName.Flags,
               hashAlgorithm: AssemblyHashAlgorithm.None);//It seems AssemblyName.HashAlgorithm is obslete so default value used.
            //Add each module's metadata
            foreach (KeyValuePair<string, ModuleBuilder> entry in _moduleStorage)
            {
                entry.Value.AppendMetadata(_metadata);
            }
            using var peStream = new FileStream(assemblyFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var ilBuilder = new BlobBuilder();
            WritePEImage(peStream, _metadata, ilBuilder);
        }

        public static System.Reflection.Emit.Experimental.AssemblyBuilder DefineDynamicAssembly(System.Reflection.AssemblyName name, System.Reflection.Emit.AssemblyBuilderAccess access)
        {
            if (name == null || name.Name == null)
            {
                throw new ArgumentNullException();
            }
            //AssemblyBuilderAccess affects runtime managment only and is not relevant for saving to disk.
            AssemblyBuilder currentAssembly = new AssemblyBuilder(name);
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
            //Create executable with the managed metadata from the specified MetadataBuilder.
            var peHeaderBuilder = new PEHeaderBuilder(
                imageCharacteristics: Characteristics.Dll //Start off with a simple DLL
                );
        
            var peBuilder = new ManagedPEBuilder(
                peHeaderBuilder,
                new MetadataRootBuilder(metadataBuilder),
                ilBuilder,
                flags: CorFlags.ILOnly,
                deterministicIdProvider: content => new BlobContentId(_guid, 0x04030201));//Const ID, will reexamine as project progresses. 

            // Write executable into the specified stream.
            var peBlob = new BlobBuilder();
            BlobContentId contentId = peBuilder.Serialize(peBlob);
            peBlob.WriteContentTo(peStream);
        }
    }
}

