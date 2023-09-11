#ifndef _MONOSHIM_METADATA_PROFILER_TYPES_H
#define _MONOSHIM_METADATA_PROFILER_TYPES_H

typedef enum {
	/**
	 * Do not instrument calls.
	 */
	MONO_PROFILER_CALL_INSTRUMENTATION_NONE = 0,
	/**
	 * Instrument method entries.
	 */
	MONO_PROFILER_CALL_INSTRUMENTATION_ENTER = 1 << 1,
	/**
	 * Also capture a call context for method entries.
	 */
	MONO_PROFILER_CALL_INSTRUMENTATION_ENTER_CONTEXT = 1 << 2,
	/**
	 * Instrument method exits.
	 */
	MONO_PROFILER_CALL_INSTRUMENTATION_LEAVE = 1 << 3,
	/**
	 * Also capture a call context for method exits.
	 */
	MONO_PROFILER_CALL_INSTRUMENTATION_LEAVE_CONTEXT = 1 << 4,
	/**
	 * Instrument method exits as a result of a tail call.
	 */
	MONO_PROFILER_CALL_INSTRUMENTATION_TAIL_CALL = 1 << 5,
	/**
	 * Instrument exceptional method exits.
	 */
	MONO_PROFILER_CALL_INSTRUMENTATION_EXCEPTION_LEAVE = 1 << 6,
} MonoProfilerCallInstrumentationFlags;

#endif/*_MONOSHIM_METADATA_PROFILER_TYPES_H*/
