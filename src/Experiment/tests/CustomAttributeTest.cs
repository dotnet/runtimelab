// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace System.Reflection.Emit.Experimental.Tests
{
    public interface IMultipleMethod
    {

        string Func(int a, string b);
        bool MoreFunc(int a, string b, bool c);
        bool DoIExist();
        void BuildAPerpetualMotionMachine();
    }

    public interface INoMethod
    {

    }

    //Currently hard-coding in Custom Attributes using the CustomAttributeBuilder.
    public class CustomAttributeTest
    {
        List<CustomAttributeBuilder> customAttributes = new List<CustomAttributeBuilder>();
        const bool _keepFiles = true;
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
            customAttributes.Add(new CustomAttributeBuilder(typeof(ComImportAttribute).GetConstructor(new Type[] { }), new object[] { }));
            customAttributes.Add(new CustomAttributeBuilder(typeof(ComVisibleAttribute).GetConstructor(new Type[] { typeof(bool) }), new object[] { true }));
            customAttributes.Add(new CustomAttributeBuilder(typeof(GuidAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { "9ED54F84-A89D-4fcd-A854-44251E925F09" }));

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

                //Add in these customAttributes
                foreach (CustomAttributeBuilder customAttribute in customAttributes)
                {
                    tb.SetCustomAttribute(customAttribute);
                }
            }

            string fileLocation = Setup();
            assemblyBuilder.Save(fileLocation);

            return fileLocation;
        }

        // Add three custom attributes to two types. One is pseudo custom attribute.
        // This also tests that Save doesn't have unnecessary duplicate references to same assembly, type etc.
        [Fact]
        public void TwoInterfaceCustomAttribute()
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
                Assert.Equal(sourceType.Attributes | TypeAttributes.Import, typeFromDisk.Attributes); // Pseudo-custom attributes are added to core TypeAttributes.

                // Ordering of custom attributes is not preserved in metadata so we sort before comparing.
                List<CustomAttributeData> attributesFromDisk = typeFromDisk.GetCustomAttributesData().ToList();
                attributesFromDisk.Sort((x, y) => x.AttributeType.ToString().CompareTo(y.AttributeType.ToString()));
                customAttributes.Sort((x, y) => x.Con.DeclaringType.ToString().CompareTo(y.Con.DeclaringType.ToString()));

                for (int j = 0; j < customAttributes.Count; j++)
                {
                    CustomAttributeBuilder sourceAttribute = customAttributes[j];
                    CustomAttributeData attributeFromDisk = attributesFromDisk[j];
                    Debug.WriteLine(attributeFromDisk.AttributeType.ToString());
                    Assert.Equal(sourceAttribute.Con.DeclaringType.ToString(), attributeFromDisk.AttributeType.ToString());
                }

                // Method comparison
                for (int j = 0; j < sourceType.GetMethods().Length; j++)
                {
                    MethodInfo sourceMethod = sourceType.GetMethods()[j];
                    MethodInfo methodFromDisk = typeFromDisk.GetMethods()[j];

                    Assert.Equal(sourceMethod.Name, methodFromDisk.Name);
                    Assert.Equal(sourceMethod.Attributes, methodFromDisk.Attributes);
                    Assert.Equal(sourceMethod.ReturnType.FullName, methodFromDisk.ReturnType.FullName);
                    // Parameter comparison
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
