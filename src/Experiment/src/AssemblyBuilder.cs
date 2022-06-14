// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.Metadata;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace System.Reflection.Emit.Experimental
{
    public class AssemblyBuilder: System.Reflection.Assembly
    {
        private static readonly Guid _s_guid = new Guid("87D4DBE1-1143-4FAD-AAB3-1001F92068E6");//Some random ID, Need to look into how these should be generated.
        private static readonly BlobContentId _s_contentId = new BlobContentId(_s_guid, 0x04030201);
        BlobBuilder _emptyBlob = new BlobBuilder();
        AssemblyName? _name;
        static MetadataBuilder _metadata = new MetadataBuilder();
        public AssemblyBuilder() { }
        private AssemblyBuilder(AssemblyName name) 
        {
            _name = name;
        }

        public void Save(string assemblyFileName)
        {
            if (assemblyFileName == null)
            {
                throw new ArgumentNullException("File name cannot be null");
            }
            using var peStream =
               new FileStream(assemblyFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            var ilBuilder = new BlobBuilder();
            WritePEImage(peStream, _metadata, ilBuilder);
            peStream.Dispose();//close or dispose?
            peStream.Close();
        }

        public static System.Reflection.Emit.Experimental.AssemblyBuilder DefineDynamicAssembly(System.Reflection.AssemblyName name, System.Reflection.Emit.AssemblyBuilderAccess access)
        {
            if (name == null || name.Name == null)
            {
                throw new ArgumentNullException("Assembly name cannot be null");
            }
            _metadata.AddModule(//Needed to add a module in here, assembly invalid without at least one module. In future, will be moved to DefineDynamicModule().
                    0,
                   _metadata.GetOrAddString(name.Name),
                   _metadata.GetOrAddGuid(_s_guid),
                    default(GuidHandle),
                    default(GuidHandle));

            _metadata.AddAssembly(//Metadata is added for the new assembly - Current design is metdata created at time of assembly defintion. Should it only be done when saved to disk?
               _metadata.GetOrAddString(value: name.Name),//FullName, CultureName?
               version: new Version(1, 0, 0, 0),
               culture: default(StringHandle),
               publicKey: default(BlobHandle),
               flags: 0,
               hashAlgorithm: AssemblyHashAlgorithm.None);
            //AssemblyBuilderAccess affects runtime managment only and is not relevant for saving to disk.
            return new AssemblyBuilder(name);
        }

        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Defining a dynamic assembly requires dynamic code.")]
        public static System.Reflection.Emit.AssemblyBuilder DefineDynamicAssembly(System.Reflection.AssemblyName name, System.Reflection.Emit.AssemblyBuilderAccess access, System.Collections.Generic.IEnumerable<System.Reflection.Emit.CustomAttributeBuilder>? assemblyAttributes) 
        { 
            throw new NotImplementedException(); 
        }

        public System.Reflection.Emit.ModuleBuilder DefineDynamicModule(string name) 
        { 
            throw new NotImplementedException(); 
        }

        public System.Reflection.Emit.ModuleBuilder? GetDynamicModule(string name) 
        { 
            throw new NotImplementedException(); 
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
                deterministicIdProvider: content => _s_contentId);

            // Write executable into the specified stream.
            var peBlob = new BlobBuilder();
            BlobContentId contentId = peBuilder.Serialize(peBlob);
            peBlob.WriteContentTo(peStream);
        }
    }
}

