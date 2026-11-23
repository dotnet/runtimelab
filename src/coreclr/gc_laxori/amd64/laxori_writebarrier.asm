; Licensed to the .NET Foundation under one or more agreements.
; The .NET Foundation licenses this file to you under the MIT license.

; The Laxori write barrier for Windows AMD64. It is the same barrier as laxori_writebarrier.S (read the description there), in
; MASM. The runtime installs it through IGCHeap::GetAssignRefFunctions as CORINFO_HELP_ASSIGN_REF and
; CORINFO_HELP_CHECKED_ASSIGN_REF (the "specialized" barrier ABI).
;
; What differs from the System V file:
;   - argument registers: rcx = destination slot, rdx = new reference (System V: rdi, rsi);
;   - the thread tag (the owner of a region) is the address of the TEB, gs:[30h] (System V: the thread pointer, fs:0);
;     laxori_private.cpp and laxori_gc.cpp compute the same value with __readgsqword(0x30);
;   - the temporary registers of the fast path are rax, r8, r9, r10 (the helper may kill rax, rcx, rdx, r8-r11), because
;     rcx and rdx hold the arguments. The shift by the region shift needs cl, so the destination moves to r10 for one
;     instruction;
;   - the xsave sequence uses rdx for the high half of the mask, so the escape path reads the reference again from its
;     save slot after the save;
;   - the C++ functions are called with 32 bytes of shadow space.
;
; Register contract of the helpers (see RBM_CALLEE_TRASH_WRITEBARRIER in jit/targetamd64.h):
;   in:     rcx = destination slot, rdx = new reference
;   killed: rax, rcx, rdx, r8, r9, r10, r11 and the flags
;   kept:   every other register, and ALL vector state (xmm0-15, ymm upper halves, zmm0-31, k0-7, mxcsr)

include AsmConstants.inc
include AsmMacros.inc

; Size of the xsave area. The mask 0xE7 (x87, SSE, AVX, opmask, ZMM_Hi256, Hi16_ZMM) needs 2688 bytes in the
; standard format. The frame has room for the 64-byte alignment too.
LAXORI_XSAVE_FRAME_SIZE    equ 2816
LAXORI_XSAVE_HEADER_OFFSET equ 512
; log2 of the size of an Immix block (32 KiB)
LAXORI_BLOCK_SHIFT         equ 15
; offset of the self pointer in the TEB
LAXORI_TEB_SELF            equ 30h

; Saves the vector state to [rsp] (the xsave frame). Kills rax and rdx.
; Test control (the native barrier test builds this file with it and must see a failure): LAXORI_WB_CONTROL_NO_VECTOR_SAVE.
LAXORI_SAVE_VECTOR_STATE macro
        local   SaveFx, SaveDone
ifndef LAXORI_WB_CONTROL_NO_VECTOR_SAVE
        ; the header of the xsave area must be zero before xrstor sees it
        xor     eax, eax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 0], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 8], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 16], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 24], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 32], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 40], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 48], rax
        mov     qword ptr [rsp + LAXORI_XSAVE_HEADER_OFFSET + 56], rax

        mov     rax, qword ptr [XsaveMaskSlot]
        test    rax, rax
        jz      SaveFx
        mov     rdx, rax
        shr     rdx, 32
        xsave   [rsp]
        ; C++ code uses legacy SSE encoding. Clear the upper halves, so that it does not pay for mixing with the
        ; caller's AVX state; xrstor puts them back.
        test    al, 4
        jz      SaveDone
        vzeroupper
        jmp     SaveDone
SaveFx:
        fxsave  [rsp]
SaveDone:
endif
        endm

; Restores the vector state from [rsp]. Kills rax and rdx.
LAXORI_RESTORE_VECTOR_STATE macro
        local   RestoreFx, RestoreDone
ifndef LAXORI_WB_CONTROL_NO_VECTOR_SAVE
        mov     rax, qword ptr [XsaveMaskSlot]
        test    rax, rax
        jz      RestoreFx
        mov     rdx, rax
        shr     rdx, 32
        xrstor  [rsp]
        jmp     RestoreDone
RestoreFx:
        fxrstor [rsp]
RestoreDone:
endif
        endm

;
;   rcx - destination address (any address; outside the Laxori heap the helper only stores)
;   rdx - new reference
;
        LEAF_ENTRY LaxoriCheckedAssignRef, _TEXT
        mov     rax, rcx
        sub     rax, qword ptr [HeapLowSlot]
        cmp     rax, qword ptr [HeapSizeSlot]
        jb      LaxoriAssignRefEntry
        ALTERNATE_ENTRY LaxoriCheckedAssignRefAVLocation
        mov     [rcx], rdx
        ret
        LEAF_END_MARKED LaxoriCheckedAssignRef, _TEXT

