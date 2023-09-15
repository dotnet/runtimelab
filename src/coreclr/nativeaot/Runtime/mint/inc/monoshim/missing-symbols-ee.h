#ifndef _MONOSHIM_MISSING_SYMBOLS_EE_H
#define _MONOSHIM_MISSING_SYMBOLS_EE_H

#include <pthread.h>

typedef uint16_t mono_unichar2;

#define g_assert_checked(expr) /* empty */

#define return_val_if_nok(error,val) do { } while (0) // FIXME

static inline void
mono_compiler_barrier(void)
{
    mono_memory_barrier(); // FIXME: too strict
}

static inline void
mono_memory_read_barrier(void)
{
    mono_memory_barrier(); // FIXME
}

static inline gboolean
mono_isunordered (double a, double b) {
    abort();
    return FALSE; // FIXME
}

static inline gboolean
mono_isfinite (double a) {
    abort();
    return TRUE; // FIXME
}

static inline gboolean
mono_try_trunc_u64(double v, guint64 *res)
{
    abort();
    return FALSE; // FIXME
}

static inline gboolean
mono_try_trunc_i64(double v, gint64 *res)
{
    abort();
    return FALSE; // FIXME
}

static inline gboolean
mono_signbit (double a) {
    abort();
    return FALSE; // FIXME
}

static inline gboolean
mono_isnan (double a) {
    abort();
    return FALSE; // FIXME
}



typedef struct _MonoException MonoException;
typedef struct _MonoArray MonoArray;
typedef struct _MonoDelegate MonoDelegate;

typedef volatile MonoObject * volatile *MonoObjectHandle;

typedef pthread_key_t MonoNativeTlsKey; // FIXME: pthread_key

typedef struct _MonoJitTlsData MonoJitTlsData;

typedef struct _MonoLMFExt MonoLMFExt;

typedef struct _MonoVTableEEData MonoVTableEEData;

typedef struct _MonoContext MonoContext;

typedef void* MonoInterpFrameHandle;

static int mono_llvm_only = 0;

static int mono_polling_required = 0;

typedef gpointer MonoFtnDesc; // FIXME

typedef struct _MonoInterpStackIter MonoInterpStackIter;

typedef struct _StackFrameInfo StackFrameInfo;

typedef void (*GcScanFunc)(gpointer*, gpointer);

typedef gboolean (*InterpJitInfoFunc)(MonoJitInfo*, gpointer);

#define MONO_EE_API_VERSION 0x100
static inline int mono_ee_api_version (void) { return MONO_EE_API_VERSION; }

