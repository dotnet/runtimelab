#include <stdatomic.h>
#include <config.h>
#include <glib.h>

#include <monoshim/missing-symbols.h>
#include <mint-abstraction-nativeaot.h>

static MonoMethodHeader *
mint_method_abstraction_placeholder_get_header(MonoMethod *self)
{
    static _Atomic(MonoMethodHeaderInstanceAbstractionNativeAot *) stored_header = NULL;
    MonoMethodHeaderInstanceAbstractionNativeAot *header;
    while (G_UNLIKELY(!(header = atomic_load(&stored_header)))) {
        MonoMethodHeaderInstanceAbstractionNativeAot *newheader = g_new0(MonoMethodHeaderInstanceAbstractionNativeAot, 1);
        newheader->code_size = 0;
        newheader->max_stack = 0;
        newheader->num_locals = 0;
        newheader->num_clauses = 0;
        newheader->init_locals = 0;
        newheader->get_local_sig = NULL;
        newheader->get_code = NULL;
        newheader->get_ip_offset = NULL;
        MonoMethodHeaderInstanceAbstractionNativeAot *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_header, &expected, newheader)) {
            g_free (newheader);
        }
    }
    return (MonoMethodHeader*)header;
}

MonoMethodInstanceAbstractionNativeAot *mint_method_abstraction_placeholder(void)
{
    static _Atomic(MonoMethodInstanceAbstractionNativeAot *) stored_method = NULL;
    MonoMethodInstanceAbstractionNativeAot *method;
    while (G_UNLIKELY(!(method = atomic_load(&stored_method)))) {
        MonoMethodInstanceAbstractionNativeAot *newmethod = g_new0(MonoMethodInstanceAbstractionNativeAot, 1);
        newmethod->name = "placeholder";
        newmethod->klass = NULL;
        newmethod->get_signature = NULL;
        newmethod->get_header = &mint_method_abstraction_placeholder_get_header;
        MonoMethodInstanceAbstractionNativeAot *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_method, &expected, newmethod)) {
            g_free (newmethod);
        }
    }
    return method;
}


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

static MonoMethodInstanceAbstractionNativeAot *
mint_get_MonoMethod_inst(MonoMethod *self)
{
    return (MonoMethodInstanceAbstractionNativeAot *)self;
}

static MonoMethodHeaderInstanceAbstractionNativeAot *
mint_get_MonoMethodHeader_inst(MonoMethodHeader *self)
{
    return (MonoMethodHeaderInstanceAbstractionNativeAot *)self;
}

MintAbstractionNativeAot *mint_itf(void) {
    static _Atomic(MintAbstractionNativeAot *) stored_itf = NULL;

    MintAbstractionNativeAot *itf;
    while (G_UNLIKELY(!(itf = atomic_load(&stored_itf)))) {
        MintAbstractionNativeAot *newitf = g_new0(MintAbstractionNativeAot, 1);

        newitf->get_default_byval_type_void = mint_get_default_byval_type_void;
        newitf->get_MonoMethod_inst = mint_get_MonoMethod_inst;
        newitf->get_MonoMethodHeader_inst = mint_get_MonoMethodHeader_inst;

        MintAbstractionNativeAot *expected= NULL;
        if (!atomic_compare_exchange_weak(&stored_itf, &expected, newitf)) {
            g_free (newitf);
        }
    }
    return itf;
}
