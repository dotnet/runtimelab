// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

using static Internal.JitInterface.CorJitApiId;

namespace Internal.JitInterface.LLVMInterop
{
    public struct LLVMContext { }

    public readonly unsafe partial struct LLVMContextRef(LLVMContext* handle)
    {
        public readonly LLVMContext* Handle = handle;

        public static LLVMContext* Create()
        {
            return ((delegate* unmanaged<LLVMContext*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMContext_Create))();
        }

        public static implicit operator LLVMContextRef(LLVMContext* value) => new(value);
        public static implicit operator LLVMContext*(LLVMContextRef value) => value.Handle;
    }

    public unsafe struct LLVMModule { }

    public readonly unsafe partial struct LLVMModuleRef(LLVMModule* handle)
    {
        public readonly LLVMModule* Handle = handle;

        public static LLVMModuleRef Create(LLVMContext* context, ReadOnlySpan<byte> name, string target, string dataLayout)
        {
            byte[] utf8Target = Encoding.UTF8.GetBytes(target);
            byte[] utf8DataLayout = Encoding.UTF8.GetBytes(dataLayout);
            fixed (byte* pName = name, pTarget = utf8Target, pDataLayout = utf8DataLayout)
            {
                var pExport = (delegate* unmanaged<LLVMContext*, byte*, nuint, byte*, nuint, byte*, nuint, LLVMModule*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_Create);
                return pExport(context, pName, (nuint)name.Length, pTarget, (nuint)utf8Target.Length, pDataLayout, (nuint)utf8DataLayout.Length);
            }
        }

        public LLVMValueRef GetNamedAlias(ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMModule*, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_GetNamedAlias);
                return pExport(Handle, pName, (nuint)name.Length);
            }
        }

        public LLVMValueRef GetNamedFunction(ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMModule*, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_GetNamedFunction);
                return pExport(Handle, pName, (nuint)name.Length);
            }
        }

        public LLVMValueRef GetNamedGlobal(ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMModule*, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_GetNamedGlobal);
                return pExport(Handle, pName, (nuint)name.Length);
            }
        }

        public LLVMValueRef AddAlias(ReadOnlySpan<byte> name, LLVMTypeRef valueType, LLVMValueRef aliasee)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMModule*, byte*, nuint, LLVMType*, LLVMValue*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_AddAlias);
                return pExport(Handle, pName, (nuint)name.Length, valueType, aliasee);
            }
        }

        public LLVMValueRef AddFunction(ReadOnlySpan<byte> name, LLVMTypeRef type)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMModule*, byte*, nuint, LLVMType*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_AddFunction);
                return pExport(Handle, pName, (nuint)name.Length, type);
            }
        }

        public LLVMValueRef AddGlobal(ReadOnlySpan<byte> name, LLVMTypeRef type, LLVMValueRef initializer)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMModule*, byte*, nuint, LLVMType*, LLVMValue*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_AddGlobal);
                return pExport(Handle, pName, (nuint)name.Length, type, initializer);
            }
        }

        public void Verify()
        {
            var pExport = (delegate* unmanaged<LLVMModule*, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_Verify);
            pExport(Handle);
        }

        public void PrintToFile(string path)
        {
            byte[] utf8Path = Encoding.UTF8.GetBytes(path);
            fixed (byte* pPath = utf8Path)
            {
                var pExport = (delegate* unmanaged<LLVMModule*, byte*, nuint, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_PrintToFile);
                pExport(Handle, pPath, (nuint)utf8Path.Length);
            }
        }

        public void WriteBitcodeToFile(string path)
        {
            byte[] utf8Path = Encoding.UTF8.GetBytes(path);
            fixed (byte* pPath = utf8Path)
            {
                var pExport = (delegate* unmanaged<LLVMModule*, byte*, nuint, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMModule_WriteBitcodeToFile);
                pExport(Handle, pPath, (nuint)utf8Path.Length);
            }
        }

        public static implicit operator LLVMModuleRef(LLVMModule* value) => new(value);
        public static implicit operator LLVMModule*(LLVMModuleRef value) => value.Handle;
    }

    public struct LLVMType { }

    public readonly unsafe partial struct LLVMTypeRef(LLVMType* handle) : IEquatable<LLVMTypeRef>
    {
        public readonly LLVMType* Handle = handle;

        public LLVMContextRef Context =>
            ((delegate* unmanaged<LLVMType*, LLVMContext*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_GetContext))(Handle);

        public LLVMTypeRef ReturnType =>
            ((delegate* unmanaged<LLVMType*, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_GetReturnType))(Handle);

        public ReadOnlySpan<LLVMTypeRef> ParamTypes
        {
            get
            {
                var pExport = (delegate* unmanaged<LLVMType*, nuint*, LLVMTypeRef*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_GetParamTypes);
                nuint paramCount;
                LLVMTypeRef* paramTypes = pExport(Handle, &paramCount);
                return new Span<LLVMTypeRef>(paramTypes, (int)paramCount);
            }
        }

        public static LLVMTypeRef GetPointer(LLVMContext* context) =>
            ((delegate* unmanaged<LLVMContext*, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_GetPointer))(context);

        public static LLVMType* GetInt(LLVMContext* context, int bitCount) =>
            ((delegate* unmanaged<LLVMContext*, int, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_GetInt))(context, bitCount);

        public static LLVMType* GetFloat(LLVMContext* context) =>
            ((delegate* unmanaged<LLVMContext*, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_GetFloat))(context);

        public static LLVMType* GetDouble(LLVMContext* context) =>
            ((delegate* unmanaged<LLVMContext*, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_GetDouble))(context);

        public static LLVMType* GetVoid(LLVMContext* context) =>
            ((delegate* unmanaged<LLVMContext*, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_GetVoid))(context);

        public static LLVMType* CreateFunction(LLVMType* result, ReadOnlySpan<LLVMTypeRef> parameters)
        {
            fixed (LLVMTypeRef* pParameters = parameters)
            {
                var pExport = (delegate* unmanaged<LLVMType*, LLVMTypeRef*, nuint, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_CreateFunction);
                return pExport(result, pParameters, (nuint)parameters.Length);
            }
        }

        public static LLVMTypeRef CreateStruct(LLVMContextRef context, ReadOnlySpan<LLVMTypeRef> elements, bool packed)
        {
            fixed (LLVMTypeRef* pElements = elements)
            {
                var pExport = (delegate* unmanaged<LLVMContext*, LLVMTypeRef*, nuint, int, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_CreateStruct);
                return pExport(context, pElements, (nuint)elements.Length, packed ? 1 : 0);
            }
        }

        public static LLVMType* CreateArray(LLVMType* elementType, ulong elementCount) =>
            ((delegate* unmanaged<LLVMType*, ulong, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMType_CreateArray))(elementType, elementCount);

        public static implicit operator LLVMTypeRef(LLVMType* value) => new(value);
        public static implicit operator LLVMType*(LLVMTypeRef value) => value.Handle;

        public override bool Equals(object obj) => obj is LLVMTypeRef @ref && Equals(@ref);
        public bool Equals(LLVMTypeRef other) => Handle == other.Handle;
        public override int GetHashCode() => (int)((nuint)Handle >> 2);

        public static bool operator ==(LLVMTypeRef left, LLVMTypeRef right) => left.Equals(right);
        public static bool operator !=(LLVMTypeRef left, LLVMTypeRef right) => !(left == right);
    }

    public struct LLVMValue { }

    public readonly unsafe partial struct LLVMValueRef(LLVMValue* handle)
    {
        public readonly LLVMValue* Handle = handle;

        public LLVMTypeRef TypeOf =>
            ((delegate* unmanaged<LLVMValue*, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_TypeOf))(Handle);

        public LLVMBasicBlockRef AppendBasicBlock(ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMValue*, byte*, nuint, LLVMBasicBlock*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_AppendBasicBlock);
                return pExport(Handle, pName, (nuint)name.Length);
            }
        }

        public void AddFunctionAttribute(ReadOnlySpan<byte> name, string value)
        {
            LLVMAttributeRef attribute = LLVMAttributeRef.Create(TypeOf.Context, name, value);

            var pExport = (delegate* unmanaged<LLVMValue*, LLVMAttributeIndex, LLVMAttributeImpl*, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_AddAttributeAtIndex);
            pExport(Handle, LLVMAttributeIndex.LLVMAttributeFunctionIndex, attribute);
        }

        public LLVMValueRef GetParam(uint index) =>
            ((delegate* unmanaged<LLVMValue*, uint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_GetParam))(Handle, index);

        public int ParamsCount =>
            ((delegate* unmanaged<LLVMValue*, int>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_GetParamCount))(Handle);

        public LLVMTypeRef GetValueType() =>
            ((delegate* unmanaged<LLVMValue*, LLVMType*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_GetValueType))(Handle);

        public uint Alignment
        {
            set => ((delegate* unmanaged<LLVMValue*, ulong, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_SetAlignment))(Handle, value);
        }

        public string Section
        {
            set
            {
                byte[] utf8Section = Encoding.UTF8.GetBytes(value);
                fixed (byte* pSection = utf8Section)
                {
                    var pExport = (delegate* unmanaged<LLVMValue*, byte*, nuint, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_SetSection);
                    pExport(Handle, pSection, (nuint)utf8Section.Length);
                }
            }
        }

        public LLVMLinkage Linkage
        {
            set => ((delegate* unmanaged<LLVMValue*, LLVMLinkage, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_SetLinkage))(Handle, value);
        }

        public LLVMValueRef Aliasee
        {
            set => ((delegate* unmanaged<LLVMValue*, LLVMValue*, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_SetAliasee))(Handle, value);
        }

        public static LLVMValueRef CreateConstNull(LLVMTypeRef type) =>
            ((delegate* unmanaged<LLVMType*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_CreateConstNull))(type);

        public static LLVMValueRef CreateConstInt(LLVMTypeRef type, ulong value) =>
            ((delegate* unmanaged<LLVMType*, ulong, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_CreateConstInt))(type, value);

        public static LLVMValueRef CreateConstIntToPtr(LLVMValue* value) =>
            ((delegate* unmanaged<LLVMValue*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_CreateConstIntToPtr))(value);

        public static LLVMValueRef CreateConstGEP(LLVMValue* address, int offset) =>
            ((delegate* unmanaged<LLVMValue*, int, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_CreateConstGEP))(address, offset);

        public static LLVMValueRef CreateConstStruct(LLVMTypeRef type, ReadOnlySpan<LLVMValueRef> elements)
        {
            fixed (LLVMValueRef* pElements = elements)
            {
                var pExport = (delegate* unmanaged<LLVMType*, LLVMValueRef*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_CreateConstStruct);
                return pExport(type, pElements, (nuint)elements.Length);
            }
        }

        public static LLVMValueRef CreateConstArray(LLVMTypeRef type, ReadOnlySpan<LLVMValueRef> elements)
        {
            fixed (LLVMValueRef* pElements = elements)
            {
                var pExport = (delegate* unmanaged<LLVMType*, LLVMValueRef*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMValue_CreateConstArray);
                return pExport(type, pElements, (nuint)elements.Length);
            }
        }

        public static implicit operator LLVMValueRef(LLVMValue* value) => new(value);
        public static implicit operator LLVMValue*(LLVMValueRef value) => value.Handle;
    }

    public struct LLVMBasicBlock { }

    public readonly unsafe partial struct LLVMBasicBlockRef(LLVMBasicBlock* handle)
    {
        public readonly LLVMBasicBlock* Handle = handle;

        public LLVMValueRef Parent =>
            ((delegate* unmanaged<LLVMBasicBlock*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBasicBlock_GetParent))(Handle);

        public void MoveAfter(LLVMBasicBlockRef block) =>
            ((delegate* unmanaged<LLVMBasicBlock*, LLVMBasicBlock*, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBasicBlock_MoveAfter))(Handle, block);

        public static implicit operator LLVMBasicBlockRef(LLVMBasicBlock* value) => new(value);
        public static implicit operator LLVMBasicBlock*(LLVMBasicBlockRef value) => value.Handle;
    }

    public struct LLVMBuilder { }

    public readonly unsafe partial struct LLVMBuilderRef(LLVMBuilder* handle) : IDisposable
    {
        public readonly LLVMBuilder* Handle = handle;

        public LLVMBasicBlockRef InsertBlock =>
            ((delegate* unmanaged<LLVMBuilder*, LLVMBasicBlock*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_GetInsertBlock))(Handle);

        public static LLVMBuilderRef Create(LLVMContextRef context) =>
            ((delegate* unmanaged<LLVMContext*, LLVMBuilder*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_Create))(context);

        public LLVMValueRef BuildICmp(LLVMIntPredicate predicate, LLVMValueRef left, LLVMValueRef right, ReadOnlySpan<byte> name = default)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMBuilder*, LLVMIntPredicate, LLVMValue*, LLVMValue*, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildICmp);
                return pExport(Handle, predicate, left, right, pName, (nuint)name.Length);
            }
        }

        public LLVMValue* BuildCondBr(LLVMValueRef cond, LLVMBasicBlockRef trueDest, LLVMBasicBlockRef falseDest)
        {
            var pExport = (delegate* unmanaged<LLVMBuilder*, LLVMValue*, LLVMBasicBlock*, LLVMBasicBlock*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildCondBr);
            return pExport(Handle, cond, trueDest, falseDest);
        }

        public LLVMValueRef BuildBr(LLVMBasicBlockRef dest)
        {
            var pExport = (delegate* unmanaged<LLVMBuilder*, LLVMBasicBlock*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildBr);
            return pExport(Handle, dest);
        }

        internal LLVMValueRef BuildGEP(LLVMValueRef address, LLVMValueRef offset, ReadOnlySpan<byte> name = default)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMBuilder*, LLVMValue*, LLVMValue*, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildGEP);
                return pExport(Handle, address, offset, pName, (nuint)name.Length);
            }
        }

        public LLVMValueRef BuildPtrToInt(LLVMValueRef value, LLVMType* type, ReadOnlySpan<byte> name = default)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMBuilder*, LLVMValue*, LLVMType*, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildPtrToInt);
                return pExport(Handle, value, type, pName, (nuint)name.Length);
            }
        }

        public LLVMValueRef BuildIntToPtr(LLVMValueRef value, ReadOnlySpan<byte> name = default)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMBuilder*, LLVMValue*, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildIntToPtr);
                return pExport(Handle, value, pName, (nuint)name.Length);
            }
        }

        public LLVMValueRef BuildPointerCast(LLVMValueRef value, LLVMTypeRef type, ReadOnlySpan<byte> name = default)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMBuilder*, LLVMValue*, LLVMType*, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildPointerCast);
                return pExport(Handle, value, type, pName, (nuint)name.Length);
            }
        }

        internal LLVMValueRef BuildCall(LLVMTypeRef funcType, LLVMValueRef callee, ReadOnlySpan<LLVMValueRef> args, ReadOnlySpan<byte> name = default)
        {
            fixed (LLVMValueRef* pArgs = args)
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMBuilder*, LLVMType*, LLVMValue*, LLVMValueRef*, nuint, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildCall);
                return pExport(Handle, funcType, callee, pArgs, (nuint)args.Length, pName, (nuint)name.Length);
            }
        }

        public LLVMValueRef BuildLoad(LLVMType* type, LLVMValueRef address, ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                var pExport = (delegate* unmanaged<LLVMBuilder*, LLVMType*, LLVMValue*, byte*, nuint, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildLoad);
                return pExport(Handle, type, address, pName, (nuint)name.Length);
            }
        }

        public LLVMValueRef BuildRetVoid() => BuildRet(null);

        public LLVMValueRef BuildRet(LLVMValueRef result) =>
            ((delegate* unmanaged<LLVMBuilder*, LLVMValue*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildRet))(Handle, result);

        public LLVMValueRef BuildUnreachable() =>
            ((delegate* unmanaged<LLVMBuilder*, LLVMValue*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_BuildUnreachable))(Handle);

        public void PositionAtEnd(LLVMBasicBlockRef block) =>
            ((delegate* unmanaged<LLVMBuilder*, LLVMBasicBlock*, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_PositionAtEnd))(Handle, block);

        // TODO-LLVM: the frequent creation and destruction of builders is a throughput problem.
        public void Dispose() =>
            ((delegate* unmanaged<LLVMBuilder*, void>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMBuilder_Dispose))(Handle);

        public static implicit operator LLVMBuilderRef(LLVMBuilder* value) => new(value);
        public static implicit operator LLVMBuilder*(LLVMBuilderRef value) => value.Handle;
    }

    public struct LLVMAttributeImpl { }

    public readonly unsafe partial struct LLVMAttributeRef(LLVMAttributeImpl* handle)
    {
        public readonly LLVMAttributeImpl* Handle = handle;

        public static LLVMAttributeRef Create(LLVMContextRef context, ReadOnlySpan<byte> name, string value)
        {
            byte[] utf8Value = Encoding.UTF8.GetBytes(value);
            fixed (byte* pName = name, pValue = utf8Value)
            {
                var pExport = (delegate* unmanaged<LLVMContext*, byte*, nuint, byte*, nuint, LLVMAttributeImpl*>)CorInfoImpl.GetJitExport(CJAI_LLVMInterop_LLVMAttribute_Create);
                return pExport(context, pName, (nuint)name.Length, pValue, (nuint)utf8Value.Length);
            }
        }

        public static implicit operator LLVMAttributeRef(LLVMAttributeImpl* value) => new(value);
        public static implicit operator LLVMAttributeImpl*(LLVMAttributeRef value) => value.Handle;

    }
}
