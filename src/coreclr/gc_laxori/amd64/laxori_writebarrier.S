// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// The Laxori write barrier for System V AMD64. The runtime installs it through IGCHeap::GetAssignRefFunctions
// as CORINFO_HELP_ASSIGN_REF and CORINFO_HELP_CHECKED_ASSIGN_REF (the "specialized" barrier ABI). It is the LXR barrier
// plus the block-table check of Laxori.
//
// What the barrier does (rdi = destination slot, rsi = new reference):
//   1. the reference is in a region that the current thread owns (the owner table entry of its block is the thread tag,
//      fs:0): if the destination is in the same region and its slot is not exposed (a bit of the private bitmap), store and
//      return. A private slot needs no log. Otherwise call LaxoriEscapeSlowPath(dst, ref), which escapes the object and
//      everything that it reaches in the region (Satori EscapeRecursively), then go on with 2.
//   2. the LXR check: test the "unlogged" bit of the destination field in side metadata (one bit for every 8-byte slot).
//      A clear bit: store and return. A set bit: call LaxoriBarrierSlowPath(dst), which logs the old value. Then store.
//
// Register contract (the one the JIT assumes for these helpers, see RBM_CALLEE_TRASH_WRITEBARRIER):
//   in:     rdi = destination slot, rsi = new reference
//   killed: rax, rcx, rdx, rsi, rdi, r8, r9, r10, r11 and the flags
//   kept:   every other register, and ALL vector state (xmm0-15, ymm upper halves, zmm0-31, k0-7, mxcsr)
// The fast paths are leaf routines that use only rax, rcx, r8. The slow paths call C++ code, which can change any
// vector register. So they save the whole vector state with xsave (fxsave when the OS does not enable xsave), call
// the C++ function, and restore the state.
//
// Relocation rules (the runtime copies the block to another page):
//   - the block is [LaxoriCheckedAssignRef, LaxoriWriteBarrierBlock_End) and must be relocatable,
//   - inside the block only local labels are used, and all branches are relative, so a copy keeps working,
//   - every value that is not known at build time is read from an 8-byte aligned slot inside the block
//     (the LaxoriPatchable_* labels); the runtime fills the slots (see GCWriteBarrierPatchableValue).

.intel_syntax noprefix
#include "unixasmmacros.inc"

// Size of the xsave area. The mask 0xE7 (x87, SSE, AVX, opmask, ZMM_Hi256, Hi16_ZMM) needs 2688 bytes in the
// standard format. The frame has room for the 64-byte alignment too.
#define LAXORI_XSAVE_FRAME_SIZE 2816
#define LAXORI_XSAVE_HEADER_OFFSET 512
// log2 of the size of an Immix block (32 KiB)
#define LAXORI_BLOCK_SHIFT 15

.macro LAXORI_SAVE_VECTOR_STATE
        // the header of the xsave area must be zero before xrstor sees it
        xor     eax, eax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 0], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 8], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 16], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 24], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 32], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 40], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 48], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 56], rax

        mov     rax, qword ptr [rip + LOCAL_LABEL(XsaveMaskSlot)]
        test    rax, rax
        jz      .Lsave_fx_\@
        mov     rdx, rax
        shr     rdx, 32
        xsave   [rsp]
        // C++ code uses legacy SSE encoding. Clear the upper halves, so that it does not pay for mixing with the
        // caller's AVX state; xrstor puts them back.
        test    al, 4
        jz      .Lsave_done_\@
        vzeroupper
        jmp     .Lsave_done_\@
.Lsave_fx_\@:
        fxsave  [rsp]
.Lsave_done_\@:
.endm

.macro LAXORI_RESTORE_VECTOR_STATE
        mov     rax, qword ptr [rip + LOCAL_LABEL(XsaveMaskSlot)]
        test    rax, rax
        jz      .Lrestore_fx_\@
        mov     rdx, rax
        shr     rdx, 32
        xrstor  [rsp]
        jmp     .Lrestore_done_\@
