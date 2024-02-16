// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using SyntaxDynamo;
using SyntaxDynamo.CSLang;
using SwiftRuntimeLibrary;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using SwiftReflector.Inventory;
using SwiftReflector.SwiftXmlReflection;
using SwiftReflector.TypeMapping;

using SwiftReflector.Demangling;
using SwiftReflector.ExceptionTools;
using SwiftReflector.SwiftInterfaceReflector;

namespace SwiftReflector
{
    public class BindingsCompiler
    {
        UnicodeMapper UnicodeMapper;
        TypeMapper TypeMapper;
        TopLevelFunctionCompiler TLFCompiler;
        SwiftInterfaceReflector.SwiftInterfaceReflector Reflector;

        public BindingsCompiler()
        {
            UnicodeMapper = new UnicodeMapper();
            TypeMapper = new TypeMapper(null, UnicodeMapper);

            TLFCompiler = new TopLevelFunctionCompiler(TypeMapper);
            Reflector = new SwiftInterfaceReflector.SwiftInterfaceReflector(null, null);
        }

        public ModuleInventory GetModuleInventory(string dylibPath, ErrorHandling errors)
        {
            var moduleInventory = ModuleInventory.FromFile(dylibPath, errors);
            return moduleInventory;
        }

        public List<ModuleDeclaration> GetModuleDeclarations(string swiftinterfacePath)
        {
            var xdoc = Reflector.Reflect(swiftinterfacePath);

            var outputFile = System.IO.Path.GetTempPath() + Path.GetFileName(swiftinterfacePath) + ".xml";
            xdoc.Save(outputFile);
            var moduleDeclarations = SwiftXmlReflection.Reflector.FromXmlFile(outputFile, null);
            return moduleDeclarations;
        }

        public void CompileModules(List<ModuleDeclaration> moduleDeclarations, ModuleInventory moduleInventory,
                                string swiftLibPath, string outputDirectory,
                                ErrorHandling errors)
        {
            foreach (ModuleDeclaration module in moduleDeclarations)
            {
                bool successfulOutput = false;
                successfulOutput |= CompileTopLevelEntities(module, moduleInventory, swiftLibPath, outputDirectory, errors);
                // successfulOutput |= CompileProtocols (module.Protocols, provider, module, moduleInventory, swiftLibPath, outputDirectory, wrapper, errors);
                // successfulOutput |= CompileClasses (module.Classes, provider, module, moduleInventory, swiftLibPath, outputDirectory, wrapper, errors);
                // successfulOutput |= CompileStructs (module.Structs, provider, module, moduleInventory, swiftLibPath, outputDirectory, wrapper, errors);
                // successfulOutput |= CompileEnums (module.Enums, provider, module, moduleInventory, swiftLibPath, outputDirectory, wrapper, errors);
                // successfulOutput |= CompileExtensions (module.Extensions, provider, module, moduleInventory, swiftLibPath, outputDirectory, wrapper, errors);
                if (!successfulOutput)
                    throw ErrorHelper.CreateError(ReflectorError.kCantHappenBase + 16, "binding-tools-for-swift could not generate any output. Check the logs and consider using '--verbose' for more information.");
            }
        }

        bool CompileTopLevelEntities(ModuleDeclaration module, ModuleInventory moduleInventory, string swiftLibPath,
                                      string outputDirectory, ErrorHandling errors)
        {
            var use = new CSUsingPackages("System", "System.Runtime.InteropServices");

            var picl = new CSClass(CSVisibility.Internal, PIClassName(module.Name + "." + "TopLevelEntities"));
            var usedPinvokes = new List<string>();

            var cl = CompileTLFuncs(module.TopLevelFunctions, module, moduleInventory,
                         swiftLibPath, outputDirectory, errors, use, null, picl, usedPinvokes);

            cl = CompileTLProps(module.TopLevelProperties, module, moduleInventory,
                         swiftLibPath, outputDirectory, errors, use, cl, picl, usedPinvokes);


            if (cl != null)
            {
                string nameSpace = TypeMapper.MapModuleToNamespace(module.Name);
                var nm = new CSNamespace(nameSpace);

                var csfile = new CSFile(use, new CSNamespace[] { nm });
                nm.Block.Add(cl);
                nm.Block.Add(picl);
                string csOutputFileName = string.Format("{1}{0}.cs", nameSpace, cl.Name.Name);
                string csOutputPath = Path.Combine(outputDirectory, csOutputFileName);

                CodeWriter.WriteToFile(csOutputPath, csfile);

            }
            else
            {
                Console.WriteLine("No top-level entities");
                return false;
            }
            return true;
        }

