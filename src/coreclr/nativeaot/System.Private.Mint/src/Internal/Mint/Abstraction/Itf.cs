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
    public IntPtr get_type_from_stack; //MonoType * (*get_type_from_stack) (int type, MonoClass *klass);
    public IntPtr mono_mint_type; // int (*mono_mint_type) (MonoType *type);
    public IntPtr get_arg_type_exact; // MonoType *(*get_arg_type_exact) (TransformData *td, int n, int *mt);
    public IntPtr type_has_references; // gboolean (*type_has_references)(MonoType *type);
    public IntPtr imethod_alloc; // gpointer (*imethod_alloc0) (TransformData *td, size_t size);
    public IntPtr load_arg; // void (*load_arg) (TransformData *td, int n);
    public IntPtr store_arg; // void (*store_arg) (TransformData *td, int n);
    public IntPtr interp_get_method; // MonoMethod* (*interp_get_method) (MonoMethod *method, guint32 token, MonoImage *image, MonoGenericContext *generic_context, MonoError *error);

    /* mono_defaults */
    public delegate* unmanaged<IntPtr/*MonoType**/> get_default_byval_type_void; // MonoType * (*get_default_byval_type_void)(void);
    public delegate* unmanaged<IntPtr/*MonoType**/> get_default_byval_type_int; // MonoType * (*get_default_byval_type_int)(void);

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
}
