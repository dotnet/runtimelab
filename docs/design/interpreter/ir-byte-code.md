# The IR byte code
## Introduction
 * There are 677 byte codes vs 263 IL opcodes 
 * Some of them are handling intrinsics (includes also stuff like String.Length, System.Diagnostics.Debugger.Break)
 * Each opcode is directly followed by arguments that have variable size depending on the opcode.
 * Both the transformation and interpretation use Mono type / method  / vtable etc data structures
 * The arguments to the opcode are immediate constants (16, 32 or 64 bit), indexes into local variables table or indexes into a per-method special data items array.
## Limitations
 * The local variable offsets in most of the IR codes are 16 bit unsigned integers, so a method can have max 65536 kB stack frame size
 * The indices into the per-method special data items array in most of the IR codes are also 16 bit unsigned integers, thus a method can have max 65536 of these.

## IR codes corresponding to IL opcodes
This section describes mapping of IL opcodes to IR opcodes and their arguments. The naming convention of the arguments is as follows:
 * "_local" suffix represents a local variable offset in the local variables area
 * "_index" suffix represents index into the per-method data items table
 * The rest are just immediate values
To eliminate repeating, the IL / IR opcodes can have list of strings specified in curly braces. These mean that there are multiple opcodes each having have one of the strings from the list. For example, CEE_LDC_{I4|I8|R4|R8} represents CEE_LDC_I4, CEE_LDC_I8, CEE_LDC_R4 and CEE_LDC_R8.

### CEE_BREAK, System.Diagnostics.Debugger.Break intrinsic
 * MINT_BREAK() 
### CEE_LDC_{I4|I8|R4|R8}
 * MINT_LDC_I4_0(u16 target_local)
 * MINT_LDC_I4_1(u16 target_local local)
 * MINT_LDC_I4_S(u16 target_local local, i16 value)
 * MINT_LDC_I4(u16 target_local local, i32 value)
 * MINT_LDC_I8_0(u16 target_local local)
 * MINT_LDC_I8_S(u16 target_local local, i16 value)
 * MINT_LDC_I8(u16 local, i64 value)
 * MINT_LDC_R4(u16 local, float32 value)
 * MINT_LDC_R8(u16 local, float64 value)
 Calls with CEE_TAIL prefix
 * MINT_TAILCALL(u16 params_offset, u16 method, u16 params_size)
 * MINT_TAILCALL_VIRT(u16 params_offset, u16 method_index, u16 params_size, u16 slot)
### CEE_JMP
 * MINT_JMP(u16 method_index)
### CEE_CALL, CEE_CALLI, CEE_CALLVIRT
 * MINT_CALL_DELEGATE(u16 return_offset, u16 call_args_offset, u16 call_args_count, u16 param_count, u16 call_args_offset)
 * MINT_CALLI(u16 return_offset, u16 fn_ptr_local, u16 call_args_offset)
 * MINT_CALLI_NAT_FAST(u16 return_offset, u16 fn_ptr_local, u16 args_local, u16 icall_sig, u16 signature_index, bool save_last_error)
 * MINT_CALLI_NAT_DYNAMIC(u16 return_offset, u16 fn_ptr_local, u16 call_args_local, u16 signature_index)
 * MINT_CALLI_NAT(u16 return_offset, u16 fn_ptr_local, u16 call_args_local, u16 signature_index , u16 imethod_index, bool save_last_error, u16 cache_index)
 * MINT_CALLVIRT_FAST(u16 return_offset, u16 call_args_local, u16 imethod_index, int16 slot)
 * MINT_CALL_VARARG(u16 return_offset, u16 call_args_local, u16 imethod_index)
 * MINT_CALL(u16 return_offset, u16 call_args_local, u16 imethod_index)
 * MINT_ICALL(u16 return_offset, u16 call_args_local, u16 signature_index, u16 target_ip_index)
 * MINT_JIT_CALL(u16 return_offset, u16 call_args_local, u16 imethod_index)
 * MINT_JIT_CALL2 - not sure, somehow used by tiered compilation
