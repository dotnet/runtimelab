API that is used during execution is underlined. It seems that quite a significant proportion can end up being used there.

### <ins>Metadata structures with widespread use. Complete access to information contained by these, either directly or via accessors.</ins>

1. <ins>**MonoMethod**</ins>
    * MonoClass *klass
    * MonoMethodSignature *signature
    * MonoGenericContext *context
    * flags
    * etc

1. <ins>**MonoClass**</ins>
    * MonoClass *parent
    * is_enum, is_vt etc
    * rank, MonoClass *element_class
    * instance_size, value_size, native_size, align
    * MonoGenericContext *context
    * MonoClassField *fields
    * MonoType _byval_arg
    * MonoImage *image
    * MonoMethod **vtable
    * has_references
    * other flags
    * etc

1. <ins>**MonoMethodSignature**</ins>
    * hasthis
    * param_count
    * MonoType *params
    * MonoType *ret
    * flags
    * MonoClassField
    * MonoType *type
    * name
    * offset
    * flags

1. <ins>**MonoType**</ins>
    * type
    * flags
    * metadata
    * obtainable size/alignment (mono_type_size)

1. <ins>**MonoMethodHeader**</ins>
    * code, code_size
    * MonoExceptionClause *clause, num_clauses
    * MonoType *locals, num_locals
    * flags

1. <ins>**MonoExceptionClause**</ins>
    * type
    * try_off, try_len 
    * handler_off, handler_len
    * filter_offset

1. <ins>**MonoTypedRef**</ins>
    * type
    * value
    * klass

### General functionality around metadata structures

1. **Class initialization**

    | Method  | Notes |
    | -- | -- |
    | mono_class_init_internal (MonoClass*)  | basic class initialization  |
    | <ins>mono_runtime_class_init_full(MonoVTable*)</ins>  | calls cctor for a class  |
    | mono_class_setup_fields (MonoClass*) | initializes mainly field offsets |
    | <ins>mono_class_vtable_checked (MonoClass*)</ins> | get vtable for class |
    | <ins>mono_class_setup_vtable (MonoClass*)</ins> | populates the class vtable |

1. **Generics**

    | Method  | Notes |
    | -- | -- |
    | <ins>mono_class_get_generic_class (MonoClass*)</ins> | for a generic instantiation, returns the generic class |
    | <ins>mono_method_get_context (MonoMethod*)</ins> | get generic context if the method is inflated generic method|
    | mono_method_get_generic_container (MonoMethod*) | get generic container for a generic method (not inflated) |
    | <ins>mono_class_inflate_generic_method_checked (MonoMethod *method, MonoGenericContext*)</ins> | inflates a generic method |
    | mono_inflate_generic_signature (MonoMethodSignature*, MonoGenericContext*) | inflates a generic signature from the context |
    | mono_class_inflate_generic_type_checked (MonoType*, MonoGenericContext*) | inflates a generic MonoType |

1. **Token Loading**

    | Method  | Notes |
    | -- | -- |
    | mono_class_get_and_inflate_typespec_checked (MonoImage*, token, MonoGenericContext*) | get class from token |
    | mono_method_get_signature_checked (MonoMethod, MonoImage, token, MonoGenericContext*) | seems to be only used for varag ? |
    | mono_metadata_parse_signature_checked (MonoImage *, token) | decodes a MonoMethodSignature* from image |
    | mini_get_class (MonoMethod*, token, MonoGenericContext*) | obtain MonoClass* from token used inside the method |
    | mono_ldstr_checked (MonoImage*, token) | obtain MonoString from token |
    | mono_ldtoken_checked (MonoImage*, token, MonoGenericContext*) | load token |
    | mono_field_from_token_checked (MonoImage*, token, MonoClass*, MonoGenericContext*) | get field from token |

1. **Additional class information**

    | Method | Notes |
    | -- | -- |
    | mono_class_is_subclass_of_internal (MonoClass *, MonoClass*) |  |
    | mono_class_get_fields_internal (MonoClass*, gpointer iterator) | iterating over all fields of class |
    | mono_class_get_cctor (MonoClass*) | gets cctor for class |
    | mono_class_has_finalizer (MonoClass*) | whether class has finalizer, slow path allocation |
    | mono_class_get_method_from_name_checked (MonoClass*, char *name, int num_params) | obtain a certain method from class |