.Lrestore_fx_\@:
        fxrstor [rsp]
.Lrestore_done_\@:
.endm

//
//   rdi - destination address (any address; outside the Laxori heap the helper only stores)
//   rsi - new reference
//
LEAF_ENTRY LaxoriCheckedAssignRef, _TEXT
        mov     rax, rdi
        sub     rax, qword ptr [rip + LOCAL_LABEL(HeapLowSlot)]
        cmp     rax, qword ptr [rip + LOCAL_LABEL(HeapSizeSlot)]
        jb      LOCAL_LABEL(AssignRefEntry)
ALTERNATE_ENTRY LaxoriCheckedAssignRefAVLocation
        mov     [rdi], rsi
        ret
LEAF_END_MARKED LaxoriCheckedAssignRef, _TEXT

//
//   rdi - destination address, must be inside the Laxori heap
//   rsi - new reference
//
LEAF_ENTRY LaxoriAssignRef, _TEXT
LOCAL_LABEL(AssignRefEntry):
        // is the reference in a region of the current thread? owner_table[(ref - heap low) >> 15] == fs:0
        mov     rax, rsi
        sub     rax, qword ptr [rip + LOCAL_LABEL(HeapLowSlot)]
        mov     r8, qword ptr [rip + LOCAL_LABEL(PrivateHeapBytesSlot)]
        cmp     rax, qword ptr [r8]
        jae     LOCAL_LABEL(LogCheck)
        shr     rax, LAXORI_BLOCK_SHIFT
        mov     r8, qword ptr [rip + LOCAL_LABEL(OwnerTableSlot)]
        mov     rdx, qword ptr [r8 + rax * 8]
        mov     rcx, qword ptr fs:[0]
        cmp     rdx, rcx
        jne     LOCAL_LABEL(LogCheck)
        // the reference is private to this thread. Same region as the destination?
        mov     rax, rdi
        xor     rax, rsi
        mov     rcx, qword ptr [rip + LOCAL_LABEL(RegionShiftSlot)]
        shr     rax, cl
        jnz     LOCAL_LABEL(EscapeEntry)
        // is the slot exposed? bit (dst >> 3) of the private bitmap
        mov     rax, rdi
        shr     rax, 6
        mov     r8, qword ptr [rip + LOCAL_LABEL(PrivateBitsSlot)]
ALTERNATE_ENTRY LaxoriPrivateBitsAVLocation
        movzx   eax, byte ptr [rax + r8]
        mov     ecx, edi
        shr     ecx, 3
        and     ecx, 7
        bt      eax, ecx
        jc      LOCAL_LABEL(EscapeEntry)
        jmp     LOCAL_LABEL(PrivateStore)
LOCAL_LABEL(LogCheck):
        // metadata byte = base[dst >> 6], bit = (dst >> 3) & 7 (granularity: one bit for every 8 bytes)
        mov     rax, rdi
        shr     rax, 6
        mov     r8, qword ptr [rip + LOCAL_LABEL(MetadataBaseSlot)]
ALTERNATE_ENTRY LaxoriAssignRefMetadataAVLocation
        movzx   eax, byte ptr [rax + r8]
        mov     ecx, edi
        shr     ecx, 3
        and     ecx, 7
        bt      eax, ecx
        jc      LOCAL_LABEL(SlowEntry)
LOCAL_LABEL(PrivateStore):
ALTERNATE_ENTRY LaxoriAssignRefAVLocation
        mov     [rdi], rsi
        ret
LEAF_END_MARKED LaxoriAssignRef, _TEXT

