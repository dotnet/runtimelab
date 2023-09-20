// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Internal.Mint.Abstraction;

// keep in sync with mint-abstraction-nativeaot.h
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Itf
{
    // FIXME: none of these should be IntPtr, they should be actual types

    /* FIXME: replace this by some actual MonoImage abstraction*/
    public IntPtr /* MonoImage **/ placeholder_image;

    /* transform.c */
    public delegate* unmanaged<int, IntPtr, MonoTypeInstanceAbstractionNativeAot*> get_type_from_stack; //MonoType * (*get_type_from_stack) (int type, MonoClass *klass);
    public IntPtr type_has_references; // gboolean (*type_has_references)(MonoType *type);
    public delegate* unmanaged<IntPtr/*InterpMethod* */, UIntPtr, IntPtr> imethod_alloc; // gpointer (*imethod_alloc0) (TransformData *td, size_t size);
    public IntPtr interp_get_method; // MonoMethod* (*interp_get_method) (MonoMethod *method, guint32 token, MonoImage *image, MonoGenericContext *generic_context, MonoError *error);

    /* mono_defaults */
    public delegate* unmanaged<MonoTypeInstanceAbstractionNativeAot*> get_default_byval_type_void; // MonoType * (*get_default_byval_type_void)(void);
    public delegate* unmanaged<MonoTypeInstanceAbstractionNativeAot*> get_default_byval_type_int; // MonoType * (*get_default_byval_type_int)(void);

    public IntPtr get_default_class_string_class; // MonoClass * (*get_default_class_string_class) (void);
    public IntPtr get_default_class_int_class; // MonoClass * (*get_default_class_int_class) (void);
    public IntPtr get_default_class_array_class; // MonoClass * (*get_default_class_array_class) (void);
    /* System.Type */
    public IntPtr get_default_class_systemtype_class; // MonoClass * (*get_default_class_systemtype_class) (void);
    /* System.RuntimeType - FIXME: audit what this is used for */
    public IntPtr get_default_class_runtimetype_class; // MonoClass * (*get_default_class_runtimetype_class) (void);
    /* System.RutnimeTypeHandle - FIXME: seems to be used for passing data to the interp, rewrite */
    public IntPtr get_default_class_typehandle_class; // MonoClass * (*get_default_class_typehandle_class) (void);
    public delegate* unmanaged<MonoMemPoolInstanceAbstraction*> create_mem_pool; // MonoMemPool * (*create_mem_pool) (void);
    public delegate* unmanaged<MonoMethodInstanceAbstractionNativeAot*, MonoMemManagerInstanceAbstraction*> m_method_get_mem_manager;


    /* opaque type instances */
    public delegate* unmanaged<IntPtr /* MonoType */, IntPtr/*MonoTypeInstanceAbstraction* */> get_MonoType_inst; // MonoTypeInstanceAbstractionNativeAot * (*get_MonoType_inst) (MonoType *self);
    public delegate* unmanaged<IntPtr /* MonoMethod* */, IntPtr/*MonoMethodInstanceAbstractionNativeAot**/> get_MonoMethod_inst; // MonoMethodInstanceAbstractionNativeAot * (*get_MonoMethod_inst) (MonoMethod *self);
    public delegate* unmanaged<IntPtr /* MonoMethodHeader* */, IntPtr /* MonoMethodHeaderInstanceAbstractionNativeAot* */> get_MonoMethodHeader_inst; // MonoMethodHeaderInstanceAbstractionNativeAot * (*get_MonoMethodHeader_inst) (MonoMethodHeader *header);

    public delegate* unmanaged<IntPtr /* MonoMethodSignature* */, IntPtr /* MonoMethodSignatureInstanceAbstractionNativeAot* */> get_MonoMethodSignature_inst; // MonoMethodSignatureInstanceAbstractionNativeAot * (*get_MonoMethodSignature_inst) (MonoMethodSignature *self);

    public delegate* unmanaged<IntPtr, IntPtr> get_MonoMemPool_inst; // MonoMemPoolInstanceAbstraction * (*get_MonoMemPool_inst) (MonoMemPool *self);

    [UnmanagedCallersOnly]
    internal static IntPtr unwrapTransparentAbstraction(IntPtr self) => self;

    // keep in sync with transform.c
    enum MintStackType : int
    {
        STACK_TYPE_I4 = 0,
        STACK_TYPE_I8 = 1,
        STACK_TYPE_R4 = 2,
        STACK_TYPE_R8 = 3,
        STACK_TYPE_O = 4,
        STACK_TYPE_VT = 5,
        STACK_TYPE_MP = 6,
        STACK_TYPE_F = 7,
    }

    static MintStackType IntPtrStackType = IntPtr.Size == 4 ? MintStackType.STACK_TYPE_I4 : MintStackType.STACK_TYPE_I8;

#pragma warning disable IDE0060 // unused parameter _klass
    [UnmanagedCallersOnly]
    internal static unsafe MonoTypeInstanceAbstractionNativeAot* mintGetTypeFromStack(int type, IntPtr /*MonoClass* */_klass)
    {
        // see the mono mint_get_type_from_stack
        switch ((MintStackType)type)
        {
            case MintStackType.STACK_TYPE_I4: return Mint.GlobalMintTypeSystem.GetMonoType((RuntimeType)typeof(int)).Value;
            case MintStackType.STACK_TYPE_I8: return Mint.GlobalMintTypeSystem.GetMonoType((RuntimeType)typeof(long)).Value;
            case MintStackType.STACK_TYPE_R4: return Mint.GlobalMintTypeSystem.GetMonoType((RuntimeType)typeof(float)).Value;
            case MintStackType.STACK_TYPE_R8: return Mint.GlobalMintTypeSystem.GetMonoType((RuntimeType)typeof(double)).Value;
            // FIXME: STACK_TYPE_O and STACK_TYPE_VT need the MonoClass to do something
            //case MintStackType.STACK_TYPE_O: return Mint.GlobalMintTypeSystem.GetMonoType(typeof(object));
            //case MintStackType.STACK_TYPE_VT: return GlobalMintTypeSystem.GetMonoType(typeof(IntPtr));
            case MintStackType.STACK_TYPE_MP: return Mint.GlobalMintTypeSystem.GetMonoType((RuntimeType)typeof(IntPtr)).Value;
            case MintStackType.STACK_TYPE_F: return Mint.GlobalMintTypeSystem.GetMonoType((RuntimeType)typeof(IntPtr)).Value;
            default:
                throw new InvalidOperationException($"can't handle MintStackType value {type}");
        }
    }
#pragma warning restore IDE0060
}
