// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo;
using SyntaxDynamo.CSLang;
using SwiftReflector.SwiftXmlReflection;
using SwiftReflector.TypeMapping;

namespace SwiftReflector
{
    public class BindingsCompiler
    {
        UnicodeMapper UnicodeMapper;
        TypeMapper TypeMapper;
        FunctionCompiler TLFCompiler;

        public BindingsCompiler()
        {
            UnicodeMapper = new UnicodeMapper();
            TypeMapper = new TypeMapper(null, UnicodeMapper);
            TLFCompiler = new FunctionCompiler(TypeMapper);
        }

        public void CompileModule(ModuleDeclaration decl, string outputDirectory, ErrorHandling errors)
        {
            var namespaceName = $"{decl.Name}Bindings";
            var csNamespace = new CSNamespace(namespaceName);
            var csUsingPackages = new CSUsingPackages("System", "System.Runtime.InteropServices");
            var csClass = new CSClass(CSVisibility.Public, decl.Name);
            
            var csFile = new CSFile(csUsingPackages, new CSNamespace[] { csNamespace });
            csNamespace.Block.Add(csClass);
            IEnumerable<CSMethod> methods = CompileFunctions(decl.Functions, errors);
            csClass.Methods.AddRange(methods);

            string csOutputPath = Path.Combine(outputDirectory, string.Format("{0}.cs", namespaceName));
            CodeWriter.WriteToFile(csOutputPath, csFile);
        }

        IEnumerable<CSMethod> CompileFunctions(IEnumerable<FunctionDeclaration> decls, ErrorHandling errors)
        {
            List<CSMethod> methods = new List<CSMethod>();
            foreach (FunctionDeclaration decl in decls)
            {
                var piMethod = TLFCompiler.CompileMethod(decl, true);
                methods.Add(piMethod);
                var publicMethod = TLFCompiler.CompileMethod(decl, false);
                methods.Add(publicMethod);

                var marshaler = new MarshalEngine(null, null, TypeMapper, null);
                var lines = marshaler.MarshalFunctionCall(decl, publicMethod, piMethod);
                publicMethod.Body.AddRange(lines);
            }

            return methods;
        }
    }
}
