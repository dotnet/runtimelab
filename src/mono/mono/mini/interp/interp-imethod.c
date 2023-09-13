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

InterpMethod*
mono_interp_get_imethod (MonoMethod *method)
{
	// FIXME: locking/concurrency/loookup
	InterpMethod *imethod;

	imethod = g_malloc0 (sizeof (InterpMethod));

	imethod->method = method;
	imethod->param_count = 0; // FIXME: sig->param_count;
	imethod->hasthis = FALSE; // sig->hasthis;
	imethod->vararg = FALSE; // sig->call_convention == MONO_CALL_VARARG;
	imethod->code_type = IMETHOD_CODE_UNKNOWN;
	// This flag allows us to optimize out the interp_entry 'is this a delegate invoke' checks
	imethod->is_invoke = FALSE; // (m_class_get_parent (method->klass) == mono_defaults.multicastdelegate_class) && !strcmp(method->name, "Invoke");
	// always optimize code if tiering is disabled
	// always optimize wrappers
	//if (!mono_interp_tiering_enabled () || method->wrapper_type != MONO_WRAPPER_NONE)
	//	imethod->optimized = TRUE;
	imethod->optimized = TRUE; // NativeAot always optimize
	imethod->rtype = mint_itf()->get_default_byval_type_void();
	//if (imethod->method->string_ctor)
	//	imethod->rtype = m_class_get_byval_arg (mono_defaults.string_class);
	//else
//		imethod->rtype = mini_get_underlying_type (sig->ret);
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

void
mint_interp_imethod_dump_code (InterpMethod *imethod)
{
	g_warning ("mint_interp_imethod_dump_code");
	g_warning ("imethod code is %p", imethod->code);
}

#endif /* NATIVEAOT_MINT */
