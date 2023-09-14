#include <stdatomic.h>
#include <config.h>
#include <glib.h>

#include <monoshim/missing-symbols.h>
#include <mint-abstraction-nativeaot.h>

#include "mint-itf.h"

static MonoType*
mint_get_default_byval_type_void(void)
{
    static _Atomic(MonoType *) stored_type = NULL;
    MonoType *type;
    while (G_UNLIKELY(!(type = atomic_load(&stored_type)))) {
        MonoTypeInstanceAbstractionNativeAot *newtype = g_new0(MonoTypeInstanceAbstractionNativeAot, 1);
        newtype->type_code = MONO_TYPE_VOID;
        MonoType *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_type, &expected, (MonoType*)newtype)) {
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
        MonoTypeInstanceAbstractionNativeAot *newtype = g_new0(MonoTypeInstanceAbstractionNativeAot, 1);
        newtype->type_code = MONO_TYPE_I4;
        MonoType *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_type, &expected, (MonoType*)newtype)) {
            g_free (newtype);
        }
    }
    return type;
}

void
mono_metadata_free_mh (MonoMethodHeader *header) {
    // no-op
    // FIXME: this might dispose of somethign in managed,
    // but we should be careful that we're refcounting if we're giving the same
    // header to multiple callers
 }


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

    newitf->get_type_from_stack = &mint_get_type_from_stack;

    newitf->imethod_alloc0 = &mint_imethod_alloc0;


    mint_itf_singleton = newitf;

}