1. **Additional method information**

    | Method | Notes |
    | -- | -- |
    | <ins>mono_method_get_header_internal (MonoMethod*)</ins> | get MonoMethodHeader for a method |
    | mono_custom_attrs_from_method_checked (MonoMethod*) | access attributes for method |
    | <ins>mono_method_get_wrapper_data (MonoMethod *method, token)</ins> | wrapper methods hold some additional metadata |
    | mono_method_get_imt_slot (MonoMethod *method) | get fixed slot for interface method |
    | <ins>mono_method_get_vtable_slot (MonoMethod *method)</ins> | get the vtable slot for the method in its class |
    | mono_get_method_constrained_with_method (MonoImage*, MonoMethod*, MonoClass* constrained, | MonoGenericContext *) | get target method when constraining it to be called on a certain type |
    | m_method_get_mem_manager | allocator specific for a method, with memory released if method is collected |

1. **Additional field information**

    | Method | Notes |
    | -- | -- |
    | mono_method_can_access_field (MonoMethod*, MonoClassField*) | |
    | mono_class_field_is_special_static (MonoClassField*) | if field is thread static, used to be used to detect context static fields with remoting |
    | mono_special_static_field_get_offset (MonoClassField*) | combining this offset with the tls data we obtain the acutal address of the field |
    | mono_static_field_get_addr (MonoVTable*, MonoClassField*) | get the address of normal static field (which is referenced from the vtable) |

1. **Misc**

    | Method | Notes |
    | -- | -- |
    | mono_class_create_array (MonoClass*, rank) | create a MonoClass* for an array of certain element type |
    | <ins>mono_thread_internal_current ()</ins> | get some runtime tls data (tls fields are for example obtained from a pointer in this structure) |
    | get_default_jit_mm | general allocator |
    | <ins>mono_defaults.corlib</ins> | MonoImage* corresponding to corlib |
    | <ins>mono_defaults.string_class mono_defaults.*_class</ins> | MonoClass* pointers corresponding to various primitive/special types |

### Information about IL opcodes

  | Method | Notes |
  | -- | -- |
  | mono_opcode_value (ip) | decodes IL opcode and returns an index for it |
  | mono_opcode [index] | obtain information about each opcode (nr pushes, nr pops, type of opcode metadata) |

### <ins>Basic object type format understanding</ins>

1. <ins>**MonoObject**</ins>
    * vtable
    * sync

1. <ins>**MonoArray**</ins>
    * MonoObject 
    * length 
    * bounds 
    * vector 

1. <ins>**MonoString**</ins>
    * MonoObject 
    * length 
    * chars 

1. <ins>**MonoDelegate**</ins>
    * MonoObject 
    * MonoObject *target 
    * MonoMethod *target_method 
    * InterpMethod *interp_invoke (method interpreted when invoking delegate) 
    * Note that handling delegates / function pointers between interpreted and compiled code is a topic by itself and this area might suffer significant modifications 

### Object allocation

  | Method | Notes |
  | -- | -- |
  | <ins>mono_object_new (MonoClass *klass)</ins> | allocates new object |
  | <ins>mono_array_new (MonoClass *klass, int len, ...)</ins> | allocated new array with provided element class |
  | <ins>mono_string_new (char *cstr)</ins> | |
  | <ins>mono_gc_alloc_obj (vtable, size)</ins> | lower level GC api for object allocation |
  | <ins>mono_get_exception_..</ins> | allocates certain exception objects to be thrown during execution |
  | <ins>mono_nullable_box (gpointer vbuf, MonoClass *klass)</ins> | boxes a nullable valuetype |
  | mono_type_get_object_checked (MonoType*) | get System.RuntimeType object for a MonoType |

### GC

  | Method | Notes |
  | -- | -- |
  | <ins>mono_gchandle_new (MonoObject*)</ins> |  |
  | <ins>mono_gchandle_free_internal (gchandle)</ins> |  |
  | <ins>mono_gc_wbarrier_generic_store_internal (gpointer** ptr, MonoObject* val)</ins> | store val reference into ptr doing any necessary gc informing bout the store. |
  | <ins>mono_value_copy_internal (gpointer dest, gpointer src, MonoClass* klass)</ins> | GC aware value copy |
  | <ins>mono_threads_safepoint ()</ins> | GC poll, suspension point |
  | <ins>MONO_ENTER_GC_SAFE / MONO_EXIT_GC_SAFE</ins> | gc state transitions for coop suspend |
  | <ins>mono_threads_attach_coop</ins> | attaches to the suspend machinery a thread that starts executing managed code |
  | <ins>mono_threads_detach_coop</ins> |  |

