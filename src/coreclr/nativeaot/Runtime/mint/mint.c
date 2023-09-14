#include <config.h>
#include <glib.h>
#include <mint-transform.h>
#include <monoshim/missing-symbols.h>
#include <mint-abstraction-nativeaot.h>
#include <mint-imethod.h>

#include "mint-itf.h"

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


int mono_interp_opt = INTERP_OPT_DEFAULT & ~INTERP_OPT_TIERING & ~INTERP_OPT_SIMD ; // FIXME

int mono_interp_traceopt = 1; // FIXME


gint32 mono_class_value_size (MonoClass *klass, guint32 *align) { MISSING_FUNC(); }
MonoMethod *
mono_get_method_checked (MonoImage *image, guint32 token, MonoClass *klass, MonoGenericContext *context, MonoError *error) { MISSING_FUNC(); }
MonoClass *mono_class_from_mono_type_internal (MonoType *type) { MISSING_FUNC(); }

const char * m_class_get_name (MonoClass *klass) { MISSING_FUNC(); }
const char * m_class_get_name_space (MonoClass *klass) { MISSING_FUNC(); }

char *mono_method_full_name (MonoMethod *method, gboolean signature) { MISSING_FUNC(); }

int mono_type_size(MonoType *type, int *alignment) { MISSING_FUNC(); }

void
mint_entrypoint(MintAbstractionNativeAot *itf);

// for testing purposes only. transform a placeholder method
InterpMethod*
mint_testing_transform_sample(intptr_t monoMethodPtr)
{
    g_warning ("mint_testing_transform_sample ignoring the input DynamicMethod");
    g_warning ("mint_testing_transform_sample will probably crash");
    ThreadContext *thread_context = NULL; // transform_method actually doesn't use thread_context
    MonoMethod *method = (MonoMethod*)mint_method_abstraction_placeholder(); // FIXME
    InterpMethod *imethod = mono_interp_get_imethod (method);
    ERROR_DECL(error);
    mono_interp_transform_method (imethod, thread_context, error);
    g_warning ("returned from \'mono_interp_transform_method\'");
    mint_interp_imethod_dump_code (imethod);
    return imethod;
}

void
mint_entrypoint(MintAbstractionNativeAot *itf)
{
    mint_itf_initialize(itf);
    g_warning("Hello from mint_entrypoint");
    g_warning("transform is %p", (void*)&mono_interp_transform_method);
}



// mint mempool

// FIXME: actually pool memory
struct _MonoMemPool {
    // empty
};

MonoMemPool *
mono_mempool_new (void) {
    return g_new0 (MonoMemPool, 1);
}

void
mono_mempool_destroy (MonoMemPool *pool) {
    g_free (pool);
}
void*
mono_mempool_alloc0 (MonoMemPool *pool, unsigned int size) {
    return g_malloc0(size);
}

// mint mem manager

struct _MonoMemoryManager {
    MonoMemPool *pool;
};

void *
mono_mem_manager_alloc0 (MonoMemoryManager *memory_manager, guint size) {
    return mono_mempool_alloc0 (memory_manager->pool, size);
}

// FIXME: actually tie to the lifetime of the dynamic method
MonoMemoryManager * m_method_get_mem_manager (MonoMethod *method) {
    static MonoMemPool placeholder_mempool;
    static MonoMemoryManager placeholder_memory_manager = { &placeholder_mempool };
    return &placeholder_memory_manager;
}