;
;   rcx - destination address, must be inside the Laxori heap
;   rdx - new reference
;
        LEAF_ENTRY LaxoriAssignRef, _TEXT
        ALTERNATE_ENTRY LaxoriAssignRefEntry
        ; is the reference in a region of the current thread? owner_table[(ref - heap low) >> 15] == the TEB address
        mov     rax, rdx
        sub     rax, qword ptr [HeapLowSlot]
        mov     r8, qword ptr [PrivateHeapBytesSlot]
        cmp     rax, qword ptr [r8]
        jae     LaxoriLogCheck
        shr     rax, LAXORI_BLOCK_SHIFT
        mov     r8, qword ptr [OwnerTableSlot]
        mov     r9, qword ptr [r8 + rax * 8]
        mov     rax, qword ptr gs:[LAXORI_TEB_SELF]
        cmp     r9, rax
        jne     LaxoriLogCheck
        ; the reference is private to this thread. Same region as the destination?
        mov     rax, rcx
        xor     rax, rdx
        mov     r10, rcx
        mov     rcx, qword ptr [RegionShiftSlot]
        shr     rax, cl
        mov     rcx, r10
        jnz     LaxoriEscapeSlow
        ; is the slot exposed? bit (dst >> 3) of the private bitmap
        mov     rax, rcx
        shr     rax, 6
        mov     r8, qword ptr [PrivateBitsSlot]
        ALTERNATE_ENTRY LaxoriPrivateBitsAVLocation
        movzx   eax, byte ptr [rax + r8]
        mov     r9d, ecx
        shr     r9d, 3
        and     r9d, 7
        bt      eax, r9d
        jc      LaxoriEscapeSlow
        jmp     LaxoriPrivateStore
        ALTERNATE_ENTRY LaxoriLogCheck
        ; metadata byte = base[dst >> 6], bit = (dst >> 3) & 7 (granularity: one bit for every 8 bytes)
        mov     rax, rcx
        shr     rax, 6
        mov     r8, qword ptr [MetadataBaseSlot]
        ALTERNATE_ENTRY LaxoriAssignRefMetadataAVLocation
        movzx   eax, byte ptr [rax + r8]
        mov     r9d, ecx
        shr     r9d, 3
        and     r9d, 7
        bt      eax, r9d
        jc      LaxoriBarrierSlow
        ALTERNATE_ENTRY LaxoriPrivateStore
        ALTERNATE_ENTRY LaxoriAssignRefAVLocation
        mov     [rcx], rdx
        ret
        LEAF_END_MARKED LaxoriAssignRef, _TEXT

;
; Escape slow path. Entered by a jump from the fast path, so the stack looks as if LaxoriAssignRef was just called.
; rcx = destination, rdx = reference to an object of a region of this thread. After the call the code goes on with the
; LXR check of the destination (a private destination has a clear unlog bit, so that is a plain store).
;
        NESTED_ENTRY LaxoriEscapeSlow, _TEXT
        push_nonvol_reg rbp
        set_frame rbp, 0
        END_PROLOGUE
        ; keep the arguments: the call below kills rcx and rdx
        push    rcx
        push    rdx
        sub     rsp, LAXORI_XSAVE_FRAME_SIZE
        and     rsp, -64

        LAXORI_SAVE_VECTOR_STATE
        ; LaxoriEscapeSlowPath(Object** destination, Object* reference): rcx is intact; the save used rdx
        mov     rdx, qword ptr [rbp - 16]
        ; shadow space for the call
        sub     rsp, 32
        call    qword ptr [EscapePathSlot]
        add     rsp, 32
        LAXORI_RESTORE_VECTOR_STATE

        lea     rsp, [rbp - 16]
        pop     rdx
        pop     rcx
        pop     rbp
        jmp     LaxoriLogCheck
        NESTED_END_MARKED LaxoriEscapeSlow, _TEXT

;
; Slow path of the log. Entered by a jump from the fast path, so the stack looks as if LaxoriAssignRef was just called.
; A nested function with unwind information. The runtime unwinds copied code through the original address.
;
        NESTED_ENTRY LaxoriBarrierSlow, _TEXT
        push_nonvol_reg rbp
        set_frame rbp, 0
        END_PROLOGUE
        ; keep the arguments: the call below kills rcx and rdx
        push    rcx
        push    rdx
        sub     rsp, LAXORI_XSAVE_FRAME_SIZE
        and     rsp, -64

        LAXORI_SAVE_VECTOR_STATE
        ; shadow space for the call
        sub     rsp, 32
        ; rcx still holds the destination: LaxoriBarrierSlowPath(Object** destination)
        call    qword ptr [SlowPathSlot]
        add     rsp, 32
        LAXORI_RESTORE_VECTOR_STATE

        lea     rsp, [rbp - 16]
        pop     rdx
        pop     rcx
        pop     rbp
        ALTERNATE_ENTRY LaxoriBarrierSlowAVLocation
        mov     [rcx], rdx
        ret
        NESTED_END_MARKED LaxoriBarrierSlow, _TEXT

;
; Patchable slots. Each one is 8-byte aligned, so the runtime updates it with one store. The runtime copies the
; value of a global (named in GetAssignRefFunctions) into the slot of the copied block.
;
_TEXT   segment para 'CODE'
        align   8
        PATCH_LABEL LaxoriPatchable_MetadataBase
MetadataBaseSlot dq 0
        PATCH_LABEL LaxoriPatchable_HeapLow
HeapLowSlot     dq 0
        PATCH_LABEL LaxoriPatchable_HeapSize
HeapSizeSlot    dq 0
        PATCH_LABEL LaxoriPatchable_SlowPath
SlowPathSlot    dq 0
        PATCH_LABEL LaxoriPatchable_XsaveMask
XsaveMaskSlot   dq 0
        PATCH_LABEL LaxoriPatchable_OwnerTable
OwnerTableSlot  dq 0
        PATCH_LABEL LaxoriPatchable_PrivateBits
PrivateBitsSlot dq 0
        PATCH_LABEL LaxoriPatchable_RegionShift
RegionShiftSlot dq 0
        PATCH_LABEL LaxoriPatchable_EscapePath
EscapePathSlot  dq 0
        PATCH_LABEL LaxoriPatchable_PrivateHeapBytes
PrivateHeapBytesSlot dq 0

        public  LaxoriWriteBarrierBlock_End
LaxoriWriteBarrierBlock_End label qword
        nop
_TEXT   ends

        end
