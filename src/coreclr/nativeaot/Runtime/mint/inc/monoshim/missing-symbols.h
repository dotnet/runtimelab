#ifndef _MONOSHIM_MISSING_SYMBOLS_H
#define _MONOSHIM_MISSING_SYMBOLS_H

#include <monoshim/utils/mono-compiler.h>
#include <monoshim/utils/mono-publib.h>
#include <monoshim/utils/mono-memory-model.h>
#include <monoshim/utils/monobitset.h>
#include <monoshim/utils/mono-error-internals.h>
#include <monoshim/utils/mono-endian.h>


#include <monoshim/metadata/blob.h>
#include <monoshim/metadata/opcodes-types.h>
#include <monoshim/metadata/profiler-types.h>
#include <monoshim/metadata/metadata-types.h>
#include <monoshim/metadata/tabledefs.h>
#include <monoshim/metadata/class-internals.h>

#include <monoshim/metadata/mono-basic-block.h>

// mini.h does this in Mono

/*
 * Pull the list of opcodes
 */
#define OPDEF(a,b,c,d,e,f,g,h,i,j) \
	a = i,

enum {
#include "monoshim/cil/opcode.def"
	CEE_LASTOP
};
#undef OPDEF


typedef struct _MonoError MonoError;

typedef struct _MonoImage MonoImage;

typedef struct _MonoType MonoType;
typedef struct _MonoClass MonoClass;

typedef struct _MonoMethod MonoMethod;
typedef struct _MonoMethodSignature MonoMethodSignature;
typedef struct _MonoMethodHeader MonoMethodHeader;

typedef struct _MonoExceptionClause MonoExceptionClause;

typedef struct _MonoClassField MonoClassField;

typedef struct _MonoGenericContext MonoGenericContext;
typedef struct _MonoGenericContainer MonoGenericContainer;

typedef struct _MonoJitICallInfo MonoJitICallInfo;

typedef int MonoJitICallId;

typedef struct _MonoVTable MonoVTable;

/* metadata algorithms */

typedef struct _MonoSimpleBasicBlock MonoSimpleBasicBlock;


/* debug support */
typedef struct _MonoDebugLineNumberEntry	MonoDebugLineNumberEntry;
struct _MonoDebugLineNumberEntry {
	uint32_t il_offset;
	uint32_t native_offset;
};


/* mini runtime */

typedef struct _SeqPoint SeqPoint;
typedef struct _MonoJitInfo MonoJitInfo;

typedef struct _MonoMemPool MonoMemPool;
typedef struct _MonoMemoryManager MonoMemoryManager;
typedef struct _MonoJitMemoryManager MonoJitMemoryManager;

typedef struct _MonoProfilerCoverageInfo MonoProfilerCoverageInfo;

/* mini.h */
#define MONO_TIME_TRACK(cost_center, expr) expr

#define MONO_PROFILER_RAISE(name,args) /* empty */

static gpointer mono_jit_trace_calls = NULL; // FIXME: hack!

static gpointer mono_stats_method_desc = NULL; // FIXME: hack!



// ====================================================================================================================
// Runtime
// ====================================================================================================================

typedef struct _MonoObject MonoObject;
typedef struct _MonoString MonoString;

typedef struct _MonoFtnDesc MonoFtnDesc;

typedef struct _MonoDelegateTrampInfo MonoDelegateTrampInfo;
typedef struct _MonoJitExceptionInfo MonoJitExceptionInfo;

typedef void * MonoGCHandle;


// ====================================================================================================================
// Functions
// ====================================================================================================================

void* mono_mempool_alloc0 (MonoMemPool *pool, unsigned int size);
static inline void*
mono_mempool_alloc (MonoMemPool *pool, unsigned int size)
{
    return mono_mempool_alloc0 (pool, size);
}
MonoMemPool * mono_mempool_new (void);
void mono_mempool_destroy (MonoMemPool *pool);

void * mono_mem_manager_alloc0 (MonoMemoryManager *memory_manager, guint size);

static inline GList*
g_list_prepend_mempool (MonoMemPool *mp, GList *list, gpointer data)
{
	GList *new_list;

	new_list = (GList *) mono_mempool_alloc (mp, sizeof (GList));
	new_list->data = data;
	new_list->prev = list ? list->prev : NULL;
    new_list->next = list;

    if (new_list->prev)
            new_list->prev->next = new_list;
    if (list)
            list->prev = new_list;

	return new_list;
}
static inline GSList*
g_slist_prepend_mempool (MonoMemPool *mp, GSList *list, gpointer  data)
{
	GSList *new_list;

	new_list = (GSList *) mono_mempool_alloc (mp, sizeof (GSList));
	new_list->data = data;
	new_list->next = list;

	return new_list;
}


static inline gboolean mono_threads_are_safepoints_enabled(void) { return FALSE; }

char *mono_method_full_name (MonoMethod *method, gboolean signature);

gboolean mono_method_has_no_body (MonoMethod *method) { return FALSE; } // FIXME(NativeAot): hack!

MonoMethodHeader* mono_method_get_header_internal (MonoMethod *method, MonoError *error);

static inline gboolean mono_debugger_method_has_breakpoint (MonoMethod *method) { return FALSE; } // FIXME: hack!

static inline gboolean mono_profiler_coverage_instrumentation_enabled (MonoMethod *method) { return FALSE; }

int mono_type_size(MonoType *type, int *alignment);

MonoClass *mono_class_from_mono_type_internal (MonoType *type);

gint32 mono_class_value_size (MonoClass *klass, guint32 *align);

MonoMethod *
mono_get_method_checked (MonoImage *image, guint32 token, MonoClass *klass, MonoGenericContext *context, MonoError *error);

static inline gboolean m_class_is_simd_type (MonoClass *klass) { return FALSE; } // FIXME(NativeAot): hack!

const char * m_class_get_name (MonoClass *klass);
const char * m_class_get_name_space (MonoClass *klass);

MonoMemoryManager * m_method_get_mem_manager (MonoMethod *method);


 // TODO(NativeAot): compile this from mono-basic-block.c
MonoSimpleBasicBlock* mono_basic_block_split (MonoMethod *method, MonoError *error, MonoMethodHeader *header);
void mono_basic_block_free (MonoSimpleBasicBlock *bb);
// also in mono-basic-block.c
int mono_opcode_size (const unsigned char *ip, const unsigned char *end);


const char * mono_opcode_name (int opcode);

static inline int
mono_is_power_of_two (guint32 val)
{
	int i, j, k;

	for (i = 0, j = 1, k = 0xfffffffe; i < 32; ++i, j = j << 1, k = k << 1) {
		if (val & j)
			break;
	}
	if (i == 32 || val & k)
		return -1;
	return i;
}

void mono_metadata_free_mh (MonoMethodHeader *header);

static inline MonoJitMemoryManager* get_default_jit_mm (void) { return NULL; } // FIXME: hack!

static inline void jit_mm_lock(MonoJitMemoryManager *jit_mm) { }
static inline void jit_mm_unlock(MonoJitMemoryManager *jit_mm) { }

static inline void mono_memory_barrier(void) { }

#endif/*_MONOSHIM_MISSING_SYMBOLS_H*/
