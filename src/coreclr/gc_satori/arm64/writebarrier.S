// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "unixasmmacros.inc"
#include "../../inc/satoriconstants.h"

// The EE copies RhpCheckedAssignRef and RhpAssignRef as one block (see InitJITWriteBarrierHelpers), and
// RhpCheckedAssignRef reaches into RhpAssignRef with PC-relative instructions: the branch to RhpCheckedEntry
// and the literal load from the patchable pool at the end of RhpAssignRef.
//
// On Apple platforms every symbol that is not an .alt_entry starts a separate atom that the linker may place
// independently, while the assembler resolves PC-relative references to local labels without relocations.
// Thus both helpers must be in one atom, and RhpAssignRef is an .alt_entry there (see its entry below).
//
// The block starts 8-byte aligned, so that the patchable slots, which are 8-byte aligned within it, are also
// aligned in the copy, where the EE updates them with single 8-byte stores.

// An alternate entry in the middle of a helper. Apple's linkers attribute unwind info to the symbol that a range
// starts at, so the range from an .alt_entry symbol has no unwind info unless an unwind region also starts there
// (see https://github.com/dotnet/runtime/pull/114982#discussion_r2083272768). This ends the current region and
// starts a new one at the entry, on Apple platforms only.
.macro ALTERNATE_ENTRY_WITH_UNWIND Name
#if defined(__APPLE__)
    .cfi_endproc
#endif
    ALTERNATE_ENTRY \Name
#if defined(__APPLE__)
    .cfi_startproc
#endif
.endm

// JIT_CheckedWriteBarrier(Object** dst, Object* src)
//
// Write barrier for writes to objects that may reside
// on the managed heap.
//
// On entry:
//   x14 : the destination address (LHS of the assignment).
//         May not be a heap location (hence the checked).
//   x15 : the object reference (RHS of the assignment).
//
// On exit:
//   x12  : trashed
//   x14  : preserved (the destination address is not modified)
//   x15  : trashed
//   x16  : trashed (ip0)
//   x17  : trashed (ip1)
    .text
    .p2align 3
    LEAF_ENTRY RhpCheckedAssignRef, _TEXT
    // See if dst is in GCHeap
    // Card bundle table value is a literal load from a patchable slot co-located with this code
    // (see RhpAssignRef below), rather than a PC-relative reference to an external global, so it
    // stays correct no matter where this code is copied to; see GetAssignRefFunctions.
        ldr     x16, LOCAL_LABEL(PatchableValue_CheckedAssignRef_CardBundleTable)
    // Native destinations may carry a top-byte tag, e.g. Android emutls TLS slots.
    // Ignore the tag only for the page-map lookup; preserve x14 for the store.
        ubfx    x17, x14, #SATORI_PAGE_BITS, #SATORI_PAGE_MAP_BITS    // dst page index
        ldrb    w12, [x16, x17]
        cbnz    x12, LOCAL_LABEL(RhpCheckedEntry)

LOCAL_LABEL(NotInHeap):
    ALTERNATE_ENTRY_WITH_UNWIND RhpCheckedAssignRefAVLocation
        str  x15, [x14]
        ret  lr
    LEAF_END_MARKED RhpCheckedAssignRef

// JIT_WriteBarrier(Object** dst, Object* src)
//
// Write barrier for writes to objects that are known to
// reside on the managed heap.
//
// On entry:
//   x14  : the destination address (LHS of the assignment)
//   x15  : the object reference (RHS of the assignment)
//
// On exit:
//   x12  : trashed
//   x14  : preserved (the destination address is not modified)
//   x15  : trashed
//   x16  : trashed (ip0)
//   x17  : trashed (ip1)
#if defined(__APPLE__)
    // An .alt_entry, so that RhpAssignRef stays in the atom of RhpCheckedAssignRef (see the top of the file).
    // As with LEAF_ENTRY, it has its own unwind info.
    .global C_FUNC(RhpAssignRef)
    .alt_entry C_FUNC(RhpAssignRef)
C_FUNC(RhpAssignRef):
    .cfi_startproc
#else
    LEAF_ENTRY RhpAssignRef, _TEXT
#endif
    // check for escaping assignment
    // 1) check if we own the source region
#ifdef FEATURE_SATORI_EXTERNAL_OBJECTS
        ldr     x16, LOCAL_LABEL(PatchableValue_AssignRef_CardBundleTable)
    LOCAL_LABEL(RhpCheckedEntry):
        lsr     x17, x15, #30                   // source page index
        ldrb    w12, [x16, x17]
        cbz     x12, LOCAL_LABEL(JustAssign)                 // null or external (immutable) object
#else
    LOCAL_LABEL(RhpCheckedEntry):
        cbz     x15, LOCAL_LABEL(JustAssign)                 // assigning null
