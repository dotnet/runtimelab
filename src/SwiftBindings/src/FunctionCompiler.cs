// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo.CSLang;
using SwiftReflector.TypeMapping;
using SwiftReflector.SwiftXmlReflection;

namespace SwiftReflector
{
    public class FunctionCompiler
    {
        TypeMapper typeMap;
        public FunctionCompiler(TypeMapper typeMap)
        {
            this.typeMap = typeMap;
        }

        public CSMethod CompileMethod(FunctionDeclaration func, bool isPinvoke)
        {
            string funcName = func.Name;
            if (isPinvoke) {
                funcName = string.Format("PIfunc_{0}", funcName);
            }

            NetTypeBundle returnType = typeMap.MapType(func, func.ReturnTypeSpec, isPinvoke, true);
            CSUsingPackages packs = new CSUsingPackages();
            CSType csReturnType = returnType.IsVoid ? CSSimpleType.Void : returnType.ToCSType(packs);
            
            var csParams = new CSParameterList();
            if (func.ParameterLists.FirstOrDefault().Count > 0){
                 var args = typeMap.MapParameterList(func, func.ParameterLists.FirstOrDefault(), isPinvoke, false, null, null, packs);
                foreach (var arg in args)
                {
                    var csType = arg.Type.ToCSType(packs);
                    csParams.Add(new CSParameter(csType, new CSIdentifier(arg.Name), arg.Type.IsReference ? CSParameterKind.Ref : CSParameterKind.None, null));
                }
            }

            if (isPinvoke)
            {
                return CSMethod.InternalPInvoke(csReturnType, funcName, $"lib{func.Module.Name}.dylib", func.MangledName, csParams);
            }
            else
            {
                return new CSMethod(CSVisibility.Public, func.IsStatic ? CSMethodKind.Static : CSMethodKind.Virtual, csReturnType, new CSIdentifier(funcName), csParams, new CSCodeBlock());
            }
        }

        static void AddUsingBlock(CSUsingPackages packs, NetTypeBundle type)
        {
            if (type.IsVoid || String.IsNullOrEmpty(type.NameSpace))
                return;
            packs.AddIfNotPresent(type.NameSpace);
        }
    }
}

