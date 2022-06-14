using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit.Experimental;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using Xunit;

namespace Experiment.Tests
{
    public class MyClassTests
    {
        AssemblyName name = new AssemblyName("MyBlankAssembly");
        [Fact]
        public void Test1()
        {
            String fileName = "BlankAssembly.dll";
            System.Console.WriteLine("Something is happening");
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name, System.Reflection.Emit.AssemblyBuilderAccess.Run);
            assemblyBuilder.Save(fileName);
            reader(fileName);//If an error is thrown, we have a malformed DLL.
        }

        private void reader(String fileName)
        {
            int i = 0;
            try
            {
                using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var peReader = new PEReader(fs);
                MetadataReader mr = peReader.GetMetadataReader();
                var asssemblyData = mr.GetAssemblyDefinition();
                Assert.Equal(name.Name, mr.GetString(asssemblyData.Name));
                fs.Dispose();
            }
            catch
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
                    Console.WriteLine("File does not exist.");
                }
            }
        }
    }
}