#define MONO_EE_CALLBACKS \
	MONO_EE_CALLBACK (void, entry_from_trampoline, (gpointer ccontext, gpointer imethod)) \
	MONO_EE_CALLBACK (void, to_native_trampoline, (gpointer addr, gpointer ccontext)) \
	MONO_EE_CALLBACK (gpointer, create_method_pointer, (MonoMethod *method, gboolean compile, MonoError *error)) \
	MONO_EE_CALLBACK (MonoFtnDesc*, create_method_pointer_llvmonly, (MonoMethod *method, gboolean unbox, MonoError *error)) \
	MONO_EE_CALLBACK (void, free_method, (MonoMethod *method)) \
	MONO_EE_CALLBACK (MonoObject*, runtime_invoke, (MonoMethod *method, void *obj, void **params, MonoObject **exc, MonoError *error)) \
	MONO_EE_CALLBACK (void, init_delegate, (MonoDelegate *del, MonoDelegateTrampInfo **out_info, MonoError *error)) \
	MONO_EE_CALLBACK (void, delegate_ctor, (MonoObjectHandle this_obj, MonoObjectHandle target, gpointer addr, MonoError *error)) \
	MONO_EE_CALLBACK (void, set_resume_state, (MonoJitTlsData *jit_tls, MonoObject *ex, MonoJitExceptionInfo *ei, MonoInterpFrameHandle interp_frame, gpointer handler_ip)) \
	MONO_EE_CALLBACK (void, get_resume_state, (const MonoJitTlsData *jit_tls, gboolean *has_resume_state, MonoInterpFrameHandle *interp_frame, gpointer *handler_ip)) \
	MONO_EE_CALLBACK (gboolean, run_finally, (StackFrameInfo *frame, int clause_index)) \
	MONO_EE_CALLBACK (gboolean, run_filter, (StackFrameInfo *frame, MonoException *ex, int clause_index, gpointer handler_ip, gpointer handler_ip_end)) \
	MONO_EE_CALLBACK (gboolean, run_clause_with_il_state, (gpointer il_state, int clause_index, MonoObject *ex, gboolean *filtered)) \
	MONO_EE_CALLBACK (void, frame_iter_init, (MonoInterpStackIter *iter, gpointer interp_exit_data)) \
	MONO_EE_CALLBACK (gboolean, frame_iter_next, (MonoInterpStackIter *iter, StackFrameInfo *frame)) \
	MONO_EE_CALLBACK (MonoJitInfo*, find_jit_info, (MonoMethod *method)) \
	MONO_EE_CALLBACK (void, set_breakpoint, (MonoJitInfo *jinfo, gpointer ip)) \
	MONO_EE_CALLBACK (void, clear_breakpoint, (MonoJitInfo *jinfo, gpointer ip)) \
	MONO_EE_CALLBACK (MonoJitInfo*, frame_get_jit_info, (MonoInterpFrameHandle frame)) \
	MONO_EE_CALLBACK (gpointer, frame_get_ip, (MonoInterpFrameHandle frame)) \
	MONO_EE_CALLBACK (gpointer, frame_get_arg, (MonoInterpFrameHandle frame, int pos)) \
	MONO_EE_CALLBACK (gpointer, frame_get_local, (MonoInterpFrameHandle frame, int pos)) \
	MONO_EE_CALLBACK (gpointer, frame_get_this, (MonoInterpFrameHandle frame)) \
	MONO_EE_CALLBACK (void, frame_arg_to_data, (MonoInterpFrameHandle frame, MonoMethodSignature *sig, int index, gpointer data)) \
	MONO_EE_CALLBACK (void, data_to_frame_arg, (MonoInterpFrameHandle frame, MonoMethodSignature *sig, int index, gconstpointer data)) \
	MONO_EE_CALLBACK (gpointer, frame_arg_to_storage, (MonoInterpFrameHandle frame, MonoMethodSignature *sig, int index)) \
	MONO_EE_CALLBACK (MonoInterpFrameHandle, frame_get_parent, (MonoInterpFrameHandle frame)) \
	MONO_EE_CALLBACK (void, start_single_stepping, (void)) \
	MONO_EE_CALLBACK (void, stop_single_stepping, (void)) \
	MONO_EE_CALLBACK (void, free_context, (gpointer)) \
	MONO_EE_CALLBACK (void, set_optimizations, (guint32)) \
	MONO_EE_CALLBACK (void, invalidate_transformed, (void)) \
	MONO_EE_CALLBACK (void, cleanup, (void)) \
	MONO_EE_CALLBACK (void, mark_stack, (gpointer thread_info, GcScanFunc func, gpointer gc_data, gboolean precise)) \
	MONO_EE_CALLBACK (void, jit_info_foreach, (InterpJitInfoFunc func, gpointer user_data)) \
	MONO_EE_CALLBACK (gboolean, sufficient_stack, (gsize size)) \
	MONO_EE_CALLBACK (void, entry_llvmonly, (gpointer res, gpointer *args, gpointer imethod)) \
	MONO_EE_CALLBACK (gpointer, get_interp_method, (MonoMethod *method)) \
	MONO_EE_CALLBACK (MonoJitInfo*, compile_interp_method, (MonoMethod *method, MonoError *error)) \
	MONO_EE_CALLBACK (gboolean, jit_call_can_be_supported, (MonoMethod *method, MonoMethodSignature *sig, gboolean is_llvm_only)) \

typedef struct _MonoEECallbacks {

#undef MONO_EE_CALLBACK
#define MONO_EE_CALLBACK(ret, name, sig) ret (*name) sig;

	MONO_EE_CALLBACKS

} MonoEECallbacks;


void mono_ee_interp_init (const char *); // not missing.  this is in interp.h

#endif /*_MONOSHIM_MISSING_SYMBOLS_EE_H*/
