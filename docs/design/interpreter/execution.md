# Execution phase

This document explicitly doesn't list stuff like locks, hashtable, memory allocations, mono_compiler_barrier

## Execution interface candidate methods:
1. GCWriteBarrier(destAddress, object*)
2. GCCopyValues(destAddress, sourceAddress, count, classHandle)
3. GCSetField(destObject*, field*, srcObject*)
4. ThrowException(exceptionObject*)
5. CreateSimpleArray(classHandle, length) - Create a single dimensional zero based array with element type specified by the classHandle and the given length
6. CreateArray(classHandle, numArgs, args) - Create an array that's not single dimensional zero based one. The args represent the array bounds.
7. GetArrayElementAddress(object*, index) - Get array element address. Even for multi-dim arrays, the index is a linear index in the array data
8. DisablePreemptiveGC()
9. EnablePreemptiveGC()
10. IsInst(object*, classHandle)
11. CastClass(object*, classHandle)
12. ClearWithReferences(address, length)
13. Box(data*, classHandle)
14. UnBox(object*)
15. SuspendEE()
16. RestartEE()
17. GetExceptionById(id) - id: null_reference, divide_by_zero, overflow, invalid_cast, index_out_of_range, array_type_mismatch, arithmetic, argument_out_of_range. Maybe use the CoreCLR RuntimeExceptionKind as the id.
18. GetVirtualMethod(methodHandle, classHandle)
19. NewObject(classHandle)
20. GetThreadStaticFieldAddress(offset) - Offset is relative to the managed TLS storage
21. GetHashCode(tryGet, object*)
22. GCPoll()

## Debugger interface methods
 * BreakPoint - breakpoint set by the debugger triggered in the interpreted code
 * SingleStep - single step completed
 * Break - System.Diagnostics.Debugger.Break() invoked by the interpreted code

## Profiler interface methods
 * TraceEnterMethod(methodHandle, ???)
 * TraceLeaveMethod(methodHandle, ???)