//
// Escape slow path. Entered by a jump from the fast path, so the stack looks as if LaxoriAssignRef was just called.
// rdi = destination, rsi = reference to an object of a region of this thread. After the call the code goes on with the
// LXR check of the destination (a private destination has a clear unlog bit, so that is a plain store).
//
NESTED_ENTRY LaxoriEscapeSlow, _TEXT, NoHandler
LOCAL_LABEL(EscapeEntry):
        push_nonvol_reg rbp
        mov     rbp, rsp
        set_cfa_register rbp, 16
        // keep the arguments: the call below kills rdi and rsi
        push    rdi
        push    rsi
        sub     rsp, LAXORI_XSAVE_FRAME_SIZE
        and     rsp, -64

        LAXORI_SAVE_VECTOR_STATE
        // LaxoriEscapeSlowPath(Object** destination, Object* reference): both are still in rdi and rsi
        call    qword ptr [rip + LOCAL_LABEL(EscapePathSlot)]
        LAXORI_RESTORE_VECTOR_STATE

        lea     rsp, [rbp - 16]
        pop     rsi
        pop     rdi
        pop     rbp
        .cfi_def_cfa rsp, 8
        .cfi_restore rbp
        jmp     LOCAL_LABEL(LogCheck)
NESTED_END LaxoriEscapeSlow, _TEXT

//
// Slow path of the log. Entered by a jump from the fast path, so the stack looks as if LaxoriAssignRef was just called.
// A nested function with unwind information. The runtime unwinds copied code through the original address.
//
NESTED_ENTRY LaxoriBarrierSlow, _TEXT, NoHandler
LOCAL_LABEL(SlowEntry):
        push_nonvol_reg rbp
        mov     rbp, rsp
        set_cfa_register rbp, 16
        // keep the arguments: the call below kills rdi and rsi
        push    rdi
        push    rsi
        sub     rsp, LAXORI_XSAVE_FRAME_SIZE
        and     rsp, -64

        LAXORI_SAVE_VECTOR_STATE
        // rdi still holds the destination: LaxoriBarrierSlowPath(Object** destination)
        call    qword ptr [rip + LOCAL_LABEL(SlowPathSlot)]
        LAXORI_RESTORE_VECTOR_STATE

        lea     rsp, [rbp - 16]
        pop     rsi
        pop     rdi
        pop     rbp
        .cfi_def_cfa rsp, 8
        .cfi_restore rbp
ALTERNATE_ENTRY LaxoriBarrierSlowAVLocation
        mov     [rdi], rsi
        ret
NESTED_END LaxoriBarrierSlow, _TEXT

//
// Patchable slots. Each one is 8-byte aligned, so the runtime updates it with one store. The runtime copies the
// value of a global (named in GetAssignRefFunctions) into the slot of the copied block.
//
        .balign 8
PATCH_LABEL LaxoriPatchable_MetadataBase
LOCAL_LABEL(MetadataBaseSlot):
        .quad   0
PATCH_LABEL LaxoriPatchable_HeapLow
LOCAL_LABEL(HeapLowSlot):
        .quad   0
PATCH_LABEL LaxoriPatchable_HeapSize
LOCAL_LABEL(HeapSizeSlot):
        .quad   0
PATCH_LABEL LaxoriPatchable_SlowPath
LOCAL_LABEL(SlowPathSlot):
        .quad   0
PATCH_LABEL LaxoriPatchable_XsaveMask
LOCAL_LABEL(XsaveMaskSlot):
        .quad   0
PATCH_LABEL LaxoriPatchable_OwnerTable
LOCAL_LABEL(OwnerTableSlot):
        .quad   0
PATCH_LABEL LaxoriPatchable_PrivateBits
LOCAL_LABEL(PrivateBitsSlot):
        .quad   0
PATCH_LABEL LaxoriPatchable_RegionShift
LOCAL_LABEL(RegionShiftSlot):
        .quad   0
PATCH_LABEL LaxoriPatchable_EscapePath
LOCAL_LABEL(EscapePathSlot):
        .quad   0
PATCH_LABEL LaxoriPatchable_PrivateHeapBytes
LOCAL_LABEL(PrivateHeapBytesSlot):
        .quad   0

        .global C_FUNC(LaxoriWriteBarrierBlock_End)
C_FUNC(LaxoriWriteBarrierBlock_End):
        nop
