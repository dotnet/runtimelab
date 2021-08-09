// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "AsmMacros.h"

// WARNING: Code in EHHelpers.cpp makes assumptions about this helper, in particular:
// - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpLockCmpXchg32AVLocation
// - Function "UnwindSimpleHelperToCaller" assumes no registers were pushed and LR contains the return address
// r0 = destination address
// r1 = value
// r2 = comparand
LEAF_ENTRY RhpLockCmpXchg32, _TEXT
          dmb
ALTERNATE_ENTRY RhpLockCmpXchg32AVLocation
LOCAL_LABEL(CmpXchg32Retry):
          ldrex        r3, [r0]
          cmp          r2, r3
          bne          LOCAL_LABEL(CmpXchg32Exit)
          strex        r12, r1, [r0]
          cmp          r12, #0
          bne          LOCAL_LABEL(CmpXchg32Retry)
LOCAL_LABEL(CmpXchg32Exit):
          mov          r0, r3
          dmb
          bx           lr
LEAF_END RhpLockCmpXchg32, _TEXT

// WARNING: Code in EHHelpers.cpp makes assumptions about this helper, in particular:
// - Function "InWriteBarrierHelper" assumes an AV due to passed in null pointer will happen at RhpLockCmpXchg64AVLocation
// - Function "UnwindSimpleHelperToCaller" assumes no registers were pushed and LR contains the return address
// r0      = destination address
// {r2,r3} = value
// sp[0+8] = comparand
LEAF_ENTRY RhpLockCmpXchg64, _TEXT
ALTERNATE_ENTRY RhpLockCmpXchg64AVLocation
          ldr          r12, [r0]        // dummy read for null check
          PROLOG_PUSH  "{r4-r6,lr}"
          dmb
          ldrd         r4, r5, [sp,#0x10]
LOCAL_LABEL(CmpXchg64Retry):
          ldrexd       r6, r1, [r0]
          cmp          r6, r4
          bne          LOCAL_LABEL(CmpXchg64Exit)
          cmp          r1, r5
          bne          LOCAL_LABEL(CmpXchg64Exit)
          strexd       r12, r2, r3, [r0]
          cmp          r12, #0
          bne          LOCAL_LABEL(CmpXchg64Retry)
LOCAL_LABEL(CmpXchg64Exit):
          mov          r0, r6
          dmb
          EPILOG_POP   "{r4-r6,pc}"
LEAF_END RhpLockCmpXchg64, _TEXT