### CEE_RET
 * MINT_RET(u16 return_local)
 * MINT_RET_LOCALLOC(u16 return_local)
 * MINT_RET_I1(u16 return_local)
 * MINT_RET_I2(u16 return_local)
 * MINT_RET_U1(u16 return_local)
 * MINT_RET_U2(u16 return_local)
 * MINT_RET_I4_IMM(int16 return_value)
 * MINT_RET_I8_IMM(int16 return_value)
 * MINT_RET_VOID()
 * MINT_RET_VOID_LOCALLOC()
 * MINT_RET_VT(u16 return_local, u16 size)
 * MINT_RET_VT_LOCALLOC(u16 return_local, u16 size)
### CEE_BR_S, CEE_BR
 * MINT_BR_S(int16 relative_offset)
 * MINT_BR(i32 relative_offset)
### CEE_BRTRUE_S, CEE_BRTRUE
 * MINT_BR{TRUE|FALSE}_I{4|8}_S(u16 bool_local, int16 offset)
 * MINT_BR{TRUE|FALSE}_I{4|8}_SP(u16 bool_local, int16 offset)
 * MINT_BR{TRUE|FALSE}_I{4|8}(u16 bool_local, i32 offset)
They get folded to MINT_BR/NOP if the operand is constant
### CEE_{BEQ|BGE|BGT|BLT|BLE|BNE_UN|BGE_UN|BGT_UN|BLE_UN|BLT_UN}_S
 * MINT_{BEQ|BGE|BGT|BLT|BLE|BNE_UN|BGE_UN|BGT_UN|BLE_UN|BLT_UN}_{I4|I8|R4|R8}_S(u16 left_local, u16 right_local, i16 offset)
### CEE_{BEQ|BGE|BGT|BLT|BLE|BNE_UN|BGE_UN|BGT_UN|BLE_UN|BLT_UN}
 * MINT_{BEQ|BGE|BGT|BLT|BLE|BNE_UN|BGE_UN|BGT_UN|BLE_UN|BLT_UN}_{I4|I8|R4|R8}(u16 left_local, u16 right_local, i32 offset)
 * MINT_{BEQ|BGE|BGT|BLT|BLE|BNE_UN|BGE_UN|BGT_UN|BLE_UN|BLT_UN}_{I4|I8}_SP(u16 left_local, u16 right_local, i16 offset)
 * MINT_{BEQ|BGE|BGT|BLT|BLE|BNE_UN|BGE_UN|BGT_UN|BLE_UN|BLT_UN}_{I4|I8}_IMM_SP(u16 left_local, u16 right_immediate, i16 offset)
These CEE_BXX can prepend MINT_CONV_I8_I4 or MINT_CONV_R8_R4
They get folded to MINT_BR/NOP if both operands are constant
### CEE_CEQ
 * MINT_CEQ0_I4(u16 target_local, u16 source_local)
### CEE_{CEQ|CNE|CGT|CGE|CGT_UN|CGE_UN|CLT|CLT_UN|CLE}
 * MINT_{CEQ|CNE|CGT|CGE|CGT_UN|CGE_UN|CLT|CLT_UN|CLE}_{I4|I8|R4|R8}(u16 target_local, u16 source_left_local, u16 source_right_local)
### CEE_CEQ can prepend MINT_CONV_R8_R4
### CEE_SWITCH
 * MINT_SWITCH(u16 source_local, u32 count, {i32 offset}[count])
### CEE_LDIND_{I1|U1|I2|U2|I4|I8|R4|R8|REF}
 * MINT_LDIND_{I1|U1|I2|U2|I4|I8|R4|R8}(u16 target_local, u16 pointer_local)
 * MINT_LDIND_OFFSET_{I1|U1|I2|U2|I4|I8|R4|R8}(u16 target_local, u16 pointer_local, u16  offset_local)
 * MINT_LDIND_OFFSET_ADD_MUL_IMM_{I1|U1|I2|U2|I4|I8}(u16 target_local, u16 pointer_local, u16  offset_local, i16  offset_immediate, i16  offset_multiplier_immediate)
 * offset= (local[offset_local}+offset_immediate)*offset_multiplier_immediate
 * MINT_LDIND_OFFSET_IMM_{I1|U1|I2|U2|I4|I8}(u16 target_local, u16 pointer_local, i16  offset_immediate)
