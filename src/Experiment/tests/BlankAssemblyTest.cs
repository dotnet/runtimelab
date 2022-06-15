using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit.Experimental;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using Xunit;

namespace Experiment.Tests
{
    public class BlankAssemblyTest
    {
        private readonly AssemblyName _name = new AssemblyName("MyBlankAssembly");
        [Fact]
        public void GenerateBlankAssemblyReadToDisk()
        {
            const String _fileName = "BlankAssembly.dll";
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(_name, System.Reflection.Emit.AssemblyBuilderAccess.Run);
            assemblyBuilder.Save(_fileName);
            Reader(_fileName);//It seems that sometimes ILSpy will point out a DLL is malformed but MetadataReader doesn't throw an error (MetadataLoadContext rather?) .
        }
       

        private void Reader(String fileName)
        {
            int i = 0;
            try
            {
                using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var peReader = new PEReader(fs);
                MetadataReader mr = peReader.GetMetadataReader();
                var asssemblyData = mr.GetAssemblyDefinition();
                var moduleData = mr.GetModuleDefinition();
                Debug.WriteLine("Assembly name is: " + mr.GetString(asssemblyData.Name));
                Assert.Equal(_name.Name, mr.GetString(asssemblyData.Name));
                Debug.WriteLine("Module name is: " + mr.GetString(moduleData.Name));
                Assert.Equal(_name.Name, mr.GetString(moduleData.Name));
                fs.Dispose();
            }
            catch//can be delay in FileWriter releasing rescource
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
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
                else
                {
                    Debug.WriteLine("File does not exist.");
                }
            }
        }
    }
}
