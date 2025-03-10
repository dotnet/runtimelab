# Transformation phase
The transformation phase "JITs" the IL code into an IR representation that the execution phase executes later.
This documents lists all functions in the transform.c file in the Mono interpreter codebase.

# Considerations for coreclr

There are various cases when a call to runtime helper is needed to perform some operation. This is likely different for Mono (may use a different set of helpers or may not need a helper). So handling such cases will likely need to be added to the transformation and execution phases. Precise details on where and how are beyond the scope of this document.

#  MonoClass, MonoField, MonoMethod and MonoType member access helpers (m_ macros map)
The following functions are helpers used to access fields in MonoClass, MonoField, MonoMethod and MonoType. They are used all over the place, so they are described here instead of at the specific places they are used.
 * m_class_get_byval_arg - N.A. on CoreCLR
 * m_class_get_element_class -> getChildType
 * m_class_get_image -> getMethodInfo, CORINFO_METHOD_INFO::scope
 * m_class_get_name -> getClassNameFromMetadata
 * m_class_get_name_space -> getClassNameFromMetadata
 * m_class_get_nested_in -> N.A., it is used in interp_handle_intrinsics only to get namespace name for nested classes, getClassNameFromMetadata just works for nested classes too
 * m_class_get_parent -> getParentType
 * m_class_get_rank -> getArrayRank
 * m_class_get_runtime_vtable - N.A. on CoreCLR
 * m_class_get_this_arg - N.A. on CoreCLR
 * m_class_has_references -> getClassAttribs() & CORINFO_FLG_CONTAINS_GC_PTR 
 * m_class_has_ref_fields -> getClassGClayout(classHnd) > 0
 * m_class_has_weak_fields - N.A. on CoreCLR
 * m_class_is_array -> getClassAttribs() & CORINFO_FLG_ARRAY
 * m_class_is_byreflike -> getClassAttribs() & CORINFO_FLG_BYREF_LIKE
 * m_class_is_enumtype -> isEnum
 * m_class_is_inited - N.A. on CoreCLR
 * m_class_is_sealed -> getMethodAttribs() & CORINFO_FLG_FINAL
 * m_class_is_simd_type -> getClassNameFromMetadata
   * compare namespace to "System.Numerics" and method to "Vector2", "Vector3", "Vector4", "Quaternion", "Plane"
   * compare namespace to "System.Runtime.Intrinsics" and method to "Vector64\`1" (Arm64 only), "Vector128\`1", "Vector256\`1" (x64 only) or "Vector512\`1" (x64 only) and then check if getTypeForPrimitiveNumericClass(getTypeInstantiationArgument()) is >= CORINFO_TYPE_BYTE && <= CORINFO_TYPE_DOUBLE. See Compiler::getBaseJitTypeAndSizeOfSIMDType for more details on how CoreCLR JIT does it.
 * m_class_is_valuetype -> isValueClass
 * m_field_get_offset -> getFieldOffset
 * m_field_get_parent -> getFieldClass
 * m_field_is_from_update -> getFieldInfo, CORINFO_FIELD_INFO::fieldFlags & CorInfoFlag.CORINFO_FLG_EnC
 * m_method_is_static -> getMethodAttribs() & CORINFO_FLG_STATIC
 * m_type_is_byref -> asCorInfoType() == CORINFO_TYPE_BYREF

# Functions that have calls to Mono APIs
For each Mono API or a Mono specific code sequence, it describes how to replace it using JIT2EEInterface methods. In cases when the whole function would be reimplemented in a slightly different way instead of just replacing Mono API calls by their JIT2EEInterface equivalents, a high level description of the function behavior with details on what JIT2EEInterface methods to use is provided.

### has_intrinsic_attribute
* replace with isIntrinsic call

### has_doesnotreturn_attribute
* Checks whether the method has "System.Diagnostics.CodeAnalysis", "DoesNotReturnAttribute"
* ICorStaticInfo doesn't contain API to access custom attributes. Possible solutions:
   - avoid calls to has_doesnotreturn_attribute, it is part of inlining process, use just canInline in these places
   - add new method to the ICorStaticInfo, similar to isIntrinsic (that might involve extending the WellKnownAttributes enum)

