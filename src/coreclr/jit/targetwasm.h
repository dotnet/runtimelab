// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#pragma once

#if !defined(TARGET_WASM)
#error The file should not be included for this platform.
#endif

// clang-format off

  #define CPU_LOAD_STORE_ARCH      1
  #define CPU_HAS_FP_SUPPORT       1
  #define ROUND_FLOAT              0       // Do not round intermed float expression results
  #define CPU_HAS_BYTE_REGS        0

  #define FEATURE_FIXED_OUT_ARGS   0       // Not relevant to LLVM/WASM
  #define FEATURE_STRUCTPROMOTE    1       // JIT Optimization to promote fields of structs into registers
  #define FEATURE_FASTTAILCALL     1       // Tail calls made as epilog+jmp
  #define FEATURE_TAILCALL_OPT     1       // opportunistic Tail calls (i.e. without ".tail" prefix) made as fast tail calls.
  #define FEATURE_SET_FLAGS        0       // Set to true to force the JIT to mark the trees with GTF_SET_FLAGS when the flags need to be set

  #define FEATURE_MULTIREG_ARGS_OR_RET  0  // Support for passing and/or returning single values in more than one register
  #define FEATURE_MULTIREG_ARGS         0  // Support for passing a single argument in more than one register
  #define FEATURE_MULTIREG_RET          0  // Support for returning a single value in more than one register
  #define FEATURE_MULTIREG_STRUCT_PROMOTE  0  // True when we want to promote fields of a multireg struct into registers
  #define MAX_PASS_MULTIREG_BYTES       0  // No multireg arguments
  #define MAX_RET_MULTIREG_BYTES        0  // No multireg return values
  #define MAX_ARG_REG_COUNT             1  // Maximum registers used to pass a single argument.
  #define MAX_RET_REG_COUNT             1  // Maximum registers used to return a value.

  #define MAX_MULTIREG_COUNT            2  // Maxiumum number of registers defined by a single instruction (including calls).
                                           // This is also the maximum number of registers for a MultiReg node.
                                           // Note that this must be greater than 1 so that GenTreeLclVar can have an array of
                                           // MAX_MULTIREG_COUNT - 1.
  #define USER_ARGS_COME_LAST      1
#if defined(TARGET_WASM32)
  #define TARGET_POINTER_SIZE      4       // equal to sizeof(void*) and the managed pointer size in bytes for this target
#else
  #define TARGET_POINTER_SIZE      8
#endif
  #define FEATURE_EH               1       // To aid platform bring-up, eliminate exceptional EH clauses (catch, filter, filter-handler, fault) and directly execute 'finally' clauses.
  #define FEATURE_EH_CALLFINALLY_THUNKS 1  // Generate call-to-finally code in "thunks" in the enclosing EH region, protected by "cloned finally" clauses.
  #define CSE_CONSTS               1       // Enable if we want to CSE constants

  #define RBM_ALLFLOAT             RBM_F0
  #define RBM_ALLDOUBLE            RBM_ALLFLOAT
  #define REG_FP_FIRST             REG_F0
  #define REG_FP_LAST              REG_F0
  #define FIRST_FP_ARGREG          REG_F0
  #define LAST_FP_ARGREG           REG_F0

  #define REGNUM_BITS              6       // number of bits in a REG_*
  #define REGMASK_BITS             32      // number of bits in a REGNUM_MASK
#if defined(TARGET_WASM32)               // morph phase uses this
  #define REGSIZE_BYTES            4       // number of bytes in one register
#else
  #define REGSIZE_BYTES            8       // number of bytes in one register
#endif
  #define MIN_ARG_AREA_FOR_CALL    0       // Minimum required outgoing argument space for a call.

  #define CODE_ALIGN               1       // code alignment requirement
  #define STACK_ALIGN              16      // stack alignment requirement
  #define STACK_ALIGN_SHIFT        4       // Shift-right amount to convert size in bytes to size in STACK_ALIGN units == log2(STACK_ALIGN)

  #define RBM_CALLEE_SAVED         RBM_R0
  #define RBM_CALLEE_TRASH         RBM_NONE

  #define RBM_ALLINT               RBM_R0

  #define CNT_CALLEE_SAVED         1
  #define CNT_CALLEE_TRASH         0 // This and below are only used for CSE heuristics; thus an optimistic estimate for an "average" target.
  #define CNT_CALLEE_ENREG         8

  #define CNT_CALLEE_SAVED_FLOAT   8
  #define CNT_CALLEE_TRASH_FLOAT   4

  #define REG_CALLEE_SAVED_ORDER   REG_R0
  #define RBM_CALLEE_SAVED_ORDER   RBM_R0

  // GenericPInvokeCalliHelper VASigCookie Parameter
  #define REG_PINVOKE_COOKIE_PARAM REG_R0

  // GenericPInvokeCalliHelper unmanaged target Parameter
  #define REG_PINVOKE_TARGET_PARAM REG_R0

  // The following defines are useful for iterating a regNumber
  #define REG_FIRST                REG_R0
  #define REG_INT_FIRST            REG_R0
  #define REG_INT_LAST             REG_R0
  #define REG_INT_COUNT            0
  #define REG_NEXT(reg)           ((regNumber)((unsigned)(reg) + 1))
  #define REG_PREV(reg)           ((regNumber)((unsigned)(reg) - 1))

  #define REG_FPBASE               REG_NA
  #define RBM_FPBASE               REG_NA
  #define STR_FPBASE               "NA"
  #define REG_SPBASE               REG_NA
  #define RBM_SPBASE               REG_NA
  #define STR_SPBASE               "NA"

  #define FIRST_ARG_STACK_OFFS     0

  #define MAX_REG_ARG              1
  #define MAX_FLOAT_REG_ARG        1
  #define REG_ARG_FIRST            REG_R0
  #define REG_ARG_LAST             REG_R0
  #define INIT_ARG_STACK_SLOT      0

  #define REG_ARG_0                REG_R0

  extern const regNumber intArgRegs [MAX_REG_ARG];
  extern const regMaskTP intArgMasks[MAX_REG_ARG];
  extern const regNumber fltArgRegs [MAX_FLOAT_REG_ARG];
  extern const regMaskTP fltArgMasks[MAX_FLOAT_REG_ARG];

  #define REG_FLTARG_0             REG_F0

  #define RBM_ARG_REGS             RBM_R0
  #define RBM_FLTARG_REGS          RBM_F0
// clang-format on
