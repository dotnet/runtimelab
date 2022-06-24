// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit.Experimental;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Experiment.Tests
{
    public class BlankAssemblyTest
    {
        [Fact]
        public void GenerateBlankAssemblyReadToDisk()
        {
            AssemblyName _name = new AssemblyName("MyBlankAssembly");
            _name.Version = new Version("4.3");
            const String _fileName = "BlankAssembly.dll";
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(_name, System.Reflection.Emit.AssemblyBuilderAccess.Run);
            //We need to create module because even a blank assembly has one module.
            assemblyBuilder.DefineDynamicModule(_name.Name);
            assemblyBuilder.Save(_fileName);
            TryLoadAssembly(_fileName,_name);//It seems that sometimes ILSpy will point out a DLL is malformed but MetadataReader doesn't throw an error (MetadataLoadContext rather?) .
        }
       
    private static void TryLoadAssembly(string filePath, AssemblyName _name)
        {
            int i = 0;
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var peReader = new PEReader(fs);
                MetadataReader mr = peReader.GetMetadataReader();
                var asssemblyData = mr.GetAssemblyDefinition();
                var moduleData = mr.GetModuleDefinition();
                Debug.WriteLine("Assembly name is: " + mr.GetString(asssemblyData.Name));
                Assert.Equal(_name.Name, mr.GetString(asssemblyData.Name));
                Debug.WriteLine("Assembly version is: " + asssemblyData.Version);
                Debug.WriteLine("Module name is: " + mr.GetString(moduleData.Name));
                Assert.Equal(_name.Name, mr.GetString(moduleData.Name));
            }
            catch(IOException)//can be delay in FileWriter releasing rescource
            {
                if (i < 5)
                {
                    i++;
                    System.Threading.Thread.Sleep(100);
                }
                else
                {
                    throw new IOException();
                }
            }
            finally
            {
                  File.Delete(filePath);
            }
        }
    }
}