### get_type_from_stack
* Gets mono type from interp's stack type. uses mono_defaults (src/mono/mono/metadata/class-internals.h) to return types from mono_defaults classes (mapped from stack type)
* Update to return CoreCLR class handle
* We might need to extend ICorStaticInfo API to get the default types or implement local helper.

### mono_mint_type
* Returns interp's mint type for type
* m_class_is_enumtype -> isEnum
* mono_class_enum_basetype_internal -> isEnum - underlyingType parameter
* m_class_get_byval_arg - N.A. on CoreCLR

### interp_create_var_explicit
* m_class_is_simd_type - check the m_ macros map above
* mono_class_from_mono_type_internal - N.A., just use CORINFO_CLASS_HANDLE

### interp_create_dummy_var
* m_class_get_byval_arg (mono_defaults.void_class) -> see get_type_from_stack
* Update to use CoreCLR void class

### interp_create_ref_handle_var
* m_class_get_byval_arg (mono_defaults.int_class) -> see get_type_from_stack
* Update to use CoreCLR int class

### push_var
* mono_class_from_mono_type_internal - N.A., just use CORINFO_CLASS_HANDLE

### push_mono_type
* mono_class_from_mono_type_internal - N.A., just use CORINFO_CLASS_HANDLE
* m_type_is_byref -> asCorInfoType() == CORINFO_TYPE_BYREF

### merge_stack_type_information
* This uses mono class field in the state, that should be resolved with state containing CoreCLR class handles instead

### handle_branch
* mono_threads_are_safepoints_enabled -> N.A. on CoreCLR
* Mono sets this flag by:
        switch (p) {
        case MONO_THREADS_SUSPEND_FULL_COOP:
        case MONO_THREADS_SUSPEND_HYBRID:
                return TRUE;
        default:
                return FALSE;
        }
* Always add safepoints (as if the mono_threads_are_safepoints_enabled always returned true)

### two_arg_branch
* m_class_get_name -> getClassNameFromMetadata

### binary_arith_op
* m_class_get_name -> getClassNameFromMetadata

### shift_op
* m_class_get_name -> getClassNameFromMetadata

### get_arg_type_exact
*  mono_method_signature_internal -> getMethodSig
*  m_class_get_byval_arg - N.A. on CoreCLR

### load_arg
* mono_method_signature_internal -> getMethodSig
  mono_class_from_mono_type_internal - N.A., just use CORINFO_CLASS_HANDLE
* mono_method_signature_internal (td->method)->pinvoke && !mono_method_signature_internal (td->method)->marshalling_disabled -> (getMethodAttribs(...) & CORINFO_FLG_PINVOKE) && pInvokeMarshalingRequired(...)
* Simplify code with 2 following calls to just use getClassSize
* mono_class_native_size
* mono_class_value_size

### store_arg
* Same changes as load_arg above

### load_local (TransformData *td, int local)
* mono_class_from_mono_type_internal - N.A., just use CORINFO_CLASS_HANDLE

### mono_interp_jit_call_supported
* Checks if a specific call to AOT compiled code (the JIT in the name is due to historical reasons) is supported by the interpreter
* mono_jit_call_can_be_supported_by_interp - rejects calls with more than 10 arguments, pinvokes, internal calls, string constructor, methods with wrappers and methods with StackCrawlMark locals.
* Then the function returns true if:
  * the target method is in AOTed module and it isn't marked as "interpret only"
  * or if the method's class is on an explicit list of classes to JIT (testing feature)
* Otherwise it returns false, indicating the call should be interpreted

### jit_call2_supported
* Checks if the target function is a pinvoke, internal call, generic method or string constructor and return false for those
* Otherwise force JITting of the target method and invoking the jitted form when tiered compilation is enabled. It emits the MINT_JIT_CALL2 IR code in this case.

### interp_generate_mae_throw
### interp_generate_void_throw
### interp_generate_ipe_throw_with_msg
* Calls method access error icall
* mono_get_jit_icall_info
* Reimplement as part of the error handling replacement

### interp_generate_ipe_bad_fallthru
* mono_disasm_code_one -> N.A. on CoreCLR?
 - ILCode is available with getMethodInfo
 - we can reuse code from JIT to disassemble IL
* Reimplement as part of the error handling replacement

### interp_create_var
* mono_type_size -> getClassSize

### interp_dump_ins_data
* m_class_get_name -> getClassNameFromMetadata
* m_class_get_name_space -> getClassNameFromMetadata
* mono_method_full_name -> printMethodName
  - the mono method is part of mono's debug helpers. Either replace with printMethodName or implement debug helper