### CEE_STIND_REF
 * MINT_STIND_REF(u16 pointer_local, u16 object_local)
### CEE_STIND_{I1|I2|I4|I8|R4|R8}
 * MINT_STIND_{I1|I2|I4|I8|R4|R8}(u16 pointer_local, u16 value_local)
 * MINT_STIND_OFFSET_{I1|I2|I4|I8}(u16 pointer_local, u16  offset_local, u16 value_local)
### CEE_{ADD|SUB|MUL|DIV|REM}
 * MINT_{ADD|SUB|MUL|DIV|REM}_{I4|I8|R4|R8}(u16 target_local, u16 source_left_local, u16 source_right_local)
 * MINT_{ADD|SUB|MUL}_{I4|I8|R4|R8}_IMM(u16 target_local, u16 source_local, i16 immediate)
 * MINT_{ADD|SUB|MUL}_{I4|I8|R4|R8}_IMM2(u16 target_local, u16 source_local, i32 immediate)
 * MINT_{AND|OR|XOR}_{I4|I8}(u16 target_local, u16 source_left_local, u16 source_right_local)
 * MINT_{AND|OR}_I4_IMM(u16 target_local, u16 source_local, i16 immediate)
 * MINT_{AND|OR}_I4_IMM2(u16 target_local, u16 source_local, i32 immediate)
 * MINT_NEG_{I4|I8|R4|R8}(u16 target_local, u16 source_local)
 * MINT_NOT_{I4|I8}(u16 target_local, u16 source_local)
 * MINT_{SHL|SHR|SHR_UN|ROL|ROR}_{I4|I8}(u16 target_local, u16 source_local, u16 shift_local)
 * MINT_{SHL|SHR|SHR_UN|ROL|ROR}_{I4|I8}_IMM(u16 target_local, u16 source_local, u16 shift)
 * MINT_SHL_AND_{I4|I8}(u16 target_local, u16 source_local, u16 shift_local)
### CEE_{DIV|REM}_UN
 * MINT_{DIV|REM}_UN_{I4|I8}(u16 target_local, u16 source_left_local, u16 source_right_local)
### CEE_{ADD|SUB|MUL}_OVF
 * MINT_{ADD|SUB|MUL}_OVF_{I4|I8}(u16 target_local, u16 source_left_local, u16 source_right_local)
### CEE_{ADD|SUB|MUL}_OVF_UN
 * MINT_{ADD|SUB|MUL}_OVF_UN_{I4|I8}(u16 target_local, u16 source_left_local, u16 source_right_local)
 * MINT_{ADD|SUB}1_{I4|I8}(u16 target_local, u16 source_left_local, u16 source_right_local)
 * MINT_ADD_MUL_{I4|I8}_IMM(u16 target_local, u16 source_local, i16 immediate_offset, i16 immediate_multiplier)
