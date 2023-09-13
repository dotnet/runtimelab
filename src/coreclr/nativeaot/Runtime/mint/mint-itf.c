#include <stdatomic.h>
#include <config.h>
#include <glib.h>

#include <monoshim/missing-symbols.h>
#include <mint-abstraction-nativeaot.h>

#include "mint-itf.h"

struct _MonoType {
    MonoGCHandle gchandle;
    MonoTypeEnum type;
};

static MonoType*
mint_get_default_byval_type_void(void)
{
    static _Atomic(MonoType *) stored_type = NULL;
    MonoType *type;
    while (G_UNLIKELY(!(type = atomic_load(&stored_type)))) {
        MonoType *newtype = g_new0(MonoType, 1);
        newtype->type = MONO_TYPE_VOID;
        MonoType *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_type, &expected, newtype)) {
            g_free (newtype);
        }
    }
    return type;
}

static MonoType*
mint_get_default_byval_type_int32(void)
{
    static _Atomic(MonoType *) stored_type = NULL;
    MonoType *type;
    while (G_UNLIKELY(!(type = atomic_load(&stored_type)))) {
        MonoType *newtype = g_new0(MonoType, 1);
        newtype->type = MONO_TYPE_I4;
        MonoType *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_type, &expected, newtype)) {
            g_free (newtype);
        }
    }
    return type;
}

static MonoType*
mint_method_signature_abstraction_ret_ult(MonoMethodSignature *self)
{
    // TODO
    return mint_get_default_byval_type_void();
}



static MonoMethodSignature *
mint_method_abstraction_placeholder_get_signature (MonoMethod *self)
{
    static _Atomic(MonoMethodSignatureInstanceAbstractionNativeAot *) stored_signature = NULL;
    MonoMethodSignatureInstanceAbstractionNativeAot *signature;
    while (G_UNLIKELY(!(signature = atomic_load(&stored_signature)))) {
        MonoMethodSignatureInstanceAbstractionNativeAot *newsignature = g_new0(MonoMethodSignatureInstanceAbstractionNativeAot, 1);
        newsignature->param_count = 0;
        newsignature->hasthis = 0;
        newsignature->ret_ult = &mint_method_signature_abstraction_ret_ult;
        MonoMethodSignatureInstanceAbstractionNativeAot *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_signature, &expected, newsignature)) {
            g_free (newsignature);
        }
    }
    return (MonoMethodSignature*)signature;
}

static uint8_t placeholder_code_bytes[] = {
    // the placeholder method is:
    0x1F, 0x2A, // (ldc.i4.s 42)
    0x26, // (pop)
    0x2A, // (ret)
};


static const uint8_t *
mint_method_abstraction_placeholder_get_code (MonoMethodHeader *self)
{
    return &placeholder_code_bytes[0];
}

static int32_t
mint_method_abstraction_placeholder_get_ip_offset (MonoMethodHeader *self, const uint8_t *ip)
{
    return ip - &placeholder_code_bytes[0];
}

static MonoMethodHeader *
mint_method_abstraction_placeholder_get_header(MonoMethod *self)
{
    static _Atomic(MonoMethodHeaderInstanceAbstractionNativeAot *) stored_header = NULL;
    MonoMethodHeaderInstanceAbstractionNativeAot *header;
    while (G_UNLIKELY(!(header = atomic_load(&stored_header)))) {
        MonoMethodHeaderInstanceAbstractionNativeAot *newheader = g_new0(MonoMethodHeaderInstanceAbstractionNativeAot, 1);
        newheader->code_size = 4; // see mint_method_abstraction_placeholder_get_code
        newheader->max_stack = 8; // it's really 1, but pretend like we're a tiny ECMA335 header
        newheader->num_locals = 0;
        newheader->num_clauses = 0;
        newheader->init_locals = 0;
        newheader->get_local_sig = NULL;
        newheader->get_code = &mint_method_abstraction_placeholder_get_code;
        newheader->get_ip_offset = &mint_method_abstraction_placeholder_get_ip_offset;
        MonoMethodHeaderInstanceAbstractionNativeAot *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_header, &expected, newheader)) {
            g_free (newheader);
        }
    }
    return (MonoMethodHeader*)header;
}

void
mono_metadata_free_mh (MonoMethodHeader *header) {
    // no-op
    // FIXME: this might dispose of somethign in managed,
    // but we should be careful that we're refcounting if we're giving the same
    // header to multiple callers
 }


MonoMethodInstanceAbstractionNativeAot *mint_method_abstraction_placeholder(void)
{
    static _Atomic(MonoMethodInstanceAbstractionNativeAot *) stored_method = NULL;
    MonoMethodInstanceAbstractionNativeAot *method;
    while (G_UNLIKELY(!(method = atomic_load(&stored_method)))) {
        MonoMethodInstanceAbstractionNativeAot *newmethod = g_new0(MonoMethodInstanceAbstractionNativeAot, 1);
        newmethod->name = "placeholder";
        newmethod->klass = NULL;
        newmethod->get_signature = &mint_method_abstraction_placeholder_get_signature;
        newmethod->get_header = &mint_method_abstraction_placeholder_get_header;
        MonoMethodInstanceAbstractionNativeAot *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_method, &expected, newmethod)) {
            g_free (newmethod);
        }
    }
    return method;
}