### mono_interp_print_code
* mono_method_full_name -> printMethodName (see more info above in interp_dump_ins_data)

### interp_method_get_header
* This can be removed and replace calls to it with getMethodInfo and use CORINFO_METHOD_INFO fields

### interp_emit_ldobj
* m_class_get_byval_arg - N.A. on CoreCLR
* mono_class_value_size -> getClassSize

### interp_emit_stobj
* m_class_get_byval_arg - N.A. on CoreCLR
* m_class_has_references -> getClassAttribs() & CORINFO_FLG_CONTAINS_GC_PTR
* mono_class_value_size -> getClassSize

### interp_emit_ldelema
* m_class_get_element_class -> getChildType
* m_class_get_rank -> getArrayRank
* mono_class_array_element_size  -> getChildType, getClassSize
* m_class_get_byval_arg - N.A. on CoreCLR
* m_class_is_valuetype  -> isValueClass

### interp_emit_metadata_update_ldflda
* m_field_is_from_update -> getFieldInfo, CORINFO_FIELD_INFO::fieldFlags & CorInfoFlag.CORINFO_FLG_EnC
* m_type_is_byref -> asCorInfoType() == CORINFO_TYPE_BYREF
* mono_class_from_mono_type_internal - N.A., just use CORINFO_CLASS_HANDLE
* m_class_get_byval_arg - N.A. on CoreCLR
* mono_metadata_make_token - not needed, we will not use the tokens here anymore
* mono_metadata_update_get_field_idx -> N.A. on CoreClr, we will change the MINT_METADATA_UPDATE_LDFLDA instruction to use helper function instead of the token, CORINFO_HELP_GETFIELDADDR in this case

### interp_handle_intrinsics
Emits code to handle intrinsics or sets the op output parameter
* m_class_get_byval_arg - N.A. on CoreCLR
* m_class_get_element_class -> getChildType
* m_class_get_image -> getMethodInfo, CORINFO_METHOD_INFO::scope
* m_class_get_name -> getClassNameFromMetadata
* m_class_get_name_space -> getClassNameFromMetadata
* m_class_get_nested_in -> N.A., it is used only to get namespace name for nested classes, getClassNameFromMetadata just works for nested classes too 
* m_class_has_references -> getClassAttribs() & CORINFO_FLG_CONTAINS_GC_PTR
* m_class_has_ref_fields -> getClassGClayout(classHnd) > 0
* m_class_is_enumtype -> isEnum
* m_class_is_valuetype -> isValueClass
* m_field_get_offset -> getFieldOffset
* m_mono_type_internal - N.A., just use CORINFO_CLASS_HANDLE
* m_type_is_byref -> asCorInfoType() == CORINFO_TYPE_BYREF
* mini_get_underlying_type -> asCorInfoType
* mini_is_gsharedvt_variable_klass - N.A. on CoreCLR -> treat as always FALSE
* mini_should_insert_breakpoint - replace by TRUE, always insert breakpoint for S.D.Debugger.Break
* mono_class_array_element_size -> getChildType, getClassSize
* mono_class_from_mono_type_internal - N.A., just use CORINFO_CLASS_HANDLE
* mono_class_get_field_from_name_full -> getFieldInClass and use num instead of name
* mono_class_get_generic_class -> not needed, only used for mini_is_gsharedvt_variable_klass, which will be always FALSE. it is used here to get generic class parameter type
* mono_class_init_internal - N.A., classes passed over the Jit2EEInterface are always fully loaded
* mono_class_is_nullable -> isNullableType
* mono_class_is_subclass_of_internal (target_method->klass, mono_defaults.array_class, FALSE)
* mono_class_value_size -> getClassSize
* mono_field_get_rva -> getFieldInfo and use fieldInfo->fieldLookup.addr
* mono_method_get_context - it is used to get instantiation argument 0 type - use getTypeInstantiationArgument(cls, 0)
* mono_type_get_object_checked -> getRuntimeTypePointer
* mono_type_get_underlying_type -> isEnum - underlyingType parameter or the class itself, when not enum
* mono_type_size -> getClassSize

### tiered_patcher
* mono_method_signature_internal -> getMethodSig

### interp_mark_ref_slots_for_var
* Use getClassGClayout

