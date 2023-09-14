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
    public delegate* unmanaged<MonoTypeInstanceAbstractionNativeAot*, int> mono_mint_type; // int (*mono_mint_type) (MonoType *type);
    public IntPtr get_arg_type_exact; // MonoType *(*get_arg_type_exact) (TransformData *td, int n, int *mt);
    public IntPtr type_has_references; // gboolean (*type_has_references)(MonoType *type);
    public IntPtr imethod_alloc; // gpointer (*imethod_alloc0) (TransformData *td, size_t size);
    public IntPtr load_arg; // void (*load_arg) (TransformData *td, int n);
    public IntPtr store_arg; // void (*store_arg) (TransformData *td, int n);
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


    /* opaque type instances */
    public delegate* unmanaged<IntPtr /* MonoType */, IntPtr/*MonoTypeInstanceAbstraction* */> get_MonoType_inst; // MonoTypeInstanceAbstractionNativeAot * (*get_MonoType_inst) (MonoType *self);
    public delegate* unmanaged<IntPtr /* MonoMethod* */, IntPtr/*MonoMethodInstanceAbstractionNativeAot**/> get_MonoMethod_inst; // MonoMethodInstanceAbstractionNativeAot * (*get_MonoMethod_inst) (MonoMethod *self);
    public delegate* unmanaged<IntPtr /* MonoMethodHeader* */, IntPtr /* MonoMethodHeaderInstanceAbstractionNativeAot* */> get_MonoMethodHeader_inst; // MonoMethodHeaderInstanceAbstractionNativeAot * (*get_MonoMethodHeader_inst) (MonoMethodHeader *header);

    public delegate* unmanaged<IntPtr /* MonoMethodSignature* */, IntPtr /* MonoMethodSignatureInstanceAbstractionNativeAot* */> get_MonoMethodSignature_inst; // MonoMethodSignatureInstanceAbstractionNativeAot * (*get_MonoMethodSignature_inst) (MonoMethodSignature *self);

    [UnmanagedCallersOnly]
    internal static IntPtr unwrapTransparentAbstraction(IntPtr self) => self;


    // keep in sync with interp-internals.h
    internal enum MintType : int
    {
        MINT_TYPE_I1 = 0,
        MINT_TYPE_U1 = 1,
        MINT_TYPE_I2 = 2,
        MINT_TYPE_U2 = 3,
        MINT_TYPE_I4 = 4,
        MINT_TYPE_I8 = 5,
        MINT_TYPE_R4 = 6,
        MINT_TYPE_R8 = 7,
        MINT_TYPE_O = 8,
        MINT_TYPE_VT = 9,
        MINT_TYPE_VOID = 10,
    }

    // FIXME: don't copy this
    private enum CorElementType : byte
    {
        ELEMENT_TYPE_END = 0x00,
        ELEMENT_TYPE_VOID = 0x01,
        ELEMENT_TYPE_BOOLEAN = 0x02,
        ELEMENT_TYPE_CHAR = 0x03,
        ELEMENT_TYPE_I1 = 0x04,
        ELEMENT_TYPE_U1 = 0x05,
        ELEMENT_TYPE_I2 = 0x06,
        ELEMENT_TYPE_U2 = 0x07,
        ELEMENT_TYPE_I4 = 0x08,
        ELEMENT_TYPE_U4 = 0x09,
        ELEMENT_TYPE_I8 = 0x0A,
        ELEMENT_TYPE_U8 = 0x0B,
        ELEMENT_TYPE_R4 = 0x0C,
        ELEMENT_TYPE_R8 = 0x0D,
        ELEMENT_TYPE_STRING = 0x0E,
        ELEMENT_TYPE_PTR = 0x0F,
        ELEMENT_TYPE_BYREF = 0x10,
        ELEMENT_TYPE_VALUETYPE = 0x11,
        ELEMENT_TYPE_CLASS = 0x12,
        ELEMENT_TYPE_VAR = 0x13,
        ELEMENT_TYPE_ARRAY = 0x14,
        ELEMENT_TYPE_GENERICINST = 0x15,
        ELEMENT_TYPE_TYPEDBYREF = 0x16,
        ELEMENT_TYPE_I = 0x18,
        ELEMENT_TYPE_U = 0x19,
        ELEMENT_TYPE_FNPTR = 0x1B,
        ELEMENT_TYPE_OBJECT = 0x1C,
        ELEMENT_TYPE_SZARRAY = 0x1D,
        ELEMENT_TYPE_MVAR = 0x1E,
        ELEMENT_TYPE_CMOD_REQD = 0x1F,
        ELEMENT_TYPE_CMOD_OPT = 0x20,
        ELEMENT_TYPE_INTERNAL = 0x21,
        ELEMENT_TYPE_MAX = 0x22,
        ELEMENT_TYPE_MODIFIER = 0x40,
        ELEMENT_TYPE_SENTINEL = 0x41,
        ELEMENT_TYPE_PINNED = 0x45,

    }

    [UnmanagedCallersOnly]

    internal static unsafe int mintGetMintTypeFromMonoType(MonoTypeInstanceAbstractionNativeAot* type)
    {
        // see the mono mono_mint_get_type
        // in particular, byref is a MONO_TYPE_I, not a MONO_TYPE_BYREF
        switch ((CorElementType)type->type_code)
        {
            case CorElementType.ELEMENT_TYPE_I4: return (int)MintType.MINT_TYPE_I4;
            case CorElementType.ELEMENT_TYPE_VOID: return (int)MintType.MINT_TYPE_VOID;
            default:
                throw new InvalidOperationException($"can't handle MonoTypeEnum value {(int)type->type_code}");
        }
    }

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
