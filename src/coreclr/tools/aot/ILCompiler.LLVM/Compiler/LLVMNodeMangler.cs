// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.Text;
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
        public sealed override Utf8String MangledBoxedTypeName(TypeDesc type)
        {
            Debug.Assert(type.IsValueType);
            return Utf8String.Concat(new Utf8String("Boxed_"), NameMangler.GetMangledTypeName(type));
        }

        public override Utf8String MethodTable(TypeDesc type)
        {
            return Utf8String.Concat(new Utf8String("__MethodTable_"), NameMangler.GetMangledTypeName(type));
        }

        public sealed override Utf8String GCStatics(TypeDesc type)
        {
            return Utf8String.Concat(new Utf8String("__GCStaticBase_"), NameMangler.GetMangledTypeName(type));
        }

        public sealed override Utf8String NonGCStatics(TypeDesc type)
        {
            return Utf8String.Concat(new Utf8String("__NonGCStaticBase_"), NameMangler.GetMangledTypeName(type));
        }

        public sealed override Utf8String ThreadStatics(TypeDesc type)
        {
            return Utf8String.Concat(new Utf8String("__ThreadStaticBase_"), NameMangler.GetMangledTypeName(type));
        }

        public override Utf8String ThreadStaticsIndex(TypeDesc type)
        {
            return Utf8String.Concat(new Utf8String("__ThreadStaticsIndex_"), NameMangler.GetMangledTypeName(type));
        }

        public sealed override Utf8String TypeGenericDictionary(TypeDesc type)
        {
            return Utf8String.Concat(GenericDictionaryNamePrefix, NameMangler.GetMangledTypeName(type));
        }

        public sealed override Utf8String MethodGenericDictionary(MethodDesc method)
        {
            return Utf8String.Concat(GenericDictionaryNamePrefix, NameMangler.GetMangledMethodName(method));
        }

        public override Utf8String ExternMethod(Utf8String unmangledName, MethodDesc method) => unmangledName;

        public override Utf8String ExternVariable(Utf8String unmangledName) => unmangledName;
    }
}