### CEE_CONV_{I,U,I1|U1|I2|U2|I4|U4|I8|U8}
 * MINT_CONV_{I1|U1|I2|U2|I8}_{I4|I8|R4|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_{I4|U4|U8}_{R4|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_I8_U4(u16 target_local, u16 source_local)
### CEE_CONV_{R4|R8}
 * MINT_CONV_{R4}_{I4|I8|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_{R8}_{I4|I8|R4}(u16 target_local, u16 source_local)
### CEE_CONV_R_UN
 * MINT_CONV_R_UN_{I4|I8}(u16 target_local, u16 source_local)
### CEE_CONV_OVF_{I,U,I1|U1|I2|U2|I4|U4|I8|U8}, CEE_CONV_OVF_{I,U,I1|U1|I2|U2|I4|U4|I8|U8}_UN
 * MINT_CONV_OVF_U8_{I4|I8|R4|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_OVF_I8_{U8|R4|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_OVF_I4_{U4|I8|U8|R4|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_OVF_U4_{I4|I8|R4|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_OVF_I1_{I4|U4|I8|U8|R4|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_OVF_I2_{I4|U4|I8|U8|R4|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_OVF_U1_{I4|I8|R4|R8}(u16 target_local, u16 source_local)
 * MINT_CONV_OVF_U2_{I4|I8|R4|R8}(u16 target_local, u16 source_local)
These conversion IRs are also generated in cases the transform decides it needs to add a conversion, i.e. for CEE_Bxx.
### CEE_CPOBJ
 * MINT_CPOBJ(u16 target_local, u16 source_local, u16 class_index)
 * MINT_CPOBJ_VT(u16 target_local, u16 source_local, u16 class_index)
 * MINT_CPOBJ_VT_NOREF(u16 target_local, u16 source_local, u16 size)
### CEE_CPOBJ gets expanded into MINT_LDIND_I + MINT_STIND_REF instead for reference types
### CEE_LDOBJ
 * MINT_LDOBJ_VT(u16 target_local, u16 source_address_local, u16 size)
It can get translated to MINT_LDIND for integer types
### CEE_LDSTR
 * MINT_LDSTR(u16 target_local, u16 string_index)
 * MINT_LDSTR_DYNAMIC(u16 target_local, u16 strtoken_index)
 * MINT_LDSTR_CSTR(u16 target_local, u16 cstr_index)
### CEE_LDTOKEN
 * MINT_LDPTR(u16 target_local, u16 ptr_index)
### CEE_MONO_JIT_ICALL_ADDR
 * MINT_LDFTN_ADDR(u16 target_local, u16 ftn_addr_index)
### CEE_LDFTN
 * MINT_LDFTN(u16 target_local, u16 interpmethod_index)
### CEE_LDVIRTFTN
 * MINT_LDVIRTFTN(u16 target_local, u16 object_local, u16 interpmethod_index)
### CEE_MONO_LD_DELEGATE_METHOD_PTR
 * MINT_LD_DELEGATE_METHOD_PTR(u16 target_local, u16 delegate_local)
### CEE_LDARGA, CEE_LDARGA_S, CEE_LDLOCA, CEE_LDLOCA_S
 * MINT_LDLOCA_S(u16 target_local, u16 source_local)
### CEE_LDELEMA
 * MINT_LDELEMA1(u16 target_local, u16 array_local, u16 index_local, u16 element_size)
 * MINT_LDELEMA(u16 target_local, u16 array_local, u16 rank, u16 element_size)
 * MINT_LDELEMA_TC(u16 target_local, u16 array_local, u16 class_index)
### CEE_LDELEM_{I1|U1|I2|U2|I4|U4|I8|R4|R8|REF}, CEE_LDELEM
 * MINT_LDELEM_{I1|U1|I2|U2|I4|U4|I8|R4|R8|REF}(u16 target_local, u16 array_local, u16 index_local)
 * MINT_LDELEM_VT(u16 target_local, u16 array_local, u16 index_local, u16 element_size)
### CEE_STELEM_{I1|U1|I2|U2|I4|I8|R4|R8|REF}, CEE_STELEM
 * MINT_STELEM_{I1|U1|I2|U2|I4|I8|R4|R8|REF|REF_UNCHECKED }(u16 array_local, u16 index_local, u16 source_local)
 * MINT_STELEM_{ VT|VT_NOREF}(u16 array_local, u16 index_local, u16 source_local, u16 class_index, u16 element_size)
### CEE_LDLEN
 * MINT_LDLEN(u16 target_local, u16 source_local)
### CEE_LOCALLOC
 * MINT_LOCALLOC(u16 target_local, u16 size_local)
### CEE_NEWOBJ and other locations not directly related to specific CEE_xxx
 * MINT_NEWOBJ_ARRAY(u16 target_local, u16 first_param_local, u32 token, u16 param_count)
 * MINT_NEWOBJ_STRING(???)
 * MINT_NEWOBJ_STRING_UNOPT(???)
 * MINT_NEWOBJ(???)
 * MINT_NEWOBJ_INLINED(u16 target_local, u16 vtable_index)
 * MINT_NEWOBJ_VT(???)
 * MINT_NEWOBJ_SLOW(???)
### CEE_NEWARR
 * MINT_NEWARR(u16 target_local, u16 size_local, u16 vtable_index)
### CEE_CASTCLASS
 * MINT_CASTCLASS_INTERFACE(u16 target_local, u16 source_local, u16 class_index)
 * MINT_CASTCLASS_COMMON(u16 target_local, u16 source_local, u16 class_index)
 * MINT_CASTCLASS(u16 target_local, u16 source_local, u16 class_index)
### CEE_ISINST
 * MINT_ISINST_INTERFACE(u16 target_local, u16 source_local, u16 class_index)
 * MINT_ISINST_COMMON(u16 target_local, u16 source_local, u16 class_index)
 * MINT_ISINST(u16 target_local, u16 source_local, u16 class_index)
### CEE_BOX
 * MINT_BOX(u16 target_local, u16 source_local, u16 vtable_index)
 * MINT_BOX_VT(u16 target_local, u16 source_local, u16 vtable_index)
 * MINT_BOX_PTR(u16 target_local, u16 source_local, u16 vtable_index)
 * MINT_BOX_NULLABLE_PTR(u16 target_local, u16 source_local, u16 vtable_index)
### CEE_UNBOX, CEE_UNBOX_ANY
 * MINT_UNBOX(u16 target_local, u16 source_local, u16 class_index)
### CEE_THROW
 * MINT_THROW(u16 exception_local)
### CEE_LDFLDA
 * MINT_LDFLDA_UNSAFE(u16 target_local, u16 source_local, u16 source_offset)
 * MINT_LDFLDA(u16 target_local, u16 source_local, u16 source_offset)
### CEE_LDFLD
 * MINT_LDFLD_{I1|U1|I2|U2|I4|I8|R4|R8|O|I8_UNALIGNED|R8_UNALIGNED}(u16 target_local, u16 source_local, u16 source_offset)
 * MINT_LDFLD_VT(u16 target_local, u16 source_local, u16 source_offset, u16 size)
### CEE_LDSFLD
 * MINT_LDSFLD_{I1|U1|I2|U2|I4|I8|R4|R8|O}(u16 target_local, u16 class_index, u16 source_index)
 * MINT_LDSFLD_VT(u16 target_local, u16 vtable_index, u16 source_index, u16 size)
 * MINT_LDSFLD_W(u16 target_local, u32 vtable_index, u32 source_index, u32 class_index)
### CEE_STFLD
 * MINT_STFLD_{I1|U1|I2|U2|I4|I8|R4|R8|O|I8_UNALIGNED|R8_UNALIGNED}(u16 target_object_local, u16 source_local, u16 source_offset)
 * MINT_STFLD_VT_NOREF(u16 target_object_local, u16 source_local, u16 source_offset, u16 size)
 * MINT_STFLD_VT(u16 target_object_local, u16 source_local, u16 source_offset, u16 class_index)
### CEE_STSFLD
 * MINT_STSFLD_{I1|U1|I2|U2|I4|I8|R4|R8|O}( u16 source_local, u16 vtable_index, u16 target_index)
 * MINT_STFLD_VT( u16 source_local, u16 vtable_index, u16 target_index, u16 size)
 * MINT_STFLD_W( u16 source_local, u32 vtable_index, u32 target_index, u32 class_index)
### CEE_LDSFLDA
 * MINT_LDSFLDA(u16 target_local, u16 vtable_index, u16 source_index)
 * MINT_LDTSFLDA(u16 target_local, u32 tls_offset)
### CEE_STOBJ
 * MINT_STOBJ_VT(u16 target_local, u16 source_local, u16 class_index)
 * MINT_STOBJ_VT_NOREF(u16 target_local, u16 source_local, u16 size)
### CEE_CKFINITE
 * MINT_CKFINITE_{R4|R8}(u16 target_local, u16 source_local)
### CEE_MKREFANY
 * MINT_MKREFANY(u16 target_local, u16 source_addr_local, u16 class_index)
### CEE_REFANYTYPE
 * MINT_REFANYTYPE(u16 target_local, u16 source_local)
### CEE_REFANYVAL
 * MINT_REFANYVAL(u16 target_local, u16 source_addr_local, u16 class_index)
### CEE_ENDFINALLY
 * MINT_ENDFINALLY(u16 clause)
### CEE_ENDFILTER
 * MINT_ENDFILTER(u16 filter_result_local)
### CEE_LEAVE, CEE_LEAVE_S
 * MINT_CALL_HANDLER(u32 offset, u16 clause)
 * MINT_CALL_HANDLER_S(u16 offset, u16 clause)
 * MINT_LEAVE_CHECK(u32 offset)
 * MINT_LEAVE_S_CHECK(u16 offset)
### CEE_RETHROW
 * MINT_RETHROW(u16 exception_object_stack_offset)
### CEE_MONO_RETHROW
 * MINT_MONO_RETHROW(u16 exception_object _local)
 CEE_INITOBJ
 * MINT_ZEROBLK_IMM(u16 target_local, u16 size)
### CEE_CPBLK
 * MINT_CPBLK(u16 target_local, u16 source_local, u16 size_local)
### CEE_INITBLK
 * MINT_INITBLK(u16 target_local, u16 fill_local, u16 size_local)
### CEE_xxx float math functions 1:1
 * MINT_ASINH(u16 target_local, u16 source_local)
 * MINT_ACOS(u16 target_local, u16 source_local)
 * MINT_ACOSH(u16 target_local, u16 source_local)
 * MINT_ASIN(u16 target_local, u16 source_local)
 * MINT_ATAN(u16 target_local, u16 source_local)
 * MINT_ATANH(u16 target_local, u16 source_local)
 * MINT_CEILING(u16 target_local, u16 source_local)
 * MINT_COS(u16 target_local, u16 source_local)
 * MINT_CBRT(u16 target_local, u16 source_local)
 * MINT_COSH(u16 target_local, u16 source_local)
 * MINT_EXP(u16 target_local, u16 source_local)
 * MINT_FLOOR(u16 target_local, u16 source_local)
 * MINT_LOG(u16 target_local, u16 source_local)
 * MINT_LOG2(u16 target_local, u16 source_local)
 * MINT_LOG10(u16 target_local, u16 source_local)
 * MINT_SIN(u16 target_local, u16 source_local)
 * MINT_SQRT(u16 target_local, u16 source_local)
 * MINT_SINH(u16 target_local, u16 source_local)
 * MINT_TAN(u16 target_local, u16 source_local)
 * MINT_TANH(u16 target_local, u16 source_local)
 * MINT_ABS(u16 target_local, u16 source_local)
 * MINT_ATAN2(u16 target_local, u16 source_local)
 * MINT_POW(u16 target_local, u16 base_local, u16 exponent_local)
 * MINT_MIN(u16 target_local, u16 source_1_local, u16 source_2_local)
 * MINT_MAX(u16 target_local, u16 source_1_local, u16 source_2_local)
 * MINT_FMA(u16 target_local, u16 addend_1_local, u16 addend_2_local, u16 multiplicant_local)
 * MINT_SCALEB(u16 target_local, u16 argument_local, u16 exponent_local)
 * MINT_ASINF(u16 target_local, u16 source_local)
 * MINT_ASINHF(u16 target_local, u16 source_local)
 * MINT_ACOSF(u16 target_local, u16 source_local)
 * MINT_ACOSHF(u16 target_local, u16 source_local)
 * MINT_ATANF(u16 target_local, u16 source_local)
 * MINT_ATANHF(u16 target_local, u16 source_local)
 * MINT_CEILINGF(u16 target_local, u16 source_local)
 * MINT_COSF(u16 target_local, u16 source_local)
 * MINT_CBRTF(u16 target_local, u16 source_local)
 * MINT_COSHF(u16 target_local, u16 source_local)
 * MINT_EXPF(u16 target_local, u16 source_local)
 * MINT_FLOORF(u16 target_local, u16 source_local)
 * MINT_LOGF(u16 target_local, u16 source_local)
 * MINT_LOG2F(u16 target_local, u16 source_local)
 * MINT_LOG10F(u16 target_local, u16 source_local)
 * MINT_SINF(u16 target_local, u16 source_local)
 * MINT_SQRTF(u16 target_local, u16 source_local)
 * MINT_SINHF(u16 target_local, u16 source_local)
 * MINT_TANF(u16 target_local, u16 source_local)
 * MINT_TANHF(u16 target_local, u16 source_local)
 * MINT_ABSF(u16 target_local, u16 source_local)
 * MINT_ATAN2F(u16 target_local, u16 source_local)
 * MINT_POWF(u16 target_local, u16 base_local, u16 exponent_local)
 * MINT_MINF(u16 target_local, u16 source_1_local, u16 source_2_local)
 * MINT_MAXF(u16 target_local, u16 source_1_local, u16 source_2_local)
 * MINT_FMAF(u16 target_local, u16 addend_1_local, u16 addend_2_local, u16 multiplicant_local)
 * MINT_SCALEBF(u16 target_local, u16 argument_local, u16 exponent_local)
### CEE_MONO_NEWOBJ
 * MINT_MONO_NEWOBJ(u16 target_local, u16 monoclass_index)
### CEE_MONO_RETOBJ
 * MINT_MONO_RETOBJ(u16 return_local)
Various CEE_xxx with CEE_VOLATILE_ prefix, CEE_MONO_MEMORY_BARRIER
 * MINT_MONO_MEMORY_BARRIER()
### CEE_MONO_LDDOMAIN
 * MINT_MONO_LDDOMAIN(u16 domain_local)
### CEE_MONO_GET_SP
 * MINT_MONO_ENABLE_GCTRANS()
## IR codes not directly corresponding to IL opcodes

### Start of methods
 * MINT_INITLOCAL(u16 offset, u16 count)
 * MINT_INITLOCALS(u16 offset, u16 count)
### System.Threading.Interlocked.CompareExchange intrinsics
 * MINT_MONO_CMPXCHG_{U1|I1|U2|I2|I4|I8}(u16 result_local, u16 dest_local, u16 value_local, u16 comparand_local)
### System.Threading.Interlocked.Exchange intrinsics
 * MINT_MONO_EXCHANGE_{U1|I1|U2|I2|I4|I8}(u16 result_local, u16 dest_local, u16 source_local)
### Vararg calls
 * MINT_INIT_ARGLIST(u16 argListLocal, u16 size)
### Various argument moves on stack when needed
 * MINT_MOV_STACK_UNOPT(u16 src_offset, u16 dst_to_src_offset, u16 size) 
### System.RuntimeMethodHandle.GetFunctionPointer intrinsic
 * MINT_LDFTN_DYNAMIC(u16 target_local, u16 method_local)
### System.SpanHelpers.ClearWithoutReferences intrinsic
 * MINT_ZEROBLK(u16 target_local, u16 size_local)
### Throw if NULL
 * MINT_CKNULL(u16 target_local, u16 source_local)
### Mark GC safe point
 * MINT_SAFEPOINT()
### Local variables moves
 * MINT_MOV_I4_{I1|U1|I2|U2}(u16 target_local, u16 source_local)
 * MINT_MOV_{1|2|4|8}(u16 target_local, u16 source_local)
 * MINT_MOV_8_{2|3|4}(u16 target_1_local, u16 source_1_local, u16 target_2_local, u16 source_2_local)
 * MINT_MOV_VT(u16 target_local, u16 source_local, u16 size)
### Hot reload
 * MINT_METADATA_UPDATE_LDFLDA(u16 target_local, u16 source_local, u16 type_index, u16 fielddef_token_index)
### JITerpreter - WASM only
 * MINT_TIER_NOP_JITERPRETER
 * MINT_TIER_PREPARE_JITERPRETER
 * MINT_TIER_MONITOR_JITERPRETER
 * MINT_TIER_ENTER_JITERPRETER
### System.Array.Rank intrinsic
 * MINT_ARRAY_RANK(u16 target_local, u16 array_local)
### System.Array.GetElementSize intrinsic
 * MINT_ARRAY_ELEMENT_SIZE(u16 target_local, u16 array_local)
### System.String.FastAllocateString intrinsic
 * MINT_NEWSTR(u16 target_local, u16 size_local)
### System.Numerics.BitOperations.xxx intrinsics
 * MINT_{CLZ|CTZ|POPCNT|LOG2}_{I4|I8}(u16 target_local, u16 size_local)
### SIMD  
 * MINT_SIMD_V128_LDC(u16 target_local, v128 value)
 * MINT_SIMD_V128_I1_CREATE(u16 target_local, u16 source_local)
 * MINT_SIMD_V128_I2_CREATE(u16 target_local, u16 source_local)
 * MINT_SIMD_V128_I4_CREATE(u16 target_local, u16 source_local)
 * MINT_SIMD_V128_I8_CREATE(u16 target_local, u16 source_local)
 * MINT_SIMD_INTRINS_P_P(u16 target_local, u16 arg1_local, u16 intrin_opcode)
 * MINT_SIMD_INTRINS_P_PP(u16 target_local, u16 arg1_local, u16 arg2_local, u16 intrin_opcode)
 * MINT_SIMD_INTRINS_P_PPP(u16 target_local, u16 arg1_local, u16 arg2_local, u16 arg3_local, u16 intrin_opcode)
### General intrinsics
 * MINT_INTRINS_SPAN_CTOR(u16 target_local, u16 source_local, u16 size_local)
 * MINT_INTRINS_CLEAR_WITH_REFERENCES(u16 target_local, u16 count_local)
 * MINT_INTRINS_MARVIN_BLOCK(u16 pp0_local, u16 pp1_local, u16 dest0_local, u16 dest1_local,)
 * MINT_INTRINS_ASCII_CHARS_TO_UPPERCASE(u16 target_local, u16 source_local)
 * MINT_INTRINS_MEMORYMARSHAL_GETARRAYDATAREF(u16 target_local, u16 source_local)
 * MINT_INTRINS_ORDINAL_IGNORE_CASE_ASCII(u16 target_local, u16 source_1_local, u16 source_2_local)
 * MINT_INTRINS_64ORDINAL_IGNORE_CASE_ASCII(u16 target_local, u16 source_1_local, u16 source_2_local)
 * MINT_INTRINS_WIDEN_ASCII_TO_UTF16(u16 result_local, u16 ascii_local, u16 wide_local, u16 count_local)
 * MINT_INTRINS_RUNTIMEHELPERS_OBJECT_HAS_COMPONENT_SIZE(u16 target_local, u16 source_local)
 * MINT_INTRINS_ENUM_HASFLAG(u16 target_local, u16 enum_local, u16 flag_local, u16 class_index)
 * MINT_INTRINS_GET_HASHCODE(u16 target_local, u16 source_local)
 * MINT_INTRINS_TRY_GET_HASHCODE(u16 target_local, u16 source_local)
 * MINT_INTRINS_GET_TYPE(u16 target_local, u16 source_local)
### System.String.Chars intrinsic
 * MINT_GETCHR(u16 target_local, u16 source_local, u16 index_local)
### System.{Span<T>|ReadOnlySpan<T>}.Item
 * MINT_GETITEM_SPAN(u16 target_local, u16 source_local, u16 index_local, u16 element_size_local)
### System.{Span<T>|ReadOnlySpan<T>}.Item only when optimizations are on
 * MINT_GETITEM_LOCALSPAN(u16 target_local, u16 source_local, u16 index_local, u16 element_size_local)
### System.String.Length intrinsic
 * MINT_STRLEN(u16 target_local, u16 source_local)
### Profiler
 * MINT_PROF_ENTER(u16 flag)
 * MINT_PROF_EXIT(u16 source_local, u16 flag, u32 size)
 * MINT_PROF_EXIT_VOID(u16 flag)
 * MINT_PROF_COVERAGE_STORE(u64 pointer)
### Tiered interpreting
 * MINT_TIER_ENTER_METHOD()
 * MINT_TIER_PATCHPOINT(u16 bb)
### Debugger
 * MINT_SDB_INTR_LOC()
 * MINT_SDB_SEQ_POINT()
 * MINT_SDB_BREAKPOINT()
 * MINT_BREAKPOINT()
