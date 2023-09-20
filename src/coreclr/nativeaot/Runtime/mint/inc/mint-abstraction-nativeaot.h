#ifndef _MINT_ABSTRACTION_NATIVEAOT_H
#define _MINT_ABSTRACTION_NATIVEAOT_H

typedef struct _MonoTypeInstanceAbstractionNativeAot {

    int32_t type_code;
    uint8_t is_byref;
    MonoGCHandle gcHandle;
} MonoTypeInstanceAbstractionNativeAot;

typedef struct _MonoMethodInstanceAbstractionVTable {
    MonoMethodSignature *(*get_signature)(MonoMethod *self);
    MonoMethodHeader *(*get_header)(MonoMethod *self);
} MonoMethodInstanceAbstractionVTable;

typedef struct _MonoMethodInstanceAbstractionNativeAot {
    MonoMethodInstanceAbstractionVTable *vtable;
    const char *name;
    MonoClass *klass;
    int8_t is_dynamic;

    MonoGCHandle gcHandle;
} MonoMethodInstanceAbstractionNativeAot;

typedef struct _MonoMethodHeaderInstanceAbstractionVTable {
    MonoType * (*get_local_sig)(MonoMethodHeader *self, int32_t i);
    // TODO: this will likely pin something in managed.  Figure out a way to tell us when it's safe to unpin
    const uint8_t * (*get_code)(MonoMethodHeader *self);
    int32_t (*get_ip_offset)(MonoMethodHeader *self, const uint8_t *ip);
} MonoMethodHeaderInstanceAbstractionVTable;

typedef struct _MonoMethodHeaderInstanceAbstractionNativeAot MonoMethodHeaderInstanceAbstractionNativeAot;
struct _MonoMethodHeaderInstanceAbstractionNativeAot {
    MonoMethodHeaderInstanceAbstractionVTable *vtable;
    int32_t code_size;
    int32_t max_stack;
    int32_t num_locals;
    int32_t num_clauses;
    int8_t init_locals;

    MonoGCHandle gcHandle;
} ;

typedef struct _MonoMethodSignatureInstanceAbstractionVTable {
    MonoType ** (*method_params)(MonoMethodSignature *self);
    MonoType * (*ret_ult)(MonoMethodSignature *self);
} MonoMethodSignatureInstanceAbstractionVTable;

typedef struct _MonoMethodSignatureInstanceAbstractionNativeAot MonoMethodSignatureInstanceAbstractionNativeAot;
struct _MonoMethodSignatureInstanceAbstractionNativeAot {
    MonoMethodSignatureInstanceAbstractionVTable *vtable;
    int32_t param_count;

    int8_t hasthis;

    MonoGCHandle gcHandle;
    MonoType** MethodParamsTypes;
};

typedef struct _TransformData TransformData; // FIXME: separate the interp-aware abstractions from the metadata ones
typedef struct InterpMethod InterpMethod; // FIXME: separate the interp-aware abstractions from the metadata ones

typedef struct _MonoMemPoolInstanceAbstractionVTable {
    void (*destroy)(MonoMemPool *self);
    void* (*alloc0)(MonoMemPool *self, uint32_t size);
} MonoMemPoolInstanceAbstractionVTable;

typedef struct _MonoMemPoolInsaanceAbstractionNativeAot {
    MonoMemPoolInstanceAbstractionVTable *vtable;
    MonoGCHandle gcHandle;
} MonoMemPoolInstanceAbstractionNativeAot;

typedef struct _MintAbstractionNativeAot {
    /* FIXME: replace this by some actual MonoImage abstraction*/
    MonoImage *placeholder_image;

    /* transform.c */
    MonoType * (*get_type_from_stack) (int type, MonoClass *klass);
    gboolean (*type_has_references)(MonoType *type);
    gpointer (*imethod_alloc0) (InterpMethod *td, size_t size);
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

    MonoMemPool * (*create_mem_pool) (void);
    MonoMemoryManager * (* m_method_get_mem_manager) (MonoMethod *method);


    /* opaque type instances */
    MonoTypeInstanceAbstractionNativeAot * (*get_MonoType_inst) (MonoType *self);
    MonoMethodInstanceAbstractionNativeAot * (*get_MonoMethod_inst) (MonoMethod *self);
    MonoMethodHeaderInstanceAbstractionNativeAot * (*get_MonoMethodHeader_inst) (MonoMethodHeader *header);

    MonoMethodSignatureInstanceAbstractionNativeAot * (*get_MonoMethodSignature_inst) (MonoMethodSignature *self);

    MonoMemPoolInstanceAbstractionNativeAot * (*get_MonoMemPool_inst) (MonoMemPool *self);
} MintAbstractionNativeAot;

MintAbstractionNativeAot *mint_itf(void);

// FIXME: for testing purposes only
MonoMethodInstanceAbstractionNativeAot *mint_method_abstraction_placeholder(void);

#endif/*_MINT_ABSTRACTION_H*/
