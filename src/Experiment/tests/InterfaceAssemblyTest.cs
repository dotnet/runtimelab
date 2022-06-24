// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit.Experimental;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Xunit;

namespace Experiment.Tests
{
    public class InterfaceAssemblyTest
    {
        const bool _keepFiles = true; // keep files after testing for inspection
        TempFileCollection _tfc;
        string _assemblyFile;

        [Fact]
        public void Setup()
        {
            Directory.CreateDirectory("testDir");
            _tfc = new TempFileCollection("testDir", false);
            _assemblyFile = _tfc.AddExtension("dll",_keepFiles);
        }

        [Fact]
        public void BlankInterfaceWithNamespace()
        {
            int _typeCount = 1;
            int _methodCount = 0;
            Setup();
            const string _interfaceName = "System.Empty.EmptyInterface";
            AssemblyName _name = new AssemblyName("MyInterfaceAssembly");
            _name.Version = new Version("1.0");
            AssemblyBuilder _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(_name, System.Reflection.Emit.AssemblyBuilderAccess.Run);
            ModuleBuilder _moduleBuilder = _assemblyBuilder.DefineDynamicModule(_name.Name);
            //Add empty interface
            _moduleBuilder.DefineType(_interfaceName, TypeAttributes.Interface);
            _assemblyBuilder.Save(_assemblyFile);

            //MetadataReader tests
            MetadataReader(_assemblyFile, _typeCount, _methodCount);

            Assembly _loadedAssembly = TryLoadAssembly(_assemblyFile);
            Debug.WriteLine("Assembly name is: " + _loadedAssembly.FullName);
            Assert.Equal(_name.Name, _loadedAssembly.GetName().Name);
            foreach (Module m in _loadedAssembly.Modules)
            {
                Debug.WriteLine("Module name is: " + m.Name);
                Assert.Equal(_name.Name, m.ScopeName);
                    foreach (Type t in m.GetTypes())
                    {
                        Debug.WriteLine("Type name is: " + t.Name);
                        Debug.WriteLine("Type namespace is: " + t.Namespace);
                        Assert.Equal("EmptyInterface", t.Name);
                        Assert.Equal("System.Empty", t.Namespace);
                        Assert.Empty(t.GetMethods());
                    }
            }           
        }

        [Fact]
        public void GenerateInterfaceWithMethod()
        {
            int _typeCount = 1;
            int _methodCount = 1;
            Setup();
            const string _interfaceName = "SimpleConversion";
            const string _methodName = "convert";
            Type _returnType = typeof(int);
            Type[] _parameters = new Type[] { typeof(string) };
            AssemblyName _name = new AssemblyName("MyInterfaceAssembly");
            _name.Version = new Version("1.0");
            AssemblyBuilder _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(_name, System.Reflection.Emit.AssemblyBuilderAccess.Run);
            ModuleBuilder _moduleBuilder = _assemblyBuilder.DefineDynamicModule(_name.Name);
            TypeBuilder _type = _moduleBuilder.DefineType(_interfaceName, TypeAttributes.Interface | TypeAttributes.Public);
            //Add method - public int convert(string)
            _type.DefineMethod(_methodName, MethodAttributes.Public, CallingConventions.HasThis, _returnType, _parameters); ;
            _assemblyBuilder.Save(_assemblyFile);

            MetadataReader(_assemblyFile, _typeCount, _methodCount);

            Assembly _loadedAssembly = TryLoadAssembly(_assemblyFile);
            Assert.Equal(_name.Name, _loadedAssembly.GetName().Name);
            Debug.WriteLine("Assembly name is: " + _loadedAssembly.FullName);
            Debug.WriteLine("Number of types in assembly: " + _loadedAssembly.GetTypes().Length);
            foreach (Type t in _loadedAssembly.GetTypes())
            {
                Debug.WriteLine("Type name is: " + t.Name);
                Assert.Equal(_interfaceName, t.Name);
                Assert.True(t.IsPublic);
                foreach (MethodInfo m in t.GetMethods())
                {
                    Debug.WriteLine("Method name is: " + m.Name + " with return type " + m.ReturnType + "and parameters: ");
                    Assert.Equal(_methodName, m.Name);
                    Assert.Equal(_returnType.Name, m.ReturnType.Name);
                    for(int i=0;i<m.GetParameters().Length;i++)
                    {  
                        Type temp = m.GetParameters()[i].ParameterType;
                        Debug.WriteLine(temp.Name);
                        Assert.Equal(_parameters[i].Name, temp.Name);
                    }
                }
            }
        }

