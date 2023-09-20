// Note: this file is included in interp.c for Mono
// For NativeAOT it is compiled separately

#ifndef NATIVEAOT_MINT
InterpMethod*
mono_interp_get_imethod (MonoMethod *method)
{
	InterpMethod *imethod;
	MonoMethodSignature *sig;
	MonoJitMemoryManager *jit_mm = jit_mm_for_method (method);
	int i;

	jit_mm_lock (jit_mm);
	imethod = (InterpMethod*)mono_internal_hash_table_lookup (&jit_mm->interp_code_hash, method);
	jit_mm_unlock (jit_mm);
	if (imethod)
		return imethod;

	sig = mono_method_signature_internal (method);

	if (method->dynamic)
		imethod = (InterpMethod*)mono_dyn_method_alloc0 (method, sizeof (InterpMethod));
	else
		imethod = (InterpMethod*)m_method_alloc0 (method, sizeof (InterpMethod));
	imethod->method = method;
	imethod->param_count = sig->param_count;
	imethod->hasthis = sig->hasthis;
	imethod->vararg = sig->call_convention == MONO_CALL_VARARG;
	imethod->code_type = IMETHOD_CODE_UNKNOWN;
	// This flag allows us to optimize out the interp_entry 'is this a delegate invoke' checks
	imethod->is_invoke = (m_class_get_parent (method->klass) == mono_defaults.multicastdelegate_class) && !strcmp(method->name, "Invoke");
	// always optimize code if tiering is disabled
	// always optimize wrappers
	if (!mono_interp_tiering_enabled () || method->wrapper_type != MONO_WRAPPER_NONE)
		imethod->optimized = TRUE;
	if (imethod->method->string_ctor)
		imethod->rtype = m_class_get_byval_arg (mono_defaults.string_class);
	else
		imethod->rtype = mini_get_underlying_type (sig->ret);
	if (method->dynamic)
		imethod->param_types = (MonoType**)mono_dyn_method_alloc0 (method, sizeof (MonoType*) * sig->param_count);
	else
		imethod->param_types = (MonoType**)m_method_alloc0 (method, sizeof (MonoType*) * sig->param_count);
	for (i = 0; i < sig->param_count; ++i)
		imethod->param_types [i] = mini_get_underlying_type (sig->params [i]);

	jit_mm_lock (jit_mm);
	InterpMethod *old_imethod;
	if (!((old_imethod = mono_internal_hash_table_lookup (&jit_mm->interp_code_hash, method)))) {
		mono_internal_hash_table_insert (&jit_mm->interp_code_hash, method, imethod);
	} else {
		imethod = old_imethod; /* leak the newly allocated InterpMethod to the mempool */
	}
	jit_mm_unlock (jit_mm);

	imethod->prof_flags = mono_profiler_get_call_instrumentation_flags (imethod->method);

	return imethod;
}

#else /* NATIVEAOT_MINT*/

#include <config.h>
#include <glib.h>
#include <monoshim/missing-symbols.h>
#include <monoshim/metadata/mint-abstraction.h>
#include <mint-abstraction-nativeaot.h>
#include <mint-imethod.h>
#include "interp-internals.h"

static inline MonoMethodSignature*
interp_method_signature (MonoMethod *method)
{
#ifndef NATIVEAOT_MINT
	return mono_method_signature_internal (method);
#else
	return MINT_VTI_ITF(MonoMethod, method, get_signature)(method);
#endif
}

static gboolean
interp_msig_hasthis (MonoMethodSignature *sig)
{
#ifndef NATIVEAOT_MINT
	return sig->hasthis;
#else
	return !!MINT_TI_ITF(MonoMethodSignature, sig, hasthis);
#endif
}

static int
interp_msig_param_count (MonoMethodSignature *sig)
{
#ifndef NATIVEAOT_MINT
	return sig->param_count;
#else
	return MINT_TI_ITF(MonoMethodSignature, sig, param_count);
#endif
}

