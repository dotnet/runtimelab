// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CustAttrLibrary;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace System.Reflection.Emit.Experimental.Tests
{
    [Author(5.0)]
    public interface IMultipleMethod
    {
        
        string Func(int a, string b);
        bool MoreFunc(int a, string b, bool c);
        bool DoIExist();
        void BuildAPerpetualMotionMachine();
    }
    [Author]
    public interface INoMethod
    {

    }
    public class CustomAttributeTest
    {
        const bool _keepFiles = true; // keep files after testing for inspection
        TempFileCollection _tfc;

        [Fact]
        private string Setup()
        {
            Directory.CreateDirectory("testDir");
            _tfc = new TempFileCollection("testDir", false);
            return _tfc.AddExtension("dll", _keepFiles);
        }
        private string WriteAssemblyToDisk(AssemblyName assemblyName, Type[] types)
        {
            int attr = 0;
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, System.Reflection.Emit.AssemblyBuilderAccess.Run);

            ModuleBuilder mb = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            foreach (Type type in types)
            {

                TypeBuilder tb = mb.DefineType(type.FullName, type.Attributes);
                foreach (var method in type.GetMethods())
                {
                    var paramTypes = Array.ConvertAll(method.GetParameters(), item => item.ParameterType);
                    tb.DefineMethod(method.Name, method.Attributes, method.CallingConvention, method.ReturnType, paramTypes);
                }
                if (attr == 0)
                {
                    //Add in CustomAttribute
                    byte[] array = new byte[] { 0,1,0,0, 0,0 ,0,0, 0,0 ,0,0 ,0,0 ,0,0 ,1,4 ,4,0 ,0,0 ,0,0 };
                    tb.SetCustomAttribute(typeof(CustAttrLibrary.AuthorAttribute).GetConstructor(new Type[] { typeof(double) }),array);
                    attr++;
                }
                else
                {
                    tb.SetCustomAttribute(typeof(CustAttrLibrary.AuthorAttribute).GetConstructor(new Type[] { }), null);
                    tb.SetCustomAttribute(typeof(ComImportAttribute).GetConstructor(new Type[] { }), null);
                    tb.SetCustomAttribute(typeof(GuidAttribute).GetConstructor(new Type[] { typeof(string) }), new byte[] { });
                }
            }
            string fileLocation = Setup();
            assemblyBuilder.Save(fileLocation);

            return fileLocation;
        }

        [Fact]
        public void OneInterfaceWithMethods()
        {
            // Construct an assembly name.
            AssemblyName assemblyName = new AssemblyName("MyDynamicAssembly");
            assemblyName.Version = new Version("7.0");

            //Construct its types via reflection.
            Type[] types = new Type[] { typeof(IMultipleMethod), typeof(INoMethod) };

            // Generate DLL from these and save it to Disk.
            string fileLocation = WriteAssemblyToDisk(assemblyName, types);

            // Read said assembly back from Disk using MetadataLoadContext
            Assembly assemblyFromDisk = AssemblyLoadTools.TryLoadAssembly(fileLocation);

            // Now compare them:

            // AssemblyName
            Assert.NotNull(assemblyFromDisk);
            Assert.Equal(assemblyName.Name, assemblyFromDisk.GetName().Name);

            // Module Name
            Module moduleFromDisk = assemblyFromDisk.Modules.First();
            Assert.Equal(assemblyName.Name, moduleFromDisk.ScopeName);

            // Type comparisons
            for (int i = 0; i < types.Length; i++)
            {
                Type sourceType = types[i];
                Type typeFromDisk = moduleFromDisk.GetTypes()[i];

                Assert.Equal(sourceType.Name, typeFromDisk.Name);
                Assert.Equal(sourceType.Namespace, typeFromDisk.Namespace);
                Assert.Equal(sourceType.Attributes, typeFromDisk.Attributes);

                foreach (var custom in sourceType.CustomAttributes)
                {
                    Debug.WriteLine("source");
                    Debug.WriteLine($"Custom attribute with name {custom.AttributeType.FullName}");
                    Debug.WriteLine($"Custom attribute constructor {custom.Constructor.Name}");
                    foreach (var argument in custom.Constructor.GetParameters())
                    {
                        Debug.WriteLine($" An argument: {argument.Name}");
                    }
                }

                foreach (var custom in typeFromDisk.CustomAttributes)
                {
                    Debug.WriteLine("disk");
                    Debug.WriteLine($"Custom attribute with name {custom.AttributeType.FullName}");
                    Debug.WriteLine($"Custom attribute constructor {custom.Constructor.Name}");
                    foreach(var argument in custom.Constructor.GetParameters())
                    {
                        Debug.WriteLine($" An argument: {argument.Name}");
                    }
                }

                // Method comparison
                for (int j = 0; j < sourceType.GetMethods().Length; j++)
                {
                    MethodInfo sourceMethod = sourceType.GetMethods()[j];
                    MethodInfo methodFromDisk = typeFromDisk.GetMethods()[j];

                    Assert.Equal(sourceMethod.Name, methodFromDisk.Name);
                    Assert.Equal(sourceMethod.Attributes, methodFromDisk.Attributes);
                    Assert.Equal(sourceMethod.ReturnType.FullName, methodFromDisk.ReturnType.FullName);
                    // Paramter comparison
                    for (int k = 0; k < sourceMethod.GetParameters().Length; k++)
                    {
                        ParameterInfo sourceParamter = sourceMethod.GetParameters()[k];
                        ParameterInfo paramterFromDisk = methodFromDisk.GetParameters()[k];
                        Assert.Equal(sourceParamter.ParameterType.FullName, paramterFromDisk.ParameterType.FullName);
                    }
                }
            }
        }
    }
}