        [Fact]
        public void ThreeInterfacesWithThreeMethods()
        {
            int _typeCount = 3;
            int _methodCount = 3;
            Setup();
            string[] _typeNames = new string[] { "IEmpty", "IVehicle", "IMath" };
            string[] _methodNames = new string[] { "drive", "conversion", "add" };
            Type[] _returnTypes = new Type[] { typeof(void), typeof(int), typeof(int) };
            Type[][] _parameters = new Type[_methodNames.Length][];
            _parameters[0]= new Type[] {};
            _parameters[1] = new Type[] { typeof(int) };
            _parameters[2] = new Type[] { typeof(int), typeof(int) };
            AssemblyName _name = new AssemblyName("MyInterfaceAssembly");
            _name.Version = new Version("1.0");
            AssemblyBuilder _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(_name, System.Reflection.Emit.AssemblyBuilderAccess.Run);
            ModuleBuilder _moduleBuilder = _assemblyBuilder.DefineDynamicModule(_name.Name);

            TypeBuilder IEmpty = _moduleBuilder.DefineType(_typeNames[0], TypeAttributes.Interface | TypeAttributes.Public);
            TypeBuilder IVehicle = _moduleBuilder.DefineType(_typeNames[1], TypeAttributes.Interface | TypeAttributes.Public);
            TypeBuilder IMath = _moduleBuilder.DefineType(_typeNames[2], TypeAttributes.Interface | TypeAttributes.Public);

            IVehicle.DefineMethod(_methodNames[0], MethodAttributes.Public, CallingConventions.HasThis, _returnTypes[0], _parameters[0]);
            IMath.DefineMethod(_methodNames[1], MethodAttributes.Public, CallingConventions.HasThis, _returnTypes[1], _parameters[1]);
            IMath.DefineMethod(_methodNames[2], MethodAttributes.Public, CallingConventions.HasThis, _returnTypes[2], _parameters[2]);
            _assemblyBuilder.Save(_assemblyFile);

            MetadataReader(_assemblyFile, _typeCount, _methodCount);

            Assembly _loadedAssembly = TryLoadAssembly(_assemblyFile);
            Assert.Equal(_name.Name, _loadedAssembly.GetName().Name);
            Debug.WriteLine("Assembly name is: " + _loadedAssembly.FullName);
            Debug.WriteLine("Number of types in assembly: " + _loadedAssembly.GetTypes().Length);
            int methodCount = 0;
            int typeCount = 0;
            foreach (Type t in _loadedAssembly.GetTypes())
            {
                Debug.WriteLine("Type name is: " + t.Name);
                Assert.Equal(_typeNames[typeCount++], t.Name);
                Assert.True(t.IsPublic);
                foreach (MethodInfo m in t.GetMethods())
                {
                    Debug.WriteLine("Method name is: " + m.Name + " with return type " + m.ReturnType + "and parameters: ");
                    Assert.Equal(_methodNames[methodCount], m.Name);
                    Assert.Equal(_returnTypes[methodCount].Name, m.ReturnType.Name);
                    for (int i = 0; i < m.GetParameters().Length; i++)
                    {
                        Type temp = m.GetParameters()[i].ParameterType;
                        Debug.WriteLine(temp.Name);
                        Assert.Equal(_parameters[methodCount][i].Name, temp.Name);
                    }
                    methodCount++;
                }
            }
        }

        private static Assembly TryLoadAssembly(string filePath)
        {
            // filePath = "C:\\Users\\t-mwolberg\\Documents\\Convert.dll";
            // Get the array of runtime assemblies.
            string[] runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
            // Create the list of assembly paths consisting of runtime assemblies and the inspected assembly.
            var paths = new List<string>(runtimeAssemblies);
            paths.Add(filePath);
            // Create PathAssemblyResolver that can resolve assemblies using the created list.
            var resolver = new PathAssemblyResolver(paths);
            var mlc = new MetadataLoadContext(resolver);
            // Load assembly into MetadataLoadContext.
            Assembly assembly = mlc.LoadFromAssemblyPath(filePath);
            return assembly;
        }

        private static void MetadataReader(string filename, int typeCount, int methodCount )
        {
            Debug.WriteLine("Using MetadataReader class");

            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var peReader = new PEReader(fs);

            MetadataReader mr = peReader.GetMetadataReader();

            Debug.WriteLine("Number of types is " + mr.TypeDefinitions.Count);
            Assert.Equal(typeCount+1, mr.TypeDefinitions.Count); //+1 for special "<Module>" type 
            foreach (TypeDefinitionHandle tdefh in mr.TypeDefinitions)
            {
                TypeDefinition tdef = mr.GetTypeDefinition(tdefh);
                string ns = mr.GetString(tdef.Namespace);
                string name = mr.GetString(tdef.Name);
                Debug.WriteLine($"Name of type is {ns}.{name}");
            }

            Debug.WriteLine("Number of methods is " + mr.MethodDefinitions.Count);
            Assert.Equal(methodCount, mr.MethodDefinitions.Count);
            foreach (MethodDefinitionHandle mdefh in mr.MethodDefinitions)
            {
                    MethodDefinition mdef = mr.GetMethodDefinition(mdefh);
                    string mname = mr.GetString(mdef.Name);
                    var owner = mr.GetTypeDefinition(mdef.GetDeclaringType());
                    Debug.WriteLine($"Method name: {mname} is owned by {mr.GetString(owner.Name)}.");
            }
    
            Debug.WriteLine("Ended MetadataReader class");
        }
    }
}