### interp_mark_ref_slots_for_vt
* Use getClassGClayout without having to drill through the value type fields and their types

### is_ip_protected
* Use getEHinfo, getMethodInfo, CORINFO_METHOD_INFO::Ehcount

### handle_stelem
* mono_class_from_mono_type_internal - N.A., just use CORINFO_CLASS_HANDLE
* The intent of this function is to rewrite MINT_STELEM_REF with MINT_STELEM_REF_UNCHECKED if lhs is T[] and rhs is T and T is sealed. JIT2EEInterface::getChildType for T[] returns the T (in clsRet output arg), so we can then match it to the element type

### initialize_clause_bblocks
* Use getEHinfo, getMethodInfo and then CORINFO_METHOD_INFO::Ehcount

### interp_handle_box_patterns
* cmethod->klass == mono_defaults.object_class -> cmethod->klass == getBuiltinClass(CLASSID_SYSTEM_OBJECT)
* mono_type_get_object_checked -> getRuntimeTypePointer
* mono_defaults.runtimetype_class -> getBuiltinClass(CLASSID_RUNTIME_TYPE)
* m_class_is_byreflike -> getClassAttribs(clsHnd) & CORINFO_FLG_BYREF_LIKE
* mono_class_is_assignable_from_internal -> compareTypesForCast returns TypeCompareState::Must in this case

### get_class_from_token
* resolveToken, getTokenTypeAsHandle

### interp_emit_sfld_access
* Use getFieldInfo to replace all the Mono specific machinery in this function. 
  * For fields created by EnC -CORINFO_FIELD_INFO::fieldFlags & CORINFO_FLG_EnC
  * CORINFO_FIELD_INFO::fieldAccessor to figure out how to access the field
  * CORINFO_FIELD_INFO::helper to get the helper needed to access the field (if any)
    