static MonoType*
interp_msig_ret_ult (MonoMethodSignature *sig)
{
#ifndef NATIVEAOT_MINT
	return mini_type_get_underlying_type (signature->ret)
#else
	return MINT_VTI_ITF(MonoMethodSignature, sig, ret_ult)(sig);
#endif
}

static MonoType**
interp_msig_get_first_param (MonoMethodSignature *sig)
{
#ifndef NATIVEAOT_MINT
	return sig->params[0];
#else
	return MINT_VTI_ITF(MonoMethodSignature, sig, method_params)(sig);
#endif
}


static gpointer
imethod_alloc0 (InterpMethod *imethod, size_t size)
{
#ifndef NATIVEAOT_MINT
	if (td->rtm->method->dynamic)
		return mono_dyn_method_alloc0 (td->rtm->method, (guint)size);
	else
		return mono_mem_manager_alloc0 (td->mem_manager, (guint)size);
#else
	return MINT_ITF(imethod_alloc0) (imethod, size);
#endif
}


InterpMethod*
mono_interp_get_imethod (MonoMethod *method)
{
	// FIXME: locking/concurrency/loookup
	InterpMethod *imethod;

	imethod = g_malloc0 (sizeof (InterpMethod));

	MonoMethodSignature *sig = interp_method_signature (method);


	imethod->method = method;
	imethod->param_count = interp_msig_param_count (sig);
	imethod->hasthis = interp_msig_hasthis (sig); // sig->hasthis;
	imethod->vararg = FALSE; // sig->call_convention == MONO_CALL_VARARG;
	imethod->code_type = IMETHOD_CODE_UNKNOWN;
	// This flag allows us to optimize out the interp_entry 'is this a delegate invoke' checks
	imethod->is_invoke = FALSE; // (m_class_get_parent (method->klass) == mono_defaults.multicastdelegate_class) && !strcmp(method->name, "Invoke");
	// always optimize code if tiering is disabled
	// always optimize wrappers
	//if (!mono_interp_tiering_enabled () || method->wrapper_type != MONO_WRAPPER_NONE)
	//	imethod->optimized = TRUE;
	imethod->optimized = TRUE; // NativeAot always optimize
	imethod->rtype = interp_msig_ret_ult (sig);

	//if (imethod->method->string_ctor)
	//	imethod->rtype = m_class_get_byval_arg (mono_defaults.string_class);
	//else
//		imethod->rtype = mini_get_underlying_type (sig->ret);
	int sig_param_count = interp_msig_param_count (sig);
	imethod->param_types = (MonoType**)imethod_alloc0 (imethod, sizeof (MonoType*) * sig_param_count);
	for (int i = 0; i < sig_param_count; ++i)
		imethod->param_types [i] = interp_msig_get_first_param (sig) [i];
	// if (method->dynamic)
	// 	imethod->param_types = (MonoType**)mono_dyn_method_alloc0 (method, sizeof (MonoType*) * sig->param_count);
	// else
	// 	imethod->param_types = (MonoType**)m_method_alloc0 (method, sizeof (MonoType*) * sig->param_count);
	// for (i = 0; i < sig->param_count; ++i)
	// 	imethod->param_types [i] = mini_get_underlying_type (sig->params [i]);

	// jit_mm_lock (jit_mm);
	// InterpMethod *old_imethod;
	// if (!((old_imethod = mono_internal_hash_table_lookup (&jit_mm->interp_code_hash, method)))) {
	// 	mono_internal_hash_table_insert (&jit_mm->interp_code_hash, method, imethod);
	// } else {
	// 	imethod = old_imethod; /* leak the newly allocated InterpMethod to the mempool */
	// }
	// jit_mm_unlock (jit_mm);

	imethod->prof_flags = MONO_PROFILER_CALL_INSTRUMENTATION_NONE; // mono_profiler_get_call_instrumentation_flags (imethod->method);

	return imethod;
}

#endif /* NATIVEAOT_MINT */
