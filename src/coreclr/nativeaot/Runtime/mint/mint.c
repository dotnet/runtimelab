#include <config.h>
#include <glib.h>
#include <mint-transform.h>
#include <monoshim/missing-symbols.h>
#include <mint-abstraction-nativeaot.h>

static void
__attribute__((noreturn))
mint_missing (const char *func)
{
	g_error ("function %s is not implemented yet", func);
}

#define MISSING_FUNC() mint_missing(__func__)

int mono_interp_opt = 0; // FIXME
int mono_interp_traceopt = 0; // FIXME

MintAbstractionNativeAot *mint_itf(void) { MISSING_FUNC(); }
void* mono_mempool_alloc0 (MonoMemPool *pool, unsigned int size) { MISSING_FUNC(); }

MonoMemPool * mono_mempool_new (void) { MISSING_FUNC(); }
void mono_mempool_destroy (MonoMemPool *pool) { MISSING_FUNC(); }

void * mono_mem_manager_alloc0 (MonoMemoryManager *memory_manager, guint size) { MISSING_FUNC(); }

MonoMemoryManager * m_method_get_mem_manager (MonoMethod *method) { MISSING_FUNC(); }


gint32 mono_class_value_size (MonoClass *klass, guint32 *align) { MISSING_FUNC(); }
MonoMethod *
mono_get_method_checked (MonoImage *image, guint32 token, MonoClass *klass, MonoGenericContext *context, MonoError *error) { MISSING_FUNC(); }
MonoClass *mono_class_from_mono_type_internal (MonoType *type) { MISSING_FUNC(); }
void mono_metadata_free_mh (MonoMethodHeader *header) { MISSING_FUNC(); }


const char * m_class_get_name (MonoClass *klass) { MISSING_FUNC(); }
const char * m_class_get_name_space (MonoClass *klass) { MISSING_FUNC(); }

char *mono_method_full_name (MonoMethod *method, gboolean signature) { MISSING_FUNC(); }

int mono_type_size(MonoType *type, int *alignment) { MISSING_FUNC(); }

void
mint_entrypoint(void);

void
mint_entrypoint(void)
{
    g_warning("Hello from mint_entrypoint");
    g_warning("transform is %p", (void*)&mono_interp_transform_method);
}