## JIT2EEInterface methods
 * getMethodInfo
 * getArgNext
 * getArgType
 * isEnum - may not be needed if the CORINFO_METHOD_INFO::args are already enum free (I think I've seen a comment that it is that case somewhere)
 * getClassSize
 * getMethodAttribs - only used at one place to check if a class is abstract when initializing a delegate 
 * getClassNameFromMetadata - diagnostic purposes in debug builds only
 * getMethodNameFromMetadata - diagnostic purposes in debug builds only
 * getChildType
 * getParentType
 * isValueClass
 * asCorInfoType
 * getArrayRank
 * getClassAttribs


## MonoClass, MonoMethod and MonoType member access helpers
The following functions are helpers used to access fields in MonoClass, MonoMethod and MonoType. They are used all over the place, so they are described here instead of at the specific places they are used.
 * m_class_get_byval_arg - N.A. on CoreCLR
 * m_class_get_element_class -> **JIT2EEInterface::getChildType**
 * m_class_get_image -> **JIT2EEInterface::getMethodInfo, CORINFO_METHOD_INFO::scope**
 * m_class_get_name -> **JIT2EEInterface::getClassNameFromMetadata**
 * m_class_get_name_space -> **JIT2EEInterface::getClassNameFromMetadata**
 * m_class_get_parent -> **JIT2EEInterface::getParentType**
 * m_class_get_rank -> **JIT2EEInterface::getArrayRank**
 * m_class_is_byreflike -> **JIT2EEInterface::getClassAttribs() & CORINFO_FLG_BYREF_LIKE**
 * m_class_is_enumtype -> **JIT2EEInterface::isEnum**
 * m_class_is_valuetype -> **JIT2EEInterface::isValueClass**
 * m_method_is_static -> **JIT2EEInterface::getMethodAttribs() & CORINFO_FLG_STATIC**
 * m_method_is_virtual -> **JIT2EEInterface::getMethodAttribs() & CORINFO_FLG_VIRTUAL**
 * m_type_is_byref -> **JIT2EEInterface::asCorInfoType() == CORINFO_TYPE_BYREF**

## Functions in the interp.c

### db_match_method
 * Used just for debug build tracing 
 * mono_method_desc_full_match - match a method by name

### debug_enter, DEBUG_LEAVE()
 * Used just for debug build tracing 
 * mono_method_full_name -> **JIT2EEInterface::getMethodNameFromMetadata**
 * mono_thread_internal_current - used for logging purposes only 

### clear_resume_state
 * Resume state is a state where the interpreter will continue to execute from after execution returns to the interpreter
 * mono_gchandle_free_internal - context holds handle to the managed Exception. Not needed for CoreCLR, the EH managed the exception object lifetime.

### set_context
 * ThreadContext (interpreter specific thread context) storage initialization.
 * mono_native_tls_set_value
 * mono_tls_get_jit_tls
   * MonoJitTlsData::interp_context stores the same ThreadContext* as the context thread local
   
### get_context
 * Get interpreter specific thread context data
 * mono_native_tls_get_value
   * pthread_getspecific on Unix
   * Direct TEB access on Windows
   * Probably switch it to a regular thread local variables for the two usages that are ThreadContext* and MonoJitTlsData*

### interp_free_context
 * Free interpreter specific thread context data, occurs at shutdown.
 * mono_native_tls_get_value

### mono_interp_error_cleanup
 * MonoError holds various details on the last error, including some dynamically allocated stuff. This function deallocates all of that.
 * Some Mono APIs that interpreter calls set the MonoError to carry details on the error that occured
 * N.A. on CoreCLR
 * mono_error_cleanup

### mono_interp_get_imethod
 * Get an existing or create a new InterpMethod for a MonoMethod
 * Creating a new one:
   * mono_method_signature_internal - to get various details like param_count, has_this etc. to store them in the InterpMethod -> **JIT2EEInterface::getMethodInfo(), CORINFO_METHOD_INFO::args, CORINFO_METHOD_INFO::args.hasThis() etc.**
   * m_class_get_parent (method->klass) == mono_defaults.multicastdelegate_class && !strcmp(method->name, "Invoke") - set the InterpMethod::is_invoke
   * There is a special handling of String..ctor method to override its return value to be String for some reason
   * mono_profiler_get_call_instrumentation_flags - call all registered profilers to get the flags of events they are interested in for the specific method and store those on the InterpMethod instance
     * MONO_PROFILER_CALL_INSTRUMENTATION_ENTER, MONO_PROFILER_CALL_INSTRUMENTATION_LEAVE, MONO_PROFILER_CALL_INSTRUMENTATION_TAIL_CALL, MONO_PROFILER_CALL_INSTRUMENTATION_EXCEPTION_LEAVE
     * CoreCLR profiler doesn't have such a filtering capability

### interp_push_lmf
 * LMF means Last Managed Frame. There is a per thread linked list of those. Interpreter pushes and pops it around some calls to Mono runtime. 
 * Pushed for
   * exception throwing
   * mono_error_convert_to_exception
   * mono_get_exception_{x}
   * mono_threads_safepoint
   * pinvoke calls
   * icalls
   * JITerpreter jitting call
   * JIT calls (these are in fact AOT code calls)
   * Debugger trampolines calls
   * mono_interp_transform_method
   * mono_runtime_class_init_full
 * mono_push_lmf

### interp_pop_lmf
 * mono_pop_lmf

### get_virtual_method
 * **Use ExecutionAPI::GetVirtualMethod**
 * For a virtual method call, get the actual method to be called
 
### stackval_from_data
 * Sets stackval::data::{x} to data bytes from the data argument based on the MonoType passed in. The MonoType argument determines the size of data to copy.
 * The converted form would use CorInfoType as argument. Major part of the usages of stackval_from_data pass in return value type / argument type from a signature. The CORINFO_SIG_INFO contains the CorInfoType directly for return type and **JIT2EEInterface::getArgType also returns CorInfoType.**
 * It seems that CoreCLR won't need to handle case that's MONO_TYPE_GENERICINST and generic instantiations don't require special handling here.

### stackval_to_data
 * Reverse of stackval_from_data. An addiitonal thing is that it calls GC write barriers for reference types and value types
 * mono_gc_wbarrier_generic_store_internal -> GCWriteBarrier
 * mono_value_copy_internal -> GCCopyValues
 * It seems that CoreCLR won't need to handle case that's MONO_TYPE_GENERICINST and generic instantiations don't require special handling here.

### handle_exception_cb
 * Unused dead code
 * mono_handle_exception

### interp_throw
 * **Use ExecutionAPI::ThrowException**
 * Call exception handling code in the runtime
 * It expects that when execution should resume after catch, the exception handling returns here so that the interpreter can restore the state to the resume location in case it was in the interpreted code. The interpreter would then pop interpreter frames if necessary and then set the interpreter state ip / sp appropriately. The interpreter loop doesn't recurse for calls to interpreted methods and it allocates new interpreter frame instances using alloca. When returning and then calling again, the interpreter frames are reused. So when resuming at certain interpreted frame, the interpreter frames of the "unwound" frames need to be pushed to the stack of interpreter frames for reuse.
 
### interp_error_convert_to_exception
 * Converts MonoError received from a Mono API to managed exception. This is N.A. for CoreCLR
 * There are also several cases when the interpreter creates the MonoError on its own using the following functions. Those will be changed to get the exception directly using **ExecutionAPI::GetExceptionById**
   * mono_error_set_out_of_memory
   * mono_error_set_argument
   * mono_error_set_platform_not_supported
 * mono_error_convert_to_exception -> N.A. on CoreCLR

### EXCEPTION_CHECKPOINT
 * Checks for thread abort and throws the ThreadAbortException if it was requested
 * mono_threads_is_critical_method - N.A. on CoreCLR
 * mono_thread_interruption_checkpoint - check for thread abort and return the exception

### do_safepoint
 * **Use ExecutionAPI::GCPoll**
 
### ves_array_create
 * mono_array_new_jagged_checked -> **ExecutionAPI::CreateArray**
 * mono_array_new_full_checked -> **ExecutionAPI::CreateArray**

### ves_array_element_address
 * **Use ExecutionAPI::GetArrayElementAddress**
 
### compute_arg_offset
 * Computes offset of a specific argument in a method signature
 * mono_mint_type - N.A. on CoreCLR, the **JIT2EEInterface::getArgType used for iterating over args returns CorInfoType which is an equivalent**

### initialize_arg_offsets
 * Ensures that the arg_offsets member in the InterpMethod is filled in with offsets of arguments in a method signature
 * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo(), CORINFO_METHOD_INFO::args**
 * mono_mint_type - N.A. on CoreCLR, the **JIT2EEInterface::getArgType** used for iterating over args returns CorInfoType which is an equivalent

### get_build_args_from_sig_info
 * Build signature describing structure for PInvoke usage. 
 * mono_class_enum_basetype_internal - **JIT2EEInterface::isEnum()** returns this too

### get_interp_to_native_trampoline
 * mono_aot_get_trampoline
 * mono_arch_get_interp_to_native_trampoline
 * mono_tramp_info_register



### ves_pinvoke_method
 * Call a PInvoke
 * mono_wasm_get_interp_to_native_trampoline
 * mono_jit_compile_method_jit_only
 * mini_get_interp_lmf_wrapper
 * mono_interp_to_native_trampoline
 * mono_arch_get_interp_native_call_info
 * mono_arch_set_native_call_context_args
 * For SWIFT
   * mono_method_signature_has_ext_callconv
   * mono_arch_get_swift_error
 * mono_arch_get_native_call_context_ret

### interp_init_delegate
 * Initialize del->interp_method
 * mono_class_is_abstract -> **JIT2EEInterface::getMethodAttribs() & CORINFO_FLG_ABSTRACT**
 * m_class_get_parent (method->klass) == mono_defaults.multicastdelegate_class && !strcmp (name, "Invoke")
   * mono_marshal_get_delegate_invoke
 * mono_create_delegate_trampoline_info

### interp_delegate_ctor
 * Construct a delegate pointing to an InterpMethod
 * **Use **JIT2EEInterface::GetDelegateCtor**
 * Since this is only called from the transform.c, move it to that file

### dump_stackval
 * Debug build diagnostics
 * mono_type_get_desc -> **JIT2EEInterface::getClassNameFromMetadata()**

### dump_retval
 * Debug build diagnostics
 * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo(), CORINFO_METHOD_INFO::args::retType**

### dump_args
 * Debug build diagnostics
 * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo(), CORINFO_METHOD_INFO::args**
 
### interp_runtime_invoke
 * Invoke a method specified by a MonoMethod passing it arguments passed to interp_runtime_invoke
 * mono_method_signature_internal - only used to get "hasThis" -> **JIT2EEInterface::getMethodInfo, CORINFO_METHOD_INFO::args, CORINFO_SIG_INFO::hasThis()**
 * mono_marshal_get_native_wrapper
 * mono_marshal_get_runtime_invoke_full
 * mono_llvm_start_native_unwind

### interp_entry
 * Main function for entering the interpreter from compiled code
 * mono_object_unbox_internal - "this" may be passed in as boxed for value types. 
 * mono_threads_attach_coop - for cases when the interpreted method is invoked by native code -> **ExecutionAPI::DisablePreemptiveGC**
 * mono_domain_get - N.A., obsolete
 * mono_marshal_get_delegate_invoke - when the method to interpret is Invoke method on a class derived from MultiCastDelegate.
 * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo(), CORINFO_METHOD_INFO::args**
 * mono_threads_detach_coop -> **ExecutionAPI::EnablePreemptiveGC**
 * mono_llvm_start_native_unwind - throws C++ exception

### do_icall
 * Invoke internal call
 * mono_marshal_clear_last_error
 * mono_marshal_set_last_error

### init_jit_call_info
 * Fill in JitCallInfo data structure with details on a call to AOT compiled managed code, like target address, signature, etc.
 * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo(), CORINFO_METHOD_INFO::args**
 * mono_jit_compile_method_jit_only - Fetch the AOTed code address.
 * mono_aot_get_method_flags - checks for generic shared value type 
 * mono_mint_type -> the CORINFO_METHOD_INFO::args::retType is already what we need here
 * mono_class_from_mono_type_internal - no op on CoreCLR
 * mono_class_value_size - extracts return value size -> **JIT2EEInterface::getClassSize(CORINFO_METHOD_INFO::args::retTypeClass)**

### do_jit_call
 * Invoke AOT compiled managed code
 * mono_llvm_catch_exception
 * mono_get_jit_tls
 * mono_error_set_exception_instance

### init_arglist
 * mono_type_stack_size

### interp_entry_from_trampoline
 * High level overview: Iterate over all arguments of the method to execute and copy them from the trampoline to the appropriate slots on the interpreter stack. Then interpret the method using mono_interp_exec_method. After the interpreted method exits, copy its result back to the trampoline and return to the caller.
 * mono_threads_attach_coop -> **ExecutionAPI::DisablePreemptiveGC**
 * mono_domain_get - N.A., obsolete
 * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo(), CORINFO_METHOD_INFO::args**
 * mono_metadata_signature_size
 * mono_arch_get_interp_native_call_info
 * mono_arch_get_native_call_context_args
 * mono_method_signature_has_ext_callconv
 * mono_arch_get_swift_error
 * mono_class_value_size -> **JIT2EEInterface::getClassSize(CORINFO_METHOD_INFO::args::retTypeClass)**
 * mono_class_from_mono_type_internal
 * mono_class_native_size -> **JIT2EEInterface::getClassSize(CORINFO_METHOD_INFO::args::retTypeClass)**
 * mono_threads_detach_coop -> **ExecutionAPI::EnablePreemptiveGC**
 * mono_llvm_start_native_unwind
 * mono_arch_set_native_call_context_ret
 * mono_arch_free_interp_native_call_info

### interp_create_method_pointer_llvmonly
 * Return an ftndesc for entering the interpreter and executing METHOD.
 * Used externally only
 * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo(), CORINFO_METHOD_INFO::args**
 * mono_jit_compile_method_jit_only
 * mono_method_get_name_full

### interp_create_method_pointer
 * Return a function pointer which can be used to call METHOD using the interpreter. Return NULL for methods which are not supported.
 * Used externally only
 * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo(), CORINFO_METHOD_INFO::args**
 * mono_metadata_signature_size
 * mono_marshal_get_wrapper_info
 * mono_wasm_get_native_to_interp_trampoline
 * mono_method_get_full_name
 * mono_error_set_platform_not_supported
 * mono_method_signature_has_ext_callconv
 * mono_jit_compile_method_jit_only
 * mono_method_get_name_full
 * mono_aot_get_trampoline
 * mono_arch_get_native_to_interp_trampoline
 * mono_tramp_info_register
 * mono_create_ftnptr_arg_trampoline

### DUMP_INSTR
 * Debug build diagnostics
 * mono_method_full_name -> **JIT2EEInterface::getMethodNameFromMetadata**
 * mono_thread_internal_current

### do_init_vtable
 * Mono specific -> N.A. on CoreCLR
 * mono_runtime_class_init_full
 * mono_error_convert_to_exception

### mono_interp_new
 * Mono specific -> N.A. on CoreCLR
 * mono_object_new_checked -> **ExecutionAPI::NewObject**
 * mono_error_cleanup

### mono_interp_isinst
 * **Use ExecutionAPI::IsInst**
 * mono_object_class
 * mono_class_is_assignable_from_checked
 * mono_error_cleanup

### mono_interp_get_native_func_wrapper
 * mono_marshal_get_native_func_wrapper
 * mono_metadata_free_marshal_spec

### mono_interp_exec_method
 * mono_threads_safepoint -> **ExecutionAPI::GCPoll**
 * MINT_NIY
   * mono_method_full_name - for debug logging -> **JIT2EEInterface::getMethodNameFromMetadata**
 * MINT_BREAK
   * mono_component_debugger ()->user_break -> **DebuggerAPI::Break**
 * MINT_BREAKPOINT
   * mono_break -> **DebuggerAPI::BreakPoint**
 * MINT_TAILCALL_VIRT
   * mono_object_unbox_internal -> **ExecutionAPI::UnBox**
   * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo, CORINFO_METHOD_INFO::args**
 * MINT_TAILCALL, MINT_TAILCALL_VIRT, MINT_JMP
   * mono_domain_get ()->stack_overflow_ex -> **ExecutionAPI::GetExceptionById**
 * MINT_CALL_DELEGATE
   * mono_get_delegate_invoke_internal
   * mono_marshal_get_delegate_invoke
   * mono_marshal_get_native_wrapper
   * mono_object_unbox_internal -> **ExecutionAPI::UnBox**
 * MINT_CALLI
   * mono_marshal_get_native_wrapper
   * mono_object_unbox_internal -> **ExecutionAPI::UnBox**
 * MINT_CALLVIRT_FAST
   * mono_object_unbox_internal -> **ExecutionAPI::UnBox**
   * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo, CORINFO_METHOD_INFO::args**
 * MINT_CALL
   * mono_domain_get ()->stack_overflow_ex -> **ExecutionAPI::GetExceptionById**
 * MINT_B{cc}_R*, MINT_B{cc}_UN_R*
   * mono_isunordered - copy the mono implementation
 * MINT_STIND_REF
   * mono_gc_wbarrier_generic_store_internal -> **ExecutionAPI::GCWriteBarrier**
 * MINT_CONV_U4_R4
   * mono_rconv_u4 - copy the mono implementation
 * MINT_CONV_U4_R8
   * mono_fconv_u4 - copy the mono implementation
 * MINT_CONV_U8_R4
   * mono_rconv_u8 - copy the mono implementation
 * MINT_CONV_U8_R8
   * mono_fconv_u8 - copy the mono implementation
 * MINT_CPOBJ_VT
   * mono_value_copy_internal -> **ExecutionAPI::GCCopyValues**
 * MINT_LDSTR_DYNAMIC
   * Mono specific, N.A. on CoreCLR
   * mono_method_get_wrapper_data
 * MINT_LDSTR_CSTR
   * Mono specific, N.A. on CoreCLR
   * mono_string_new_wrapper_internal
 * MINT_NEWOBJ
   * mono_gc_alloc_obj -> **ExecutionAPI::NewObject**
   * mono_error_set_out_of_memory
 * MINT_NEWOBJ_SLOW
   * mono_class_vtable_checked - N.A. on CoreCLR
   * mono_runtime_class_init_full - N.A. on CoreCLR, classes are already fully loaded
   * mono_object_new_checked -> **ExecutionAPI::NewObject**
 * MINT_INTRINS_CLEAR_WITH_REFERENCES
   * mono_gc_bzero_aligned -> **ExecutionAPI::ClearWithReferences**
 * MINT_ISINST_COMMON/MINT_CASTCLASS_COMMON
   * **Use ExecutionAPI::CastClass**
 * MINT_UNBOX
   * **Use ExecutionAPI::UnBox**
 * MINT_STFLD_* for I, U, R, O
   * mono_gc_wbarrier_set_field_internal -> **ExecutionAPI::GCSetField**
 * MINT_STFLD_VT
   * mono_value_copy_internal -> **ExecutionAPI::GCCopyValues**
 * MINT_LDTSFLDA
   * **Use ExecutionAPI::GetThreadStaticFieldAddress**
 * MINT_STOBJ_VT
   * mono_value_copy_internal -> **ExecutionAPI::GCCopyValues**
 * MINT_CONV_OVF_U8_R4, MINT_CONV_OVF_U8_R8, MINT_CONV_OVF_I8_R4, MINT_CONV_OVF_I8_R8
   * mono_try_trunc_i64 - copy the mono implementation
 * MINT_BOX
   * **Use ExecutionAPI::Box**
 * MINT_BOX_VT
   * **Use ExecutionAPI::Box**
 * MINT_BOX_PTR
   * **Use ExecutionAPI::Box**
 * MINT_BOX_NULLABLE_PTR
   * **Use ExecutionAPI::Box**
 * MINT_NEWARR
   * **Use ExecutionAPI::CreateSimpleArray**
 * MINT_NEWSTR
   * Used only by System.String.FastAllocateString intrinsic
   * mono_string_new_size_checked
 * MINT_LDLEN
   * Used only by System.Array.get_Length intrinsic
   * mono_array_length_internal
 * MINT_GETCHR
   * Used only by System.String.get_Chars intrinsic
   * mono_string_length_internal
   * mono_string_chars_internal
 * MINT_STRLEN
   * Used only by System.String.get_Length intrinsic
   * mono_string_length_internal
 * MINT_ARRAY_RANK
   * Used only by System.Array.get_Rank intrinsic
   * mono_object_class
 * MINT_ARRAY_ELEMENT_SIZE
   * Used only by System.Array.GetElementSize intrinsic
   * mono_object_class
   * mono_array_element_size
 * MINT_LDELEMA1
   * **Use ExecutionAPI::to get GetArrayElementAddress**
 * MINT_LDELEMA
   * **Use ExecutionAPI::to get GetArrayElementAddress**
 * MINT_LDELEM_* for I, U, R, REF
   * **Use ExecutionAPI::to get GetArrayElementAddress**
 * MINT_LDELEM_VT
   * **Use ExecutionAPI::to get GetArrayElementAddress**
 * MINT_STELEM_* for I, U, R
   * **Use ExecutionAPI::to get GetArrayElementAddress**
 * MINT_STELEM_REF_UNCHECKED
   * **Use ExecutionAPI::to get GetArrayElementAddress**
 * MINT_STELEM_REF
   * **Use ExecutionAPI::to get GetArrayElementAddress**
 * MINT_STELEM_VT
   * **Use ExecutionAPI::to get GetArrayElementAddress**
 * MINT_STELEM_VT_NOREF
   * **Use ExecutionAPI::to get GetArrayElementAddress**
 * MINT_CKFINITE_R4, MINT_CKFINITE_R8
   * mono_isfinite - copy mono implementation or use std::isfinite if we move to C++
 * MINT_MONO_RETOBJ
   * Mono specific -> N.A. on CoreCLR
   * mono_method_signature_internal
 * MINT_MONO_LDDOMAIN
   * Obsolete and mono specific -> N.A. on CoreCLR
   * mono_domain_get
 * MINT_C{cc}_R*
   * mono_isunordered - copy mono implementation
 * MINT_LDFTN_DYNAMIC
   * Used only by System.RuntimeMethodHandle.GetFunctionPointer intrinsic
   * Comment in transform.c says: We must intrinsify this method on interp so we don't return a pointer to native code entering interpreter.
   * Q: why is the implementation checking for attributes / generic type definition when there is only one use case?
   * mono_class_is_gtd // generic type definition
   * mono_exception_from_name_msg
   * mono_method_has_unmanaged_callers_only_attribute
 * MINT_PROF_ENTER
   * mono_trace_enter_method -> **ProfilerAPI::TraceEnterMethod**
 * MINT_PROF_EXIT, MINT_PROF_EXIT_VOID
   * mono_trace_leave_method -> **ProfilerAPI::TraceLeaveMethod**
 * MINT_INTRINS_GET_HASHCODE
   * **Use ExecutionAPI::GetHashCode**
 * MINT_INTRINS_TRY_GET_HASHCODE
   * **Use ExecutionAPI::GetHashCode**
 * MINT_METADATA_UPDATE_LDFLDA
   * Load address of a field that was added by EnC
   * On CoreCLR, it will likely need to call a JIT helper. So we may let the transformation phase store the helper address in the MINT_METADATA_UPDATE_LDFLDA instruction, maybe even create a new IR opcode for all field accesses via a helper in general. Or add an ExecutionAPI method to invoke the JIT_GetFieldAddr which is the helper used here.
   * mono_metadata_update_added_field_ldflda
 * MINT_TIER_PREPARE_JITERPRETER
   * WASM specific (JITerpreter)
   * mono_jiterp_patch_opcode
   * mono_jiterp_patch_opcode
 * MINT_TIER_MONITOR_JITERPRETER
   * WASM specific (JITerpreter)
   * mono_jiterp_monitor_trace
### interp_set_resume_state
 * mono_gchandle_free_internal - N.A. on CoreCLR, the handle is used to keep the exception object alive and the EH in CoreCLR handles that on its own
 * mono_gchandle_new_internal - dtto
### interp_run_finally
 * Run the finally clause identified by CLAUSE_INDEX in the interpreter frame given by frame->interp_frame.
 * mono_llvm_start_native_unwind - propagate an exception from the finally
### interp_run_filter
 * Run the filter clause identified by CLAUSE_INDEX in the interpreter frame given by frame->interp_frame
 * mono_llvm_start_native_unwind - strange, it seems to propagate an exception from the filter while it should be swallowed
### interp_run_clause_with_il_state
 * This is used to run clauses that are located in AOTed code
 * It is Mono specific and it is used to run filters and finallys when running LLVM AOT compiled code, as it uses standard native exception handling for AOT code and filters would not work there due to the nature of the native EH.
 * Run exception handling clause
 * mono_method_signature_internal -> **JIT2EEInterface::getMethodInfo, CORINFO_METHOD_INFO::args**
 * mono_method_get_header_internal
 * mono_metadata_free_mh
 * mono_llvm_start_native_unwind
### interp_print_method_counts
 * Internal interpreter diagnostic prints
 * mono_method_full_name -> **JIT2EEInterface::getMethodNameFromMetadata**
### metadata_update_backup_frames
 * mono_trace
 * mono_method_full_name -> **JIT2EEInterface::getMethodNameFromMetadata**
### interp_invalidate_transformed
 * This is used to invalidate "transformed" state of all InterpMethod instances. It is used when EnC updates stuff.
 * mono_metadata_has_updates
 * mono_stop_world -> **ExecutionAPI::SuspendEE**
 * mono_alc_get_all - this is specific to how mono stores all the InterpMethod instances
 * mono_restart_world -> **ExecutionAPI::RestartEE**
### interp_jit_info_foreach
 * This iterates over all InterpMethod instances and copies out some mono specific JIT info. Mono uses it at one place only for eventpipe rundown.
 * mono_alc_get_all - this is specific to how mono stores all the InterpMethod instances
### mono_ee_interp_init
 * mono_ee_api_version - for assert that the runtime API version matches what the interpreter was built against,
 * mono_native_tls_alloc - reserve thread specific data id for thread context (~ pthread_key_create on Unix). For CoreCLR, we should probably use regular TLS variable instead.
### mono_jiterp_check_pending_unwind
 * WASM specific (JITerpreter)
 * mono_llvm_start_native_unwind - throw C++ exception to unwind out of the current block of interpreter frames
### mono_jiterp_interp_entry
 * WASM specific (JITerpreter). It is called when a call from interpreter to JITerpreted code returns.
 * mono_threads_detach_coop -> **ExecutionAPI::EnablePreemptiveGC**
 * mono_llvm_start_native_unwind - throw C++ exception to unwind out of the current block of interpreter frames
 
## Functions that Mono runtime invokes
The list below contains all the functions from interp.c that the runtime can invoke. These include debugger related methods too.
 * interp_entry_from_trampoline
 * interp_to_native_trampoline
 * interp_create_method_pointer
 * interp_create_method_pointer_llvmonly
 * interp_free_method
 * interp_runtime_invoke
 * interp_init_delegate
 * interp_delegate_ctor
 * interp_set_resume_state
 * interp_get_resume_state
 * interp_run_finally
 * interp_run_filter
 * interp_run_clause_with_il_state
 * interp_frame_iter_init
 * interp_frame_iter_next
 * interp_find_jit_info
 * interp_set_breakpoint
 * interp_clear_breakpoint
 * interp_frame_get_jit_info
 * interp_frame_get_ip
 * interp_frame_get_arg
 * interp_frame_get_local
 * interp_frame_get_this
 * interp_frame_arg_to_data
 * interp_data_to_frame_arg
 * interp_frame_arg_to_storage
 * interp_frame_get_parent
 * interp_start_single_stepping
 * interp_stop_single_stepping
 * interp_free_context
 * interp_set_optimizations
 * interp_invalidate_transformed
 * interp_cleanup
 * interp_mark_stack
 * interp_jit_info_foreach
 * interp_sufficient_stack
 * interp_entry_llvmonly
 * interp_get_interp_method
 * interp_compile_interp_method

 ## Functions that don't use any Mono APIs
These functions might still use glib APIs for memory allocation, bitset, hashtable or linked list.
 * need_native_unwind
 * lookup_imethod
 * append_imethod
 * get_target_imethod
 * get_vtable_ee_data 
 * get_method_table
 * alloc_method_table
 * get_virtual_method_fast
 * interp_throw_ex_general
 * ves_array_calculate_index
 * imethod_alloc0
 * get_arg_offset_fast
 * get_arg_offset
 * filter_type_for_args_from_sig
 * build_args_from_sig
 * interp_frame_arg_to_data
 * interp_data_to_frame_arg
 * interp_frame_arg_to_storage
 * interp_to_native_trampoline
 * ftnptr_to_imethod
 * imethod_to_ftnptr
 * jit_call_cb
 * do_icall_wrapper
 * interp_entry_general
 * interp_entry_llvmonly
 * interp_get_interp_method
 * interp_compile_interp_method
 * interp_no_native_to_managed
 * no_llvmonly_interp_method_pointer
 * interp_free_method
 * mono_interp_enum_hasflag
 * interp_simd_create
 * g_warning_d
 * interp_error_xsx
 * method_entry
 * min_f, max_f, min_d, max_d
 * interp_parse_options
 * interp_frame_get_ip
 * interp_frame_iter_init
 * interp_frame_iter_next
 * interp_find_jit_info
 * interp_set_breakpoint
 * interp_clear_breakpoint
 * interp_frame_get_jit_info
 * interp_frame_get_arg
 * interp_frame_get_local
 * interp_frame_get_this
 * interp_frame_get_parent
 * interp_start_single_stepping
 * interp_stop_single_stepping
 * interp_mark_frame_no_ref_slots
 * interp_mark_no_ref_slots
 * interp_mark_stack
 * opcode_count_comparer
 * interp_print_op_count
 * interp_add_imethod
 * imethod_opcount_comparer
 * interp_print_method_counts
 * interp_set_optimizations
 * invalidate_transform
 * copy_imethod_for_frame
 * metadata_update_prepare_to_invalidate
 * interp_copy_jit_info_func
 * interp_sufficient_stack
 * interp_cleanup
 * mono_jiterp_stackval_to_data
 * mono_jiterp_stackval_from_data
 * mono_jiterp_get_arg_offset
 * mono_jiterp_overflow_check_i4
 * mono_jiterp_overflow_check_u4
 * mono_jiterp_ld_delegate_method_ptr
 * mono_jiterp_check_pending_unwind
 * mono_jiterp_get_context
 * mono_jiterp_frame_data_allocator_alloc
 * mono_jiterp_isinst
 * mono_jiterp_interp_entry
 * mono_jiterp_get_polling_required_address
 * mono_jiterp_do_safepoint
 * mono_jiterp_imethod_to_ftnptr
 * mono_jiterp_enum_hasflag
 * mono_jiterp_get_simd_intrinsic
 * mono_jiterp_get_simd_opcode
 * mono_jiterp_get_opcode_info
 * mono_jiterp_placeholder_trace
 * mono_jiterp_placeholder_jit_call
 * mono_jiterp_get_interp_entry_func