#define WRAP_TY(type) type##InstanceAbstractionNativeAot
#define UNWRAP_FN_NAME(type) mint_get_##type##_inst
#define UNWRAP_FN_DECL(type) WRAP_TY(type)* UNWRAP_FN_NAME(type)(type *self)
#define UNWRAP_FN_IMPL(type) UNWRAP_FN_DECL(type) { return (WRAP_TY(type)*)self; }

static UNWRAP_FN_IMPL(MonoMethod);
static UNWRAP_FN_IMPL(MonoMethodHeader);
static UNWRAP_FN_IMPL(MonoMethodSignature);


// FIXME: this belongs in the transform abstraction
#define STACK_TYPE_I4 0
#define STACK_TYPE_I8 1
#define STACK_TYPE_R4 2
#define STACK_TYPE_R8 3
#define STACK_TYPE_O  4
#define STACK_TYPE_VT 5
#define STACK_TYPE_MP 6
#define STACK_TYPE_F  7

#if SIZEOF_VOID_P == 8
#define STACK_TYPE_I STACK_TYPE_I8
#else
#define STACK_TYPE_I STACK_TYPE_I4
#endif


static MonoType *
mint_get_type_from_stack (int type, MonoClass *klass)
{
    	switch (type) {
		case STACK_TYPE_I4: return mint_get_default_byval_type_int32();
#if 0
		case STACK_TYPE_I8: return m_class_get_byval_arg (mono_defaults.int64_class);
		case STACK_TYPE_R4: return m_class_get_byval_arg (mono_defaults.single_class);
		case STACK_TYPE_R8: return m_class_get_byval_arg (mono_defaults.double_class);
		case STACK_TYPE_O: return (klass && !m_class_is_valuetype (klass)) ? m_class_get_byval_arg (klass) : m_class_get_byval_arg (mono_defaults.object_class);
		case STACK_TYPE_VT: return m_class_get_byval_arg (klass);
		case STACK_TYPE_MP:
		case STACK_TYPE_F:
			return m_class_get_byval_arg (mono_defaults.int_class);
#endif
		default:
            g_error ("can't handle stack type %d", type);
	}
}

// FIXME: this doesn't belong here
#define MINT_TYPE_I1 0
#define MINT_TYPE_U1 1
#define MINT_TYPE_I2 2
#define MINT_TYPE_U2 3
#define MINT_TYPE_I4 4
#define MINT_TYPE_I8 5
#define MINT_TYPE_R4 6
#define MINT_TYPE_R8 7
#define MINT_TYPE_O  8
#define MINT_TYPE_VT 9
#define MINT_TYPE_VOID 10


static int
mint_get_mint_type_from_type(MonoType *type)
{
    // see the mono mono_mint_get_type
    // in particular, byref is a MONO_TYPE_I, not a MONO_TYPE_BYREF
    switch (type->type) {
        case MONO_TYPE_I4: return MINT_TYPE_I4;
        case MONO_TYPE_VOID: return MINT_TYPE_VOID;
        default:
            g_error("can't handle MonoTypeEnum value %d", type->type);
    }
}

gpointer
mint_imethod_alloc0 (TransformData *td, size_t size)
{
    // FIXME: this shouldn't go here. instead we should do like the mono version:
    //
    // if (td->rtm->method->dynamic)
    //   return mono_dyn_method_alloc0 (td->rtm->method, (guint)size);
    // else
    //   return mono_mem_manager_alloc0 (td->mem_manager, (guint)size);
    //
    // in particular we should have a memory manager tied to the InterpMethod that
    // is bound to the lifetime of the dynamic method delegate

    return g_malloc0 (size);
}

static MintAbstractionNativeAot * mint_itf_singleton;

MintAbstractionNativeAot *mint_itf(void) {
#if 0
    static _Atomic(MintAbstractionNativeAot *) stored_itf = NULL;

    MintAbstractionNativeAot *itf;
    while (G_UNLIKELY(!(itf = atomic_load(&stored_itf)))) {
        MintAbstractionNativeAot *newitf = g_new0(MintAbstractionNativeAot, 1);

        newitf->get_default_byval_type_void = mint_get_default_byval_type_void;
        newitf->get_MonoMethod_inst = UNWRAP_FN_NAME(MonoMethod);
        newitf->get_MonoMethodHeader_inst = UNWRAP_FN_NAME(MonoMethodHeader);
        newitf->get_MonoMethodSignature_inst = UNWRAP_FN_NAME(MonoMethodSignature);

        newitf->get_type_from_stack = &mint_get_type_from_stack;
        newitf->mono_mint_type = &mint_get_mint_type_from_type;

        newitf->imethod_alloc0 = &mint_imethod_alloc0;

        MintAbstractionNativeAot *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_itf, &expected, newitf)) {
            g_free (newitf);
        }
    }
    return itf;
#else
    return mint_itf_singleton;
#endif
}

void
mint_itf_initialize(MintAbstractionNativeAot* newitf)
{
    // TODO: these should all be set from managed

    newitf->get_default_byval_type_void = mint_get_default_byval_type_void;
    newitf->get_MonoMethod_inst = UNWRAP_FN_NAME(MonoMethod);
    newitf->get_MonoMethodHeader_inst = UNWRAP_FN_NAME(MonoMethodHeader);
    newitf->get_MonoMethodSignature_inst = UNWRAP_FN_NAME(MonoMethodSignature);

    newitf->get_type_from_stack = &mint_get_type_from_stack;
    newitf->mono_mint_type = &mint_get_mint_type_from_type;

    newitf->imethod_alloc0 = &mint_imethod_alloc0;


    mint_itf_singleton = newitf;

}