### interp_emit_ldsflda
* m_field_get_parent -> getFieldClass
* mono_class_vtable_checked - n.a., use CORINFO_TYPE_CLASS
* mono_class_field_is_special_static - see [interp_emit_sfld_access](#interp_emit_sfld_access)
* mono_special_static_field_get_offset - see [interp_emit_sfld_access](#interp_emit_sfld_access)

### interp_handle_isinst
* This handles both isinst and castclass
* It has special handling (generates different IR opcodes) for 
  * Non-generic interfaces 
  * For non-generic non-arrays that are not nullable
* Generic type can be checked by getTypeInstantiationArgument(clsHnd, 0) returning NO_CLASS_HANDLE
* Array can be checked by getArrayRank returning non-zero or by getClassFlags() & CORINFO_FLG_ARRAY

### type_has_references
* Use getClassFlags() & CORINFO_FLG_CONTAINS_GC_PTR
        
### interp_method_compute_offsets
* Computes offsets of arguments in the interpreter stack and fill in the variable types 
* Iterates over arguments, the ported version would iterate like this:
  * CORINFO_ARG_LIST_HANDLE arg = 0
  * getArgNext(arg) to get next arg
  * getArgType
* Swift specific - get special swift_error argument - implement the same way as Compiler::impPopArgsForSwiftCall
* mono_class_has_failure - N.A., all the classes we get are fully loaded
* mono_error_set_for_class_failure - N.A.
* header->num_clauses -> getMethodInfo, CORINFO_METHOD_INFO::Ehcount
    
### mono_interp_type_size
* Use getClassSize + getClassAlignmentRequirement
   
### interp_save_debug_info
* This function generates debug info for a specified method. It comprises argument info, local vars info, line numbers and code start and size.
* Use setBoundaries, setVars

### get_basic_blocks
* Iteration over EH clauses:
  * header->num_clauses -> getMethodInfo, CORINFO_METHOD_INFO::Ehcount
  * getEHinfo to get details for each EH clause

### interp_field_from_token
* Use resolveToken, getFieldInfo

### interp_emit_swiftcall_struct_lowering
* Swift only
* Implement it the way Compiler::impPopArgsForSwiftCall does
    
### interp_try_devirt
* Use resolveVirtualMethod

### get_virt_method_slot
* Use getMethodVTableOffset

### emit_convert
* mini_get_underlying_type -> asCorInfoType

### interp_get_method
* Use resolveToken, CORINFO_RESOLVED_TOKEN::hMethod
    
### interp_constrained_box
* mono_class_is_nullable -> isNullableType
* mono_class_vtable_checked - N.A, use CORINFO_CLASS_HANDLE in the data for MINT_BOX_PTR.

### interp_inline_newobj 
* Bails out for classes with finalizers (and weak fields which is not something that coreclr would support)
* mono_class_has_finalizer -> getNewHelper, pHasSideEffects output argument is set to true.
* m_class_is_valuetype -> isValueClass 
* mono_class_vtable_checked -> N.A., use the CORINFO_CLASS_HANDLE
* interp_method_get_header -> N.A., use the CORINFO_METHOD_HANDLE
* mono_metadata_free_mh - N.A. for coreclr

### interp_inline_method
* mono_method_signature_internal -> getMethodSig
* mono_method_get_generic_container->context -> CORINFO_SIG_INFO::sigInst
* has_intrinsic_attribute -> isIntrinsic
* target_method->iflags & METHOD_IMPL_ATTRIBUTE_AGGRESSIVE_INLINING -> getMethodAttrs & CORINFO_FLG_FORCEINLINE
* MONO_PROFILER_RAISE(inline_method) macro invocation (calls mono_profiler_raise_inline_method) for InterpMethods marked by MONO_PROFILER_CALL_INSTRUMENTATION_TAIL_CALL.

### interp_method_check_inlining
* Use canInline instead of all the machinery below

### is_metadata_update_disabled
* Checks for hot reload enabled in general -> there is no equivalent on the Jit2EEInterface

### interp_get_icall_sig
* Mono supports limited number of signature types for icalls (internal calls). This function gets an enum value describing the specific convention by analyzing the passed in signature. For example MINT_ICALLSIG_P_V represents a void returning function (V) with a single argument that can be passed as pointer sized value (P) like int, boolean, enum, reference, pointer, ...
* The execution part then switches over this when invoking the icall to cast the icall address to a function pointer matching the signature and pass the correct number of arguments to it.
* Use CORINFO_SIG_INFO::numArgs, CORINFO_SIG_INFO::args

### is_scalar_vtype
* Return whenever TYPE represents a vtype with only one scalar member
* Only used by interp_get_icall_sig
* Implement like Compiler::isTrivialPointerSizedStruct

### interp_transform_internal_calls
* Looks mono specific - gets a wrapper for calling the internal call - native wrapper or synchronized wrapper
* Might need something like this for QCALLs though.

### interp_transform_call
* This function figures out the type of the call and emits one of the following IR call instructions: MINT_JIT_CALL, MINT_CALL_DELEGATE, MINT_CALLI_NAT_FAST, MINT_CALLI_NAT_DYNAMIC, MINT_CALLI_NAT, MINT_CALLI, MINT_CALL_VARARG, MINT_CALLVIRT_FAST, MINT_CALL.
* Use getCallInfo as a source of information
* It is possible that for coreclr, the set of call types that we would want to distinguish would be different.
* Here is what it does in high level view:
  * Get the signature
  * If swift interop is compiled in, do swift lowering, which gets a transformed signature
  * Perform access check
  * For string ctors, get a different signature (modify the return type to string)
  * For intrinsics, call interp_handle_intrinsics and be done
  * For constrained_class that is enum and the target method is GetHashCode, replace the method called by a base type one, comment says it is to avoid boxing.
  * Further for constrained_class
    * get a new target_method based on the constraint
    * One more time for intrinsics, call interp_handle_intrinsics and be done
    * mono_class_has_dim_conflicts (constrained_class) && mono_class_is_method_ambiguous (constrained_class, virt_method) -> generate throw IR
    * Follow the rules for constrained calls from ECMA spec
  * For abstract methods generate throw IR
  * Handle tail calls
  * Try to devirtualize the call
  * interp_transform_internal_calls
  * Optionally inline the call and be done
  * If this is called while inlining a method do some heuristic that may trigger rejection of the inlining
  * convert delegate invoke to a indirect call on the interp_invoke_impl field
  * Create and align call arguments on the stack, also create call_args array using create_call_args (this represents an array of all call arg vars in the order they are pushed to the stack. This makes it easy to find all source vars for these types of opcodes. This is terminated with -1)
  * Allocate interp stack slot for the return value
  * For intrinsics, emit the IR instruction that the intrinsic required and set dreg / sregs on the interpreter instruction 
  * For JIT calls, emit MINT_JIT_CALL
  * For delegate calls, emit MINT_CALL_DELEGATE
  * For calli
    * Handle calls to native code
    * Handle icalls emission, use MINT_CALLI_NAT_FAST
    * Handle pinvokes in dynamically emitted modules via MINT_CALLI_NAT_DYNAMIC
    * Handle other calls to native code via MINT_CALLI_NAT
    * Otherwise emit MINT_CALLI
  * For other than calli
    * If the callee has vararg calling convention, emit MINT_CALL_VARARG
    * If it is a virtual method, emit MINT_CALLVIRT_FAST
    * Otherwise emit MINT_CALL
    * Tiered compilation: For all of the three cases above, if the call is patchable, mark the IR instruction by INTERP_INST_FLAG_RECORD_CALL_PATCH flag and add the IR instruction as a key to patchsite_hash table with the target method as a value
  * Alocate some call info data structure attached to the just generated IR instruction

### mono_interp_transform_method
* This function "compiles" a method into the IR. It has a special handling for MultiCastDelegate invocation methods and internal / runtime calls.
* mono_metadata_update_thread_expose_published
* Return error if the class containing the method is not fully instantiated
  * mono_class_is_open_constructed_type -> N.A., open types are never passed over the Jit2EEInterface
  * mono_error_set_invalid_operation -> N.A., see ^^^
* Ensure the class containing the method is initialized
  * mono_class_vtable_checked - N.A., use the CORINFO_CLASS_HANDLE instead of vtable
  * method_class_vt->initialized, mono_runtime_class_init_full -> initClass
* Get generic context of the method - N.A., open types are never passed over the Jit2EEInterface
  * mono_method_signature_internal (method)->is_inflated
  * mono_method_get_context
  * mono_method_get_generic_container
  * generic_container->context
* If method->iflags & (METHOD_IMPL_ATTRIBUTE_INTERNAL_CALL | METHOD_IMPL_ATTRIBUTE_RUNTIME) -> internal calls and runtime methods
  * This means the method is represented by a native method, the code changes the method to call to a wrapper that invokes the native method
  * If the InterpMethod is already marked as transformed, return
  * If it is internal call that's not a member of Array
    * mono_marshal_get_native_wrapper
  * Else 
    * If it is MultiCastDelegate..ctor
      * mono_marshal_get_icall_wrapper(mono_get_jit_icall_info ()->ves_icall_mono_delegate_ctor_interp)
    * Else if it is MultiCastDelegate.Invoke
      * mono_marshal_get_delegate_invoke (method, NULL);
    * Else if it is MultiCastDelegate.BeginInvoke
      * mono_marshal_get_delegate_begin_invoke (method);
    * Else if it is MultiCastDelegate.EndInvoke
      * mono_marshal_get_delegate_end_invoke (method);
  * Mark the InterpMethod as transformed
* If the method is marked with UnsafeAccessor attribute
  * method = mono_marshal_get_unsafe_accessor_wrapper
  * In Jit2EEInterface, the getMethodInfo seems to handle unsafe accessors and some intrinsics
* Call the [generate](#generate) function
                
### generate
* This is the main function that translates IL to IR
* First it does some initialization of TransformData structure that holds all the info needed during the transformation
* It calls [generate_code](#generate_code) to generate the IR of the method
* It calls generate_compacted_code to perform some relocations in the IR generated by the previous call
* Finally it fills some InterpMethod fields like EH info, seq points etc.
        
### generate_code
* m_class_get_image -> getMethodInfo, CORINFO_METHOD_INFO::scope
* mono_method_signature_internal  -> getMethodInfo, CORINFO_METHOD_INFO::args
* mono_basic_block_split
  * Generate list of basic blocks from the IL of the method
  * We may want to copy this function from Mono
* Get Debug seq points for the method being translated
  * Replace all the sequence points extraction by getBoundaries
  * mono_debug_lookup_method - lookup symbol information for the method
  * mono_debug_lookup_method_async_debug_info - lookup debug info for async method in portable PDB file
  * mono_debug_get_seq_points -> getBoundaries
  * mono_debug_image_has_debug_info - check if there is a PDB debug file for the assembly containing the method being transformed
  * mono_debug_generate_enc_seq_points_without_debug_info - returns true if there is no debug info, so the interpreter should generate seq points as it processes the IL. In that case, the transformation phase inserts MINT_SDB_SEQ_POINT IR opcode on each CEE_NOP, CEE_CALL, CEE_CALVIRT and CEE_CALLI.
* mono_debugger_method_has_breakpoint - check if there is a breakpoint set at method being transformed. 
  * Interpreter inserts MINT_BREAKPOINT IR opcode at the beginning of the method if there was a breakpoint set
* If verbose_level
  * mono_disasm_code
  * mono_method_full_name
* mono_trace_eval
* mono_threads_are_safepoints_enabled
* mono_opcode_size
* If verbose_level
  * mono_opcode_name
* Switch over all IL codes
* m_class_get_byval_arg(klass)
  * This basically just gets MonoType representing the MonoClass. E.g. System.Int32 would get a MonoType with MONO_TYPE_I4.
  * The naming comes from the fact that it returns type (MonoType - ~TypeSpec) used to pass an object of the specified class as argument by value, but the usage in the mono interpreter is wider than that.
  * Used by
    * CEE_LDTOKEN
    * CEE_STELEM 
    * CEE_LDELEM 
    * CEE_BOX
    * CEE_STFLD
    * CEE_UNBOX_ANY
    * CEE_UNBOX
    * CEE_NEWOBJ
    * CEE_CPOBJ
* m_class_get_parent == mono_defaults.array_class -> getArrayRank() != 0 ?
* mini_get_class -> resolveToken, CORINFO_RESOLVED_TOKEN::hClass
* mini_type_get_underlying_type -> asCorInfoType
  * Used by CEE_RET only
* mono_class_array_element_size -> getChildType, getClassSize
* mono_class_get_and_inflate_typespec_checked -> resolveToken, CORINFO_RESOLVED_TOKEN::hClass
* mono_class_get_flags -> getClassAttribs
* mono_class_get_method_from_name_checked
  * Used to get method by name, but it is used in places where we can use other technique to get the result we want
  * In CEE_BOX, use getBoxHelper
  * In CEE_UNBOX, use getUnboxHelper
  * In interp_transform_call, it is used to get GetHashCode method on an enum base type. Use isEnum, the underlyingType output argument. This is not needed for coreclr, the getCallInfo takes care of that.
* mono_class_get_nullable_param_internal -> getTypeInstantiationArgument(clsHandle, 0)
* mono_class_has_dim_conflicts - N.A., handling such stuff is hidden behind the Jit2EEInterface
  * It checks if class has conflicting default interface methods
  * Used by CEE_LDFTN
* mono_class_has_finalizer -> getNewHelper, pHasSideEffects output argument is set to true.
* mono_class_inflate_generic_type_checked - N.A., generic types are always passed as inflated over the Jit2EEInterface
* mono_class_init_internal - N.A., classes passed over the Jit2EEInterface are always fully loaded
* mono_class_is_assignable_from_internal -> compareTypesForCast
  * It is used by CEE_ISINST only to optimize the case when the previous IR was one of the MINT_BOX*
* mono_class_is_method_ambiguous -> N.A., handling such stuff is hidden behind the Jit2EEInterface
* mono_class_is_nullable -> isNullableType
* mono_class_native_size - N.A., used by mono specific IL opcodes only
* mono_class_setup_fields - N.A., handling such stuff is hidden behind the Jit2EEInterface
* mono_class_value_size -> getClassSize
* mono_class_vtable_checked - N.A. for coreclr
* mono_error_set_for_class_failure - N.A., classes passed over the Jit2EEInterface are always fully loaded
* mono_error_set_generic_error - error representing System.InvalidProgramException is returned from the generate_code
* mono_error_set_member_access - when attempt to use CEE_NEWOBJ on an abstract class, error with message "Cannot create an abstract class: XYZ" is returned from the generate_code.
* mono_field_get_type_internal -> getFieldInfo, CORINFO_FIELD_INFO::fieldType
* mono_get_method_checked -> resolveToken, CORINFO_RESOLVED_TOKEN::hMethod
* mono_get_method_constrained_with_method
  * In interp_transform_call with preceeding CEE_CONSTRAINED -> pass the token from the constrained to getCallInfo as the pConstrainedResolvedToken argument
  * In CEE_LDFTN with preceeding CEE_CONSTRAINED -> use getCallInfo with the token from the constrained as the pConstrainedResolvedToken argument
* mono_ldstr_checked -> getStringLiteral
* mono_marshal_get_managed_wrapper - N.A. for coreclr
* mono_metadata_token_index -> constructStringLiteral or getLazyStringLiteralHelper
  * Used in CEE_LDSTR
* mono_metadata_token_table - N.A., these details are hidden behind token resolving
  * Used in CEE_SIZEOF
* mono_method_can_access_method -> getCallInfo, CORINFO_CALL_INFO::accessAllowed
  * Used by CEE_LDFTN 
* mono_method_get_context
  * It is needed to get the instantiation argument 0 for Span and ReadOnlySpan constructor.
  * So we can get it via getTypeInstantiationArgument(clsHandle, 0)
* mono_method_get_wrapper_data - N.A., Mono specific
* mono_method_has_unmanaged_callers_only_attribute -> N.A., it was used only for sanity checks and the the getCallInfo does those internally
  * Used by CEE_LDFTN
* mono_mint_type -> asCorInfoType
* mono_trace_eval
  * Mono specific, will need to have something similar for profiler for coreclr
* mono_type_size(mono_type_create_from_typespec_checked) -> resolveToken, getClassSize( CORINFO_RESOLVED_TOKEN::hClass)
  * Used in CEE_SIZEOF
* mono_type_get_desc -> getClassNameFromMetadata
* mono_type_get_full_name -> getClassNameFromMetadata
* mono_type_get_object_checked -> getRuntimeTypePointer
  * gets System.RuntimeType object for a MonoType
  * Used by CEE_LDTOKEN

## Functions that don't use any Mono APIs
These functions might still use glib APIs for memory allocation, bitset, hashtable or linked list. Or they use mono_interp_* or mono_mint_* functions, which are local to the interpreter.

* interp_new_ins
* interp_add_ins
* interp_add_ins_explicit
* interp_insert_ins
* interp_insert_ins_bb
* interp_clear_ins
* interp_ins_is_nop
* interp_prev_ins
* interp_next_ins
* get_stack_size
* get_tos_offset
* interp_create_stack_var
* set_type_and_var
* set_simple_type_and_var
* interp_make_var_renamable
* interp_create_renamed_fixed_var
* push_type
* push_simple_type
* push_type_vt
* push_types
* interp_get_mov_for_type
* get_mint_type_size
* try_fold_one_arg_branch
* interp_add_conv
* try_fold_two_arg_branch
* unary_arith_op(TransformData *td, int mint_op)
* can_store
* emit_ldptr
* mono_interp_print_td_code
* interp_ip_in_cbb
* interp_ins_is_ldc
* get_type_comparison_op
* check_stack_helper
* ensure_stack
* realloc_stack
* push_type_explicit
* fixup_newbb_stack_locals
* init_bb_stack_state
* one_arg_branch
* store_local
* init_last_ins_call
* imethod_alloc0
* interp_generate_icall_throw
* interp_dump_compacted_ins
* interp_dump_code
* interp_dump_ins
* get_data_item_wide_index
* get_data_item_index
* get_data_item_index_imethod
* is_data_item_wide_index
* get_data_item_index_nonshared
* interp_dump_bb
* interp_get_const_from_ldc_i4
* interp_get_ldc_i4_from_const
* interp_get_ldind_for_mt
* interp_get_stind_for_mt
* generate_compacted_code
* mono_jiterp_insert_ins
* mono_interp_transform_init
* mono_test_interp_generate_code
* get_native_offset
* interp_squash_initlocals
* interp_fix_localloc_ret
* add_patchpoint_data
* get_var_offset
* get_short_brop
* interp_is_short_offset
* interp_compute_native_offset_estimates
* interp_foreach_ins_var
* interp_foreach_ins_svar
* interp_get_ins_length
* alloc_unopt_global_local
* handle_relocations
* should_insert_seq_point
* handle_ldelem
* handle_stind
* handle_ldind
* interp_emit_load_const
* get_unaligned_opcode
* mono_test_interp_method_compute_offsets
* interp_emit_memory_barrier
* collect_pred_seq_points
* interp_realign_simd_params
* interp_emit_arg_conv
* save_seq_points
* insert_pred_seq_point
* get_bb
* interp_alloc_bb
* create_call_args
* interp_type_as_ptr
* recursively_make_pred_seq_points
