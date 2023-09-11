#ifndef _MINT_ABSTRACTION_NATIVEAOT_H
#define _MINT_ABSTRACTION_NATIVEAOT_H

typedef struct _MonoTypeInstanceAbstractionNativeAot {
    int type;
} MonoTypeInstanceAbstractionNativeAot;

typedef struct _MonoMethodInstanceAbstractionNativeAot {
    const char *name;
    MonoClass *klass;
} MonoMethodInstanceAbstractionNativeAot;

typedef struct _MonoMethodHeaderInstanceAbstractionNativeAot {
    int code_size;
} MonoMethodHeaderInstanceAbstractionNativeAot;

typedef struct _MintAbstractionNativeAot {
    /* transform.c */
    MonoType * (*get_type_from_stack) (int type, MonoClass *klass);
    int (*mono_mint_type) (MonoType *type);
    int (*get_arg_type_exact) (TransformData *td, int n, int *mt);
    gboolean (*type_has_references)(MonoType *type);
    gpointer (*imethod_alloc0) (TransformData *td, size_t size);
    void (*load_arg) (TransformData *td, int n);
    void (*store_arg) (TransformData *td, int n);
    MonoMethod* (*interp_get_method) (MonoMethod *method, guint32 token, MonoImage *image, MonoGenericContext *generic_context, MonoError *error);

    /* mono_defaults */
    MonoType * (*get_default_byval_type_void)(void);
    MonoType * (*get_default_byval_type_int)(void);

    MonoClass * (*get_default_class_string_class) (void);
    MonoClass * (*get_default_class_int_class) (void);
    MonoClass * (*get_default_class_array_class) (void);
    /* System.Type */
    MonoClass * (*get_default_class_systemtype_class) (void);
    /* System.RuntimeType - FIXME: audit what this is used for */
    MonoClass * (*get_default_class_runtimetype_class) (void);
    /* System.RutnimeTypeHandle - FIXME: seems to be used for passing data to the interp, rewrite */
    MonoClass * (*get_default_class_typehandle_class) (void);


    /* opaque type instances */
    MonoTypeInstanceAbstractionNativeAot * (*get_MonoType_inst) (MonoType *self);
    MonoMethodInstanceAbstractionNativeAot * (*get_MonoMethod_inst) (MonoMethod *self);
    MonoMethodHeaderInstanceAbstractionNativeAot * (*get_MonoMethodHeader_inst) (MonoMethodHeader *header);
} MintAbstractionNativeAot;

MintAbstractionNativeAot *mint_itf(void);

#endif/*_MINT_ABSTRACTION_H*/
