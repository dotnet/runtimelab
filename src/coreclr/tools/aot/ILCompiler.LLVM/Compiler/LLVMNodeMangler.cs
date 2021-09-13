// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;
using System.Diagnostics;

namespace ILCompiler
{
    //
    // The naming format of these names is known to the debugger
    // 
    public sealed class LLVMNodeMangler : NodeMangler
    {
        // Mangled name of boxed version of a type
        public sealed override string MangledBoxedTypeName(TypeDesc type)
        {
            Debug.Assert(type.IsValueType);
            return "Boxed_" + NameMangler.GetMangledTypeName(type);
        }

        public override string MethodTable(TypeDesc type)
        {
            return "__MethodTable_" + NameMangler.GetMangledTypeName(type);
        }

        public sealed override string GCStatics(TypeDesc type)
        {
            return "__GCStaticBase_" + NameMangler.GetMangledTypeName(type);
        }

        public sealed override string NonGCStatics(TypeDesc type)
        {
            return "__NonGCStaticBase_" + NameMangler.GetMangledTypeName(type);
        }

        public sealed override string ThreadStatics(TypeDesc type)
        {
            return "__ThreadStaticBase_" + NameMangler.GetMangledTypeName(type);
        }

        public override string ThreadStaticsIndex(TypeDesc type)
        {
            return "__ThreadStaticsIndex_" + NameMangler.GetMangledTypeName(type);
        }

        public sealed override string TypeGenericDictionary(TypeDesc type)
        {
            return GenericDictionaryNamePrefix + NameMangler.GetMangledTypeName(type);
        }

        public sealed override string MethodGenericDictionary(MethodDesc method)
        {
            return GenericDictionaryNamePrefix + NameMangler.GetMangledMethodName(method);
        }
    }
}