        CSClass CompileTLFuncs(IEnumerable<FunctionDeclaration> funcs,
                                ModuleDeclaration module, ModuleInventory moduleInventory, string swiftLibPath,
                                string outputDirectory, ErrorHandling errors,
                                CSUsingPackages use, CSClass cl, CSClass picl, List<string> usedPinvokeNames)
        {
            var methods = new List<CSMethod>();

            foreach (FunctionDeclaration func in funcs)
            {
                try
                {
                    if (func.IsProperty)
                        continue;

                    if (func.IsDeprecated || func.IsUnavailable)
                        continue;

                    var tlf = XmlToTLFunctionMapper.ToTLFunction(func, moduleInventory, TypeMapper);
                    CompileToDirectFunction(func, tlf, "", func.Parent, use, swiftLibPath, methods, picl, usedPinvokeNames);
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
            }

            if (methods.Count > 0)
            {
                cl = cl ?? new CSClass(CSVisibility.Public, "TopLevelEntities");
                cl.Methods.AddRange(methods);
            }
            return cl;
        }

        void CompileToDirectFunction(FunctionDeclaration func, TLFunction tlf, string homonymSuffix, BaseDeclaration context,
                          CSUsingPackages use, string swiftLibPath, List<CSMethod> methods, CSClass picl,
                                      List<string> usedPinvokeNames)
        {
            // FIXME - need to do operators
            if (tlf.Operator != OperatorType.None)
                return;

            var baseName = TypeMapper.SanitizeIdentifier(tlf.Name.Name);
            var pinvokeMethodName = PIFuncName(baseName + homonymSuffix);
            pinvokeMethodName = Uniqueify(pinvokeMethodName, usedPinvokeNames);
            usedPinvokeNames.Add(pinvokeMethodName);

            var pinvokeMethodRef = PIClassName($"{func.Module.Name}.TopLevelEntities") + "." + pinvokeMethodName;

            var piMethod = TLFCompiler.CompileMethod(func, use, PInvokeName(swiftLibPath),
                tlf.MangledName, pinvokeMethodName, true, true, false);
            picl.Methods.Add(piMethod);

            var publicMethodOrig = TLFCompiler.CompileMethod(func, use, PInvokeName(swiftLibPath),
                tlf.MangledName, null, false, false, false);

            CSIdentifier wrapperName = GetMethodWrapperName(func, publicMethodOrig, homonymSuffix);
            CSVisibility visibility = GetMethodWrapperVisibility(func, publicMethodOrig);

            // rebuild the method as static
            var publicMethod = new CSMethod(visibility, CSMethodKind.Static, publicMethodOrig.Type,
                             wrapperName, publicMethodOrig.Parameters, publicMethodOrig.Body);
            publicMethod.GenericParameters.AddRange(publicMethodOrig.GenericParameters);
            publicMethod.GenericConstraints.AddRange(publicMethodOrig.GenericConstraints);

            var localIdents = new List<string> {
                publicMethod.Name.Name, pinvokeMethodName
            };
            localIdents.AddRange(publicMethod.Parameters.Select(p => p.Name.Name));

            var marshaler = new MarshalEngine(use, localIdents, TypeMapper, null);
            var lines = marshaler.MarshalFunctionCall(func, false, pinvokeMethodRef, publicMethod.Parameters,
                func, func.ReturnTypeSpec, publicMethod.Type, null, null, false, func,
                false, -1, func.HasThrows);
            publicMethod.Body.AddRange(lines);
            methods.Add(publicMethod);
        }

        CSClass CompileTLProps(IEnumerable<PropertyDeclaration> props,
                                ModuleDeclaration module, ModuleInventory moduleInventory, string swiftLibPath,
                                string outputDirectory, ErrorHandling errors,
                                CSUsingPackages use, CSClass cl, CSClass picl, List<string> usedPinvokes)
        {
            var properties = new List<CSProperty>();

            foreach (PropertyDeclaration prop in props)
            {
                if (prop.IsDeprecated || prop.IsUnavailable)
                    continue;
                try
                {
                    cl = cl ?? new CSClass(CSVisibility.Public, "TopLevelEntities");

                    // Calculated properties have a matching __method
                    string backingMethodName = ("__" + prop.Name);
                    CSMethod backingMethod = cl.Methods.FirstOrDefault(x => x.Name.Name == backingMethodName);
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
            }

            if (properties.Count > 0)
            {
                cl.Properties.AddRange(properties);
            }
            return cl;
        }

        string PIClassName(string fullClassName)
        {
            if (!fullClassName.Contains('.'))
                throw new ArgumentOutOfRangeException(nameof(fullClassName), String.Format("Class name {0} should be a full class name.", fullClassName));
            fullClassName = fullClassName.Substring(fullClassName.IndexOf('.') + 1).Replace('.', '_');
            return "NativeMethodsFor" + fullClassName;
        }

        string PIClassName(DotNetName fullClassName)
        {
            return PIClassName(fullClassName.Namespace + "." + fullClassName.TypeName);
        }

        string PIClassName(SwiftClassName name)
        {
            return PIClassName(TypeMapper.GetDotNetNameForSwiftClassName(name));
        }

        string PIFuncName(SwiftName functionName)
        {
            return String.Format("PIfunc_{0}", functionName.Name);
        }

        string PIFuncName(string functionName)
        {
            return $"PIfunc_{functionName}";
        }

        CSIdentifier GetMethodWrapperName(FunctionDeclaration func, CSMethod method, string homonymSuffix)
        {
            string prefix = func.IsProperty ? "__" : "";
            return new CSIdentifier(prefix + method.Name.Name + homonymSuffix);
        }


        CSVisibility GetMethodWrapperVisibility(FunctionDeclaration func, CSMethod method)
        {
            return func.IsProperty ? CSVisibility.Private : method.Visibility;
        }

        static string PInvokeName(string libFullPath, string originalLibrary = null)
        {
            return LibOrFrameworkFromPath(libFullPath);
        }

        static string LibOrFrameworkFromPath(string libFullPath)
        {
            string directory = Path.GetDirectoryName(libFullPath);
            string file = Path.GetFileName(libFullPath);
            return file;
        }

        public static string Uniqueify(string name, IEnumerable<string> names)
        {
            int thisTime = 0;
            var sb = new StringBuilder(name);
            while (names.Contains(sb.ToString()))
            {
                sb.Clear().Append(name).Append(thisTime++);
            }
            return sb.ToString();
        }
    }
}
