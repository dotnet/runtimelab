#include <config.h>
#include <glib.h>
#include <mint-transform.h>
#include <monoshim/missing-symbols.h>
#include <mint-abstraction-nativeaot.h>
#include <mint-ee-abstraction-nativeaot.h>
#include <mint-imethod.h>

#include "mint-itf.h"
#include "mint.h"

static void
__attribute__((noreturn))
mint_missing (const char *func)
{
	g_error ("function %s is not implemented yet", func);
}

#define MISSING_FUNC() mint_missing(__func__)

// FIXME: this doesn't belong here
enum {
	INTERP_OPT_NONE = 0,
	INTERP_OPT_INLINE = 1,
	INTERP_OPT_CPROP = 2,
	INTERP_OPT_SUPER_INSTRUCTIONS = 4,
	INTERP_OPT_BBLOCKS = 8,
	INTERP_OPT_TIERING = 16,
	INTERP_OPT_SIMD = 32,
	INTERP_OPT_DEFAULT = INTERP_OPT_INLINE | INTERP_OPT_CPROP | INTERP_OPT_SUPER_INSTRUCTIONS | INTERP_OPT_BBLOCKS | INTERP_OPT_TIERING | INTERP_OPT_SIMD
};


extern int mono_interp_opt;

extern int mono_interp_traceopt;

gint32 mono_class_value_size (MonoClass *klass, guint32 *align) { MISSING_FUNC(); }
MonoMethod *
mono_get_method_checked (MonoImage *image, guint32 token, MonoClass *klass, MonoGenericContext *context, MonoError *error) { MISSING_FUNC(); }
MonoClass *mono_class_from_mono_type_internal (MonoType *type) { MISSING_FUNC(); }

const char * m_class_get_name (MonoClass *klass) { MISSING_FUNC(); }
const char * m_class_get_name_space (MonoClass *klass) { MISSING_FUNC(); }

char *mono_method_full_name (MonoMethod *method, gboolean signature) { MISSING_FUNC(); }

int mono_type_size(MonoType *type, int *alignment) { MISSING_FUNC(); }

void*
m_method_alloc0 (MonoMethod *method, guint size) { MISSING_FUNC(); }


InterpMethod*
mint_testing_transform_sample(MonoMethod *monoMethodPtr)
{
    ThreadContext *thread_context = NULL; // transform_method actually doesn't use thread_context
    InterpMethod *imethod = mono_interp_get_imethod (/*method*/ monoMethodPtr);
    ERROR_DECL(error);
    mono_interp_transform_method (imethod, thread_context, error);
    return imethod;
}

void
mint_entrypoint(MintAbstractionNativeAot *itf, MintEEAbstractionNativeAot* eeItf)
{
    mono_interp_opt = INTERP_OPT_DEFAULT & ~INTERP_OPT_TIERING & ~INTERP_OPT_SIMD ; // FIXME
    mono_interp_traceopt = 1; // FIXME
    mint_itf_initialize(itf);
    mint_ee_itf_initialize(eeItf); // FIXME: get it from managed
}



// mint mempool

MonoMemPool *
mono_mempool_new (void) {
    return mint_itf()->create_mem_pool();
}

void
mono_mempool_destroy (MonoMemPool *pool) {
    mint_itf()->get_MonoMemPool_inst(pool)->vtable->destroy(pool);
}
void*
mono_mempool_alloc0 (MonoMemPool *pool, unsigned int size) {
    return mint_itf()->get_MonoMemPool_inst(pool)->vtable->alloc0(pool, size);
}

// mint memory manger

// for the NativeAOT shim, a mem manager _is_ a mempool - don't make a distinction
struct _MonoMemoryManager {
    MonoMemPoolInstanceAbstractionNativeAot mempool;
};

void *
mono_mem_manager_alloc0 (MonoMemoryManager *memory_manager, guint size) {
    // FIXME: abstraction discipline...
    return mono_mempool_alloc0((MonoMemPool*)&memory_manager->mempool, size);
}

// FIXME: actually tie to the lifetime of the dynamic method
MonoMemoryManager * m_method_get_mem_manager (MonoMethod *method) {
    return mint_itf()->m_method_get_mem_manager(method);
}