### EH

  | Method | Notes |
  | -- | -- |
  | <ins>mono_push_lmf / mono_pop_lmf ()</ins> | push/pop thread local information that the runtime can use to unwind into the interpreter |
  |<ins> mono_handle_exception (MonoContext *ctx, MonoObject *exc)</ins> | calls into the runtime to throw ex, with native register context serving as starting point of unwinding |

### Wrappers

  | Method | Notes |
  | -- | -- |
  | <ins>mono_marshal_get_synchronized_wrapper</ins> | obtain MonoMethod for a synchronized wrapper calling the target method |
  | <ins>mono_marshal_get_native_wrapper</ins> | obtain MonoMethod for a wrapper calling a pinvoke method |
  | <ins>mono_marshal_get_native_func_wrapper</ins> | wrapper similar to the pinvoke for calling native function pointers |
  | <ins>mono_marshal_get_managed_wrapper</ins> | wrapper for entering runtime from native code (UnmanagedCallersOnly) |
  | <ins>mono_marshal_get_delegate_invoke</ins> | obtain wrapper for invoking multicast delegate |
  | mono_marshal_get_delegate_begin_invoke, mono_marshal_get_delegate_end_invoke |  |
  | mono_marshal_get_icall_wrapper | wrapper for icall |

### Casting/Inheritance

  | Method | Notes |
  | -- | -- |
  | <ins>mono_object_isinst (MonoObject*, MonoClass*)</ins> | check whether object is instance of class |
  | <ins>mono_class_is_assignable_from_internal (MonoClass*, MonoClass*)</ins> |  |
  | <ins>mono_class_has_parent_fast (MonoClass, MonoClass*)</ins> | simple version of the above, not handling interfaces etc |
  | <ins>MONO_VTABLE_IMPLEMENTS_INTERFACE (MonoVTable*, interface_id)</ins> | check whether an object with a certain vtable implements an interface |

### Calls

  | Method | Notes |
  | -- | -- |
  | <ins>mono_class_interface_offset (MonoClass* k, MonoClass* iface)</ins> | returns a slot for interface in a class that implements the interface |
  | <ins>mono_method_get_vtable_slot (MonoMethod*)</ins> | returns the vtable slot for a method |
  | <ins>m_class_get_vtable (MonoClass*)</ins> | returns the vtable of a class |
  | <ins>vtable->interp_vtable</ins> | returns the separate vtable where interpreter holds its method pointers |
  | <ins>vtable->"interface_table"</ins> | another separate table for interpreter method pointers, in mono this is stored at negative offset from the vtable structure |

### Native Interop

Some assembly code will have to be present. It should be emitted during AOT compilation and loaded at runtime.

  | Method | Notes |
  | -- | -- |
  | <ins>mono_arch_get_interp_native_call_info (MonoMethodSignature*)</ins> | low level information about each arg location according to cconv |
  | <ins>mono_create_ftnptr_arg_trampoline (interp_entry, InterpMethod*)</ins> | callable native thunk that embedds data so it can enter the interpreter when called |
  | <ins>mini_get_gsharedvt_out_sig_wrapper (MonoMethodSignature*)</ins> | wrapper for transition to jit |
  | <ins>mini_get_interp_in_wrapper (MonoMethodSignature*)</ins> | wrapper for entering interpreter from jit code |
  | <ins>mono_jit_compile_method_jit_only (MonoMethod *method)</ins> | for interop with jit, can also look into aot |
  | mono_aot_get_method (MonoMethod *method) | search for method in aot images |
  | mono_find_jit_icall_info (token) | returns information for internal call token. This information will also contain the native pointer to be called |

### Debugger

It is unclear how relevant this is since the debugging story will probably be a separate topic by itself.

| Method | Notes |
| -- | -- |
| <ins>mono_component_debugger ()->user_break</ins> | called by `System.Diagnostics.Debugger:Break` |
| <ins>mini_get_breakpoint_trampoline ()</ins> | thunk for calling into debugger for breakpoint |
| <ins>mini_get_single_step_trampoline ()</ins> | thunk for calling into debugger for single step |
| mono_debug_add_method (MonoMethod*, debug_info) | add some debug information associated with the compiled code for method, that the runtime needs |
| mono_debug_lookup_method (MonoMethod*) | get some debug information for method |

### Profiler

There are several places calling mono_profiler_raise_* methods in order to raise different profiler events. 

### Metadata Update

There are various mono_metadata_update_* methods for supporting this feature. Doesn't seem relevant for the prototype at this stage. 
