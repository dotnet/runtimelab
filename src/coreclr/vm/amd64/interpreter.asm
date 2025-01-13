; Licensed to the .NET Foundation under one or more agreements.
; The .NET Foundation licenses this file to you under the MIT license.

; ***********************************************************************
; File: JitHelpers_Fast.asm
;
; Notes: routinues which we believe to be on the hot path for managed
;        code in most scenarios.
; ***********************************************************************


include AsmMacros.inc
include asmconstants.inc

extern ?InterpretMethod@@YA_JPEAUInterpreterMethodInfo@@PEAEPEAX@Z:proc

;
; This routine rearrange the stack so that it will be the way coreclr!InterpretMethod wants.
;
; This routine should only be called by the InterpreterStub from ReadyToRun code, where it puts a pointer to a pointer to the InterpreterMethodInfo in rax and leave the argument untouched
;
; In the sequel, stack grows to the left (i.e. left hand side is lower virtual address), rsp always point to the leftmost (i.e. topmost) of the stack
;
; The state of right now
;
; rax -> &&InterpreterMethodInfo
; 
; [ret] [s0] [s1] [s2] [s3] [spill args]
; 

;
; Desired state before call
;
; rcx -> &InterpreterMethodInfo
; rdx -> ilArgs = rsp + 20h
; TODO: r8 should be stubContext, necessary for IL Stub
;
; +0h  +8h  +10h +18h +20h  +28h  +30h +38h
; [s0] [s1] [s2] [s3] [rcx] [rdx] [r8] [r9] [na] [ret] [s0] [s1] [s2] [s3] [spill args]
; <- shadow slots  ->                                  <- shadow slots  ->
;
; The na slot is meant to make sure the stack is aligned
;

NESTED_ENTRY InterpreterRoutine, _TEXT
        alloc_stack 48h

END_PROLOGUE
        mov [rsp+38h], r9
        mov [rsp+30h], r8
        mov [rsp+28h], rdx
        mov [rsp+20h], rcx
        mov rcx, [rax]
        lea rdx, [rsp + 20h]
        call ?InterpretMethod@@YA_JPEAUInterpreterMethodInfo@@PEAEPEAX@Z
        add rsp, 48h
        ret
NESTED_END InterpreterRoutine, _TEXT

NESTED_ENTRY InterpreterVirtualRoutine, _TEXT
        alloc_stack 48h

END_PROLOGUE
        ; At this point, I have rax pointing to the slot number, and rcx pointing to this
        ; I am making two assumptions
        ; 1. virtual slot < 8 (i.e. we are always calling in the 1st chunk), and
        ; 2. we are always going into the InterpreterMethodInfo
        
        ; rcx was pointing to the object
        mov r8, [rcx]
        ; r8 is pointing to the method table
        add r8, 48h
        ; r8 is pointing to the pointer to the vtable
        mov r8, [r8]
        ; r8 is pointing to the vtable

        ; rax was pointing to the slot number
        mov rax, [rax]
        ; rax is the slot number
        shl rax, 3
        ; rax is the offset from the v-table
        add r8, rax
        ; r8 is pointing to the interpreter-precode
        mov [rsp+38h], r9
        mov [rsp+30h], r8
        mov [rsp+28h], rdx
        mov [rsp+20h], rcx
        mov rcx, [r8] ; rcx is the interpreter-precode
        lea rdx, [rsp + 20h]
        call ?InterpretMethod@@YA_JPEAUInterpreterMethodInfo@@PEAEPEAX@Z
        add rsp, 48h
        ret
NESTED_END InterpreterVirtualRoutine, _TEXT

        end