#endif
        and     x16,  x15, #0xFFFFFFFFFFE00000  // source region
        ldr     x12, [x16]                      // region tag

        // Must match SatoriUtil::GetCurrentThreadTag; x18 is not the Unix thread tag.
#ifdef TARGET_APPLE
        mrs     x17, TPIDRRO_EL0
        and     x17, x17, #-8                   // thread tag on OSX
#else
        mrs     x17, TPIDR_EL0                  // thread tag on other Unix
#endif
        cmp     x12, x17
        bne     LOCAL_LABEL(AssignAndMarkCards)              // not local to this thread

    // 2) check if the src and dst are from the same region
        and     x12, x14, #0xFFFFFFFFFFE00000   // target aligned to region
        cmp     x12, x16
        bne     LOCAL_LABEL(RecordEscape)                    // cross region assignment. definitely escaping

    // 3) check if the target is exposed
        ubfx    x17, x14,#9,#12                 // word index = (dst >> 9) & 0x1FFFFF
        ldr     x17, [x16, x17, lsl #3]         // mark word = [region + index * 8]
        lsr     x12, x14, #3                    // bit = (dst >> 3) [& 63]
        lsr     x17, x17, x12
        tbnz    x17, #0, LOCAL_LABEL(RecordEscape)           // target is exposed. record an escape.

    // UNORDERED! assignment of unescaped, null or external (immutable) object
LOCAL_LABEL(JustAssign):
    ALTERNATE_ENTRY_WITH_UNWIND RhpAssignRefAVLocationNotHeap
        str      x15, [x14]
        ret      lr

LOCAL_LABEL(AssignAndMarkCards):
    ALTERNATE_ENTRY_WITH_UNWIND RhpAssignRefAVLocation
        stlr    x15, [x14]

    // TUNING: barriers in different modes could be separate pieces of code, but barrier switch 
    //         needs to suspend EE, not sure if skipping mode check would worth that much.
        ldr     x17, LOCAL_LABEL(PatchableValue_AssignAndMarkCards_WriteBarrierState)
    // check the barrier state. this must be done after the assignment (in program order
    // if state == 2 we do not set or dirty cards.
        tbz     x17, #1, LOCAL_LABEL(DoCards)

LOCAL_LABEL(ExitNoCards):
        ret     lr

LOCAL_LABEL(DoCards):
    // if src and dst are in the same region, cards are not needed, unless concurrent
        and     x12, x14, #0xFFFFFFFFFFE00000   // target aligned to region
        cmp     x12, x16
        beq     LOCAL_LABEL(CheckConcurrent)    // same region, just check if barrier is not concurrent

    // dst is in another region - cards are needed if src is ephemeral (gen < 2)
        ldr     w12, [x16, 16]                  // source region + 16 -> generation
        tbz     x12, #1, LOCAL_LABEL(MarkCards)

LOCAL_LABEL(CheckConcurrent):
    // if not concurrent, exit
        cbz     x17, LOCAL_LABEL(ExitNoCards)

LOCAL_LABEL(MarkCards):
    // only x12, x15, x16 and x17 may be trashed here, so we keep the card offset in x12,
    // the group address in x15, the page in x16, and reuse x17 for both the loaded byte
    // and the value being stored. That avoids needing any extra temps.

    // fetch card location for x14
        ldr     x12, LOCAL_LABEL(PatchableValue_MarkCards_CardTable)  // fetch the page map value (patched; see GetAssignRefFunctions)
        lsr     x16, x14, #30
        ldr     x16, [x12, x16, lsl #3]              // page
        sub     x12, x14, x16   // offset in page
        lsr     x15, x12, #20   // group index
        lsr     x12, x12, #9    // card offset
        add     x15, x16, x15, lsl #1   // group address (page + index * 2)

    // check if concurrent marking is in progress
        cbnz    x17, LOCAL_LABEL(DirtyCard)

    // SETTING CARD FOR X14
LOCAL_LABEL(SetCard):
        ldrb    w17, [x16, x12]
        cbnz    w17, LOCAL_LABEL(Exit)
        mov     w17, #1
        strb    w17, [x16, x12]
LOCAL_LABEL(SetGroup):
        ldrb    w17, [x15, #0x80]
        cbnz    w17, LOCAL_LABEL(CardSet)
        mov     w17, #1
        strb    w17, [x15, #0x80]
LOCAL_LABEL(SetPage):
        ldrb    w17, [x16]
        cbnz    w17, LOCAL_LABEL(CardSet)
        mov     w17, #1
        strb    w17, [x16]

LOCAL_LABEL(CardSet):
    // check if concurrent marking is still not in progress
    // (uses x17 so that the card offset in x12 stays live for DirtyCard)
        ldr     x17, LOCAL_LABEL(PatchableValue_CardSet_WriteBarrierState)  // fetch the barrier state value (patched; see GetAssignRefFunctions)
        cbnz    x17, LOCAL_LABEL(DirtyCard)

LOCAL_LABEL(Exit):
        ret  lr

    // DIRTYING CARD FOR X14
LOCAL_LABEL(DirtyCard):
        add     x12, x16, x12   // card address
        mov     w17, #4
        // must be after the field write to allow concurrent clean
        stlrb   w17, [x12]
LOCAL_LABEL(DirtyGroup):
        ldrb    w12, [x15, #0x80]
        tbnz    w12, #2, LOCAL_LABEL(Exit)
        strb    w17, [x15, #0x80]
LOCAL_LABEL(DirtyPage):
        ldrb    w12, [x16]
        tbnz    w12, #2, LOCAL_LABEL(Exit)
        strb    w17, [x16]
        ret  lr

    // this is expected to be rare.
LOCAL_LABEL(RecordEscape):

    // 4) check if the source is escaped (x16 has source region)
        add         x12, x15, #8                   // escape bit is MT + 1
        ubfx        x17, x12, #9,#12               // word index = (dst >> 9) & 0x1FFFFF
        ldr         x17, [x16, x17, lsl #3]        // mark word = [region + index * 8]
        lsr         x12, x12, #3                   // bit = (dst >> 3) [& 63]
        lsr         x17, x17, x12
        tbnz        x17, #0, LOCAL_LABEL(AssignAndMarkCards)        // source is already escaped.

        // because of the barrier call convention we need to preserve x0-x11, x13, x14, x15 and x30.
        // x12, x16 and x17 are trashable per the barrier contract, and x12 is rewritten before
        // being read again. x29 is callee-saved, so EscapeFn preserves it for us.
        // also save q0, in case it is used for stack clearing.
        // Hopefully EscapeFn cannot corrupt other vector regs, since there is no float math or vectorizable code in there.

        stp     x30, x0, [sp, -16 * 9]!
        stp     x1,  x2, [sp, 16 * 1]
        stp     x3,  x4, [sp, 16 * 2]
        stp     x5,  x6, [sp, 16 * 3]
        stp     x7,  x8, [sp, 16 * 4]
        stp     x9,  x10,[sp, 16 * 5]
        stp     x11, x13,[sp, 16 * 6]
        stp     x14, x15,[sp, 16 * 7]
        str     q0,      [sp, 16 * 8]

        // void SatoriRegion::EscapeFn(SatoriObject** dst, SatoriObject* src, SatoriRegion* region)
        // mov  x0, x14  EscapeFn does not use dst, it is just to avoid arg shuffle on x64
        mov  x1, x15
        mov  x2, x16                       // source region
        ldr  x12, [x16, #8]                 // EscapeFn address
        blr  x12

        ldr     q0,      [sp, 16 * 8]
        ldp     x14, x15,[sp, 16 * 7]
        ldp     x11, x13,[sp, 16 * 6]
        ldp     x9,  x10,[sp, 16 * 5]
        ldp     x7,  x8, [sp, 16 * 4]
        ldp     x5,  x6, [sp, 16 * 3]
        ldp     x3,  x4, [sp, 16 * 2]
        ldp     x1,  x2, [sp, 16 * 1]
        ldp     x30, x0, [sp], 16 * 9

        and     x16, x15, #0xFFFFFFFFFFE00000  // source region
        b       LOCAL_LABEL(AssignAndMarkCards)

    // Patchable literal pool holding the cached values of the globals referenced above. Keeping
    // this co-located with the code (rather than referencing the globals by address) means the
    // PC-relative literal loads above stay correct no matter where this code is copied to; the EE
    // refreshes these slots after copying and whenever the globals' values change; see
    // GetAssignRefFunctions.
        .p2align 3
    PATCH_LABEL PatchableValue_CheckedAssignRef_CardBundleTable
    LOCAL_LABEL(PatchableValue_CheckedAssignRef_CardBundleTable):
        .quad 0
    PATCH_LABEL PatchableValue_AssignRef_CardBundleTable
    LOCAL_LABEL(PatchableValue_AssignRef_CardBundleTable):
        .quad 0
    PATCH_LABEL PatchableValue_AssignAndMarkCards_WriteBarrierState
    LOCAL_LABEL(PatchableValue_AssignAndMarkCards_WriteBarrierState):
        .quad 0
    PATCH_LABEL PatchableValue_MarkCards_CardTable
    LOCAL_LABEL(PatchableValue_MarkCards_CardTable):
        .quad 0
    PATCH_LABEL PatchableValue_CardSet_WriteBarrierState
    LOCAL_LABEL(PatchableValue_CardSet_WriteBarrierState):
        .quad 0
    LEAF_END_MARKED RhpAssignRef
