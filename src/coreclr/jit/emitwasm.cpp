// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XX                                                                           XX
XX                             emitwasm.cpp                                   XX
XX                                                                           XX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
*/

#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

#if defined(TARGET_WASM32) || defined(TARGET_WASM64) // TODO Wasm

/*****************************************************************************/
/*****************************************************************************/

#include "instr.h"
#include "emit.h"
#include "codegen.h"

bool IsSSEInstruction(instruction ins)
{
    return (ins >= INS_FIRST_SSE_INSTRUCTION) && (ins <= INS_LAST_SSE_INSTRUCTION);
}

bool IsSSEOrAVXInstruction(instruction ins)
{
    return (ins >= INS_FIRST_SSE_INSTRUCTION) && (ins <= INS_LAST_AVX_INSTRUCTION);
}

bool IsAVXOnlyInstruction(instruction ins)
{
    return (ins >= INS_FIRST_AVX_INSTRUCTION) && (ins <= INS_LAST_AVX_INSTRUCTION);
}

bool IsFMAInstruction(instruction ins)
{
    return (ins >= INS_FIRST_FMA_INSTRUCTION) && (ins <= INS_LAST_FMA_INSTRUCTION);
}

bool IsBMIInstruction(instruction ins)
{
    return (ins >= INS_FIRST_BMI_INSTRUCTION) && (ins <= INS_LAST_BMI_INSTRUCTION);
}

regNumber getBmiRegNumber(instruction ins)
{
    switch (ins)
    {
        case INS_blsi:
        {
            return (regNumber)3;
        }

        case INS_blsmsk:
        {
            return (regNumber)2;
        }

        case INS_blsr:
        {
            return (regNumber)1;
        }

        default:
        {
            assert(IsBMIInstruction(ins));
            return REG_NA;
        }
    }
}

regNumber getSseShiftRegNumber(instruction ins)
{
    switch (ins)
    {
        case INS_psrldq:
        {
            return (regNumber)3;
        }

        case INS_pslldq:
        {
            return (regNumber)7;
        }

        case INS_psrld:
        case INS_psrlw:
        case INS_psrlq:
        {
            return (regNumber)2;
        }

        case INS_pslld:
        case INS_psllw:
        case INS_psllq:
        {
            return (regNumber)6;
        }

        case INS_psrad:
        case INS_psraw:
        {
            return (regNumber)4;
        }

        default:
        {
            assert(!"Invalid instruction for SSE2 instruction of the form: opcode reg, immed8");
            return REG_NA;
        }
    }
}

bool emitter::IsAVXInstruction(instruction ins)
{
    return UseVEXEncoding() && IsSSEOrAVXInstruction(ins);
}

// Returns true if the AVX instruction is a binary operator that requires 3 operands.
// When we emit an instruction with only two operands, we will duplicate the destination
// as a source.
// TODO-XArch-Cleanup: This is a temporary solution for now. Eventually this needs to
// be formalized by adding an additional field to instruction table to
// to indicate whether a 3-operand instruction.
bool emitter::IsDstDstSrcAVXInstruction(instruction ins)
{
    return ((CodeGenInterface::instInfo[ins] & INS_Flags_IsDstDstSrcAVXInstruction) != 0) && IsAVXInstruction(ins);
}

// Returns true if the AVX instruction requires 3 operands that duplicate the source
// register in the vvvv field.
// TODO-XArch-Cleanup: This is a temporary solution for now. Eventually this needs to
// be formalized by adding an additional field to instruction table to
// to indicate whether a 3-operand instruction.
bool emitter::IsDstSrcSrcAVXInstruction(instruction ins)
{
    return ((CodeGenInterface::instInfo[ins] & INS_Flags_IsDstSrcSrcAVXInstruction) != 0) && IsAVXInstruction(ins);
}

//------------------------------------------------------------------------
// AreUpper32BitsZero: check if some previously emitted
//     instruction set the upper 32 bits of reg to zero.
//
// Arguments:
//    reg - register of interest
//
// Return Value:
//    true if previous instruction zeroed reg's upper 32 bits.
//    false if it did not, or if we can't safely determine.
//
// Notes:
//    Currently only looks back one instruction.
//
//    movsx eax, ... might seem viable but we always encode this
//    instruction with a 64 bit destination. See TakesRexWPrefix.

bool emitter::AreUpper32BitsZero(regNumber reg)
{
    // If there are no instructions in this IG, we can look back at
    // the previous IG's instructions if this IG is an extension.
    //
    if ((emitCurIGinsCnt == 0) && ((emitCurIG->igFlags & IGF_EXTEND) == 0))
    {
        return false;
    }

    instrDesc* id  = emitLastIns;
    insFormat  fmt = id->idInsFmt();

    // This isn't meant to be a comprehensive check. Just look for what
    // seems to be common.
    switch (fmt)
    {
        case IF_RWR_CNS:
        case IF_RRW_CNS:
        case IF_RRW_SHF:
        case IF_RWR_RRD:
        case IF_RRW_RRD:
        case IF_RWR_MRD:
        case IF_RWR_SRD:
        case IF_RWR_ARD:

            // Bail if not writing to the right register
            if (id->idReg1() != reg)
            {
                return false;
            }

            // Bail if movsx, we always have movsx sign extend to 8 bytes
            if (id->idIns() == INS_movsx)
            {
                return false;
            }

            // movzx always zeroes the upper 32 bits.
            if (id->idIns() == INS_movzx)
            {
                return true;
            }

            // Else rely on operation size.
            return (id->idOpSize() == EA_4BYTE);

        default:
            break;
    }

    return false;
}

//------------------------------------------------------------------------
// AreFlagsSetToZeroCmp: Checks if the previous instruction set the SZ, and optionally OC, flags to
//                       the same values as if there were a compare to 0
//
// Arguments:
//    reg - register of interest
//    opSize - size of register
//    needsOCFlags - additionally check the overflow and carry flags
//
// Return Value:
//    true if the previous instruction set the flags for reg
//    false if not, or if we can't safely determine
//
// Notes:
//    Currently only looks back one instruction.
bool emitter::AreFlagsSetToZeroCmp(regNumber reg, emitAttr opSize, bool needsOCFlags)
{
    assert(reg != REG_NA);
    // Don't look back across IG boundaries (possible control flow)
    if (emitCurIGinsCnt == 0 && ((emitCurIG->igFlags & IGF_EXTEND) == 0))
    {
        return false;
    }

    instrDesc* id  = emitLastIns;
    insFormat  fmt = id->idInsFmt();

    // make sure op1 is a reg
    switch (fmt)
    {
        case IF_RWR_CNS:
        case IF_RRW_CNS:
        case IF_RRW_SHF:
        case IF_RWR_RRD:
        case IF_RRW_RRD:
        case IF_RWR_MRD:
        case IF_RWR_SRD:
        case IF_RRW_SRD:
        case IF_RWR_ARD:
        case IF_RRW_ARD:
        case IF_RWR:
        case IF_RRD:
        case IF_RRW:
            break;

        default:
            return false;
    }

    if (id->idReg1() != reg)
    {
        return false;
    }

    switch (id->idIns())
    {
        case INS_adc:
        case INS_add:
        case INS_dec:
        case INS_dec_l:
        case INS_inc:
        case INS_inc_l:
        case INS_neg:
        case INS_shr_1:
        case INS_shl_1:
        case INS_sar_1:
        case INS_sbb:
        case INS_sub:
        case INS_xadd:
            if (needsOCFlags)
            {
                return false;
            }
            FALLTHROUGH;
        // these always set OC to 0
        case INS_and:
        case INS_or:
        case INS_xor:
            return id->idOpSize() == opSize;

        default:
            break;
    }

    return false;
}

//------------------------------------------------------------------------
// IsDstSrcImmAvxInstruction: Checks if the instruction has a "reg, reg/mem, imm" or
//                            "reg/mem, reg, imm" form for the legacy, VEX, and EVEX
//                            encodings.
//
// Arguments:
//    instruction -- processor instruction to check
//
// Return Value:
//    true if instruction has a "reg, reg/mem, imm" or "reg/mem, reg, imm" encoding
//    form for the legacy, VEX, and EVEX encodings.
//
//    That is, the instruction takes two operands, one of which is immediate, and it
//    does not need to encode any data in the VEX.vvvv field.
//
static bool IsDstSrcImmAvxInstruction(instruction ins)
{
    switch (ins)
    {
        case INS_aeskeygenassist:
        case INS_extractps:
        case INS_pextrb:
        case INS_pextrw:
        case INS_pextrd:
        case INS_pextrq:
        case INS_pshufd:
        case INS_pshufhw:
        case INS_pshuflw:
        case INS_roundpd:
        case INS_roundps:
            return true;
        default:
            return false;
    }
}

// -------------------------------------------------------------------
// Is4ByteSSEInstruction: Returns true if the SSE instruction is a 4-byte opcode.
//
// Arguments:
//    ins  -  instruction
//
// Note that this should be true for any of the instructions in instrsXArch.h
// that use the SSE38 or SSE3A macro but returns false if the VEX encoding is
// in use, since that encoding does not require an additional byte.
bool emitter::Is4ByteSSEInstruction(instruction ins)
{
    return !UseVEXEncoding() && EncodedBySSE38orSSE3A(ins);
}

// Returns true if this instruction requires a VEX prefix
// All AVX instructions require a VEX prefix
bool emitter::TakesVexPrefix(instruction ins)
{
    // special case vzeroupper as it requires 2-byte VEX prefix
    // special case the fencing, movnti and the prefetch instructions as they never take a VEX prefix
    switch (ins)
    {
        case INS_lfence:
        case INS_mfence:
        case INS_movnti:
        case INS_prefetchnta:
        case INS_prefetcht0:
        case INS_prefetcht1:
        case INS_prefetcht2:
        case INS_sfence:
        case INS_vzeroupper:
            return false;
        default:
            break;
    }

    return IsAVXInstruction(ins);
}

// Add base VEX prefix without setting W, R, X, or B bits
// L bit will be set based on emitter attr.
//
// 2-byte VEX prefix = C5 <R,vvvv,L,pp>
// 3-byte VEX prefix = C4 <R,X,B,m-mmmm> <W,vvvv,L,pp>
//  - R, X, B, W - bits to express corresponding REX prefixes
//  - m-mmmmm (5-bit)
//    0-00001 - implied leading 0F opcode byte
//    0-00010 - implied leading 0F 38 opcode bytes
//    0-00011 - implied leading 0F 3A opcode bytes
//    Rest    - reserved for future use and usage of them will uresult in Undefined instruction exception
//
// - vvvv (4-bits) - register specifier in 1's complement form; must be 1111 if unused
// - L - scalar or AVX-128 bit operations (L=0),  256-bit operations (L=1)
// - pp (2-bits) - opcode extension providing equivalent functionality of a SIMD size prefix
//                 these prefixes are treated mandatory when used with escape opcode 0Fh for
//                 some SIMD instructions
//   00  - None   (0F    - packed float)
//   01  - 66     (66 0F - packed double)
//   10  - F3     (F3 0F - scalar float
//   11  - F2     (F2 0F - scalar double)
#define DEFAULT_3BYTE_VEX_PREFIX 0xC4E07800000000ULL
#define DEFAULT_3BYTE_VEX_PREFIX_MASK 0xFFFFFF00000000ULL
#define LBIT_IN_3BYTE_VEX_PREFIX 0x00000400000000ULL
emitter::code_t emitter::AddVexPrefix(instruction ins, code_t code, emitAttr attr)
{
    // The 2-byte VEX encoding is preferred when possible, but actually emitting
    // it depends on a number of factors that we may not know until much later.
    //
    // In order to handle this "easily", we just carry the 3-byte encoding all
    // the way through and "fix-up" the encoding when the VEX prefix is actually
    // emitted, by simply checking that all the requirements were met.

    // Only AVX instructions require VEX prefix
    assert(IsAVXInstruction(ins));

    // Shouldn't have already added VEX prefix
    assert(!hasVexPrefix(code));

    assert((code & DEFAULT_3BYTE_VEX_PREFIX_MASK) == 0);

    code |= DEFAULT_3BYTE_VEX_PREFIX;

    if (attr == EA_32BYTE)
    {
        // Set L bit to 1 in case of instructions that operate on 256-bits.
        code |= LBIT_IN_3BYTE_VEX_PREFIX;
    }

    return code;
}

// Returns true if this instruction, for the given EA_SIZE(attr), will require a REX.W prefix
bool TakesRexWPrefix(instruction ins, emitAttr attr)
{
    // Because the current implementation of AVX does not have a way to distinguish between the register
    // size specification (128 vs. 256 bits) and the operand size specification (32 vs. 64 bits), where both are
    // required, the instruction must be created with the register size attribute (EA_16BYTE or EA_32BYTE),
    // and here we must special case these by the opcode.
    switch (ins)
    {
        case INS_vpermpd:
        case INS_vpermq:
        case INS_vpsrlvq:
        case INS_vpsllvq:
        case INS_pinsrq:
        case INS_pextrq:
        case INS_vfmadd132pd:
        case INS_vfmadd213pd:
        case INS_vfmadd231pd:
        case INS_vfmadd132sd:
        case INS_vfmadd213sd:
        case INS_vfmadd231sd:
        case INS_vfmaddsub132pd:
        case INS_vfmaddsub213pd:
        case INS_vfmaddsub231pd:
        case INS_vfmsubadd132pd:
        case INS_vfmsubadd213pd:
        case INS_vfmsubadd231pd:
        case INS_vfmsub132pd:
        case INS_vfmsub213pd:
        case INS_vfmsub231pd:
        case INS_vfmsub132sd:
        case INS_vfmsub213sd:
        case INS_vfmsub231sd:
        case INS_vfnmadd132pd:
        case INS_vfnmadd213pd:
        case INS_vfnmadd231pd:
        case INS_vfnmadd132sd:
        case INS_vfnmadd213sd:
        case INS_vfnmadd231sd:
        case INS_vfnmsub132pd:
        case INS_vfnmsub213pd:
        case INS_vfnmsub231pd:
        case INS_vfnmsub132sd:
        case INS_vfnmsub213sd:
        case INS_vfnmsub231sd:
        case INS_vpmaskmovq:
        case INS_vpgatherdq:
        case INS_vpgatherqq:
        case INS_vgatherdpd:
        case INS_vgatherqpd:
            return true;
        default:
            break;
    }

#ifdef TARGET_AMD64
    // movsx should always sign extend out to 8 bytes just because we don't track
    // whether the dest should be 4 bytes or 8 bytes (attr indicates the size
    // of the source, not the dest).
    // A 4-byte movzx is equivalent to an 8 byte movzx, so it is not special
    // cased here.
    //
    // Rex_jmp = jmp with rex prefix always requires rex.w prefix.
    if (ins == INS_movsx || ins == INS_rex_jmp)
    {
        return true;
    }

    if (EA_SIZE(attr) != EA_8BYTE)
    {
        return false;
    }

    if (IsSSEOrAVXInstruction(ins))
    {
        switch (ins)
        {
            case INS_andn:
            case INS_bextr:
            case INS_blsi:
            case INS_blsmsk:
            case INS_blsr:
            case INS_bzhi:
            case INS_cvttsd2si:
            case INS_cvttss2si:
            case INS_cvtsd2si:
            case INS_cvtss2si:
            case INS_cvtsi2sd:
            case INS_cvtsi2ss:
            case INS_mov_xmm2i:
            case INS_mov_i2xmm:
            case INS_movnti:
            case INS_mulx:
            case INS_pdep:
            case INS_pext:
            case INS_rorx:
                return true;
            default:
                return false;
        }
    }

    // TODO-XArch-Cleanup: Better way to not emit REX.W when we don't need it, than just testing all these
    // opcodes...
    // These are all the instructions that default to 8-byte operand without the REX.W bit
    // With 1 special case: movzx because the 4 byte version still zeros-out the hi 4 bytes
    // so we never need it
    if ((ins != INS_push) && (ins != INS_pop) && (ins != INS_movq) && (ins != INS_movzx) && (ins != INS_push_hide) &&
        (ins != INS_pop_hide) && (ins != INS_ret) && (ins != INS_call) && !((ins >= INS_i_jmp) && (ins <= INS_l_jg)))
    {
        return true;
    }
    else
    {
        return false;
    }
#else  //! TARGET_AMD64 = TARGET_X86
    return false;
#endif //! TARGET_AMD64
}

// Returns true if using this register will require a REX.* prefix.
// Since XMM registers overlap with YMM registers, this routine
// can also be used to know whether a YMM register if the
// instruction in question is AVX.
bool IsExtendedReg(regNumber reg)
{
#ifdef TARGET_AMD64
    return ((reg >= REG_R8) && (reg <= REG_R15)) || ((reg >= REG_XMM8) && (reg <= REG_XMM15));
#else
    // X86 JIT operates in 32-bit mode and hence extended reg are not available.
    return false;
#endif
}

// Returns true if using this register, for the given EA_SIZE(attr), will require a REX.* prefix
bool IsExtendedReg(regNumber reg, emitAttr attr)
{
#ifdef TARGET_AMD64
    // Not a register, so doesn't need a prefix
    if (reg > REG_XMM15)
    {
        return false;
    }

    // Opcode field only has 3 bits for the register, these high registers
    // need a 4th bit, that comes from the REX prefix (eiter REX.X, REX.R, or REX.B)
    if (IsExtendedReg(reg))
    {
        return true;
    }

    if (EA_SIZE(attr) != EA_1BYTE)
    {
        return false;
    }

    // There are 12 one byte registers addressible 'below' r8b:
    //     al, cl, dl, bl, ah, ch, dh, bh, spl, bpl, sil, dil.
    // The first 4 are always addressible, the last 8 are divided into 2 sets:
    //     ah,  ch,  dh,  bh
    //          -- or --
    //     spl, bpl, sil, dil
    // Both sets are encoded exactly the same, the difference is the presence
    // of a REX prefix, even a REX prefix with no other bits set (0x40).
    // So in order to get to the second set we need a REX prefix (but no bits).
    //
    // TODO-AMD64-CQ: if we ever want to start using the first set, we'll need a different way of
    // encoding/tracking/encoding registers.
    return (reg >= REG_RSP);
#else
    // X86 JIT operates in 32-bit mode and hence extended reg are not available.
    return false;
#endif
}

// Since XMM registers overlap with YMM registers, this routine
// can also used to know whether a YMM register in case of AVX instructions.
bool IsXMMReg(regNumber reg)
{
#ifdef TARGET_AMD64
    return (reg >= REG_XMM0) && (reg <= REG_XMM15);
#else  // !TARGET_AMD64
    return (reg >= REG_XMM0) && (reg <= REG_XMM7);
#endif // !TARGET_AMD64
}

// Returns bits to be encoded in instruction for the given register.
unsigned RegEncoding(regNumber reg)
{
    static_assert((REG_XMM0 & 0x7) == 0, "bad XMMBASE");
    return (unsigned)(reg & 0x7);
}

// Utility routines that abstract the logic of adding REX.W, REX.R, REX.X, REX.B and REX prefixes
// SSE2: separate 1-byte prefix gets added before opcode.
// AVX:  specific bits within VEX prefix need to be set in bit-inverted form.
emitter::code_t emitter::AddRexWPrefix(instruction ins, code_t code)
{
    if (UseVEXEncoding() && IsAVXInstruction(ins))
    {
        if (TakesVexPrefix(ins))
        {
            // W-bit is available only in 3-byte VEX prefix that starts with byte C4.
            assert(hasVexPrefix(code));

            // W-bit is the only bit that is added in non bit-inverted form.
            return emitter::code_t(code | 0x00008000000000ULL);
        }
    }
#ifdef TARGET_AMD64
    return emitter::code_t(code | 0x4800000000ULL);
#else
    assert(!"UNREACHED");
    return code;
#endif
}

#ifdef TARGET_AMD64

emitter::code_t emitter::AddRexRPrefix(instruction ins, code_t code)
{
    if (UseVEXEncoding() && IsAVXInstruction(ins))
    {
        if (TakesVexPrefix(ins))
        {
            // R-bit is supported by both 2-byte and 3-byte VEX prefix
            assert(hasVexPrefix(code));

            // R-bit is added in bit-inverted form.
            return code & 0xFF7FFFFFFFFFFFULL;
        }
    }

    return code | 0x4400000000ULL;
}

emitter::code_t emitter::AddRexXPrefix(instruction ins, code_t code)
{
    if (UseVEXEncoding() && IsAVXInstruction(ins))
    {
        if (TakesVexPrefix(ins))
        {
            // X-bit is available only in 3-byte VEX prefix that starts with byte C4.
            assert(hasVexPrefix(code));

            // X-bit is added in bit-inverted form.
            return code & 0xFFBFFFFFFFFFFFULL;
        }
    }

    return code | 0x4200000000ULL;
}

emitter::code_t emitter::AddRexBPrefix(instruction ins, code_t code)
{
    if (UseVEXEncoding() && IsAVXInstruction(ins))
    {
        if (TakesVexPrefix(ins))
        {
            // B-bit is available only in 3-byte VEX prefix that starts with byte C4.
            assert(hasVexPrefix(code));

            // B-bit is added in bit-inverted form.
            return code & 0xFFDFFFFFFFFFFFULL;
        }
    }

    return code | 0x4100000000ULL;
}

// Adds REX prefix (0x40) without W, R, X or B bits set
emitter::code_t emitter::AddRexPrefix(instruction ins, code_t code)
{
    assert(!UseVEXEncoding() || !IsAVXInstruction(ins));
    return code | 0x4000000000ULL;
}

#endif // TARGET_AMD64

bool isPrefix(BYTE b)
{
    assert(b != 0);    // Caller should check this
    assert(b != 0x67); // We don't use the address size prefix
    assert(b != 0x65); // The GS segment override prefix is emitted separately
    assert(b != 0x64); // The FS segment override prefix is emitted separately
    assert(b != 0xF0); // The lock prefix is emitted separately
    assert(b != 0x2E); // We don't use the CS segment override prefix
    assert(b != 0x3E); // Or the DS segment override prefix
    assert(b != 0x26); // Or the ES segment override prefix
    assert(b != 0x36); // Or the SS segment override prefix

    // That just leaves the size prefixes used in SSE opcodes:
    //      Scalar Double  Scalar Single  Packed Double
    return ((b == 0xF2) || (b == 0xF3) || (b == 0x66));
}

// Outputs VEX prefix (in case of AVX instructions) and REX.R/X/W/B otherwise.
unsigned emitter::emitOutputRexOrVexPrefixIfNeeded(instruction ins, BYTE* dst, code_t& code)
{
    abort();
}

#ifdef TARGET_AMD64
/*****************************************************************************
 * Is the last instruction emitted a call instruction?
 */
bool emitter::emitIsLastInsCall()
{
    if ((emitLastIns != nullptr) && (emitLastIns->idIns() == INS_call))
    {
        return true;
    }

    return false;
}

/*****************************************************************************
 * We're about to create an epilog. If the last instruction we output was a 'call',
 * then we need to insert a NOP, to allow for proper exception-handling behavior.
 */
void emitter::emitOutputPreEpilogNOP()
{
    if (emitIsLastInsCall())
    {
        emitIns(INS_nop);
    }
}

#endif // TARGET_AMD64

// Size of rex prefix in bytes
unsigned emitter::emitGetRexPrefixSize(instruction ins)
{
    // In case of AVX instructions, REX prefixes are part of VEX prefix.
    // And hence requires no additional byte to encode REX prefixes.
    if (IsAVXInstruction(ins))
    {
        return 0;
    }

    // If not AVX, then we would need 1-byte to encode REX prefix.
    return 1;
}

// Size of vex prefix in bytes
unsigned emitter::emitGetVexPrefixSize(instruction ins, emitAttr attr)
{
    if (IsAVXInstruction(ins))
    {
        return 3;
    }

    // If not AVX, then we don't need to encode vex prefix.
    return 0;
}

//------------------------------------------------------------------------
// emitGetAdjustedSize: Determines any size adjustment needed for a given instruction based on the current
// configuration.
//
// Arguments:
//    ins   -- The instruction being emitted
//    attr  -- The emit attribute
//    code  -- The current opcode and any known prefixes
unsigned emitter::emitGetAdjustedSize(instruction ins, emitAttr attr, code_t code)
{
    unsigned adjustedSize = 0;

    if (IsAVXInstruction(ins))
    {
        // VEX prefix encodes some bytes of the opcode and as a result, overall size of the instruction reduces.
        // Therefore, to estimate the size adding VEX prefix size and size of instruction opcode bytes will always
        // overstimate.
        // Instead this routine will adjust the size of VEX prefix based on the number of bytes of opcode it encodes so
        // that
        // instruction size estimate will be accurate.
        // Basically this  will decrease the vexPrefixSize, so that opcodeSize + vexPrefixAdjustedSize will be the right
        // size.
        //
        // rightOpcodeSize + vexPrefixSize
        //  = (opcodeSize - ExtrabytesSize) + vexPrefixSize
        //  = opcodeSize + (vexPrefixSize - ExtrabytesSize)
        //  = opcodeSize + vexPrefixAdjustedSize

        unsigned vexPrefixAdjustedSize = emitGetVexPrefixSize(ins, attr);
        assert(vexPrefixAdjustedSize == 3);

        // In this case, opcode will contains escape prefix at least one byte,
        // vexPrefixAdjustedSize should be minus one.
        vexPrefixAdjustedSize -= 1;

        // Get the fourth byte in Opcode.
        // If this byte is non-zero, then we should check whether the opcode contains SIMD prefix or not.
        BYTE check = (code >> 24) & 0xFF;
        if (check != 0)
        {
            // 3-byte opcode: with the bytes ordered as 0x2211RM33 or
            // 4-byte opcode: with the bytes ordered as 0x22114433
            // Simd prefix is at the first byte.
            BYTE sizePrefix = (code >> 16) & 0xFF;
            if (sizePrefix != 0 && isPrefix(sizePrefix))
            {
                vexPrefixAdjustedSize -= 1;
            }

            // If the opcode size is 4 bytes, then the second escape prefix is at fourth byte in opcode.
            // But in this case the opcode has not counted R\M part.
            // opcodeSize + VexPrefixAdjustedSize - ExtraEscapePrefixSize + ModR\MSize
            //=opcodeSize + VexPrefixAdjustedSize -1 + 1
            //=opcodeSize + VexPrefixAdjustedSize
            // So although we may have second byte escape prefix, we won't decrease vexPrefixAdjustedSize.
        }

        adjustedSize = vexPrefixAdjustedSize;
    }
    else if (Is4ByteSSEInstruction(ins))
    {
        // The 4-Byte SSE instructions require one additional byte to hold the ModRM byte
        adjustedSize++;
    }
    else
    {
        if (ins == INS_crc32)
        {
            // Adjust code size for CRC32 that has 4-byte opcode but does not use SSE38 or EES3A encoding.
            adjustedSize++;
        }

        if ((attr == EA_2BYTE) && (ins != INS_movzx) && (ins != INS_movsx))
        {
            // Most 16-bit operand instructions will need a 0x66 prefix.
            adjustedSize++;
        }
    }

    return adjustedSize;
}

// Get size of rex or vex prefix emitted in code
unsigned emitter::emitGetPrefixSize(code_t code)
{
    if (hasVexPrefix(code))
    {
        return 3;
    }

    if (hasRexPrefix(code))
    {
        return 1;
    }

    return 0;
}

#ifdef TARGET_X86
/*****************************************************************************
 *
 *  Record a non-empty stack
 */

void emitter::emitMarkStackLvl(unsigned stackLevel)
{
    assert(int(stackLevel) >= 0);
    assert(emitCurStackLvl == 0);
    assert(emitCurIG->igStkLvl == 0);
    assert(emitCurIGfreeNext == emitCurIGfreeBase);

    assert(stackLevel && stackLevel % sizeof(int) == 0);

    emitCurStackLvl = emitCurIG->igStkLvl = stackLevel;

    if (emitMaxStackDepth < emitCurStackLvl)
    {
        JITDUMP("Upping emitMaxStackDepth from %d to %d\n", emitMaxStackDepth, emitCurStackLvl);
        emitMaxStackDepth = emitCurStackLvl;
    }
}
#endif

/*****************************************************************************
 *
 *  Get hold of the address mode displacement value for an indirect call.
 */

//inline ssize_t emitter::emitGetInsCIdisp(instrDesc* id)
//{
//    if (id->idIsLargeCall())
//    {
//        return ((instrDescCGCA*)id)->idcDisp;
//    }
//    else
//    {
//        assert(!id->idIsLargeDsp());
//        assert(!id->idIsLargeCns());
//
//        return id->idAddr()->iiaAddrMode.amDisp;
//    }
//}

/** ***************************************************************************
 *
 *  The following table is used by the instIsFP()/instUse/DefFlags() helpers.
 */

// clang-format off
const insFlags      CodeGenInterface::instInfo[] =
{
    #define INST0(id, nm, um, mr,                 flags) static_cast<insFlags>(flags),
    #define INST1(id, nm, um, mr,                 flags) static_cast<insFlags>(flags),
    #define INST2(id, nm, um, mr, mi,             flags) static_cast<insFlags>(flags),
    #define INST3(id, nm, um, mr, mi, rm,         flags) static_cast<insFlags>(flags),
    #define INST4(id, nm, um, mr, mi, rm, a4,     flags) static_cast<insFlags>(flags),
    #define INST5(id, nm, um, mr, mi, rm, a4, rr, flags) static_cast<insFlags>(flags),
    #include "instrs.h"
    #undef  INST0
    #undef  INST1
    #undef  INST2
    #undef  INST3
    #undef  INST4
    #undef  INST5
};
// clang-format on

/*****************************************************************************
 *
 *  Initialize the table used by emitInsModeFormat().
 */

// clang-format off
const BYTE          emitter::emitInsModeFmtTab[] =
{
    #define INST0(id, nm, um, mr,                 flags) um,
    #define INST1(id, nm, um, mr,                 flags) um,
    #define INST2(id, nm, um, mr, mi,             flags) um,
    #define INST3(id, nm, um, mr, mi, rm,         flags) um,
    #define INST4(id, nm, um, mr, mi, rm, a4,     flags) um,
    #define INST5(id, nm, um, mr, mi, rm, a4, rr, flags) um,
    #include "instrs.h"
    #undef  INST0
    #undef  INST1
    #undef  INST2
    #undef  INST3
    #undef  INST4
    #undef  INST5
};
// clang-format on

#ifdef DEBUG
unsigned const emitter::emitInsModeFmtCnt = _countof(emitInsModeFmtTab);
#endif

/*****************************************************************************
 *
 *  Combine the given base format with the update mode of the instuction.
 */

inline emitter::insFormat emitter::emitInsModeFormat(instruction ins, insFormat base)
{
    assert(IF_RRD + IUM_RD == IF_RRD);
    assert(IF_RRD + IUM_WR == IF_RWR);
    assert(IF_RRD + IUM_RW == IF_RRW);

    return (insFormat)(base + emitInsUpdateMode(ins));
}

// This is a helper we need due to Vs Whidbey #254016 in order to distinguish
// if we can not possibly be updating an integer register. This is not the best
// solution, but the other ones (see bug) are going to be much more complicated.
bool emitter::emitInsCanOnlyWriteSSE2OrAVXReg(instrDesc* id)
{
    instruction ins = id->idIns();

    if (!IsSSEOrAVXInstruction(ins))
    {
        return false;
    }

    switch (ins)
    {
        case INS_andn:
        case INS_bextr:
        case INS_blsi:
        case INS_blsmsk:
        case INS_blsr:
        case INS_bzhi:
        case INS_cvttsd2si:
        case INS_cvttss2si:
        case INS_cvtsd2si:
        case INS_cvtss2si:
        case INS_extractps:
        case INS_mov_xmm2i:
        case INS_movmskpd:
        case INS_movmskps:
        case INS_mulx:
        case INS_pdep:
        case INS_pext:
        case INS_pmovmskb:
        case INS_pextrb:
        case INS_pextrd:
        case INS_pextrq:
        case INS_pextrw:
        case INS_pextrw_sse41:
        case INS_rorx:
        {
            // These SSE instructions write to a general purpose integer register.
            return false;
        }

        default:
        {
            return true;
        }
    }
}

/*****************************************************************************
 *
 *  Returns the base encoding of the given CPU instruction.
 */

inline size_t insCode(instruction ins)
{
    // clang-format off
    const static
    size_t          insCodes[] =
    {
        #define INST0(id, nm, um, mr,                 flags) mr,
        #define INST1(id, nm, um, mr,                 flags) mr,
        #define INST2(id, nm, um, mr, mi,             flags) mr,
        #define INST3(id, nm, um, mr, mi, rm,         flags) mr,
        #define INST4(id, nm, um, mr, mi, rm, a4,     flags) mr,
        #define INST5(id, nm, um, mr, mi, rm, a4, rr, flags) mr,
        #include "instrs.h"
        #undef  INST0
        #undef  INST1
        #undef  INST2
        #undef  INST3
        #undef  INST4
        #undef  INST5
    };
    // clang-format on

    assert((unsigned)ins < _countof(insCodes));
    assert((insCodes[ins] != BAD_CODE));

    return insCodes[ins];
}

/*****************************************************************************
 *
 *  Returns the "AL/AX/EAX, imm" accumulator encoding of the given instruction.
 */

inline size_t insCodeACC(instruction ins)
{
    // clang-format off
    const static
    size_t          insCodesACC[] =
    {
        #define INST0(id, nm, um, mr,                 flags)
        #define INST1(id, nm, um, mr,                 flags)
        #define INST2(id, nm, um, mr, mi,             flags)
        #define INST3(id, nm, um, mr, mi, rm,         flags)
        #define INST4(id, nm, um, mr, mi, rm, a4,     flags) a4,
        #define INST5(id, nm, um, mr, mi, rm, a4, rr, flags) a4,
        #include "instrs.h"
        #undef  INST0
        #undef  INST1
        #undef  INST2
        #undef  INST3
        #undef  INST4
        #undef  INST5
    };
    // clang-format on

    assert((unsigned)ins < _countof(insCodesACC));
    assert((insCodesACC[ins] != BAD_CODE));

    return insCodesACC[ins];
}

/*****************************************************************************
 *
 *  Returns the "register" encoding of the given CPU instruction.
 */

inline size_t insCodeRR(instruction ins)
{
    // clang-format off
    const static
    size_t          insCodesRR[] =
    {
        #define INST0(id, nm, um, mr,                 flags)
        #define INST1(id, nm, um, mr,                 flags)
        #define INST2(id, nm, um, mr, mi,             flags)
        #define INST3(id, nm, um, mr, mi, rm,         flags)
        #define INST4(id, nm, um, mr, mi, rm, a4,     flags)
        #define INST5(id, nm, um, mr, mi, rm, a4, rr, flags) rr,
        #include "instrs.h"
        #undef  INST0
        #undef  INST1
        #undef  INST2
        #undef  INST3
        #undef  INST4
        #undef  INST5
    };
    // clang-format on

    assert((unsigned)ins < _countof(insCodesRR));
    assert((insCodesRR[ins] != BAD_CODE));

    return insCodesRR[ins];
}

// clang-format off
const static
size_t          insCodesRM[] =
{
    #define INST0(id, nm, um, mr,                 flags)
    #define INST1(id, nm, um, mr,                 flags)
    #define INST2(id, nm, um, mr, mi,             flags)
    #define INST3(id, nm, um, mr, mi, rm,         flags) rm,
    #define INST4(id, nm, um, mr, mi, rm, a4,     flags) rm,
    #define INST5(id, nm, um, mr, mi, rm, a4, rr, flags) rm,
    #include "instrs.h"
    #undef  INST0
    #undef  INST1
    #undef  INST2
    #undef  INST3
    #undef  INST4
    #undef  INST5
};
// clang-format on

// Returns true iff the give CPU instruction has an RM encoding.
inline bool hasCodeRM(instruction ins)
{
    assert((unsigned)ins < _countof(insCodesRM));
    return ((insCodesRM[ins] != BAD_CODE));
}

/*****************************************************************************
 *
 *  Returns the "reg, [r/m]" encoding of the given CPU instruction.
 */

inline size_t insCodeRM(instruction ins)
{
    assert((unsigned)ins < _countof(insCodesRM));
    assert((insCodesRM[ins] != BAD_CODE));

    return insCodesRM[ins];
}

// clang-format off
const static
size_t          insCodesMI[] =
{
    #define INST0(id, nm, um, mr,                 flags)
    #define INST1(id, nm, um, mr,                 flags)
    #define INST2(id, nm, um, mr, mi,             flags) mi,
    #define INST3(id, nm, um, mr, mi, rm,         flags) mi,
    #define INST4(id, nm, um, mr, mi, rm, a4,     flags) mi,
    #define INST5(id, nm, um, mr, mi, rm, a4, rr, flags) mi,
    #include "instrs.h"
    #undef  INST0
    #undef  INST1
    #undef  INST2
    #undef  INST3
    #undef  INST4
    #undef  INST5
};
// clang-format on

// Returns true iff the give CPU instruction has an MI encoding.
inline bool hasCodeMI(instruction ins)
{
    assert((unsigned)ins < _countof(insCodesMI));
    return ((insCodesMI[ins] != BAD_CODE));
}

/*****************************************************************************
 *
 *  Returns the "[r/m], 32-bit icon" encoding of the given CPU instruction.
 */

inline size_t insCodeMI(instruction ins)
{
    assert((unsigned)ins < _countof(insCodesMI));
    assert((insCodesMI[ins] != BAD_CODE));

    return insCodesMI[ins];
}

// clang-format off
const static
size_t          insCodesMR[] =
{
    #define INST0(id, nm, um, mr,                 flags)
    #define INST1(id, nm, um, mr,                 flags) mr,
    #define INST2(id, nm, um, mr, mi,             flags) mr,
    #define INST3(id, nm, um, mr, mi, rm,         flags) mr,
    #define INST4(id, nm, um, mr, mi, rm, a4,     flags) mr,
    #define INST5(id, nm, um, mr, mi, rm, a4, rr, flags) mr,
    #include "instrs.h"
    #undef  INST0
    #undef  INST1
    #undef  INST2
    #undef  INST3
    #undef  INST4
    #undef  INST5
};
// clang-format on

// Returns true iff the give CPU instruction has an MR encoding.
inline bool hasCodeMR(instruction ins)
{
    assert((unsigned)ins < _countof(insCodesMR));
    return ((insCodesMR[ins] != BAD_CODE));
}

/*****************************************************************************
 *
 *  Returns the "[r/m], reg" or "[r/m]" encoding of the given CPU instruction.
 */

inline size_t insCodeMR(instruction ins)
{
    assert((unsigned)ins < _countof(insCodesMR));
    assert((insCodesMR[ins] != BAD_CODE));

    return insCodesMR[ins];
}

// Return true if the instruction uses the SSE38 or SSE3A macro in instrsXArch.h.
bool emitter::EncodedBySSE38orSSE3A(instruction ins)
{
    const size_t SSE38 = 0x0F660038;
    const size_t SSE3A = 0x0F66003A;
    const size_t MASK  = 0xFFFF00FF;

    size_t insCode = 0;

    if (!IsSSEOrAVXInstruction(ins))
    {
        return false;
    }

    if (hasCodeRM(ins))
    {
        insCode = insCodeRM(ins);
    }
    else if (hasCodeMI(ins))
    {
        insCode = insCodeMI(ins);
    }
    else if (hasCodeMR(ins))
    {
        insCode = insCodeMR(ins);
    }

    insCode &= MASK;
    return insCode == SSE38 || insCode == SSE3A;
}

/*****************************************************************************
 *
 *  Returns an encoding for the specified register to be used in the bit0-2
 *  part of an opcode.
 */

inline unsigned emitter::insEncodeReg012(instruction ins, regNumber reg, emitAttr size, code_t* code)
{
    assert(reg < REG_STK);

#ifdef TARGET_AMD64
    // Either code is not NULL or reg is not an extended reg.
    // If reg is an extended reg, instruction needs to be prefixed with 'REX'
    // which would require code != NULL.
    assert(code != nullptr || !IsExtendedReg(reg));

    if (IsExtendedReg(reg))
    {
        *code = AddRexBPrefix(ins, *code); // REX.B
    }
    else if ((EA_SIZE(size) == EA_1BYTE) && (reg > REG_RBX) && (code != nullptr))
    {
        // We are assuming that we only use/encode SPL, BPL, SIL and DIL
        // not the corresponding AH, CH, DH, or BH
        *code = AddRexPrefix(ins, *code); // REX
    }
#endif // TARGET_AMD64

    unsigned regBits = RegEncoding(reg);

    assert(regBits < 8);
    return regBits;
}

/*****************************************************************************
 *
 *  Returns an encoding for the specified register to be used in the bit3-5
 *  part of an opcode.
 */

inline unsigned emitter::insEncodeReg345(instruction ins, regNumber reg, emitAttr size, code_t* code)
{
    assert(reg < REG_STK);

#ifdef TARGET_AMD64
    // Either code is not NULL or reg is not an extended reg.
    // If reg is an extended reg, instruction needs to be prefixed with 'REX'
    // which would require code != NULL.
    assert(code != nullptr || !IsExtendedReg(reg));

    if (IsExtendedReg(reg))
    {
        *code = AddRexRPrefix(ins, *code); // REX.R
    }
    else if ((EA_SIZE(size) == EA_1BYTE) && (reg > REG_RBX) && (code != nullptr))
    {
        // We are assuming that we only use/encode SPL, BPL, SIL and DIL
        // not the corresponding AH, CH, DH, or BH
        *code = AddRexPrefix(ins, *code); // REX
    }
#endif // TARGET_AMD64

    unsigned regBits = RegEncoding(reg);

    assert(regBits < 8);
    return (regBits << 3);
}

/***********************************************************************************
 *
 *  Returns modified AVX opcode with the specified register encoded in bits 3-6 of
 *  byte 2 of VEX prefix.
 */
inline emitter::code_t emitter::insEncodeReg3456(instruction ins, regNumber reg, emitAttr size, code_t code)
{
    assert(reg < REG_STK);
    assert(IsAVXInstruction(ins));
    assert(hasVexPrefix(code));

    // Get 4-bit register encoding
    // RegEncoding() gives lower 3 bits
    // IsExtendedReg() gives MSB.
    code_t regBits = RegEncoding(reg);
    if (IsExtendedReg(reg))
    {
        regBits |= 0x08;
    }

    // VEX prefix encodes register operand in 1's complement form
    // Shift count = 4-bytes of opcode + 0-2 bits
    assert(regBits <= 0xF);
    regBits <<= 35;
    return code ^ regBits;
}

/*****************************************************************************
 *
 *  Returns an encoding for the specified register to be used in the bit3-5
 *  part of an SIB byte (unshifted).
 *  Used exclusively to generate the REX.X bit and truncate the register.
 */

inline unsigned emitter::insEncodeRegSIB(instruction ins, regNumber reg, code_t* code)
{
    assert(reg < REG_STK);

#ifdef TARGET_AMD64
    // Either code is not NULL or reg is not an extended reg.
    // If reg is an extended reg, instruction needs to be prefixed with 'REX'
    // which would require code != NULL.
    assert(code != nullptr || reg < REG_R8 || (reg >= REG_XMM0 && reg < REG_XMM8));

    if (IsExtendedReg(reg))
    {
        *code = AddRexXPrefix(ins, *code); // REX.X
    }
    unsigned regBits = RegEncoding(reg);
#else  // !TARGET_AMD64
    unsigned regBits = reg;
#endif // !TARGET_AMD64

    assert(regBits < 8);
    return regBits;
}

/*****************************************************************************
 *
 *  Returns the "[r/m]" opcode with the mod/RM field set to register.
 */

inline emitter::code_t emitter::insEncodeMRreg(instruction ins, code_t code)
{
    // If Byte 4 (which is 0xFF00) is 0, that's where the RM encoding goes.
    // Otherwise, it will be placed after the 4 byte encoding.
    if ((code & 0xFF00) == 0)
    {
        assert((code & 0xC000) == 0);
        code |= 0xC000;
    }

    return code;
}

/*****************************************************************************
 *
 *  Returns the given "[r/m]" opcode with the mod/RM field set to register.
 */

inline emitter::code_t emitter::insEncodeRMreg(instruction ins, code_t code)
{
    // If Byte 4 (which is 0xFF00) is 0, that's where the RM encoding goes.
    // Otherwise, it will be placed after the 4 byte encoding.
    if ((code & 0xFF00) == 0)
    {
        assert((code & 0xC000) == 0);
        code |= 0xC000;
    }
    return code;
}

/*****************************************************************************
 *
 *  Returns the "byte ptr [r/m]" opcode with the mod/RM field set to
 *  the given register.
 */

inline emitter::code_t emitter::insEncodeMRreg(instruction ins, regNumber reg, emitAttr size, code_t code)
{
    assert((code & 0xC000) == 0);
    code |= 0xC000;
    unsigned regcode = insEncodeReg012(ins, reg, size, &code) << 8;
    code |= regcode;
    return code;
}

/*****************************************************************************
 *
 *  Returns the "byte ptr [r/m], icon" opcode with the mod/RM field set to
 *  the given register.
 */

inline emitter::code_t emitter::insEncodeMIreg(instruction ins, regNumber reg, emitAttr size, code_t code)
{
    assert((code & 0xC000) == 0);
    code |= 0xC000;
    unsigned regcode = insEncodeReg012(ins, reg, size, &code) << 8;
    code |= regcode;
    return code;
}

/*****************************************************************************
 *
 *  Returns true iff the given instruction does not have a "[r/m], icon" form, but *does* have a
 *  "reg,reg,imm8" form.
 */
inline bool insNeedsRRIb(instruction ins)
{
    // If this list gets longer, use a switch or a table.
    return ins == INS_imul;
}

/*****************************************************************************
 *
 *  Returns the "reg,reg,imm8" opcode with both the reg's set to the
 *  the given register.
 */
inline emitter::code_t emitter::insEncodeRRIb(instruction ins, regNumber reg, emitAttr size)
{
    assert(size == EA_4BYTE); // All we handle for now.
    assert(insNeedsRRIb(ins));
    // If this list gets longer, use a switch, or a table lookup.
    code_t   code    = 0x69c0;
    unsigned regcode = insEncodeReg012(ins, reg, size, &code);
    // We use the same register as source and destination.  (Could have another version that does both regs...)
    code |= regcode;
    code |= (regcode << 3);
    return code;
}

/*****************************************************************************
 *
 *  Returns the "+reg" opcode with the the given register set into the low
 *  nibble of the opcode
 */

inline emitter::code_t emitter::insEncodeOpreg(instruction ins, regNumber reg, emitAttr size)
{
    code_t   code    = insCodeRR(ins);
    unsigned regcode = insEncodeReg012(ins, reg, size, &code);
    code |= regcode;
    return code;
}

/*****************************************************************************
 *
 *  Return the 'SS' field value for the given index scale factor.
 */

inline unsigned emitter::insSSval(unsigned scale)
{
    assert(scale == 1 || scale == 2 || scale == 4 || scale == 8);

    const static BYTE scales[] = {
        0x00, // 1
        0x40, // 2
        0xFF, // 3
        0x80, // 4
        0xFF, // 5
        0xFF, // 6
        0xFF, // 7
        0xC0, // 8
    };

    return scales[scale - 1];
}

const instruction emitJumpKindInstructions[] = {INS_nop,

#define JMP_SMALL(en, rev, ins) INS_##ins,
#include "emitjmps.h"

                                                INS_call};

const emitJumpKind emitReverseJumpKinds[] = {
    EJ_NONE,

#define JMP_SMALL(en, rev, ins) EJ_##rev,
#include "emitjmps.h"
};

/*****************************************************************************
 * Look up the instruction for a jump kind
 */

/*static*/ instruction emitter::emitJumpKindToIns(emitJumpKind jumpKind)
{
    assert((unsigned)jumpKind < ArrLen(emitJumpKindInstructions));
    return emitJumpKindInstructions[jumpKind];
}

/*****************************************************************************
 * Reverse the conditional jump
 */

/* static */ emitJumpKind emitter::emitReverseJumpKind(emitJumpKind jumpKind)
{
    assert(jumpKind < EJ_COUNT);
    return emitReverseJumpKinds[jumpKind];
}

/*****************************************************************************
 * The size for these instructions is less than EA_4BYTE,
 * but the target register need not be byte-addressable
 */

inline bool emitInstHasNoCode(instruction ins)
{
    if (ins == INS_align)
    {
        return true;
    }

    return false;
}

/*****************************************************************************
 * When encoding instructions that operate on byte registers
 * we have to ensure that we use a low register (EAX, EBX, ECX or EDX)
 * otherwise we will incorrectly encode the instruction
 */

bool emitter::emitVerifyEncodable(instruction ins, emitAttr size, regNumber reg1, regNumber reg2 /* = REG_NA */)
{
#if CPU_HAS_BYTE_REGS
    if (size != EA_1BYTE) // Not operating on a byte register is fine
    {
        return true;
    }

    if ((ins != INS_movsx) && // These three instructions support high register
        (ins != INS_movzx)    // encodings for reg1
#ifdef FEATURE_HW_INTRINSICS
        && (ins != INS_crc32)
#endif
            )
    {
        // reg1 must be a byte-able register
        if ((genRegMask(reg1) & RBM_BYTE_REGS) == 0)
        {
            return false;
        }
    }
    // if reg2 is not REG_NA then reg2 must be a byte-able register
    if ((reg2 != REG_NA) && ((genRegMask(reg2) & RBM_BYTE_REGS) == 0))
    {
        return false;
    }
#endif
    // The instruction can be encoded
    return true;
}

/*****************************************************************************
 *
 *  Estimate the size (in bytes of generated code) of the given instruction.
 */

inline UNATIVE_OFFSET emitter::emitInsSize(code_t code)
{
    UNATIVE_OFFSET size = (code & 0xFF000000) ? 4 : (code & 0x00FF0000) ? 3 : 2;
#ifdef TARGET_AMD64
    size += emitGetPrefixSize(code);
#endif
    return size;
}

//------------------------------------------------------------------------
// emitInsSizeRR: Determines the code size for an instruction encoding that does not have any addressing modes
//
// Arguments:
//    ins   -- The instruction being emitted
//    code  -- The current opcode and any known prefixes
inline UNATIVE_OFFSET emitter::emitInsSizeRR(instrDesc* id, code_t code)
{
    assert(false);
    //assert(id->idIns() != INS_invalid);

    //instruction ins  = id->idIns();
    //emitAttr    attr = id->idOpSize();

    //UNATIVE_OFFSET sz = emitInsSize(code);

    //sz += emitGetAdjustedSize(ins, attr, code);

    //// REX prefix
    //if (TakesRexWPrefix(ins, attr) || IsExtendedReg(id->idReg1(), attr) || IsExtendedReg(id->idReg2(), attr) ||
    //    (!id->idIsSmallDsc() && (IsExtendedReg(id->idReg3(), attr) || IsExtendedReg(id->idReg4(), attr))))
    //{
    //    sz += emitGetRexPrefixSize(ins);
    //}

    //return sz;
    return 0;
}

//------------------------------------------------------------------------
// emitInsSizeRR: Determines the code size for an instruction encoding that does not have any addressing modes and
// includes an immediate value
//
// Arguments:
//    ins   -- The instruction being emitted
//    code  -- The current opcode and any known prefixes
//    val   -- The immediate value to encode
inline UNATIVE_OFFSET emitter::emitInsSizeRR(instrDesc* id, code_t code, int val)
{
    instruction    ins       = id->idIns();
    UNATIVE_OFFSET valSize   = EA_SIZE_IN_BYTES(id->idOpSize());
    bool           valInByte = ((signed char)val == val) && (ins != INS_mov) && (ins != INS_test);

#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(valSize <= sizeof(INT32) || !id->idIsCnsReloc());
#endif // TARGET_AMD64

    if (valSize > sizeof(INT32))
    {
        valSize = sizeof(INT32);
    }

    if (id->idIsCnsReloc())
    {
        valInByte = false; // relocs can't be placed in a byte
        assert(valSize == sizeof(INT32));
    }

    if (valInByte)
    {
        valSize = sizeof(char);
    }
    else
    {
        assert(!IsSSEOrAVXInstruction(ins));
    }

    return valSize + emitInsSizeRR(id, code);
}

inline UNATIVE_OFFSET emitter::emitInsSizeRR(instruction ins, regNumber reg1, regNumber reg2, emitAttr attr)
{
    emitAttr size = EA_SIZE(attr);

    UNATIVE_OFFSET sz;

    // If Byte 4 (which is 0xFF00) is zero, that's where the RM encoding goes.
    // Otherwise, it will be placed after the 4 byte encoding, making the total 5 bytes.
    // This would probably be better expressed as a different format or something?
    code_t code = insCodeRM(ins);

    if ((code & 0xFF00) != 0)
    {
        sz = IsSSEOrAVXInstruction(ins) ? emitInsSize(code) : 5;
    }
    else
    {
        sz = emitInsSize(insEncodeRMreg(ins, code));
    }

    sz += emitGetAdjustedSize(ins, size, insCodeRM(ins));

    // REX prefix
    if (!hasRexPrefix(code))
    {
        if ((TakesRexWPrefix(ins, size) && ((ins != INS_xor) || (reg1 != reg2))) || IsExtendedReg(reg1, attr) ||
            IsExtendedReg(reg2, attr))
        {
            sz += emitGetRexPrefixSize(ins);
        }
    }

    return sz;
}

/*****************************************************************************/

inline UNATIVE_OFFSET emitter::emitInsSizeSV(code_t code, int var, int dsp)
{
    UNATIVE_OFFSET size = emitInsSize(code);
    UNATIVE_OFFSET offs;
    bool           offsIsUpperBound = true;
    bool           EBPbased         = true;

    /*  Is this a temporary? */

    if (var < 0)
    {
        /* An address off of ESP takes an extra byte */

        if (!emitHasFramePtr)
        {
            size++;
        }

        // The offset is already assigned. Find the temp.
        TempDsc* tmp = codeGen->regSet.tmpFindNum(var, RegSet::TEMP_USAGE_USED);
        if (tmp == nullptr)
        {
            // It might be in the free lists, if we're working on zero initializing the temps.
            tmp = codeGen->regSet.tmpFindNum(var, RegSet::TEMP_USAGE_FREE);
        }
        assert(tmp != nullptr);
        offs = tmp->tdTempOffs();

        // We only care about the magnitude of the offset here, to determine instruction size.
        if (emitComp->isFramePointerUsed())
        {
            if ((int)offs < 0)
            {
                offs = -(int)offs;
            }
        }
        else
        {
            // SP-based offsets must already be positive.
            assert((int)offs >= 0);
        }
    }
    else
    {

        /* Get the frame offset of the (non-temp) variable */

        offs = dsp + emitComp->lvaFrameAddress(var, &EBPbased);

        /* An address off of ESP takes an extra byte */

        if (!EBPbased)
        {
            ++size;
        }

        /* Is this a stack parameter reference? */

        if ((emitComp->lvaIsParameter(var)
#if !defined(TARGET_AMD64) || defined(UNIX_AMD64_ABI)
             && !emitComp->lvaIsRegArgument(var)
#endif // !TARGET_AMD64 || UNIX_AMD64_ABI
                 ) ||
            (static_cast<unsigned>(var) == emitComp->lvaRetAddrVar))
        {
            /* If no EBP frame, arguments and ret addr are off of ESP, above temps */

            if (!EBPbased)
            {
                assert((int)offs >= 0);

                offsIsUpperBound = false; // since #temps can increase
                offs += emitMaxTmpSize;
            }
        }
        else
        {
            /* Locals off of EBP are at negative offsets */

            if (EBPbased)
            {
#if defined(TARGET_AMD64) && !defined(UNIX_AMD64_ABI)
                // If localloc is not used, then ebp chaining is done and hence
                // offset of locals will be at negative offsets, Otherwise offsets
                // will be positive.  In future, when RBP gets positioned in the
                // middle of the frame so as to optimize instruction encoding size,
                // the below asserts needs to be modified appropriately.
                // However, for Unix platforms, we always do frame pointer chaining,
                // so offsets from the frame pointer will always be negative.
                if (emitComp->compLocallocUsed || emitComp->opts.compDbgEnC)
                {
                    noway_assert((int)offs >= 0);
                }
                else
#endif
                {
                    // Dev10 804810 - failing this assert can lead to bad codegen and runtime crashes
                    CLANG_FORMAT_COMMENT_ANCHOR;

#ifdef UNIX_AMD64_ABI
                    LclVarDsc* varDsc         = emitComp->lvaTable + var;
                    bool       isRegPassedArg = varDsc->lvIsParam && varDsc->lvIsRegArg;
                    // Register passed args could have a stack offset of 0.
                    noway_assert((int)offs < 0 || isRegPassedArg || emitComp->opts.IsOSR());
#else  // !UNIX_AMD64_ABI

                    // OSR transitioning to RBP frame currently can have mid-frame FP
                    noway_assert(((int)offs < 0) || emitComp->opts.IsOSR());
#endif // !UNIX_AMD64_ABI
                }

                assert(emitComp->lvaTempsHaveLargerOffsetThanVars());

                // lvaInlinedPInvokeFrameVar and lvaStubArgumentVar are placed below the temps
                if (unsigned(var) == emitComp->lvaInlinedPInvokeFrameVar ||
                    unsigned(var) == emitComp->lvaStubArgumentVar)
                {
                    offs -= emitMaxTmpSize;
                }

                if ((int)offs < 0)
                {
                    // offset is negative
                    return size + ((int(offs) >= SCHAR_MIN) ? sizeof(char) : sizeof(int));
                }
#ifdef TARGET_AMD64
                // This case arises for localloc frames
                else
                {
                    return size + ((offs <= SCHAR_MAX) ? sizeof(char) : sizeof(int));
                }
#endif
            }

            if (emitComp->lvaTempsHaveLargerOffsetThanVars() == false)
            {
                offs += emitMaxTmpSize;
            }
        }
    }

    assert((int)offs >= 0);

#if !FEATURE_FIXED_OUT_ARGS

    /* Are we addressing off of ESP? */

    if (!emitHasFramePtr)
    {
        /* Adjust the effective offset if necessary */

        if (emitCntStackDepth)
            offs += emitCurStackLvl;

        // we could (and used to) check for the special case [sp] here but the stack offset
        // estimator was off, and there is very little harm in overestimating for such a
        // rare case.
    }

#endif // !FEATURE_FIXED_OUT_ARGS

//  printf("lcl = %04X, tmp = %04X, stk = %04X, offs = %04X\n",
//         emitLclSize, emitMaxTmpSize, emitCurStackLvl, offs);

#ifdef TARGET_AMD64
    bool useSmallEncoding = (SCHAR_MIN <= (int)offs) && ((int)offs <= SCHAR_MAX);
#else
    bool useSmallEncoding = (offs <= size_t(SCHAR_MAX));
#endif

    // If it is ESP based, and the offset is zero, we will not encode the disp part.
    if (!EBPbased && offs == 0)
    {
        return size;
    }
    else
    {
        return size + (useSmallEncoding ? sizeof(char) : sizeof(int));
    }
}

inline UNATIVE_OFFSET emitter::emitInsSizeSV(instrDesc* id, code_t code, int var, int dsp)
{
    assert(id->idIns() != INS_invalid);
    instruction    ins      = id->idIns();
    emitAttr       attrSize = id->idOpSize();
    UNATIVE_OFFSET prefix   = emitGetAdjustedSize(ins, attrSize, code);

    // REX prefix
    if (TakesRexWPrefix(ins, attrSize) || IsExtendedReg(id->idReg1(), attrSize) ||
        IsExtendedReg(id->idReg2(), attrSize))
    {
        prefix += emitGetRexPrefixSize(ins);
    }

    return prefix + emitInsSizeSV(code, var, dsp);
}

inline UNATIVE_OFFSET emitter::emitInsSizeSV(instrDesc* id, code_t code, int var, int dsp, int val)
{
    assert(id->idIns() != INS_invalid);
    instruction    ins       = id->idIns();
    emitAttr       attrSize  = id->idOpSize();
    UNATIVE_OFFSET valSize   = EA_SIZE_IN_BYTES(attrSize);
    UNATIVE_OFFSET prefix    = emitGetAdjustedSize(ins, attrSize, code);
    bool           valInByte = ((signed char)val == val) && (ins != INS_mov) && (ins != INS_test);

#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(valSize <= sizeof(int) || !id->idIsCnsReloc());
#endif // TARGET_AMD64

    if (valSize > sizeof(int))
    {
        valSize = sizeof(int);
    }

    if (id->idIsCnsReloc())
    {
        valInByte = false; // relocs can't be placed in a byte
        assert(valSize == sizeof(int));
    }

    if (valInByte)
    {
        valSize = sizeof(char);
    }
    else
    {
        assert(!IsSSEOrAVXInstruction(ins));
    }

    // 64-bit operand instructions will need a REX.W prefix
    if (TakesRexWPrefix(ins, attrSize) || IsExtendedReg(id->idReg1(), attrSize) ||
        IsExtendedReg(id->idReg2(), attrSize))
    {
        prefix += emitGetRexPrefixSize(ins);
    }

    return prefix + valSize + emitInsSizeSV(code, var, dsp);
}

/*****************************************************************************/

//static bool baseRegisterRequiresSibByte(regNumber base)
//{
//#ifdef TARGET_AMD64
//    return base == REG_ESP || base == REG_R12;
//#else
//    return base == REG_ESP;
//#endif
//}

//static bool baseRegisterRequiresDisplacement(regNumber base)
//{
//#ifdef TARGET_AMD64
//    return base == REG_EBP || base == REG_R13;
//#else
//    return base == REG_EBP;
//#endif
//}

UNATIVE_OFFSET emitter::emitInsSizeAM(instrDesc* id, code_t code)
{
    assert(false);
    return 0;
}

inline UNATIVE_OFFSET emitter::emitInsSizeAM(instrDesc* id, code_t code, int val)
{
    assert(id->idIns() != INS_invalid);
    instruction    ins       = id->idIns();
    UNATIVE_OFFSET valSize   = EA_SIZE_IN_BYTES(id->idOpSize());
    bool           valInByte = ((signed char)val == val) && (ins != INS_mov) && (ins != INS_test);

    // We should never generate BT mem,reg because it has poor performance. BT mem,imm might be useful
    // but it requires special handling of the immediate value (it is always encoded in a byte).
    // Let's not complicate things until this is needed.
    assert(ins != INS_bt);

#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(valSize <= sizeof(INT32) || !id->idIsCnsReloc());
#endif // TARGET_AMD64

    if (valSize > sizeof(INT32))
    {
        valSize = sizeof(INT32);
    }

    if (id->idIsCnsReloc())
    {
        valInByte = false; // relocs can't be placed in a byte
        assert(valSize == sizeof(INT32));
    }

    if (valInByte)
    {
        valSize = sizeof(char);
    }
    else
    {
        assert(!IsSSEOrAVXInstruction(ins));
    }

    return valSize + emitInsSizeAM(id, code);
}

inline UNATIVE_OFFSET emitter::emitInsSizeCV(instrDesc* id, code_t code)
{
    assert(id->idIns() != INS_invalid);
    instruction ins      = id->idIns();
    emitAttr    attrSize = id->idOpSize();

    // fgMorph changes any statics that won't fit into 32-bit addresses
    // into constants with an indir, rather than GT_CLS_VAR
    // so we should only hit this path for statics that are RIP-relative
    UNATIVE_OFFSET size = sizeof(INT32);

    size += emitGetAdjustedSize(ins, attrSize, code);

    // 64-bit operand instructions will need a REX.W prefix
    if (TakesRexWPrefix(ins, attrSize) || IsExtendedReg(id->idReg1(), attrSize) ||
        IsExtendedReg(id->idReg2(), attrSize))
    {
        size += emitGetRexPrefixSize(ins);
    }

    return size + emitInsSize(code);
}

inline UNATIVE_OFFSET emitter::emitInsSizeCV(instrDesc* id, code_t code, int val)
{
    instruction    ins       = id->idIns();
    UNATIVE_OFFSET valSize   = EA_SIZE_IN_BYTES(id->idOpSize());
    bool           valInByte = ((signed char)val == val) && (ins != INS_mov) && (ins != INS_test);

#ifndef TARGET_AMD64
    // occasionally longs get here on x86
    if (valSize > sizeof(INT32))
        valSize = sizeof(INT32);
#endif // !TARGET_AMD64

    if (id->idIsCnsReloc())
    {
        valInByte = false; // relocs can't be placed in a byte
        assert(valSize == sizeof(INT32));
    }

    if (valInByte)
    {
        valSize = sizeof(char);
    }
    else
    {
        assert(!IsSSEOrAVXInstruction(ins));
    }

    return valSize + emitInsSizeCV(id, code);
}

/*****************************************************************************
 *
 *  Allocate instruction descriptors for instructions with address modes.
 */

inline emitter::instrDesc* emitter::emitNewInstrAmd(emitAttr size, ssize_t dsp)
{
    assert(false);
    return 0;
}

/*****************************************************************************
 *
 *  Set the displacement field in an instruction. Only handles instrDescAmd type.
 */

inline void emitter::emitSetAmdDisp(instrDescAmd* id, ssize_t dsp)
{
    if (dsp < AM_DISP_MIN || dsp > AM_DISP_MAX)
    {
        id->idSetIsLargeDsp();
#ifdef DEBUG
        id->idAddr()->iiaAddrMode.amDisp = AM_DISP_BIG_VAL;
#endif
        id->idaAmdVal = dsp;
    }
    else
    {
        id->idSetIsSmallDsp();
        id->idAddr()->iiaAddrMode.amDisp = dsp;
        assert(id->idAddr()->iiaAddrMode.amDisp == dsp); // make sure the value fit
    }
}

/*****************************************************************************
 *
 *  Allocate an instruction descriptor for an instruction that uses both
 *  an address mode displacement and a constant.
 */

emitter::instrDesc* emitter::emitNewInstrAmdCns(emitAttr size, ssize_t dsp, int cns)
{
    assert(false);
    return 0;
}

//-----------------------------------------------------------------------------
//
//  The next instruction will be a loop head entry point
//  So insert an alignment instruction here to ensure that
//  we can properly align the code.
//
void emitter::emitLoopAlign(unsigned short paddingBytes)
{
    assert(false);
}

//-----------------------------------------------------------------------------
//
//  The next instruction will be a loop head entry point
//  So insert alignment instruction(s) here to ensure that
//  we can properly align the code.
//
//  This emits more than one `INS_align` instruction depending on the
//  alignmentBoundary parameter.
//
void emitter::emitLongLoopAlign(unsigned short alignmentBoundary)
{
    assert(false);
}

/*****************************************************************************
 *
 *  Add a NOP instruction of the given size.
 */

void emitter::emitIns_Nop(unsigned size)
{
    assert(size <= MAX_ENCODED_SIZE);

    instrDesc* id = emitNewInstr();
    id->idIns(INS_nop);
    id->idInsFmt(IF_NONE);
    id->idCodeSize(size);

    dispIns(id);
    emitCurIGsize += size;
}

/*****************************************************************************
 *
 *  Add an instruction with no operands.
 */
void emitter::emitIns(instruction ins)
{
    assert(false);
}

// Add an instruction with no operands, but whose encoding depends on the size
// (Only CDQ/CQO currently)
void emitter::emitIns(instruction ins, emitAttr attr)
{
    UNATIVE_OFFSET sz;
    instrDesc*     id   = emitNewInstr(attr);
    code_t         code = insCodeMR(ins);
    assert(ins == INS_cdq);
    assert((code & 0xFFFFFF00) == 0);
    sz = 1;

    insFormat fmt = IF_NONE;

    sz += emitGetAdjustedSize(ins, attr, code);
    if (TakesRexWPrefix(ins, attr))
    {
        sz += emitGetRexPrefixSize(ins);
    }

    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// emitMapFmtForIns: map the instruction format based on the instruction.
// Shift-by-a-constant instructions have a special format.
//
// Arguments:
//    fmt - the instruction format to map
//    ins - the instruction
//
// Returns:
//    The mapped instruction format.
//
emitter::insFormat emitter::emitMapFmtForIns(insFormat fmt, instruction ins)
{
    switch (ins)
    {
        case INS_rol_N:
        case INS_ror_N:
        case INS_rcl_N:
        case INS_rcr_N:
        case INS_shl_N:
        case INS_shr_N:
        case INS_sar_N:
        {
            switch (fmt)
            {
                case IF_RRW_CNS:
                    return IF_RRW_SHF;
                case IF_MRW_CNS:
                    return IF_MRW_SHF;
                case IF_SRW_CNS:
                    return IF_SRW_SHF;
                case IF_ARW_CNS:
                    return IF_ARW_SHF;
                default:
                    unreached();
            }
        }

        default:
            return fmt;
    }
}

//------------------------------------------------------------------------
// emitMapFmtAtoM: map the address mode formats ARD, ARW, and AWR to their direct address equivalents.
//
// Arguments:
//    fmt - the instruction format to map
//
// Returns:
//    The mapped instruction format.
//
emitter::insFormat emitter::emitMapFmtAtoM(insFormat fmt)
{
    switch (fmt)
    {
        case IF_ARD:
            return IF_MRD;
        case IF_AWR:
            return IF_MWR;
        case IF_ARW:
            return IF_MRW;

        case IF_RRD_ARD:
            return IF_RRD_MRD;
        case IF_RWR_ARD:
            return IF_RWR_MRD;
        case IF_RWR_ARD_CNS:
            return IF_RWR_MRD_CNS;
        case IF_RRW_ARD:
            return IF_RRW_MRD;
        case IF_RRW_ARD_CNS:
            return IF_RRW_MRD_CNS;
        case IF_RWR_RRD_ARD:
            return IF_RWR_RRD_MRD;
        case IF_RWR_RRD_ARD_CNS:
            return IF_RWR_RRD_MRD_CNS;
        case IF_RWR_RRD_ARD_RRD:
            return IF_RWR_RRD_MRD_RRD;

        case IF_ARD_RRD:
            return IF_MRD_RRD;
        case IF_AWR_RRD:
            return IF_MWR_RRD;
        case IF_ARW_RRD:
            return IF_MRW_RRD;

        case IF_ARD_CNS:
            return IF_MRD_CNS;
        case IF_AWR_CNS:
            return IF_MWR_CNS;
        case IF_ARW_CNS:
            return IF_MRW_CNS;

        case IF_AWR_RRD_CNS:
            return IF_MWR_RRD_CNS;

        case IF_ARW_SHF:
            return IF_MRW_SHF;

        default:
            unreached();
    }
}

//------------------------------------------------------------------------
// emitHandleMemOp: For a memory operand, fill in the relevant fields of the instrDesc.
//
// Arguments:
//    indir - the memory operand.
//    id - the instrDesc to fill in.
//    fmt - the instruction format to use. This must be one of the ARD, AWR, or ARW formats. If necessary (such as for
//          GT_CLS_VAR_ADDR), this function will map it to the correct format.
//    ins - the instruction we are generating. This might affect the instruction format we choose.
//
// Assumptions:
//    The correctly sized instrDesc must already be created, e.g., via emitNewInstrAmd() or emitNewInstrAmdCns();
//
// Post-conditions:
//    For base address of int constant:
//        -- the caller must have added the int constant base to the instrDesc when creating it via
//           emitNewInstrAmdCns().
//    For simple address modes (base + scale * index + offset):
//        -- the base register, index register, and scale factor are set.
//        -- the caller must have added the addressing mode offset int constant to the instrDesc when creating it via
//           emitNewInstrAmdCns().
//
//    The instruction format is set.
//
//    idSetIsDspReloc() is called if necessary.
//
void emitter::emitHandleMemOp(GenTreeIndir* indir, instrDesc* id, insFormat fmt, instruction ins)
{
    assert(false);
}

// Takes care of storing all incoming register parameters
// into its corresponding shadow space (defined by the x64 ABI)
void emitter::spillIntArgRegsToShadowSlots()
{
    unsigned       argNum;
    instrDesc*     id;
    UNATIVE_OFFSET sz;

    assert(emitComp->compGeneratingProlog);

    for (argNum = 0; argNum < MAX_REG_ARG; ++argNum)
    {
        regNumber argReg = intArgRegs[argNum];

        // The offsets for the shadow space start at RSP + 8
        // (right before the caller return address)
        int offset = (argNum + 1) * EA_PTRSIZE;

        id = emitNewInstrAmd(EA_PTRSIZE, offset);
        id->idIns(INS_mov);
        id->idInsFmt(IF_AWR_RRD);
        id->idAddr()->iiaAddrMode.amBaseReg = REG_SPBASE;
        id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;
        id->idAddr()->iiaAddrMode.amScale   = emitEncodeScale(1);

        // The offset has already been set in the intrDsc ctor,
        // make sure we got it right.
        assert(emitGetInsAmdAny(id) == ssize_t(offset));

        id->idReg1(argReg);
        sz = emitInsSizeAM(id, insCodeMR(INS_mov));
        id->idCodeSize(sz);
        emitCurIGsize += sz;
    }
}

//------------------------------------------------------------------------
// emitInsLoadInd: Emits a "mov reg, [mem]" (or a variant such as "movzx" or "movss")
// instruction for a GT_IND node.
//
// Arguments:
//    ins - the instruction to emit
//    attr - the instruction operand size
//    dstReg - the destination register
//    mem - the GT_IND node
//
void emitter::emitInsLoadInd(instruction ins, emitAttr attr, regNumber dstReg, GenTreeIndir* mem)
{
    assert(mem->OperIs(GT_IND, GT_NULLCHECK));

    GenTree* addr = mem->Addr();

    if (addr->OperGet() == GT_CLS_VAR_ADDR)
    {
        emitIns_R_C(ins, attr, dstReg, addr->AsClsVar()->gtClsVarHnd, 0);
        return;
    }

    if (addr->OperIs(GT_LCL_VAR_ADDR, GT_LCL_FLD_ADDR))
    {
        GenTreeLclVarCommon* varNode = addr->AsLclVarCommon();
        unsigned             offset  = varNode->GetLclOffs();
        emitIns_R_S(ins, attr, dstReg, varNode->GetLclNum(), offset);

        // Updating variable liveness after instruction was emitted.
        // TODO-Review: it appears that this call to genUpdateLife does nothing because it
        // returns quickly when passed GT_LCL_VAR_ADDR or GT_LCL_FLD_ADDR. Below, emitInsStoreInd
        // had similar code that replaced `varNode` with `mem` (to fix a GC hole). It might be
        // appropriate to do that here as well, but doing so showed no asm diffs, so it's not
        // clear when this scenario gets hit, at least for GC refs.
        codeGen->genUpdateLife(varNode);
        return;
    }

    assert(addr->OperIsAddrMode() || (addr->IsCnsIntOrI() && addr->isContained()) || !addr->isContained());
    ssize_t    offset = mem->Offset();
    instrDesc* id     = emitNewInstrAmd(attr, offset);
    id->idIns(ins);
    id->idReg1(dstReg);
    emitHandleMemOp(mem, id, IF_RWR_ARD, ins);
    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins));
    id->idCodeSize(sz);
    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// emitInsStoreInd: Emits a "mov [mem], reg/imm" (or a variant such as "movss")
// instruction for a GT_STOREIND node.
//
// Arguments:
//    ins - the instruction to emit
//    attr - the instruction operand size
//    mem - the GT_STOREIND node
//
void emitter::emitInsStoreInd(instruction ins, emitAttr attr, GenTreeStoreInd* mem)
{
    assert(mem->OperIs(GT_STOREIND));

    GenTree* addr = mem->Addr();
    GenTree* data = mem->Data();

    if (addr->OperGet() == GT_CLS_VAR_ADDR)
    {
        if (data->isContainedIntOrIImmed())
        {
            emitIns_C_I(ins, attr, addr->AsClsVar()->gtClsVarHnd, 0, (int)data->AsIntConCommon()->IconValue());
        }
        else
        {
            assert(!data->isContained());
            emitIns_C_R(ins, attr, addr->AsClsVar()->gtClsVarHnd, data->GetRegNum(), 0);
        }
        return;
    }

    if (addr->OperIs(GT_LCL_VAR_ADDR, GT_LCL_FLD_ADDR))
    {
        GenTreeLclVarCommon* varNode = addr->AsLclVarCommon();
        unsigned             offset  = varNode->GetLclOffs();
        if (data->isContainedIntOrIImmed())
        {
            emitIns_S_I(ins, attr, varNode->GetLclNum(), offset, (int)data->AsIntConCommon()->IconValue());
        }
        else
        {
            assert(!data->isContained());
            emitIns_S_R(ins, attr, data->GetRegNum(), varNode->GetLclNum(), offset);
        }

        // Updating variable liveness after instruction was emitted
        codeGen->genUpdateLife(mem);
        return;
    }

    ssize_t        offset = mem->Offset();
    UNATIVE_OFFSET sz;
    instrDesc*     id;

    if (data->isContainedIntOrIImmed())
    {
        int icon = (int)data->AsIntConCommon()->IconValue();
        id       = emitNewInstrAmdCns(attr, offset, icon);
        id->idIns(ins);
        emitHandleMemOp(mem, id, IF_AWR_CNS, ins);
        sz = emitInsSizeAM(id, insCodeMI(ins), icon);
        id->idCodeSize(sz);
    }
    else
    {
        assert(!data->isContained());
        id = emitNewInstrAmd(attr, offset);
        id->idIns(ins);
        emitHandleMemOp(mem, id, IF_AWR_RRD, ins);
        id->idReg1(data->GetRegNum());
        sz = emitInsSizeAM(id, insCodeMR(ins));
        id->idCodeSize(sz);
    }

    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// emitInsStoreLcl: Emits a "mov [mem], reg/imm" (or a variant such as "movss")
// instruction for a GT_STORE_LCL_VAR node.
//
// Arguments:
//    ins - the instruction to emit
//    attr - the instruction operand size
//    varNode - the GT_STORE_LCL_VAR node
//
void emitter::emitInsStoreLcl(instruction ins, emitAttr attr, GenTreeLclVarCommon* varNode)
{
    assert(varNode->OperIs(GT_STORE_LCL_VAR));
    assert(varNode->GetRegNum() == REG_NA); // stack store

    GenTree* data = varNode->gtGetOp1();
    codeGen->inst_set_SV_var(varNode);

    if (data->isContainedIntOrIImmed())
    {
        emitIns_S_I(ins, attr, varNode->GetLclNum(), 0, (int)data->AsIntConCommon()->IconValue());
    }
    else
    {
        assert(!data->isContained());
        emitIns_S_R(ins, attr, data->GetRegNum(), varNode->GetLclNum(), 0);
    }

    // Updating variable liveness after instruction was emitted
    codeGen->genUpdateLife(varNode);
}

//------------------------------------------------------------------------
// emitInsBinary: Emits an instruction for a node which takes two operands
//
// Arguments:
//    ins - the instruction to emit
//    attr - the instruction operand size
//    dst - the destination and first source operand
//    src - the second source operand
//
// Assumptions:
//  i) caller of this routine needs to call genConsumeReg()
// ii) caller of this routine needs to call genProduceReg()
regNumber emitter::emitInsBinary(instruction ins, emitAttr attr, GenTree* dst, GenTree* src)
{
    assert(false);
    return (regNumber)0;
}

//------------------------------------------------------------------------
// emitInsRMW: Emit logic for Read-Modify-Write binary instructions.
//
// Responsible for emitting a single instruction that will perform an operation of the form:
//      *addr = *addr <BinOp> src
// For example:
//      ADD [RAX], RCX
//
// Arguments:
//    ins - instruction to generate
//    attr - emitter attribute for instruction
//    storeInd - indir for RMW addressing mode
//    src - source operand of instruction
//
// Assumptions:
//    Lowering has taken care of recognizing the StoreInd pattern of:
//          StoreInd( AddressTree, BinOp( Ind ( AddressTree ), Operand ) )
//    The address to store is already sitting in a register.
//
// Notes:
//    This is a no-produce operation, meaning that no register output will
//    be produced for future use in the code stream.
//
void emitter::emitInsRMW(instruction ins, emitAttr attr, GenTreeStoreInd* storeInd, GenTree* src)
{
    GenTree* addr = storeInd->Addr();
    addr          = addr->gtSkipReloadOrCopy();
    assert(addr->OperIs(GT_LCL_VAR, GT_LCL_VAR_ADDR, GT_LEA, GT_CLS_VAR_ADDR, GT_CNS_INT));

    instrDesc*     id = nullptr;
    UNATIVE_OFFSET sz;

    ssize_t offset = 0;
    if (addr->OperGet() != GT_CLS_VAR_ADDR)
    {
        offset = storeInd->Offset();
    }

    if (src->isContainedIntOrIImmed())
    {
        GenTreeIntConCommon* intConst = src->AsIntConCommon();
        int                  iconVal  = (int)intConst->IconValue();
        switch (ins)
        {
            case INS_rcl_N:
            case INS_rcr_N:
            case INS_rol_N:
            case INS_ror_N:
            case INS_shl_N:
            case INS_shr_N:
            case INS_sar_N:
                iconVal &= 0x7F;
                break;
            default:
                break;
        }

        id = emitNewInstrAmdCns(attr, offset, iconVal);
        emitHandleMemOp(storeInd, id, IF_ARW_CNS, ins);
        id->idIns(ins);
        sz = emitInsSizeAM(id, insCodeMI(ins), iconVal);
    }
    else
    {
        assert(!src->isContained()); // there must be one non-contained src

        // ind, reg
        id = emitNewInstrAmd(attr, offset);
        emitHandleMemOp(storeInd, id, IF_ARW_RRD, ins);
        id->idReg1(src->GetRegNum());
        id->idIns(ins);
        sz = emitInsSizeAM(id, insCodeMR(ins));
    }

    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// emitInsRMW: Emit logic for Read-Modify-Write unary instructions.
//
// Responsible for emitting a single instruction that will perform an operation of the form:
//      *addr = UnaryOp *addr
// For example:
//      NOT [RAX]
//
// Arguments:
//    ins - instruction to generate
//    attr - emitter attribute for instruction
//    storeInd - indir for RMW addressing mode
//
// Assumptions:
//    Lowering has taken care of recognizing the StoreInd pattern of:
//          StoreInd( AddressTree, UnaryOp( Ind ( AddressTree ) ) )
//    The address to store is already sitting in a register.
//
// Notes:
//    This is a no-produce operation, meaning that no register output will
//    be produced for future use in the code stream.
//
void emitter::emitInsRMW(instruction ins, emitAttr attr, GenTreeStoreInd* storeInd)
{
    GenTree* addr = storeInd->Addr();
    addr          = addr->gtSkipReloadOrCopy();
    assert(addr->OperIs(GT_LCL_VAR, GT_LCL_VAR_ADDR, GT_CLS_VAR_ADDR, GT_LEA, GT_CNS_INT));

    ssize_t offset = 0;
    if (addr->OperGet() != GT_CLS_VAR_ADDR)
    {
        offset = storeInd->Offset();
    }

    instrDesc* id = emitNewInstrAmd(attr, offset);
    emitHandleMemOp(storeInd, id, IF_ARW, ins);
    id->idIns(ins);
    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeMR(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

/*****************************************************************************
 *
 *  Add an instruction referencing a single register.
 */

void emitter::emitIns_R(instruction ins, emitAttr attr, regNumber reg)
{
    emitAttr size = EA_SIZE(attr);

    assert(size <= EA_PTRSIZE);
    noway_assert(emitVerifyEncodable(ins, size, reg));

    UNATIVE_OFFSET sz;
    instrDesc*     id = emitNewInstrSmall(attr);

    switch (ins)
    {
        case INS_inc:
        case INS_dec:
#ifdef TARGET_AMD64

            sz = 2; // x64 has no 1-byte opcode (it is the same encoding as the REX prefix)

#else // !TARGET_AMD64

            if (size == EA_1BYTE)
                sz = 2; // Use the long form as the small one has no 'w' bit
            else
                sz = 1; // Use short form

#endif // !TARGET_AMD64

            break;

        case INS_pop:
        case INS_pop_hide:
        case INS_push:
        case INS_push_hide:

            /* We don't currently push/pop small values */

            assert(size == EA_PTRSIZE);

            sz = 1;
            break;

        default:

            /* All the sixteen INS_setCCs are contiguous. */

            if (INS_seto <= ins && ins <= INS_setg)
            {
                // Rough check that we used the endpoints for the range check

                assert(INS_seto + 0xF == INS_setg);

                // The caller must specify EA_1BYTE for 'attr'

                assert(attr == EA_1BYTE);

                /* We expect this to always be a 'big' opcode */

                assert(insEncodeMRreg(ins, reg, attr, insCodeMR(ins)) & 0x00FF0000);

                size = attr;

                sz = 3;
                break;
            }
            else
            {
                sz = 2;
                break;
            }
    }
    insFormat fmt = emitInsModeFormat(ins, IF_RRD);

    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idReg1(reg);

    // Vex bytes
    sz += emitGetAdjustedSize(ins, attr, insEncodeMRreg(ins, reg, attr, insCodeMR(ins)));

    // REX byte
    if (IsExtendedReg(reg, attr) || TakesRexWPrefix(ins, attr))
    {
        sz += emitGetRexPrefixSize(ins);
    }

    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;

    emitAdjustStackDepthPushPop(ins);
}

/*****************************************************************************
 *
 *  Add an instruction referencing a register and a constant.
 */

void emitter::emitIns_R_I(instruction ins, emitAttr attr, regNumber reg, ssize_t val)
{
    assert(false);
}

/*****************************************************************************
 *
 *  Add an instruction referencing an integer constant.
 */

void emitter::emitIns_I(instruction ins, emitAttr attr, cnsval_ssize_t val)
{
    UNATIVE_OFFSET sz;
    instrDesc*     id;
    bool           valInByte = ((signed char)val == (target_ssize_t)val);

#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(EA_SIZE(attr) < EA_8BYTE || !EA_IS_CNS_RELOC(attr));
#endif

    if (EA_IS_CNS_RELOC(attr))
    {
        valInByte = false; // relocs can't be placed in a byte
    }

    switch (ins)
    {
        case INS_loop:
        case INS_jge:
            sz = 2;
            break;

        case INS_ret:
            sz = 3;
            break;

        case INS_push_hide:
        case INS_push:
            sz = valInByte ? 2 : 5;
            break;

        default:
            NO_WAY("unexpected instruction");
    }

    id = emitNewInstrSC(attr, val);
    id->idIns(ins);
    id->idInsFmt(IF_CNS);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;

    emitAdjustStackDepthPushPop(ins);
}

/*****************************************************************************
 *
 *  Add a "jump through a table" instruction.
 */

void emitter::emitIns_IJ(emitAttr attr, regNumber reg, unsigned base)
{
    assert(EA_SIZE(attr) == EA_4BYTE);

    UNATIVE_OFFSET    sz  = 3 + 4;
    const instruction ins = INS_i_jmp;

    if (IsExtendedReg(reg, attr))
    {
        sz += emitGetRexPrefixSize(ins);
    }

    instrDesc* id = emitNewInstrAmd(attr, base);

    id->idIns(ins);
    id->idInsFmt(IF_ARD);
    id->idAddr()->iiaAddrMode.amBaseReg = REG_NA;
    id->idAddr()->iiaAddrMode.amIndxReg = reg;
    id->idAddr()->iiaAddrMode.amScale   = emitter::OPSZP;

#ifdef DEBUG
    id->idDebugOnlyInfo()->idMemCookie = base;
#endif

    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

/*****************************************************************************
 *
 *  Add an instruction with a static data member operand. If 'size' is 0, the
 *  instruction operates on the address of the static member instead of its
 *  value (e.g. "push offset clsvar", rather than "push dword ptr [clsvar]").
 */

void emitter::emitIns_C(instruction ins, emitAttr attr, CORINFO_FIELD_HANDLE fldHnd, int offs)
{
    // Static always need relocs
    if (!jitStaticFldIsGlobAddr(fldHnd))
    {
        attr = EA_SET_FLG(attr, EA_DSP_RELOC_FLG);
    }

    UNATIVE_OFFSET sz;
    instrDesc*     id;

    /* Are we pushing the offset of the class variable? */

    if (EA_IS_OFFSET(attr))
    {
        assert(ins == INS_push);
        sz = 1 + TARGET_POINTER_SIZE;

        id = emitNewInstrDsp(EA_1BYTE, offs);
        id->idIns(ins);
        id->idInsFmt(IF_MRD_OFF);
    }
    else
    {
        insFormat fmt = emitInsModeFormat(ins, IF_MRD);

        id = emitNewInstrDsp(attr, offs);
        id->idIns(ins);
        id->idInsFmt(fmt);
        sz = emitInsSizeCV(id, insCodeMR(ins));
    }

    if (TakesRexWPrefix(ins, attr))
    {
        // REX.W prefix
        sz += emitGetRexPrefixSize(ins);
    }

    id->idAddr()->iiaFieldHnd = fldHnd;

    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;

    emitAdjustStackDepthPushPop(ins);
}

/*****************************************************************************
 *
 *  Add an instruction with two register operands.
 */

void emitter::emitIns_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2)
{
    emitAttr size = EA_SIZE(attr);

    /* We don't want to generate any useless mov instructions! */
    CLANG_FORMAT_COMMENT_ANCHOR;

#ifdef TARGET_AMD64
    // Same-reg 4-byte mov can be useful because it performs a
    // zero-extension to 8 bytes.
    assert(ins != INS_mov || reg1 != reg2 || size == EA_4BYTE);
#else
    assert(ins != INS_mov || reg1 != reg2);
#endif // TARGET_AMD64

    assert(size <= EA_32BYTE);
    noway_assert(emitVerifyEncodable(ins, size, reg1, reg2));

    UNATIVE_OFFSET sz = emitInsSizeRR(ins, reg1, reg2, attr);

    /* Special case: "XCHG" uses a different format */
    insFormat fmt = (ins == INS_xchg) ? IF_RRW_RRW : emitInsModeFormat(ins, IF_RRD_RRD);

    instrDesc* id = emitNewInstrSmall(attr);
    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idReg1(reg1);
    id->idReg2(reg2);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

/*****************************************************************************
 *
 *  Add an instruction with two register operands and an integer constant.
 */

void emitter::emitIns_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int ival)
{
#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(EA_SIZE(attr) < EA_8BYTE || !EA_IS_CNS_RELOC(attr));
#endif

    instrDesc* id = emitNewInstrSC(attr, ival);

    id->idIns(ins);
    id->idInsFmt(IF_RRW_RRW_CNS);
    id->idReg1(reg1);
    id->idReg2(reg2);

    code_t code = 0;

    switch (ins)
    {
        case INS_pextrb:
        case INS_pextrd:
        case INS_pextrq:
        case INS_pextrw_sse41:
        case INS_extractps:
        case INS_vextractf128:
        case INS_vextracti128:
        case INS_shld:
        case INS_shrd:
        {
            code = insCodeMR(ins);
            break;
        }

        case INS_psrldq:
        case INS_pslldq:
        {
            code = insCodeMI(ins);
            break;
        }

        default:
        {
            code = insCodeRM(ins);
            break;
        }
    }

    UNATIVE_OFFSET sz = emitInsSizeRR(id, code, ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_AR(instruction ins, emitAttr attr, regNumber base, int offs)
{
    assert(ins == INS_prefetcht0 || ins == INS_prefetcht1 || ins == INS_prefetcht2 || ins == INS_prefetchnta);

    instrDesc* id = emitNewInstrAmd(attr, offs);

    id->idIns(ins);

    id->idInsFmt(IF_ARD);
    id->idAddr()->iiaAddrMode.amBaseReg = base;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeMR(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// emitIns_AR_R_R: emits the code for an instruction that takes a base memory register, two register operands
//                 and that does not return a value
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op2Reg    -- The register of the second operand
//    op3Reg    -- The register of the third operand
//    base      -- The base register used for the memory address (first operand)
//    offs      -- The offset from base
//
void emitter::emitIns_AR_R_R(
    instruction ins, emitAttr attr, regNumber op2Reg, regNumber op3Reg, regNumber base, int offs)
{
    assert(IsSSEOrAVXInstruction(ins));
    assert(IsThreeOperandAVXInstruction(ins));

    instrDesc* id = emitNewInstrAmd(attr, offs);

    id->idIns(ins);
    id->idReg1(op2Reg);
    id->idReg2(op3Reg);

    id->idInsFmt(IF_AWR_RRD_RRD);
    id->idAddr()->iiaAddrMode.amBaseReg = base;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeMR(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_A(instruction ins, emitAttr attr, regNumber reg1, GenTreeIndir* indir)
{
    ssize_t    offs = indir->Offset();
    instrDesc* id   = emitNewInstrAmd(attr, offs);

    id->idIns(ins);
    id->idReg1(reg1);

    emitHandleMemOp(indir, id, IF_RRW_ARD, ins);

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_A_I(instruction ins, emitAttr attr, regNumber reg1, GenTreeIndir* indir, int ival)
{
    noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg1));
    assert(IsSSEOrAVXInstruction(ins));

    ssize_t    offs = indir->Offset();
    instrDesc* id   = emitNewInstrAmdCns(attr, offs, ival);

    id->idIns(ins);
    id->idReg1(reg1);

    emitHandleMemOp(indir, id, IF_RRW_ARD_CNS, ins);

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins), ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_AR_I(instruction ins, emitAttr attr, regNumber reg1, regNumber base, int offs, int ival)
{
    noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg1));
    assert(IsSSEOrAVXInstruction(ins));

    instrDesc* id = emitNewInstrAmdCns(attr, offs, ival);

    id->idIns(ins);
    id->idReg1(reg1);

    id->idInsFmt(IF_RRW_ARD_CNS);
    id->idAddr()->iiaAddrMode.amBaseReg = base;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins), ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_C_I(
    instruction ins, emitAttr attr, regNumber reg1, CORINFO_FIELD_HANDLE fldHnd, int offs, int ival)
{
    // Static always need relocs
    if (!jitStaticFldIsGlobAddr(fldHnd))
    {
        attr = EA_SET_FLG(attr, EA_DSP_RELOC_FLG);
    }

    noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg1));
    assert(IsSSEOrAVXInstruction(ins));

    instrDesc* id = emitNewInstrCnsDsp(attr, ival, offs);

    id->idIns(ins);
    id->idInsFmt(IF_RRW_MRD_CNS);
    id->idReg1(reg1);
    id->idAddr()->iiaFieldHnd = fldHnd;

    UNATIVE_OFFSET sz = emitInsSizeCV(id, insCodeRM(ins), ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_S_I(instruction ins, emitAttr attr, regNumber reg1, int varx, int offs, int ival)
{
    noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg1));
    assert(IsSSEOrAVXInstruction(ins));

    instrDesc* id = emitNewInstrCns(attr, ival);

    id->idIns(ins);
    id->idInsFmt(IF_RRW_SRD_CNS);
    id->idReg1(reg1);
    id->idAddr()->iiaLclVar.initLclVarAddr(varx, offs);

#ifdef DEBUG
    id->idDebugOnlyInfo()->idVarRefOffs = emitVarRefOffs;
#endif

    UNATIVE_OFFSET sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs, ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_R_S(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int varx, int offs)
{
    assert(IsSSEOrAVXInstruction(ins));
    assert(IsThreeOperandAVXInstruction(ins));

    instrDesc* id = emitNewInstr(attr);

    id->idIns(ins);
    id->idInsFmt(IF_RWR_RRD_SRD);
    id->idReg1(reg1);
    id->idReg2(reg2);
    id->idAddr()->iiaLclVar.initLclVarAddr(varx, offs);

#ifdef DEBUG
    id->idDebugOnlyInfo()->idVarRefOffs = emitVarRefOffs;
#endif

    UNATIVE_OFFSET sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_R_A(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, GenTreeIndir* indir)
{
    assert(IsSSEOrAVXInstruction(ins));
    assert(IsThreeOperandAVXInstruction(ins));

    ssize_t    offs = indir->Offset();
    instrDesc* id   = emitNewInstrAmd(attr, offs);

    id->idIns(ins);
    id->idReg1(reg1);
    id->idReg2(reg2);

    emitHandleMemOp(indir, id, IF_RWR_RRD_ARD, ins);

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_R_AR(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, regNumber base, int offs)
{
    assert(IsSSEOrAVXInstruction(ins));
    assert(IsThreeOperandAVXInstruction(ins));

    instrDesc* id = emitNewInstrAmd(attr, offs);

    id->idIns(ins);
    id->idReg1(reg1);
    id->idReg2(reg2);

    id->idInsFmt(IF_RWR_RRD_ARD);
    id->idAddr()->iiaAddrMode.amBaseReg = base;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// IsAVX2GatherInstruction: return true if the instruction is AVX2 Gather
//
// Arguments:
//    ins - the instruction to check
// Return Value:
//    true if the instruction is AVX2 Gather
//
bool IsAVX2GatherInstruction(instruction ins)
{
    switch (ins)
    {
        case INS_vpgatherdd:
        case INS_vpgatherdq:
        case INS_vpgatherqd:
        case INS_vpgatherqq:
        case INS_vgatherdps:
        case INS_vgatherdpd:
        case INS_vgatherqps:
        case INS_vgatherqpd:
            return true;
        default:
            return false;
    }
}

//------------------------------------------------------------------------
// emitIns_R_AR_R: Emits an AVX2 Gather instructions
//
// Arguments:
//    ins - the instruction to emit
//    attr - the instruction operand size
//    reg1 - the destination and first source operand
//    reg2 - the mask operand (encoded in VEX.vvvv)
//    base - the base register of address to load
//    index - the index register of VSIB
//    scale - the scale number of VSIB
//    offs - the offset added to the memory address from base
//
void emitter::emitIns_R_AR_R(instruction ins,
                             emitAttr    attr,
                             regNumber   reg1,
                             regNumber   reg2,
                             regNumber   base,
                             regNumber   index,
                             int         scale,
                             int         offs)
{
    assert(IsAVX2GatherInstruction(ins));

    instrDesc* id = emitNewInstrAmd(attr, offs);

    id->idIns(ins);
    id->idReg1(reg1);
    id->idReg2(reg2);

    id->idInsFmt(IF_RWR_ARD_RRD);
    id->idAddr()->iiaAddrMode.amBaseReg = base;
    id->idAddr()->iiaAddrMode.amIndxReg = index;
    id->idAddr()->iiaAddrMode.amScale   = emitEncodeSize((emitAttr)scale);

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_R_C(
    instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, CORINFO_FIELD_HANDLE fldHnd, int offs)
{
    assert(IsSSEOrAVXInstruction(ins));
    assert(IsThreeOperandAVXInstruction(ins));

    // Static always need relocs
    if (!jitStaticFldIsGlobAddr(fldHnd))
    {
        attr = EA_SET_FLG(attr, EA_DSP_RELOC_FLG);
    }

    instrDesc* id = emitNewInstrDsp(attr, offs);

    id->idIns(ins);
    id->idInsFmt(IF_RWR_RRD_MRD);
    id->idReg1(reg1);
    id->idReg2(reg2);
    id->idAddr()->iiaFieldHnd = fldHnd;

    UNATIVE_OFFSET sz = emitInsSizeCV(id, insCodeRM(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

/*****************************************************************************
*
*  Add an instruction with three register operands.
*/

void emitter::emitIns_R_R_R(instruction ins, emitAttr attr, regNumber targetReg, regNumber reg1, regNumber reg2)
{
    assert(false);
}

void emitter::emitIns_R_R_AR_I(
    instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, regNumber base, int offs, int ival)
{
    assert(IsSSEOrAVXInstruction(ins));
    assert(IsThreeOperandAVXInstruction(ins));

    instrDesc* id = emitNewInstrAmdCns(attr, offs, ival);

    id->idIns(ins);
    id->idReg1(reg1);
    id->idReg2(reg2);

    id->idInsFmt(IF_RWR_RRD_ARD_CNS);
    id->idAddr()->iiaAddrMode.amBaseReg = base;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins), ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_R_C_I(
    instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, CORINFO_FIELD_HANDLE fldHnd, int offs, int ival)
{
    assert(IsSSEOrAVXInstruction(ins));
    assert(IsThreeOperandAVXInstruction(ins));

    // Static always need relocs
    if (!jitStaticFldIsGlobAddr(fldHnd))
    {
        attr = EA_SET_FLG(attr, EA_DSP_RELOC_FLG);
    }

    instrDesc* id = emitNewInstrCnsDsp(attr, ival, offs);

    id->idIns(ins);
    id->idInsFmt(IF_RWR_RRD_MRD_CNS);
    id->idReg1(reg1);
    id->idReg2(reg2);
    id->idAddr()->iiaFieldHnd = fldHnd;

    UNATIVE_OFFSET sz = emitInsSizeCV(id, insCodeRM(ins), ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

/**********************************************************************************
* emitIns_R_R_R_I: Add an instruction with three register operands and an immediate.
*
* Arguments:
*    ins       - the instruction to add
*    attr      - the emitter attribute for instruction
*    targetReg - the target (destination) register
*    reg1      - the first source register
*    reg2      - the second source register
*    ival      - the immediate value
*/

void emitter::emitIns_R_R_R_I(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber reg1, regNumber reg2, int ival)
{
    assert(false);
}

void emitter::emitIns_R_R_S_I(
    instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int varx, int offs, int ival)
{
    assert(IsSSEOrAVXInstruction(ins));
    assert(IsThreeOperandAVXInstruction(ins));

    instrDesc* id = emitNewInstrCns(attr, ival);

    id->idIns(ins);
    id->idInsFmt(IF_RWR_RRD_SRD_CNS);
    id->idReg1(reg1);
    id->idReg2(reg2);
    id->idAddr()->iiaLclVar.initLclVarAddr(varx, offs);

#ifdef DEBUG
    id->idDebugOnlyInfo()->idVarRefOffs = emitVarRefOffs;
#endif

    UNATIVE_OFFSET sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs, ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// encodeXmmRegAsIval: Encodes a XMM register into imm[7:4] for use by a SIMD instruction
//
// Arguments
//    opReg -- The register being encoded
//
// Returns:
//    opReg encoded in imm[7:4]
static int encodeXmmRegAsIval(regNumber opReg)
{
    // AVX/AVX2 supports 4-reg format for vblendvps/vblendvpd/vpblendvb,
    // which encodes the fourth register into imm8[7:4]
    assert(opReg >= XMMBASE);
    int ival = (opReg - XMMBASE) << 4;

    assert((ival >= 0) && (ival <= 255));
    return (int8_t)ival;
}

//------------------------------------------------------------------------
// emitIns_R_R_A_R: emits the code for an instruction that takes a register operand, a GenTreeIndir address,
//                  another register operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op3Reg    -- The register of the third operand
//    indir     -- The GenTreeIndir used for the memory address
//
// Remarks:
//    op2 is built from indir
//
void emitter::emitIns_R_R_A_R(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op3Reg, GenTreeIndir* indir)
{
    assert(isAvxBlendv(ins));
    assert(UseVEXEncoding());

    int        ival = encodeXmmRegAsIval(op3Reg);
    ssize_t    offs = indir->Offset();
    instrDesc* id   = emitNewInstrAmdCns(attr, offs, ival);

    id->idIns(ins);
    id->idReg1(targetReg);
    id->idReg2(op1Reg);

    emitHandleMemOp(indir, id, IF_RWR_RRD_ARD_RRD, ins);

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins), ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// emitIns_R_R_AR_R: emits the code for an instruction that takes a register operand, a base memory
//                   register, another register operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operands
//    op3Reg    -- The register of the third operand
//    base      -- The base register used for the memory address
//    offs      -- The offset added to the memory address from base
//
// Remarks:
//    op2 is built from base + offs
//
void emitter::emitIns_R_R_AR_R(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op3Reg, regNumber base, int offs)
{
    assert(isAvxBlendv(ins));
    assert(UseVEXEncoding());

    int        ival = encodeXmmRegAsIval(op3Reg);
    instrDesc* id   = emitNewInstrAmdCns(attr, offs, ival);

    id->idIns(ins);
    id->idReg1(targetReg);
    id->idReg2(op1Reg);

    id->idInsFmt(IF_RWR_RRD_ARD_RRD);
    id->idAddr()->iiaAddrMode.amBaseReg = base;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins), ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// emitIns_R_R_C_R: emits the code for an instruction that takes a register operand, a field handle +
//                  offset,  another register operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op3Reg    -- The register of the third operand
//    fldHnd    -- The CORINFO_FIELD_HANDLE used for the memory address
//    offs      -- The offset added to the memory address from fldHnd
//
// Remarks:
//    op2 is built from fldHnd + offs
//
void emitter::emitIns_R_R_C_R(instruction          ins,
                              emitAttr             attr,
                              regNumber            targetReg,
                              regNumber            op1Reg,
                              regNumber            op3Reg,
                              CORINFO_FIELD_HANDLE fldHnd,
                              int                  offs)
{
    assert(isAvxBlendv(ins));
    assert(UseVEXEncoding());

    // Static always need relocs
    if (!jitStaticFldIsGlobAddr(fldHnd))
    {
        attr = EA_SET_FLG(attr, EA_DSP_RELOC_FLG);
    }

    int        ival = encodeXmmRegAsIval(op3Reg);
    instrDesc* id   = emitNewInstrCnsDsp(attr, ival, offs);

    id->idIns(ins);
    id->idReg1(targetReg);
    id->idReg2(op1Reg);

    id->idInsFmt(IF_RWR_RRD_MRD_RRD);
    id->idAddr()->iiaFieldHnd = fldHnd;

    UNATIVE_OFFSET sz = emitInsSizeCV(id, insCodeRM(ins), ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

//------------------------------------------------------------------------
// emitIns_R_R_R_S: emits the code for a instruction that takes a register operand, a variable index +
//                  offset, another register operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op3Reg    -- The register of the third operand
//    varx      -- The variable index used for the memory address
//    offs      -- The offset added to the memory address from varx
//
// Remarks:
//    op2 is built from varx + offs
//
void emitter::emitIns_R_R_S_R(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op3Reg, int varx, int offs)
{
    assert(isAvxBlendv(ins));
    assert(UseVEXEncoding());

    int        ival = encodeXmmRegAsIval(op3Reg);
    instrDesc* id   = emitNewInstrCns(attr, ival);

    id->idIns(ins);
    id->idReg1(targetReg);
    id->idReg2(op1Reg);

    id->idInsFmt(IF_RWR_RRD_SRD_RRD);
    id->idAddr()->iiaLclVar.initLclVarAddr(varx, offs);

    UNATIVE_OFFSET sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs, ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_R_R_R(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber reg1, regNumber reg2, regNumber reg3)
{
    assert(false);
}

/*****************************************************************************
 *
 *  Add an instruction with a register + static member operands.
 */
void emitter::emitIns_R_C(instruction ins, emitAttr attr, regNumber reg, CORINFO_FIELD_HANDLE fldHnd, int offs)
{
    // Static always need relocs
    if (!jitStaticFldIsGlobAddr(fldHnd))
    {
        attr = EA_SET_FLG(attr, EA_DSP_RELOC_FLG);
    }

    emitAttr size = EA_SIZE(attr);

    assert(size <= EA_32BYTE);
    noway_assert(emitVerifyEncodable(ins, size, reg));

    UNATIVE_OFFSET sz;
    instrDesc*     id;

    // Are we MOV'ing the offset of the class variable into EAX?
    if (EA_IS_OFFSET(attr))
    {
        id = emitNewInstrDsp(EA_1BYTE, offs);
        id->idIns(ins);
        id->idInsFmt(IF_RWR_MRD_OFF);
        id->idReg1(reg);

        assert(ins == INS_mov && reg == REG_EAX);

        // Special case: "mov eax, [addr]" is smaller
        sz = 1 + TARGET_POINTER_SIZE;
    }
    else
    {
        insFormat fmt = emitInsModeFormat(ins, IF_RRD_MRD);

        id = emitNewInstrDsp(attr, offs);
        id->idIns(ins);
        id->idInsFmt(fmt);
        id->idReg1(reg);

#ifdef TARGET_X86
        // Special case: "mov eax, [addr]" is smaller.
        // This case is not enabled for amd64 as it always uses RIP relative addressing
        // and it results in smaller instruction size than encoding 64-bit addr in the
        // instruction.
        if (ins == INS_mov && reg == REG_EAX)
        {
            sz = 1 + TARGET_POINTER_SIZE;
            if (size == EA_2BYTE)
                sz += 1;
        }
        else
#endif // TARGET_X86
        {
            sz = emitInsSizeCV(id, insCodeRM(ins));
        }

        // Special case: mov reg, fs:[ddd]
        if (fldHnd == FLD_GLOBAL_FS)
        {
            sz += 1;
        }
    }

    id->idCodeSize(sz);

    id->idAddr()->iiaFieldHnd = fldHnd;

    dispIns(id);
    emitCurIGsize += sz;
}

/*****************************************************************************
 *
 *  Add an instruction with a static member + register operands.
 */

void emitter::emitIns_C_R(instruction ins, emitAttr attr, CORINFO_FIELD_HANDLE fldHnd, regNumber reg, int offs)
{
    // Static always need relocs
    if (!jitStaticFldIsGlobAddr(fldHnd))
    {
        attr = EA_SET_FLG(attr, EA_DSP_RELOC_FLG);
    }

    emitAttr size = EA_SIZE(attr);

#if defined(TARGET_X86)
    // For x86 it is valid to storeind a double sized operand in an xmm reg to memory
    assert(size <= EA_8BYTE);
#else
    assert(size <= EA_PTRSIZE);
#endif

    noway_assert(emitVerifyEncodable(ins, size, reg));

    instrDesc* id  = emitNewInstrDsp(attr, offs);
    insFormat  fmt = emitInsModeFormat(ins, IF_MRD_RRD);

    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idReg1(reg);

    UNATIVE_OFFSET sz;

#ifdef TARGET_X86
    // Special case: "mov [addr], EAX" is smaller.
    // This case is not enable for amd64 as it always uses RIP relative addressing
    // and it will result in smaller instruction size than encoding 64-bit addr in
    // the instruction.
    if (ins == INS_mov && reg == REG_EAX)
    {
        sz = 1 + TARGET_POINTER_SIZE;

        if (size == EA_2BYTE)
            sz += 1;

        // REX prefix
        if (TakesRexWPrefix(ins, attr) || IsExtendedReg(reg, attr))
        {
            sz += emitGetRexPrefixSize(ins);
        }
    }
    else
#endif // TARGET_X86
    {
        sz = emitInsSizeCV(id, insCodeMR(ins));
    }

    // Special case: mov reg, fs:[ddd]
    if (fldHnd == FLD_GLOBAL_FS)
    {
        sz += 1;
    }

    id->idCodeSize(sz);

    id->idAddr()->iiaFieldHnd = fldHnd;

    dispIns(id);
    emitCurIGsize += sz;
}

/*****************************************************************************
 *
 *  Add an instruction with a static member + constant.
 */

void emitter::emitIns_C_I(instruction ins, emitAttr attr, CORINFO_FIELD_HANDLE fldHnd, int offs, int val)
{
    // Static always need relocs
    if (!jitStaticFldIsGlobAddr(fldHnd))
    {
        attr = EA_SET_FLG(attr, EA_DSP_RELOC_FLG);
    }

    insFormat fmt;

    switch (ins)
    {
        case INS_rcl_N:
        case INS_rcr_N:
        case INS_rol_N:
        case INS_ror_N:
        case INS_shl_N:
        case INS_shr_N:
        case INS_sar_N:
            assert(val != 1);
            fmt = IF_MRW_SHF;
            val &= 0x7F;
            break;

        default:
            fmt = emitInsModeFormat(ins, IF_MRD_CNS);
            break;
    }

    instrDesc* id = emitNewInstrCnsDsp(attr, val, offs);
    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idAddr()->iiaFieldHnd = fldHnd;

    code_t         code = insCodeMI(ins);
    UNATIVE_OFFSET sz   = emitInsSizeCV(id, code, val);

    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_J_S(instruction ins, emitAttr attr, BasicBlock* dst, int varx, int offs)
{
    assert(ins == INS_mov);
    assert(dst->bbFlags & BBF_JMP_TARGET);

    instrDescLbl* id = emitNewInstrLbl();

    id->idIns(ins);
    id->idInsFmt(IF_SWR_LABEL);
    id->idAddr()->iiaBBlabel = dst;

    /* The label reference is always long */

    id->idjShort    = 0;
    id->idjKeepLong = 1;

    /* Record the current IG and offset within it */

    id->idjIG   = emitCurIG;
    id->idjOffs = emitCurIGsize;

    /* Append this instruction to this IG's jump list */

    id->idjNext      = emitCurIGjmpList;
    emitCurIGjmpList = id;

    UNATIVE_OFFSET sz = sizeof(INT32) + emitInsSizeSV(id, insCodeMI(ins), varx, offs);
    id->dstLclVar.initLclVarAddr(varx, offs);
#ifdef DEBUG
    id->idDebugOnlyInfo()->idVarRefOffs = emitVarRefOffs;
#endif

#if EMITTER_STATS
    emitTotalIGjmps++;
#endif

#ifndef TARGET_AMD64
    // Storing the address of a basicBlock will need a reloc
    // as the instruction uses the absolute address,
    // not a relative address.
    //
    // On Amd64, Absolute code addresses should always go through a reloc to
    // to be encoded as RIP rel32 offset.
    if (emitComp->opts.compReloc)
#endif
    {
        id->idSetIsDspReloc();
    }

    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

/*****************************************************************************
 *
 *  Add a label instruction.
 */
void emitter::emitIns_R_L(instruction ins, emitAttr attr, BasicBlock* dst, regNumber reg)
{
    assert(ins == INS_lea);
    assert(dst->bbFlags & BBF_JMP_TARGET);

    instrDescJmp* id = emitNewInstrJmp();

    id->idIns(ins);
    id->idReg1(reg);
    id->idInsFmt(IF_RWR_LABEL);
    id->idOpSize(EA_SIZE(attr)); // emitNewInstrJmp() sets the size (incorrectly) to EA_1BYTE
    id->idAddr()->iiaBBlabel = dst;

    /* The label reference is always long */

    id->idjShort    = 0;
    id->idjKeepLong = 1;

    /* Record the current IG and offset within it */

    id->idjIG   = emitCurIG;
    id->idjOffs = emitCurIGsize;

    /* Append this instruction to this IG's jump list */

    id->idjNext      = emitCurIGjmpList;
    emitCurIGjmpList = id;

#ifdef DEBUG
    // Mark the catch return
    if (emitComp->compCurBB->bbJumpKind == BBJ_EHCATCHRET)
    {
        id->idDebugOnlyInfo()->idCatchRet = true;
    }
#endif // DEBUG

#if EMITTER_STATS
    emitTotalIGjmps++;
#endif

    // Set the relocation flags - these give hint to zap to perform
    // relocation of the specified 32bit address.
    //
    // Note the relocation flags influence the size estimate.
    id->idSetRelocFlags(attr);

    UNATIVE_OFFSET sz = emitInsSizeAM(id, insCodeRM(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

/*****************************************************************************
 *
 *  The following adds instructions referencing address modes.
 */

void emitter::emitIns_I_AR(instruction ins, emitAttr attr, int val, regNumber reg, int disp)
{
    assert((CodeGen::instIsFP(ins) == false) && (EA_SIZE(attr) <= EA_8BYTE));

#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(EA_SIZE(attr) < EA_8BYTE || !EA_IS_CNS_RELOC(attr));
#endif

    insFormat fmt;

    switch (ins)
    {
        case INS_rcl_N:
        case INS_rcr_N:
        case INS_rol_N:
        case INS_ror_N:
        case INS_shl_N:
        case INS_shr_N:
        case INS_sar_N:
            assert(val != 1);
            fmt = IF_ARW_SHF;
            val &= 0x7F;
            break;

        default:
            fmt = emitInsModeFormat(ins, IF_ARD_CNS);
            break;
    }

    /*
    Useful if you want to trap moves with 0 constant
    if (ins == INS_mov && val == 0 && EA_SIZE(attr) >= EA_4BYTE)
    {
        printf("MOV 0\n");
    }
    */

    UNATIVE_OFFSET sz;
    instrDesc*     id = emitNewInstrAmdCns(attr, disp, val);
    id->idIns(ins);
    id->idInsFmt(fmt);

    id->idAddr()->iiaAddrMode.amBaseReg = reg;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeMI(ins), val);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_I_AI(instruction ins, emitAttr attr, int val, ssize_t disp)
{
    assert((CodeGen::instIsFP(ins) == false) && (EA_SIZE(attr) <= EA_8BYTE));

#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(EA_SIZE(attr) < EA_8BYTE || !EA_IS_CNS_RELOC(attr));
#endif

    insFormat fmt;

    switch (ins)
    {
        case INS_rcl_N:
        case INS_rcr_N:
        case INS_rol_N:
        case INS_ror_N:
        case INS_shl_N:
        case INS_shr_N:
        case INS_sar_N:
            assert(val != 1);
            fmt = IF_ARW_SHF;
            val &= 0x7F;
            break;

        default:
            fmt = emitInsModeFormat(ins, IF_ARD_CNS);
            break;
    }

    /*
    Useful if you want to trap moves with 0 constant
    if (ins == INS_mov && val == 0 && EA_SIZE(attr) >= EA_4BYTE)
    {
        printf("MOV 0\n");
    }
    */

    UNATIVE_OFFSET sz;
    instrDesc*     id = emitNewInstrAmdCns(attr, disp, val);
    id->idIns(ins);
    id->idInsFmt(fmt);

    id->idAddr()->iiaAddrMode.amBaseReg = REG_NA;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeMI(ins), val);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_AR(instruction ins, emitAttr attr, regNumber reg, regNumber base, int disp)
{
    emitIns_R_ARX(ins, attr, reg, base, REG_NA, 1, disp);
}

void emitter::emitIns_R_AI(instruction ins, emitAttr attr, regNumber ireg, ssize_t disp)
{
    assert((CodeGen::instIsFP(ins) == false) && (EA_SIZE(attr) <= EA_8BYTE) && (ireg != REG_NA));
    noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), ireg));

    UNATIVE_OFFSET sz;
    instrDesc*     id  = emitNewInstrAmd(attr, disp);
    insFormat      fmt = emitInsModeFormat(ins, IF_RRD_ARD);

    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idReg1(ireg);

    id->idAddr()->iiaAddrMode.amBaseReg = REG_NA;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeRM(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_AR_R(instruction ins, emitAttr attr, regNumber reg, regNumber base, cnsval_ssize_t disp)
{
    emitIns_ARX_R(ins, attr, reg, base, REG_NA, 1, disp);
}

//------------------------------------------------------------------------
// emitIns_S_R_I: emits the code for an instruction that takes a stack operand,
//                a register operand, and an immediate.
//
// Arguments:
//    ins       - The instruction being emitted
//    attr      - The emit attribute
//    varNum    - The varNum of the stack operand
//    offs      - The offset for the stack operand
//    reg       - The register operand
//    ival      - The immediate value
//
void emitter::emitIns_S_R_I(instruction ins, emitAttr attr, int varNum, int offs, regNumber reg, int ival)
{
    // This is only used for INS_vextracti128 and INS_vextractf128, and for these 'ival' must be 0 or 1.
    assert(ins == INS_vextracti128 || ins == INS_vextractf128);
    assert((ival == 0) || (ival == 1));
    instrDesc* id = emitNewInstrAmdCns(attr, 0, ival);

    id->idIns(ins);
    id->idInsFmt(IF_SWR_RRD_CNS);
    id->idReg1(reg);
    id->idAddr()->iiaLclVar.initLclVarAddr(varNum, offs);
#ifdef DEBUG
    id->idDebugOnlyInfo()->idVarRefOffs = emitVarRefOffs;
#endif

    UNATIVE_OFFSET sz = emitInsSizeSV(id, insCodeMR(ins), varNum, offs, ival);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_A_R_I(instruction ins, emitAttr attr, GenTreeIndir* indir, regNumber reg, int imm)
{
    assert((ins == INS_vextracti128) || (ins == INS_vextractf128));
    assert(attr == EA_32BYTE);
    assert(reg != REG_NA);

    instrDesc* id = emitNewInstrAmdCns(attr, indir->Offset(), imm);
    id->idIns(ins);
    id->idReg1(reg);
    emitHandleMemOp(indir, id, IF_AWR_RRD_CNS, ins);
    UNATIVE_OFFSET size = emitInsSizeAM(id, insCodeMR(ins), imm);
    id->idCodeSize(size);
    dispIns(id);
    emitCurIGsize += size;
}

void emitter::emitIns_AI_R(instruction ins, emitAttr attr, regNumber ireg, ssize_t disp)
{
    UNATIVE_OFFSET sz;
    instrDesc*     id = emitNewInstrAmd(attr, disp);
    insFormat      fmt;

    if (ireg == REG_NA)
    {
        fmt = emitInsModeFormat(ins, IF_ARD);
    }
    else
    {
        fmt = emitInsModeFormat(ins, IF_ARD_RRD);

        assert((CodeGen::instIsFP(ins) == false) && (EA_SIZE(attr) <= EA_8BYTE));
        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), ireg));

        id->idReg1(ireg);
    }

    id->idIns(ins);
    id->idInsFmt(fmt);

    id->idAddr()->iiaAddrMode.amBaseReg = REG_NA;
    id->idAddr()->iiaAddrMode.amIndxReg = REG_NA;

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeMR(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;

    emitAdjustStackDepthPushPop(ins);
}

void emitter::emitIns_I_ARR(instruction ins, emitAttr attr, int val, regNumber reg, regNumber rg2, int disp)
{
    assert((CodeGen::instIsFP(ins) == false) && (EA_SIZE(attr) <= EA_8BYTE));

#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(EA_SIZE(attr) < EA_8BYTE || !EA_IS_CNS_RELOC(attr));
#endif

    insFormat fmt;

    switch (ins)
    {
        case INS_rcl_N:
        case INS_rcr_N:
        case INS_rol_N:
        case INS_ror_N:
        case INS_shl_N:
        case INS_shr_N:
        case INS_sar_N:
            assert(val != 1);
            fmt = IF_ARW_SHF;
            val &= 0x7F;
            break;

        default:
            fmt = emitInsModeFormat(ins, IF_ARD_CNS);
            break;
    }

    UNATIVE_OFFSET sz;
    instrDesc*     id = emitNewInstrAmdCns(attr, disp, val);
    id->idIns(ins);
    id->idInsFmt(fmt);

    id->idAddr()->iiaAddrMode.amBaseReg = reg;
    id->idAddr()->iiaAddrMode.amIndxReg = rg2;
    id->idAddr()->iiaAddrMode.amScale   = emitter::OPSZ1;

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeMI(ins), val);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_ARR(instruction ins, emitAttr attr, regNumber reg, regNumber base, regNumber index, int disp)
{
    emitIns_R_ARX(ins, attr, reg, base, index, 1, disp);
}

void emitter::emitIns_ARR_R(instruction ins, emitAttr attr, regNumber reg, regNumber base, regNumber index, int disp)
{
    emitIns_ARX_R(ins, attr, reg, base, index, 1, disp);
}

void emitter::emitIns_I_ARX(
    instruction ins, emitAttr attr, int val, regNumber reg, regNumber rg2, unsigned mul, int disp)
{
    assert((CodeGen::instIsFP(ins) == false) && (EA_SIZE(attr) <= EA_8BYTE));

#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(EA_SIZE(attr) < EA_8BYTE || !EA_IS_CNS_RELOC(attr));
#endif

    insFormat fmt;

    switch (ins)
    {
        case INS_rcl_N:
        case INS_rcr_N:
        case INS_rol_N:
        case INS_ror_N:
        case INS_shl_N:
        case INS_shr_N:
        case INS_sar_N:
            assert(val != 1);
            fmt = IF_ARW_SHF;
            val &= 0x7F;
            break;

        default:
            fmt = emitInsModeFormat(ins, IF_ARD_CNS);
            break;
    }

    UNATIVE_OFFSET sz;
    instrDesc*     id = emitNewInstrAmdCns(attr, disp, val);

    id->idIns(ins);
    id->idInsFmt(fmt);

    id->idAddr()->iiaAddrMode.amBaseReg = reg;
    id->idAddr()->iiaAddrMode.amIndxReg = rg2;
    id->idAddr()->iiaAddrMode.amScale   = emitEncodeScale(mul);

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeMI(ins), val);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_ARX(
    instruction ins, emitAttr attr, regNumber reg, regNumber base, regNumber index, unsigned scale, int disp)
{
    assert(!CodeGen::instIsFP(ins) && (EA_SIZE(attr) <= EA_32BYTE) && (reg != REG_NA));
    noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg));

    if ((ins == INS_lea) && (reg == base) && (index == REG_NA) && (disp == 0))
    {
        // Maybe the emitter is not the common place for this optimization, but it's a better choke point
        // for all the emitIns(ins, tree), we would have to be analyzing at each call site
        //
        return;
    }

    UNATIVE_OFFSET sz;
    instrDesc*     id  = emitNewInstrAmd(attr, disp);
    insFormat      fmt = emitInsModeFormat(ins, IF_RRD_ARD);

    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idReg1(reg);

    id->idAddr()->iiaAddrMode.amBaseReg = base;
    id->idAddr()->iiaAddrMode.amIndxReg = index;
    id->idAddr()->iiaAddrMode.amScale   = emitEncodeScale(scale);

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeRM(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_ARX_R(
    instruction ins, emitAttr attr, regNumber reg, regNumber base, regNumber index, unsigned scale, cnsval_ssize_t disp)
{
    UNATIVE_OFFSET sz;
    instrDesc*     id = emitNewInstrAmd(attr, disp);
    insFormat      fmt;

    if (reg == REG_NA)
    {
        fmt = emitInsModeFormat(ins, IF_ARD);
    }
    else
    {
        fmt = emitInsModeFormat(ins, IF_ARD_RRD);

        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg));
        assert(!CodeGen::instIsFP(ins) && (EA_SIZE(attr) <= EA_32BYTE));

        id->idReg1(reg);
    }

    id->idIns(ins);
    id->idInsFmt(fmt);

    id->idAddr()->iiaAddrMode.amBaseReg = base;
    id->idAddr()->iiaAddrMode.amIndxReg = index;
    id->idAddr()->iiaAddrMode.amScale   = emitEncodeScale(scale);

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeMR(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;

    emitAdjustStackDepthPushPop(ins);
}

void emitter::emitIns_I_AX(instruction ins, emitAttr attr, int val, regNumber reg, unsigned mul, int disp)
{
    assert((CodeGen::instIsFP(ins) == false) && (EA_SIZE(attr) <= EA_8BYTE));

#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(EA_SIZE(attr) < EA_8BYTE || !EA_IS_CNS_RELOC(attr));
#endif

    insFormat fmt;

    switch (ins)
    {
        case INS_rcl_N:
        case INS_rcr_N:
        case INS_rol_N:
        case INS_ror_N:
        case INS_shl_N:
        case INS_shr_N:
        case INS_sar_N:
            assert(val != 1);
            fmt = IF_ARW_SHF;
            val &= 0x7F;
            break;

        default:
            fmt = emitInsModeFormat(ins, IF_ARD_CNS);
            break;
    }

    UNATIVE_OFFSET sz;
    instrDesc*     id = emitNewInstrAmdCns(attr, disp, val);
    id->idIns(ins);
    id->idInsFmt(fmt);

    id->idAddr()->iiaAddrMode.amBaseReg = REG_NA;
    id->idAddr()->iiaAddrMode.amIndxReg = reg;
    id->idAddr()->iiaAddrMode.amScale   = emitEncodeScale(mul);

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeMI(ins), val);
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_AX(instruction ins, emitAttr attr, regNumber ireg, regNumber reg, unsigned mul, int disp)
{
    assert((CodeGen::instIsFP(ins) == false) && (EA_SIZE(attr) <= EA_8BYTE) && (ireg != REG_NA));
    noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), ireg));

    UNATIVE_OFFSET sz;
    instrDesc*     id  = emitNewInstrAmd(attr, disp);
    insFormat      fmt = emitInsModeFormat(ins, IF_RRD_ARD);

    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idReg1(ireg);

    id->idAddr()->iiaAddrMode.amBaseReg = REG_NA;
    id->idAddr()->iiaAddrMode.amIndxReg = reg;
    id->idAddr()->iiaAddrMode.amScale   = emitEncodeScale(mul);

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeRM(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_AX_R(instruction ins, emitAttr attr, regNumber ireg, regNumber reg, unsigned mul, int disp)
{
    UNATIVE_OFFSET sz;
    instrDesc*     id = emitNewInstrAmd(attr, disp);
    insFormat      fmt;

    if (ireg == REG_NA)
    {
        fmt = emitInsModeFormat(ins, IF_ARD);
    }
    else
    {
        fmt = emitInsModeFormat(ins, IF_ARD_RRD);
        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), ireg));
        assert((CodeGen::instIsFP(ins) == false) && (EA_SIZE(attr) <= EA_8BYTE));

        id->idReg1(ireg);
    }

    id->idIns(ins);
    id->idInsFmt(fmt);

    id->idAddr()->iiaAddrMode.amBaseReg = REG_NA;
    id->idAddr()->iiaAddrMode.amIndxReg = reg;
    id->idAddr()->iiaAddrMode.amScale   = emitEncodeScale(mul);

    assert(emitGetInsAmdAny(id) == disp); // make sure "disp" is stored properly

    sz = emitInsSizeAM(id, insCodeMR(ins));
    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;

    emitAdjustStackDepthPushPop(ins);
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_I: emits the code for an instruction that takes a register operand, an immediate operand
//                     and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    ival      -- The immediate value
//
// Notes:
//    This will handle the required register copy if 'op1Reg' and 'targetReg' are not the same, and
//    the 3-operand format is not available.
//    This is not really SIMD-specific, but is currently only used in that context, as that's
//    where we frequently need to handle the case of generating 3-operand or 2-operand forms
//    depending on what target ISA is supported.
//
void emitter::emitIns_SIMD_R_R_I(instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, int ival)
{
    if (UseVEXEncoding() || IsDstSrcImmAvxInstruction(ins))
    {
        emitIns_R_R_I(ins, attr, targetReg, op1Reg, ival);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_I(ins, attr, targetReg, ival);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_A: emits the code for a SIMD instruction that takes a register operand, a GenTreeIndir address,
//                     and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    indir     -- The GenTreeIndir used for the memory address
//
void emitter::emitIns_SIMD_R_R_A(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, GenTreeIndir* indir)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_A(ins, attr, targetReg, op1Reg, indir);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_A(ins, attr, targetReg, indir);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_AR: emits the code for a SIMD instruction that takes a register operand, a base memory register,
//                      and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    base      -- The base register used for the memory address
//    offset    -- The memory offset
//
void emitter::emitIns_SIMD_R_R_AR(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber base, int offset)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_AR(ins, attr, targetReg, op1Reg, base, offset);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_AR(ins, attr, targetReg, base, offset);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_C: emits the code for a SIMD instruction that takes a register operand, a field handle + offset,
//                     and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    fldHnd    -- The CORINFO_FIELD_HANDLE used for the memory address
//    offs      -- The offset added to the memory address from fldHnd
//
void emitter::emitIns_SIMD_R_R_C(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, CORINFO_FIELD_HANDLE fldHnd, int offs)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_C(ins, attr, targetReg, op1Reg, fldHnd, offs);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_C(ins, attr, targetReg, fldHnd, offs);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_R: emits the code for a SIMD instruction that takes two register operands, and that returns a
//                     value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op2Reg    -- The register of the second operand
//
void emitter::emitIns_SIMD_R_R_R(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op2Reg)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_R(ins, attr, targetReg, op1Reg, op2Reg);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            // Ensure we aren't overwriting op2
            assert(op2Reg != targetReg);

            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_R(ins, attr, targetReg, op2Reg);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_S: emits the code for a SIMD instruction that takes a register operand, a variable index + offset,
//                     and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    varx      -- The variable index used for the memory address
//    offs      -- The offset added to the memory address from varx
//
void emitter::emitIns_SIMD_R_R_S(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, int varx, int offs)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_S(ins, attr, targetReg, op1Reg, varx, offs);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_S(ins, attr, targetReg, varx, offs);
    }
}

#ifdef FEATURE_HW_INTRINSICS
//------------------------------------------------------------------------
// emitIns_SIMD_R_R_A_I: emits the code for a SIMD instruction that takes a register operand, a GenTreeIndir address,
//                       an immediate operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    indir     -- The GenTreeIndir used for the memory address
//    ival      -- The immediate value
//
void emitter::emitIns_SIMD_R_R_A_I(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, GenTreeIndir* indir, int ival)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_A_I(ins, attr, targetReg, op1Reg, indir, ival, IF_RWR_RRD_ARD_CNS);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_A_I(ins, attr, targetReg, indir, ival);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_AR_I: emits the code for a SIMD instruction that takes a register operand, a base memory register,
//                        an immediate operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    base      -- The base register used for the memory address
//    ival      -- The immediate value
//
void emitter::emitIns_SIMD_R_R_AR_I(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber base, int ival)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_AR_I(ins, attr, targetReg, op1Reg, base, 0, ival);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_AR_I(ins, attr, targetReg, base, 0, ival);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_C_I: emits the code for a SIMD instruction that takes a register operand, a field handle + offset,
//                       an immediate operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    fldHnd    -- The CORINFO_FIELD_HANDLE used for the memory address
//    offs      -- The offset added to the memory address from fldHnd
//    ival      -- The immediate value
//
void emitter::emitIns_SIMD_R_R_C_I(instruction          ins,
                                   emitAttr             attr,
                                   regNumber            targetReg,
                                   regNumber            op1Reg,
                                   CORINFO_FIELD_HANDLE fldHnd,
                                   int                  offs,
                                   int                  ival)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_C_I(ins, attr, targetReg, op1Reg, fldHnd, offs, ival);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_C_I(ins, attr, targetReg, fldHnd, offs, ival);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_R_I: emits the code for a SIMD instruction that takes two register operands, an immediate operand,
//                       and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op2Reg    -- The register of the second operand
//    ival      -- The immediate value
//
void emitter::emitIns_SIMD_R_R_R_I(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op2Reg, int ival)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_R_I(ins, attr, targetReg, op1Reg, op2Reg, ival);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            // Ensure we aren't overwriting op2
            assert(op2Reg != targetReg);

            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_R_I(ins, attr, targetReg, op2Reg, ival);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_S_I: emits the code for a SIMD instruction that takes a register operand, a variable index + offset,
//                       an imediate operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    varx      -- The variable index used for the memory address
//    offs      -- The offset added to the memory address from varx
//    ival      -- The immediate value
//
void emitter::emitIns_SIMD_R_R_S_I(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, int varx, int offs, int ival)
{
    if (UseVEXEncoding())
    {
        emitIns_R_R_S_I(ins, attr, targetReg, op1Reg, varx, offs, ival);
    }
    else
    {
        if (op1Reg != targetReg)
        {
            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_S_I(ins, attr, targetReg, varx, offs, ival);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_R_A: emits the code for a SIMD instruction that takes two register operands, a GenTreeIndir address,
//                       and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op2Reg    -- The register of the second operand
//    indir     -- The GenTreeIndir used for the memory address
//
void emitter::emitIns_SIMD_R_R_R_A(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op2Reg, GenTreeIndir* indir)
{
    assert(IsFMAInstruction(ins));
    assert(UseVEXEncoding());

    if (op1Reg != targetReg)
    {
        // Ensure we aren't overwriting op2
        assert(op2Reg != targetReg);

        emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
    }

    emitIns_R_R_A(ins, attr, targetReg, op2Reg, indir);
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_R_AR: emits the code for a SIMD instruction that takes two register operands, a base memory
//                        register, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operands
//    op2Reg    -- The register of the second operand
//    base      -- The base register used for the memory address
//
void emitter::emitIns_SIMD_R_R_R_AR(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op2Reg, regNumber base)
{
    assert(IsFMAInstruction(ins));
    assert(UseVEXEncoding());

    if (op1Reg != targetReg)
    {
        // Ensure we aren't overwriting op2
        assert(op2Reg != targetReg);

        emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
    }

    emitIns_R_R_AR(ins, attr, targetReg, op2Reg, base, 0);
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_R_C: emits the code for a SIMD instruction that takes two register operands, a field handle +
//                       offset, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op2Reg    -- The register of the second operand
//    fldHnd    -- The CORINFO_FIELD_HANDLE used for the memory address
//    offs      -- The offset added to the memory address from fldHnd
//
void emitter::emitIns_SIMD_R_R_R_C(instruction          ins,
                                   emitAttr             attr,
                                   regNumber            targetReg,
                                   regNumber            op1Reg,
                                   regNumber            op2Reg,
                                   CORINFO_FIELD_HANDLE fldHnd,
                                   int                  offs)
{
    assert(IsFMAInstruction(ins));
    assert(UseVEXEncoding());

    if (op1Reg != targetReg)
    {
        // Ensure we aren't overwriting op2
        assert(op2Reg != targetReg);

        emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
    }

    emitIns_R_R_C(ins, attr, targetReg, op2Reg, fldHnd, offs);
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_R_R: emits the code for a SIMD instruction that takes three register operands, and that returns a
//                     value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op2Reg    -- The register of the second operand
//    op3Reg    -- The register of the second operand
//
void emitter::emitIns_SIMD_R_R_R_R(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op2Reg, regNumber op3Reg)
{
    if (IsFMAInstruction(ins))
    {
        assert(UseVEXEncoding());

        if (op1Reg != targetReg)
        {
            // Ensure we aren't overwriting op2 or op3

            assert(op2Reg != targetReg);
            assert(op3Reg != targetReg);

            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }

        emitIns_R_R_R(ins, attr, targetReg, op2Reg, op3Reg);
    }
    else if (UseVEXEncoding())
    {
        assert(isAvxBlendv(ins) || isSse41Blendv(ins));

        // convert SSE encoding of SSE4.1 instructions to VEX encoding
        switch (ins)
        {
            case INS_blendvps:
                ins = INS_vblendvps;
                break;
            case INS_blendvpd:
                ins = INS_vblendvpd;
                break;
            case INS_pblendvb:
                ins = INS_vpblendvb;
                break;
            default:
                break;
        }
        emitIns_R_R_R_R(ins, attr, targetReg, op1Reg, op2Reg, op3Reg);
    }
    else
    {
        assert(isSse41Blendv(ins));
        // SSE4.1 blendv* hardcode the mask vector (op3) in XMM0
        if (op3Reg != REG_XMM0)
        {
            // Ensure we aren't overwriting op1 or op2
            assert(op1Reg != REG_XMM0);
            assert(op2Reg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, REG_XMM0, op3Reg);
        }
        if (op1Reg != targetReg)
        {
            // Ensure we aren't overwriting op2 or oop3 (which should be REG_XMM0)
            assert(op2Reg != targetReg);
            assert(targetReg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }
        emitIns_R_R(ins, attr, targetReg, op2Reg);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_R_S: emits the code for a SIMD instruction that takes two register operands, a variable index +
//                       offset, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op2Reg    -- The register of the second operand
//    varx      -- The variable index used for the memory address
//    offs      -- The offset added to the memory address from varx
//
void emitter::emitIns_SIMD_R_R_R_S(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op2Reg, int varx, int offs)
{
    assert(IsFMAInstruction(ins));
    assert(UseVEXEncoding());

    if (op1Reg != targetReg)
    {
        // Ensure we aren't overwriting op2
        assert(op2Reg != targetReg);

        emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
    }

    emitIns_R_R_S(ins, attr, targetReg, op2Reg, varx, offs);
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_A_R: emits the code for a SIMD instruction that takes a register operand, a GenTreeIndir address,
//                       another register operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op3Reg    -- The register of the third operand
//    indir     -- The GenTreeIndir used for the memory address
//
void emitter::emitIns_SIMD_R_R_A_R(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op3Reg, GenTreeIndir* indir)
{
    if (UseVEXEncoding())
    {
        assert(isAvxBlendv(ins) || isSse41Blendv(ins));

        // convert SSE encoding of SSE4.1 instructions to VEX encoding
        switch (ins)
        {
            case INS_blendvps:
            {
                ins = INS_vblendvps;
                break;
            }

            case INS_blendvpd:
            {
                ins = INS_vblendvpd;
                break;
            }

            case INS_pblendvb:
            {
                ins = INS_vpblendvb;
                break;
            }

            default:
            {
                break;
            }
        }

        emitIns_R_R_A_R(ins, attr, targetReg, op1Reg, op3Reg, indir);
    }
    else
    {
        assert(isSse41Blendv(ins));

        // SSE4.1 blendv* hardcode the mask vector (op3) in XMM0
        if (op3Reg != REG_XMM0)
        {
            // Ensure we aren't overwriting op1
            assert(op1Reg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, REG_XMM0, op3Reg);
        }
        if (op1Reg != targetReg)
        {
            // Ensure we aren't overwriting op3 (which should be REG_XMM0)
            assert(targetReg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }

        emitIns_R_A(ins, attr, targetReg, indir);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_AR_R: emits the code for a SIMD instruction that takes a register operand, a base memory
//                        register, another register operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operands
//    op3Reg    -- The register of the third operand
//    base      -- The base register used for the memory address
//
void emitter::emitIns_SIMD_R_R_AR_R(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op3Reg, regNumber base)
{
    if (UseVEXEncoding())
    {
        assert(isAvxBlendv(ins) || isSse41Blendv(ins));

        // convert SSE encoding of SSE4.1 instructions to VEX encoding
        switch (ins)
        {
            case INS_blendvps:
            {
                ins = INS_vblendvps;
                break;
            }

            case INS_blendvpd:
            {
                ins = INS_vblendvpd;
                break;
            }

            case INS_pblendvb:
            {
                ins = INS_vpblendvb;
                break;
            }

            default:
            {
                break;
            }
        }

        emitIns_R_R_AR_R(ins, attr, targetReg, op1Reg, op3Reg, base, 0);
    }
    else
    {
        assert(isSse41Blendv(ins));

        // SSE4.1 blendv* hardcode the mask vector (op3) in XMM0
        if (op3Reg != REG_XMM0)
        {
            // Ensure we aren't overwriting op1
            assert(op1Reg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, REG_XMM0, op3Reg);
        }
        if (op1Reg != targetReg)
        {
            // Ensure we aren't overwriting op3 (which should be REG_XMM0)
            assert(targetReg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }

        emitIns_R_AR(ins, attr, targetReg, base, 0);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_C_R: emits the code for a SIMD instruction that takes a register operand, a field handle +
//                       offset,  another register operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op3Reg    -- The register of the third operand
//    fldHnd    -- The CORINFO_FIELD_HANDLE used for the memory address
//    offs      -- The offset added to the memory address from fldHnd
//
void emitter::emitIns_SIMD_R_R_C_R(instruction          ins,
                                   emitAttr             attr,
                                   regNumber            targetReg,
                                   regNumber            op1Reg,
                                   regNumber            op3Reg,
                                   CORINFO_FIELD_HANDLE fldHnd,
                                   int                  offs)
{
    if (UseVEXEncoding())
    {
        assert(isAvxBlendv(ins) || isSse41Blendv(ins));

        // convert SSE encoding of SSE4.1 instructions to VEX encoding
        switch (ins)
        {
            case INS_blendvps:
            {
                ins = INS_vblendvps;
                break;
            }

            case INS_blendvpd:
            {
                ins = INS_vblendvpd;
                break;
            }

            case INS_pblendvb:
            {
                ins = INS_vpblendvb;
                break;
            }

            default:
            {
                break;
            }
        }

        emitIns_R_R_C_R(ins, attr, targetReg, op1Reg, op3Reg, fldHnd, offs);
    }
    else
    {
        assert(isSse41Blendv(ins));

        // SSE4.1 blendv* hardcode the mask vector (op3) in XMM0
        if (op3Reg != REG_XMM0)
        {
            // Ensure we aren't overwriting op1
            assert(op1Reg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, REG_XMM0, op3Reg);
        }
        if (op1Reg != targetReg)
        {
            // Ensure we aren't overwriting op3 (which should be REG_XMM0)
            assert(targetReg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }

        emitIns_R_C(ins, attr, targetReg, fldHnd, offs);
    }
}

//------------------------------------------------------------------------
// emitIns_SIMD_R_R_S_R: emits the code for a SIMD instruction that takes a register operand, a variable index +
//                       offset, another register operand, and that returns a value in register
//
// Arguments:
//    ins       -- The instruction being emitted
//    attr      -- The emit attribute
//    targetReg -- The target register
//    op1Reg    -- The register of the first operand
//    op3Reg    -- The register of the third operand
//    varx      -- The variable index used for the memory address
//    offs      -- The offset added to the memory address from varx
//
void emitter::emitIns_SIMD_R_R_S_R(
    instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg, regNumber op3Reg, int varx, int offs)
{
    if (UseVEXEncoding())
    {
        assert(isAvxBlendv(ins) || isSse41Blendv(ins));

        // convert SSE encoding of SSE4.1 instructions to VEX encoding
        switch (ins)
        {
            case INS_blendvps:
            {
                ins = INS_vblendvps;
                break;
            }

            case INS_blendvpd:
            {
                ins = INS_vblendvpd;
                break;
            }

            case INS_pblendvb:
            {
                ins = INS_vpblendvb;
                break;
            }

            default:
            {
                break;
            }
        }

        emitIns_R_R_S_R(ins, attr, targetReg, op1Reg, op3Reg, varx, offs);
    }
    else
    {
        assert(isSse41Blendv(ins));

        // SSE4.1 blendv* hardcode the mask vector (op3) in XMM0
        if (op3Reg != REG_XMM0)
        {
            // Ensure we aren't overwriting op1
            assert(op1Reg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, REG_XMM0, op3Reg);
        }
        if (op1Reg != targetReg)
        {
            // Ensure we aren't overwriting op3 (which should be REG_XMM0)
            assert(targetReg != REG_XMM0);

            emitIns_R_R(INS_movaps, attr, targetReg, op1Reg);
        }

        emitIns_R_S(ins, attr, targetReg, varx, offs);
    }
}
#endif // FEATURE_HW_INTRINSICS

/*****************************************************************************
 *
 *  The following add instructions referencing stack-based local variables.
 */

void emitter::emitIns_S(instruction ins, emitAttr attr, int varx, int offs)
{
    UNATIVE_OFFSET sz;
    instrDesc*     id  = emitNewInstr(attr);
    insFormat      fmt = emitInsModeFormat(ins, IF_SRD);

    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idAddr()->iiaLclVar.initLclVarAddr(varx, offs);

    sz = emitInsSizeSV(id, insCodeMR(ins), varx, offs);
    id->idCodeSize(sz);

#ifdef DEBUG
    id->idDebugOnlyInfo()->idVarRefOffs = emitVarRefOffs;
#endif
    dispIns(id);
    emitCurIGsize += sz;

    emitAdjustStackDepthPushPop(ins);
}

void emitter::emitIns_S_R(instruction ins, emitAttr attr, regNumber ireg, int varx, int offs)
{
    UNATIVE_OFFSET sz;
    instrDesc*     id  = emitNewInstr(attr);
    insFormat      fmt = emitInsModeFormat(ins, IF_SRD_RRD);

    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idReg1(ireg);
    id->idAddr()->iiaLclVar.initLclVarAddr(varx, offs);

    sz = emitInsSizeSV(id, insCodeMR(ins), varx, offs);

#ifdef TARGET_X86
    if (attr == EA_1BYTE)
    {
        assert(isByteReg(ireg));
    }
#endif

    id->idCodeSize(sz);
#ifdef DEBUG
    id->idDebugOnlyInfo()->idVarRefOffs = emitVarRefOffs;
#endif
    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_R_S(instruction ins, emitAttr attr, regNumber ireg, int varx, int offs)
{
    emitAttr size = EA_SIZE(attr);
    noway_assert(emitVerifyEncodable(ins, size, ireg));

    UNATIVE_OFFSET sz;
    instrDesc*     id  = emitNewInstr(attr);
    insFormat      fmt = emitInsModeFormat(ins, IF_RRD_SRD);

    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idReg1(ireg);
    id->idAddr()->iiaLclVar.initLclVarAddr(varx, offs);

    sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs);
    id->idCodeSize(sz);
#ifdef DEBUG
    id->idDebugOnlyInfo()->idVarRefOffs = emitVarRefOffs;
#endif
    dispIns(id);
    emitCurIGsize += sz;
}

void emitter::emitIns_S_I(instruction ins, emitAttr attr, int varx, int offs, int val)
{
#ifdef TARGET_AMD64
    // mov reg, imm64 is the only opcode which takes a full 8 byte immediate
    // all other opcodes take a sign-extended 4-byte immediate
    noway_assert(EA_SIZE(attr) < EA_8BYTE || !EA_IS_CNS_RELOC(attr));
#endif

    insFormat fmt;

    switch (ins)
    {
        case INS_rcl_N:
        case INS_rcr_N:
        case INS_rol_N:
        case INS_ror_N:
        case INS_shl_N:
        case INS_shr_N:
        case INS_sar_N:
            assert(val != 1);
            fmt = IF_SRW_SHF;
            val &= 0x7F;
            break;

        default:
            fmt = emitInsModeFormat(ins, IF_SRD_CNS);
            break;
    }

    instrDesc* id = emitNewInstrCns(attr, val);
    id->idIns(ins);
    id->idInsFmt(fmt);
    id->idAddr()->iiaLclVar.initLclVarAddr(varx, offs);

    UNATIVE_OFFSET sz = emitInsSizeSV(id, insCodeMI(ins), varx, offs, val);
    id->idCodeSize(sz);
#ifdef DEBUG
    id->idDebugOnlyInfo()->idVarRefOffs = emitVarRefOffs;
#endif
    dispIns(id);
    emitCurIGsize += sz;
}

/*****************************************************************************
 *
 *  Record that a jump instruction uses the short encoding
 *
 */
void emitter::emitSetShortJump(instrDescJmp* id)
{
    if (id->idjKeepLong)
    {
        return;
    }

    id->idjShort = true;
}

/*****************************************************************************
 *
 *  Add a jmp instruction.
 *  When dst is NULL, instrCount specifies number of instructions
 *       to jump: positive is forward, negative is backward.
 */

void emitter::emitIns_J(instruction ins, BasicBlock* dst, int instrCount /* = 0 */)
{
    assert(false);

}

#if !FEATURE_FIXED_OUT_ARGS

//------------------------------------------------------------------------
// emitAdjustStackDepthPushPop: Adjust the current and maximum stack depth.
//
// Arguments:
//    ins - the instruction. Only INS_push and INS_pop adjust the stack depth.
//
// Notes:
//    1. Alters emitCurStackLvl and possibly emitMaxStackDepth.
//    2. emitCntStackDepth must be set (0 in prolog/epilog, one DWORD elsewhere)
//
void emitter::emitAdjustStackDepthPushPop(instruction ins)
{
    if (ins == INS_push)
    {
        emitCurStackLvl += emitCntStackDepth;

        if (emitMaxStackDepth < emitCurStackLvl)
        {
            JITDUMP("Upping emitMaxStackDepth from %d to %d\n", emitMaxStackDepth, emitCurStackLvl);
            emitMaxStackDepth = emitCurStackLvl;
        }
    }
    else if (ins == INS_pop)
    {
        emitCurStackLvl -= emitCntStackDepth;
        assert((int)emitCurStackLvl >= 0);
    }
}

//------------------------------------------------------------------------
// emitAdjustStackDepth: Adjust the current and maximum stack depth.
//
// Arguments:
//    ins - the instruction. Only INS_add and INS_sub adjust the stack depth.
//          It is assumed that the add/sub is on the stack pointer.
//    val - the number of bytes to add to or subtract from the stack pointer.
//
// Notes:
//    1. Alters emitCurStackLvl and possibly emitMaxStackDepth.
//    2. emitCntStackDepth must be set (0 in prolog/epilog, one DWORD elsewhere)
//
void emitter::emitAdjustStackDepth(instruction ins, ssize_t val)
{
    // If we're in the prolog or epilog, or otherwise not tracking the stack depth, just return.
    if (emitCntStackDepth == 0)
        return;

    if (ins == INS_sub)
    {
        S_UINT32 newStackLvl(emitCurStackLvl);
        newStackLvl += S_UINT32(val);
        noway_assert(!newStackLvl.IsOverflow());

        emitCurStackLvl = newStackLvl.Value();

        if (emitMaxStackDepth < emitCurStackLvl)
        {
            JITDUMP("Upping emitMaxStackDepth from %d to %d\n", emitMaxStackDepth, emitCurStackLvl);
            emitMaxStackDepth = emitCurStackLvl;
        }
    }
    else if (ins == INS_add)
    {
        S_UINT32 newStackLvl = S_UINT32(emitCurStackLvl) - S_UINT32(val);
        noway_assert(!newStackLvl.IsOverflow());

        emitCurStackLvl = newStackLvl.Value();
    }
}

#endif // EMIT_TRACK_STACK_DEPTH

/*****************************************************************************
 *
 *  Add a call instruction (direct or indirect).
 *      argSize<0 means that the caller will pop the arguments
 *
 * The other arguments are interpreted depending on callType as shown:
 * Unless otherwise specified, ireg,xreg,xmul,disp should have default values.
 *
 * EC_FUNC_TOKEN       : addr is the method address
 * EC_FUNC_TOKEN_INDIR : addr is the indirect method address
 * EC_FUNC_ADDR        : addr is the absolute address of the function
 * EC_FUNC_VIRTUAL     : "call [ireg+disp]"
 *
 * If callType is one of these emitCallTypes, addr has to be NULL.
 * EC_INDIR_R          : "call ireg".
 * EC_INDIR_SR         : "call lcl<disp>" (eg. call [ebp-8]).
 * EC_INDIR_C          : "call clsVar<disp>" (eg. call [clsVarAddr])
 * EC_INDIR_ARD        : "call [ireg+xreg*xmul+disp]"
 *
 */

// clang-format off
void emitter::emitIns_Call(EmitCallType          callType,
                           CORINFO_METHOD_HANDLE methHnd,
                           INDEBUG_LDISASM_COMMA(CORINFO_SIG_INFO* sigInfo) // used to report call sites to the EE
                           void*                 addr,
                           ssize_t               argSize,
                           emitAttr              retSize
                           MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(emitAttr secondRetSize),
                           VARSET_VALARG_TP      ptrVars,
                           regMaskTP             gcrefRegs,
                           regMaskTP             byrefRegs,
                           IL_OFFSETX            ilOffset, // = BAD_IL_OFFSET
                           regNumber             ireg,     // = REG_NA
                           regNumber             xreg,     // = REG_NA
                           unsigned              xmul,     // = 0
                           ssize_t               disp,     // = 0
                           bool                  isJump)   // = false
// clang-format on
{
    /* Sanity check the arguments depending on callType */

    assert(callType < EC_COUNT);
    assert((callType != EC_FUNC_TOKEN && callType != EC_FUNC_TOKEN_INDIR && callType != EC_FUNC_ADDR) ||
           (ireg == REG_NA && xreg == REG_NA && xmul == 0 && disp == 0));
    assert(callType != EC_FUNC_VIRTUAL || (ireg < REG_COUNT && xreg == REG_NA && xmul == 0));
    assert(callType < EC_INDIR_R || callType == EC_INDIR_ARD || callType == EC_INDIR_C || addr == nullptr);
    assert(callType != EC_INDIR_R || (ireg < REG_COUNT && xreg == REG_NA && xmul == 0 && disp == 0));
    assert(callType != EC_INDIR_SR ||
           (ireg == REG_NA && xreg == REG_NA && xmul == 0 && disp < (int)emitComp->lvaCount));
    assert(callType != EC_INDIR_C || (ireg == REG_NA && xreg == REG_NA && xmul == 0 && disp != 0));

    // Our stack level should be always greater than the bytes of arguments we push. Just
    // a sanity test.
    assert((unsigned)abs((signed)argSize) <= codeGen->genStackLevel);

    // Trim out any callee-trashed registers from the live set.
    regMaskTP savedSet = emitGetGCRegsSavedOrModified(methHnd);
    gcrefRegs &= savedSet;
    byrefRegs &= savedSet;

#ifdef DEBUG
    if (EMIT_GC_VERBOSE)
    {
        printf("\t\t\t\t\t\t\tCall: GCvars=%s ", VarSetOps::ToString(emitComp, ptrVars));
        dumpConvertedVarSet(emitComp, ptrVars);
        printf(", gcrefRegs=");
        printRegMaskInt(gcrefRegs);
        emitDispRegSet(gcrefRegs);
        printf(", byrefRegs=");
        printRegMaskInt(byrefRegs);
        emitDispRegSet(byrefRegs);
        printf("\n");
    }
#endif

    /* Managed RetVal: emit sequence point for the call */
    if (emitComp->opts.compDbgInfo && ilOffset != BAD_IL_OFFSET)
    {
        codeGen->genIPmappingAdd(ilOffset, false);
    }

    /*
        We need to allocate the appropriate instruction descriptor based
        on whether this is a direct/indirect call, and whether we need to
        record an updated set of live GC variables.

        The stats for a ton of classes is as follows:

            Direct call w/o  GC vars        220,216
            Indir. call w/o  GC vars        144,781

            Direct call with GC vars          9,440
            Indir. call with GC vars          5,768
     */

    instrDesc* id;

    assert(argSize % REGSIZE_BYTES == 0);
    int argCnt = (int)(argSize / (int)REGSIZE_BYTES); // we need a signed-divide

    if (callType >= EC_FUNC_VIRTUAL)
    {
        /* Indirect call, virtual calls */

        assert(callType == EC_FUNC_VIRTUAL || callType == EC_INDIR_R || callType == EC_INDIR_SR ||
               callType == EC_INDIR_C || callType == EC_INDIR_ARD);

        id = emitNewInstrCallInd(argCnt, disp, ptrVars, gcrefRegs, byrefRegs,
                                 retSize MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(secondRetSize));
    }
    else
    {
        // Helper/static/nonvirtual/function calls (direct or through handle),
        // and calls to an absolute addr.

        assert(callType == EC_FUNC_TOKEN || callType == EC_FUNC_TOKEN_INDIR || callType == EC_FUNC_ADDR);

        id = emitNewInstrCallDir(argCnt, ptrVars, gcrefRegs, byrefRegs,
                                 retSize MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(secondRetSize));
    }

    /* Update the emitter's live GC ref sets */

    VarSetOps::Assign(emitComp, emitThisGCrefVars, ptrVars);
    emitThisGCrefRegs = gcrefRegs;
    emitThisByrefRegs = byrefRegs;

    /* Set the instruction - special case jumping a function */
    instruction ins = INS_call;

    if (isJump)
    {
        assert(callType == EC_FUNC_TOKEN || callType == EC_FUNC_TOKEN_INDIR || callType == EC_INDIR_ARD);
        if (callType == EC_FUNC_TOKEN)
        {
            ins = INS_l_jmp;
        }
        else
        {
            ins = INS_i_jmp;
        }
    }
    id->idIns(ins);

    id->idSetIsNoGC(emitNoGChelper(methHnd));

    UNATIVE_OFFSET sz;

    // Record the address: method, indirection, or funcptr
    if (callType >= EC_FUNC_VIRTUAL)
    {
        // This is an indirect call (either a virtual call or func ptr call)

        switch (callType)
        {
            case EC_INDIR_C:
                // Indirect call using an absolute code address.
                // Must be marked as relocatable and is done at the
                // branch target location.
                goto CALL_ADDR_MODE;

            case EC_INDIR_R: // the address is in a register

                id->idSetIsCallRegPtr();

                FALLTHROUGH;

            case EC_INDIR_ARD: // the address is an indirection

                goto CALL_ADDR_MODE;

            case EC_INDIR_SR: // the address is in a lcl var

                id->idInsFmt(IF_SRD);
                // disp is really a lclVarNum
                noway_assert((unsigned)disp == (size_t)disp);
                id->idAddr()->iiaLclVar.initLclVarAddr((unsigned)disp, 0);
                sz = emitInsSizeSV(id, insCodeMR(INS_call), (unsigned)disp, 0);

                break;

            case EC_FUNC_VIRTUAL:

            CALL_ADDR_MODE:

                // fall-through

                // The function is "ireg" if id->idIsCallRegPtr(),
                // else [ireg+xmul*xreg+disp]

                id->idInsFmt(IF_ARD);

                id->idAddr()->iiaAddrMode.amBaseReg = ireg;
                id->idAddr()->iiaAddrMode.amIndxReg = xreg;
                id->idAddr()->iiaAddrMode.amScale   = xmul ? emitEncodeScale(xmul) : emitter::OPSZ1;

                sz = emitInsSizeAM(id, insCodeMR(INS_call));

                if (ireg == REG_NA && xreg == REG_NA)
                {
                    if (codeGen->genCodeIndirAddrNeedsReloc(disp))
                    {
                        id->idSetIsDspReloc();
                    }
#ifdef TARGET_AMD64
                    else
                    {
                        // An absolute indir address that doesn't need reloc should fit within 32-bits
                        // to be encoded as offset relative to zero.  This addr mode requires an extra
                        // SIB byte
                        noway_assert(static_cast<int>(reinterpret_cast<intptr_t>(addr)) == (size_t)addr);
                        sz++;
                    }
#endif // TARGET_AMD64
                }

                break;

            default:
                NO_WAY("unexpected instruction");
                break;
        }
    }
    else if (callType == EC_FUNC_TOKEN_INDIR)
    {
        /* "call [method_addr]" */

        assert(addr != nullptr);

        id->idInsFmt(IF_METHPTR);
        id->idAddr()->iiaAddr = (BYTE*)addr;
        sz                    = 6;

        // Since this is an indirect call through a pointer and we don't
        // currently pass in emitAttr into this function, we query codegen
        // whether addr needs a reloc.
        if (codeGen->genCodeIndirAddrNeedsReloc((size_t)addr))
        {
            id->idSetIsDspReloc();
        }
#ifdef TARGET_AMD64
        else
        {
            // An absolute indir address that doesn't need reloc should fit within 32-bits
            // to be encoded as offset relative to zero.  This addr mode requires an extra
            // SIB byte
            noway_assert(static_cast<int>(reinterpret_cast<intptr_t>(addr)) == (size_t)addr);
            sz++;
        }
#endif // TARGET_AMD64
    }
    else
    {
        /* This is a simple direct call: "call helper/method/addr" */

        assert(callType == EC_FUNC_TOKEN || callType == EC_FUNC_ADDR);

        assert(addr != nullptr);

        id->idInsFmt(IF_METHOD);
        sz = 5;

        id->idAddr()->iiaAddr = (BYTE*)addr;

        if (callType == EC_FUNC_ADDR)
        {
            id->idSetIsCallAddr();
        }

        // Direct call to a method and no addr indirection is needed.
        if (codeGen->genCodeAddrNeedsReloc((size_t)addr))
        {
            id->idSetIsDspReloc();
        }
    }

#ifdef DEBUG
    if (emitComp->verbose && 0)
    {
        if (id->idIsLargeCall())
        {
            if (callType >= EC_FUNC_VIRTUAL)
            {
                printf("[%02u] Rec call GC vars = %s\n", id->idDebugOnlyInfo()->idNum,
                       VarSetOps::ToString(emitComp, ((instrDescCGCA*)id)->idcGCvars));
            }
            else
            {
                printf("[%02u] Rec call GC vars = %s\n", id->idDebugOnlyInfo()->idNum,
                       VarSetOps::ToString(emitComp, ((instrDescCGCA*)id)->idcGCvars));
            }
        }
    }

    id->idDebugOnlyInfo()->idMemCookie = (size_t)methHnd; // method token
    id->idDebugOnlyInfo()->idCallSig   = sigInfo;
#endif // DEBUG

#ifdef LATE_DISASM
    if (addr != nullptr)
    {
        codeGen->getDisAssembler().disSetMethod((size_t)addr, methHnd);
    }
#endif // LATE_DISASM

    id->idCodeSize(sz);

    dispIns(id);
    emitCurIGsize += sz;

#if !FEATURE_FIXED_OUT_ARGS

    /* The call will pop the arguments */

    if (emitCntStackDepth && argSize > 0)
    {
        noway_assert((ssize_t)emitCurStackLvl >= argSize);
        emitCurStackLvl -= (int)argSize;
        assert((int)emitCurStackLvl >= 0);
    }

#endif // !FEATURE_FIXED_OUT_ARGS
}

#ifdef DEBUG
/*****************************************************************************
 *
 *  The following called for each recorded instruction -- use for debugging.
 */
void emitter::emitInsSanityCheck(instrDesc* id)
{
    // make certain you only try to put relocs on things that can have them.
    ID_OPS idOp = (ID_OPS)emitFmtToOps[id->idInsFmt()];
    if ((idOp == ID_OP_SCNS) && id->idIsLargeCns())
    {
        idOp = ID_OP_CNS;
    }

    if (id->idIsDspReloc())
    {
        assert(idOp == ID_OP_NONE || idOp == ID_OP_AMD || idOp == ID_OP_DSP || idOp == ID_OP_DSP_CNS ||
               idOp == ID_OP_AMD_CNS || idOp == ID_OP_SPEC || idOp == ID_OP_CALL || idOp == ID_OP_JMP ||
               idOp == ID_OP_LBL);
    }

    if (id->idIsCnsReloc())
    {
        assert(idOp == ID_OP_CNS || idOp == ID_OP_AMD_CNS || idOp == ID_OP_DSP_CNS || idOp == ID_OP_SPEC ||
               idOp == ID_OP_CALL || idOp == ID_OP_JMP);
    }
}
#endif

/*****************************************************************************
 *
 *  Return the allocated size (in bytes) of the given instruction descriptor.
 */

size_t emitter::emitSizeOfInsDsc(instrDesc* id)
{
    if (emitIsScnsInsDsc(id))
    {
        return SMALL_IDSC_SIZE;
    }

    assert((unsigned)id->idInsFmt() < emitFmtCount);

    ID_OPS idOp = (ID_OPS)emitFmtToOps[id->idInsFmt()];

    // An INS_call instruction may use a "fat" direct/indirect call descriptor
    // except for a local call to a label (i.e. call to a finally)
    // Only ID_OP_CALL and ID_OP_SPEC check for this, so we enforce that the
    //  INS_call instruction always uses one of these idOps

    if (id->idIns() == INS_call)
    {
        assert(idOp == ID_OP_CALL || // is a direct   call
               idOp == ID_OP_SPEC || // is a indirect call
               idOp == ID_OP_JMP);   // is a local call to finally clause
    }

    switch (idOp)
    {
        case ID_OP_NONE:
#if FEATURE_LOOP_ALIGN
            if (id->idIns() == INS_align)
            {
                return sizeof(instrDescAlign);
            }
#endif
            break;

        case ID_OP_LBL:
            return sizeof(instrDescLbl);

        case ID_OP_JMP:
            return sizeof(instrDescJmp);

        case ID_OP_CALL:
        case ID_OP_SPEC:
            if (id->idIsLargeCall())
            {
                /* Must be a "fat" indirect call descriptor */
                return sizeof(instrDescCGCA);
            }

            FALLTHROUGH;

        case ID_OP_SCNS:
        case ID_OP_CNS:
        case ID_OP_DSP:
        case ID_OP_DSP_CNS:
            if (id->idIsLargeCns())
            {
                if (id->idIsLargeDsp())
                {
                    return sizeof(instrDescCnsDsp);
                }
                else
                {
                    return sizeof(instrDescCns);
                }
            }
            else
            {
                if (id->idIsLargeDsp())
                {
                    return sizeof(instrDescDsp);
                }
                else
                {
                    return sizeof(instrDesc);
                }
            }
        case ID_OP_AMD:
        case ID_OP_AMD_CNS:
            if (id->idIsLargeCns())
            {
                if (id->idIsLargeDsp())
                {
                    return sizeof(instrDescCnsAmd);
                }
                else
                {
                    return sizeof(instrDescCns);
                }
            }
            else
            {
                if (id->idIsLargeDsp())
                {
                    return sizeof(instrDescAmd);
                }
                else
                {
                    return sizeof(instrDesc);
                }
            }

        default:
            NO_WAY("unexpected instruction descriptor format");
            break;
    }

    return sizeof(instrDesc);
}

/*****************************************************************************/
#ifdef DEBUG
/*****************************************************************************
 *
 *  Return a string that represents the given register.
 */

const char* emitter::emitRegName(regNumber reg, emitAttr attr, bool varName)
{
    static char          rb[2][128];
    static unsigned char rbc = 0;

    const char* rn = emitComp->compRegVarName(reg, varName);

#ifdef TARGET_AMD64
    char suffix = '\0';

    switch (EA_SIZE(attr))
    {
        case EA_32BYTE:
            return emitYMMregName(reg);

        case EA_16BYTE:
            return emitXMMregName(reg);

        case EA_8BYTE:
            if ((REG_XMM0 <= reg) && (reg <= REG_XMM15))
            {
                return emitXMMregName(reg);
            }
            break;

        case EA_4BYTE:
            if ((REG_XMM0 <= reg) && (reg <= REG_XMM15))
            {
                return emitXMMregName(reg);
            }

            if (reg > REG_R15)
            {
                break;
            }

            if (reg > REG_RDI)
            {
                suffix = 'd';
                goto APPEND_SUFFIX;
            }
            rbc        = (rbc + 1) % 2;
            rb[rbc][0] = 'e';
            rb[rbc][1] = rn[1];
            rb[rbc][2] = rn[2];
            rb[rbc][3] = 0;
            rn         = rb[rbc];
            break;

        case EA_2BYTE:
            if (reg > REG_RDI)
            {
                suffix = 'w';
                goto APPEND_SUFFIX;
            }
            rn++;
            break;

        case EA_1BYTE:
            if (reg > REG_RDI)
            {
                suffix = 'b';
            APPEND_SUFFIX:
                rbc        = (rbc + 1) % 2;
                rb[rbc][0] = rn[0];
                rb[rbc][1] = rn[1];
                if (rn[2])
                {
                    assert(rn[3] == 0);
                    rb[rbc][2] = rn[2];
                    rb[rbc][3] = suffix;
                    rb[rbc][4] = 0;
                }
                else
                {
                    rb[rbc][2] = suffix;
                    rb[rbc][3] = 0;
                }
            }
            else
            {
                rbc        = (rbc + 1) % 2;
                rb[rbc][0] = rn[1];
                if (reg < 4)
                {
                    rb[rbc][1] = 'l';
                    rb[rbc][2] = 0;
                }
                else
                {
                    rb[rbc][1] = rn[2];
                    rb[rbc][2] = 'l';
                    rb[rbc][3] = 0;
                }
            }

            rn = rb[rbc];
            break;

        default:
            break;
    }
#endif // TARGET_AMD64

#ifdef TARGET_X86
    assert(strlen(rn) >= 3);

    switch (EA_SIZE(attr))
    {
        case EA_32BYTE:
            return emitYMMregName(reg);

        case EA_16BYTE:
            return emitXMMregName(reg);

        case EA_8BYTE:
            if ((REG_XMM0 <= reg) && (reg <= REG_XMM7))
            {
                return emitXMMregName(reg);
            }
            break;

        case EA_4BYTE:
            if ((REG_XMM0 <= reg) && (reg <= REG_XMM7))
            {
                return emitXMMregName(reg);
            }
            break;

        case EA_2BYTE:
            rn++;
            break;

        case EA_1BYTE:
            rbc        = (rbc + 1) % 2;
            rb[rbc][0] = rn[1];
            rb[rbc][1] = 'l';
            strcpy_s(&rb[rbc][2], sizeof(rb[0]) - 2, rn + 3);

            rn = rb[rbc];
            break;

        default:
            break;
    }
#endif // TARGET_X86

#if 0
    // The following is useful if you want register names to be tagged with * or ^ representing gcref or byref, respectively,
    // however it's possibly not interesting most of the time.
    if (EA_IS_GCREF(attr) || EA_IS_BYREF(attr))
    {
        if (rn != rb[rbc])
        {
            rbc = (rbc+1)%2;
            strcpy_s(rb[rbc], sizeof(rb[rbc]), rn);
            rn = rb[rbc];
        }

        if (EA_IS_GCREF(attr))
        {
            strcat_s(rb[rbc], sizeof(rb[rbc]), "*");
        }
        else if (EA_IS_BYREF(attr))
        {
            strcat_s(rb[rbc], sizeof(rb[rbc]), "^");
        }
    }
#endif // 0

    return rn;
}

/*****************************************************************************
 *
 *  Return a string that represents the given FP register.
 */

const char* emitter::emitFPregName(unsigned reg, bool varName)
{
    assert(reg < REG_COUNT);

    return emitComp->compFPregVarName((regNumber)(reg), varName);
}

/*****************************************************************************
 *
 *  Return a string that represents the given XMM register.
 */

const char* emitter::emitXMMregName(unsigned reg)
{
    static const char* const regNames[] = {
#define REGDEF(name, rnum, mask, sname) "x" sname,
#include "register.h"
    };

    assert(reg < REG_COUNT);
    assert(reg < _countof(regNames));

    return regNames[reg];
}

/*****************************************************************************
 *
 *  Return a string that represents the given YMM register.
 */

const char* emitter::emitYMMregName(unsigned reg)
{
    static const char* const regNames[] = {
#define REGDEF(name, rnum, mask, sname) "y" sname,
#include "register.h"
    };

    assert(reg < REG_COUNT);
    assert(reg < _countof(regNames));

    return regNames[reg];
}

/*****************************************************************************
 *
 *  Display a static data member reference.
 */

void emitter::emitDispClsVar(CORINFO_FIELD_HANDLE fldHnd, ssize_t offs, bool reloc /* = false */)
{
    int doffs;

    /* Filter out the special case of fs:[offs] */

    // Munge any pointers if we want diff-able disassembly
    if (emitComp->opts.disDiffable)
    {
        ssize_t top12bits = (offs >> 20);
        if ((top12bits != 0) && (top12bits != -1))
        {
            offs = 0xD1FFAB1E;
        }
    }

    if (fldHnd == FLD_GLOBAL_FS)
    {
        printf("FS:[0x%04X]", offs);
        return;
    }

    if (fldHnd == FLD_GLOBAL_DS)
    {
        printf("[0x%04X]", offs);
        return;
    }

    printf("[");

    doffs = Compiler::eeGetJitDataOffs(fldHnd);

    if (reloc)
    {
        printf("reloc ");
    }

    if (doffs >= 0)
    {
        if (doffs & 1)
        {
            printf("@CNS%02u", doffs - 1);
        }
        else
        {
            printf("@RWD%02u", doffs);
        }

        if (offs)
        {
            printf("%+Id", offs);
        }
    }
    else
    {
        printf("classVar[%#x]", emitComp->dspPtr(fldHnd));

        if (offs)
        {
            printf("%+Id", offs);
        }
    }

    printf("]");

    if (emitComp->opts.varNames && offs < 0)
    {
        printf("'%s", emitComp->eeGetFieldName(fldHnd));
        if (offs)
        {
            printf("%+Id", offs);
        }
        printf("'");
    }
}

/*****************************************************************************
 *
 *  Display a stack frame reference.
 */

void emitter::emitDispFrameRef(int varx, int disp, int offs, bool asmfm)
{
    int  addr;
    bool bEBP;

    printf("[");

    if (!asmfm || emitComp->lvaDoneFrameLayout == Compiler::NO_FRAME_LAYOUT)
    {
        if (varx < 0)
        {
            printf("TEMP_%02u", -varx);
        }
        else
        {
            printf("V%02u", +varx);
        }

        if (disp < 0)
        {
            printf("-0x%X", -disp);
        }
        else if (disp > 0)
        {
            printf("+0x%X", +disp);
        }
    }

    if (emitComp->lvaDoneFrameLayout == Compiler::FINAL_FRAME_LAYOUT)
    {
        if (!asmfm)
        {
            printf(" ");
        }

        addr = emitComp->lvaFrameAddress(varx, &bEBP) + disp;

        if (bEBP)
        {
            printf(STR_FPBASE);

            if (addr < 0)
            {
                printf("-%02XH", -addr);
            }
            else if (addr > 0)
            {
                printf("+%02XH", addr);
            }
        }
        else
        {
            /* Adjust the offset by amount currently pushed on the stack */

            printf(STR_SPBASE);

            if (addr < 0)
            {
                printf("-%02XH", -addr);
            }
            else if (addr > 0)
            {
                printf("+%02XH", addr);
            }

#if !FEATURE_FIXED_OUT_ARGS

            if (emitCurStackLvl)
                printf("+%02XH", emitCurStackLvl);

#endif // !FEATURE_FIXED_OUT_ARGS
        }
    }

    printf("]");

    if (varx >= 0 && emitComp->opts.varNames)
    {
        LclVarDsc*  varDsc;
        const char* varName;

        assert((unsigned)varx < emitComp->lvaCount);
        varDsc  = emitComp->lvaTable + varx;
        varName = emitComp->compLocalVarName(varx, offs);

        if (varName)
        {
            printf("'%s", varName);

            if (disp < 0)
            {
                printf("-%d", -disp);
            }
            else if (disp > 0)
            {
                printf("+%d", +disp);
            }

            printf("'");
        }
    }
}

/*****************************************************************************
 *
 *  Display an reloc value
 *  If we are formatting for an assembly listing don't print the hex value
 *  since it will prevent us from doing assembly diffs
 */
void emitter::emitDispReloc(ssize_t value)
{
    if (emitComp->opts.disAsm)
    {
        printf("(reloc)");
    }
    else
    {
        printf("(reloc 0x%Ix)", emitComp->dspPtr(value));
    }
}

/*****************************************************************************
 *
 *  Display an address mode.
 */

void emitter::emitDispAddrMode(instrDesc* id, bool noDetail)
{
    assert(false);
}

/*****************************************************************************
 *
 *  If the given instruction is a shift, display the 2nd operand.
 */

void emitter::emitDispShift(instruction ins, int cnt)
{
    switch (ins)
    {
        case INS_rcl_1:
        case INS_rcr_1:
        case INS_rol_1:
        case INS_ror_1:
        case INS_shl_1:
        case INS_shr_1:
        case INS_sar_1:
            printf(", 1");
            break;

        case INS_rcl:
        case INS_rcr:
        case INS_rol:
        case INS_ror:
        case INS_shl:
        case INS_shr:
        case INS_sar:
            printf(", cl");
            break;

        case INS_rcl_N:
        case INS_rcr_N:
        case INS_rol_N:
        case INS_ror_N:
        case INS_shl_N:
        case INS_shr_N:
        case INS_sar_N:
            printf(", %d", cnt);
            break;

        default:
            break;
    }
}

/*****************************************************************************
 *
 *  Display (optionally) the bytes for the instruction encoding in hex
 */

void emitter::emitDispInsHex(instrDesc* id, BYTE* code, size_t sz)
{
    // We do not display the instruction hex if we want diff-able disassembly
    if (!emitComp->opts.disDiffable)
    {
#ifdef TARGET_AMD64
        // how many bytes per instruction we format for
        const size_t digits = 10;
#else // TARGET_X86
        const size_t digits = 6;
#endif
        printf(" ");
        for (unsigned i = 0; i < sz; i++)
        {
            printf("%02X", (*((BYTE*)(code + i))));
        }

        if (sz < digits)
        {
            printf("%.*s", 2 * (digits - sz), "                         ");
        }
    }
}

/*****************************************************************************
 *
 *  Display the given instruction.
 */

void emitter::emitDispIns(
    instrDesc* id, bool isNew, bool doffs, bool asmfm, unsigned offset, BYTE* code, size_t sz, insGroup* ig)
{
    assert(false);

}

/*****************************************************************************/
#endif

/*****************************************************************************
 *
 *  Output nBytes bytes of NOP instructions
 */

//static BYTE* emitOutputNOP(BYTE* dst, size_t nBytes)
//{
//    assert(nBytes <= 15);
//
//#ifndef TARGET_AMD64
//    // TODO-X86-CQ: when VIA C3 CPU's are out of circulation, switch to the
//    // more efficient real NOP: 0x0F 0x1F +modR/M
//    // Also can't use AMD recommended, multiple size prefixes (i.e. 0x66 0x66 0x90 for 3 byte NOP)
//    // because debugger and msdis don't like it, so maybe VIA doesn't either
//    // So instead just stick to repeating single byte nops
//
//    switch (nBytes)
//    {
//        case 15:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 14:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 13:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 12:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 11:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 10:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 9:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 8:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 7:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 6:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 5:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 4:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 3:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 2:
//            *dst++ = 0x90;
//            FALLTHROUGH;
//        case 1:
//            *dst++ = 0x90;
//            break;
//        case 0:
//            break;
//    }
//#else  // TARGET_AMD64
//    switch (nBytes)
//    {
//        case 2:
//            *dst++ = 0x66;
//            FALLTHROUGH;
//        case 1:
//            *dst++ = 0x90;
//            break;
//        case 0:
//            break;
//        case 3:
//            *dst++ = 0x0F;
//            *dst++ = 0x1F;
//            *dst++ = 0x00;
//            break;
//        case 4:
//            *dst++ = 0x0F;
//            *dst++ = 0x1F;
//            *dst++ = 0x40;
//            *dst++ = 0x00;
//            break;
//        case 6:
//            *dst++ = 0x66;
//            FALLTHROUGH;
//        case 5:
//            *dst++ = 0x0F;
//            *dst++ = 0x1F;
//            *dst++ = 0x44;
//            *dst++ = 0x00;
//            *dst++ = 0x00;
//            break;
//        case 7:
//            *dst++ = 0x0F;
//            *dst++ = 0x1F;
//            *dst++ = 0x80;
//            *dst++ = 0x00;
//            *dst++ = 0x00;
//            *dst++ = 0x00;
//            *dst++ = 0x00;
//            break;
//        case 15:
//            // More than 3 prefixes is slower than just 2 NOPs
//            dst = emitOutputNOP(emitOutputNOP(dst, 7), 8);
//            break;
//        case 14:
//            // More than 3 prefixes is slower than just 2 NOPs
//            dst = emitOutputNOP(emitOutputNOP(dst, 7), 7);
//            break;
//        case 13:
//            // More than 3 prefixes is slower than just 2 NOPs
//            dst = emitOutputNOP(emitOutputNOP(dst, 5), 8);
//            break;
//        case 12:
//            // More than 3 prefixes is slower than just 2 NOPs
//            dst = emitOutputNOP(emitOutputNOP(dst, 4), 8);
//            break;
//        case 11:
//            *dst++ = 0x66;
//            FALLTHROUGH;
//        case 10:
//            *dst++ = 0x66;
//            FALLTHROUGH;
//        case 9:
//            *dst++ = 0x66;
//            FALLTHROUGH;
//        case 8:
//            *dst++ = 0x0F;
//            *dst++ = 0x1F;
//            *dst++ = 0x84;
//            *dst++ = 0x00;
//            *dst++ = 0x00;
//            *dst++ = 0x00;
//            *dst++ = 0x00;
//            *dst++ = 0x00;
//            break;
//    }
//#endif // TARGET_AMD64
//
//    return dst;
//}

//--------------------------------------------------------------------
// emitOutputAlign: Outputs NOP to align the loop
//
// Arguments:
//   ig - Current instruction group
//   id - align instruction that holds amount of padding (NOPs) to add
//   dst - Destination buffer
//
// Return Value:
//   None.
//
// Notes:
//   Amount of padding needed to align the loop is already calculated. This
//   method extracts that information and inserts suitable NOP instructions.
//
BYTE* emitter::emitOutputAlign(insGroup* ig, instrDesc* id, BYTE* dst)
{
    assert(false);
    return 0;
}

/*****************************************************************************
 *
 *  Output an instruction involving an address mode.
 */

BYTE* emitter::emitOutputAM(BYTE* dst, instrDesc* id, code_t code, CnsVal* addc)
{
    assert(false);
    return 0;
}

/*****************************************************************************
 *
 *  Output an instruction involving a stack frame value.
 */

BYTE* emitter::emitOutputSV(BYTE* dst, instrDesc* id, code_t code, CnsVal* addc)
{
    assert(false);
    return 0;
}

/*****************************************************************************
 *
 *  Output an instruction with a static data member (class variable).
 */

BYTE* emitter::emitOutputCV(BYTE* dst, instrDesc* id, code_t code, CnsVal* addc)
{
    assert(false);
    return 0;
}

/*****************************************************************************
 *
 *  Output an instruction with one register operand.
 */

BYTE* emitter::emitOutputR(BYTE* dst, instrDesc* id)
{
    code_t code;

    instruction ins  = id->idIns();
    regNumber   reg  = id->idReg1();
    emitAttr    size = id->idOpSize();

    // We would to update GC info correctly
    assert(!IsSSEInstruction(ins));
    assert(!IsAVXInstruction(ins));

    // Get the 'base' opcode
    switch (ins)
    {
        case INS_inc:
        case INS_dec:

#ifdef TARGET_AMD64
            if (true)
#else
            if (size == EA_1BYTE)
#endif
            {
                assert(INS_inc_l == INS_inc + 1);
                assert(INS_dec_l == INS_dec + 1);

                // Can't use the compact form, use the long form
                ins = (instruction)(ins + 1);
                if (size == EA_2BYTE)
                {
                    // Output a size prefix for a 16-bit operand
                    dst += emitOutputByte(dst, 0x66);
                }

                code = insCodeRR(ins);
                if (size != EA_1BYTE)
                {
                    // Set the 'w' bit to get the large version
                    code |= 0x1;
                }

                if (TakesRexWPrefix(ins, size))
                {
                    code = AddRexWPrefix(ins, code);
                }

                // Register...
                unsigned regcode = insEncodeReg012(ins, reg, size, &code);

                // Output the REX prefix
                dst += emitOutputRexOrVexPrefixIfNeeded(ins, dst, code);

                dst += emitOutputWord(dst, code | (regcode << 8));
            }
            else
            {
                if (size == EA_2BYTE)
                {
                    // Output a size prefix for a 16-bit operand
                    dst += emitOutputByte(dst, 0x66);
                }
                dst += emitOutputByte(dst, insCodeRR(ins) | insEncodeReg012(ins, reg, size, nullptr));
            }
            break;

        case INS_pop:
        case INS_pop_hide:
        case INS_push:
        case INS_push_hide:

            assert(size == EA_PTRSIZE);
            code = insEncodeOpreg(ins, reg, size);

            assert(!TakesVexPrefix(ins));
            assert(!TakesRexWPrefix(ins, size));

            // Output the REX prefix
            dst += emitOutputRexOrVexPrefixIfNeeded(ins, dst, code);

            dst += emitOutputByte(dst, code);
            break;

        case INS_bswap:
        {
            assert(size >= EA_4BYTE && size <= EA_PTRSIZE); // 16-bit BSWAP is undefined

            // The Intel instruction set reference for BSWAP states that extended registers
            // should be enabled via REX.R, but per Vol. 2A, Sec. 2.2.1.2 (see also Figure 2-7),
            // REX.B should instead be used if the register is encoded in the opcode byte itself.
            // Therefore the default logic of insEncodeReg012 is correct for this case.

            code = insCodeRR(ins);

            if (TakesRexWPrefix(ins, size))
            {
                code = AddRexWPrefix(ins, code);
            }

            // Register...
            unsigned regcode = insEncodeReg012(ins, reg, size, &code);

            // Output the REX prefix
            dst += emitOutputRexOrVexPrefixIfNeeded(ins, dst, code);

            dst += emitOutputWord(dst, code | (regcode << 8));
            break;
        }

        case INS_seto:
        case INS_setno:
        case INS_setb:
        case INS_setae:
        case INS_sete:
        case INS_setne:
        case INS_setbe:
        case INS_seta:
        case INS_sets:
        case INS_setns:
        case INS_setp:
        case INS_setnp:
        case INS_setl:
        case INS_setge:
        case INS_setle:
        case INS_setg:

            assert(id->idGCref() == GCT_NONE);
            assert(size == EA_1BYTE);

            code = insEncodeMRreg(ins, reg, EA_1BYTE, insCodeMR(ins));

            // Output the REX prefix
            dst += emitOutputRexOrVexPrefixIfNeeded(ins, dst, code);

            // We expect this to always be a 'big' opcode
            assert(code & 0x00FF0000);

            dst += emitOutputByte(dst, code >> 16);
            dst += emitOutputWord(dst, code & 0x0000FFFF);

            break;

        case INS_mulEAX:
        case INS_imulEAX:

            // Kill off any GC refs in EAX or EDX
            emitGCregDeadUpd(REG_EAX, dst);
            emitGCregDeadUpd(REG_EDX, dst);

            FALLTHROUGH;

        default:

            assert(id->idGCref() == GCT_NONE);

            code = insEncodeMRreg(ins, reg, size, insCodeMR(ins));

            if (size != EA_1BYTE)
            {
                // Set the 'w' bit to get the large version
                code |= 0x1;

                if (size == EA_2BYTE)
                {
                    // Output a size prefix for a 16-bit operand
                    dst += emitOutputByte(dst, 0x66);
                }
            }

            code = AddVexPrefixIfNeeded(ins, code, size);

            if (TakesRexWPrefix(ins, size))
            {
                code = AddRexWPrefix(ins, code);
            }

            // Output the REX prefix
            dst += emitOutputRexOrVexPrefixIfNeeded(ins, dst, code);

            dst += emitOutputWord(dst, code);
            break;
    }

    // Are we writing the register? if so then update the GC information
    switch (id->idInsFmt())
    {
        case IF_RRD:
            break;
        case IF_RWR:
            if (id->idGCref())
            {
                emitGCregLiveUpd(id->idGCref(), id->idReg1(), dst);
            }
            else
            {
                emitGCregDeadUpd(id->idReg1(), dst);
            }
            break;
        case IF_RRW:
        {
#ifdef DEBUG
            regMaskTP regMask = genRegMask(reg);
#endif
            if (id->idGCref())
            {
                // The reg must currently be holding either a gcref or a byref
                // and the instruction must be inc or dec
                assert(((emitThisGCrefRegs | emitThisByrefRegs) & regMask) &&
                       (ins == INS_inc || ins == INS_dec || ins == INS_inc_l || ins == INS_dec_l));
                assert(id->idGCref() == GCT_BYREF);
                // Mark it as holding a GCT_BYREF
                emitGCregLiveUpd(GCT_BYREF, id->idReg1(), dst);
            }
            else
            {
                // Can't use RRW to trash a GC ref.  It's OK for unverifiable code
                // to trash Byrefs.
                assert((emitThisGCrefRegs & regMask) == 0);
            }
        }
        break;
        default:
#ifdef DEBUG
            emitDispIns(id, false, false, false);
#endif
            assert(!"unexpected instruction format");
            break;
    }

    return dst;
}

/*****************************************************************************
 *
 *  Output an instruction with two register operands.
 */

BYTE* emitter::emitOutputRR(BYTE* dst, instrDesc* id)
{
    assert(false);
    return 0;
}

BYTE* emitter::emitOutputRRR(BYTE* dst, instrDesc* id)
{
    assert(false);
    return 0;
}

/*****************************************************************************
 *
 *  Output an instruction with a register and constant operands.
 */

BYTE* emitter::emitOutputRI(BYTE* dst, instrDesc* id)
{
    assert(false);
    return 0;
}

/*****************************************************************************
 *
 *  Output an instruction with a constant operand.
 */

BYTE* emitter::emitOutputIV(BYTE* dst, instrDesc* id)
{
    code_t      code;
    instruction ins       = id->idIns();
    emitAttr    size      = id->idOpSize();
    ssize_t     val       = emitGetInsSC(id);
    bool        valInByte = ((signed char)val == (target_ssize_t)val);

    // We would to update GC info correctly
    assert(!IsSSEInstruction(ins));
    assert(!IsAVXInstruction(ins));

#ifdef TARGET_AMD64
    // all these opcodes take a sign-extended 4-byte immediate, max
    noway_assert(size < EA_8BYTE || ((int)val == val && !id->idIsCnsReloc()));
#endif

    if (id->idIsCnsReloc())
    {
        valInByte = false; // relocs can't be placed in a byte

        // Of these instructions only the push instruction can have reloc
        assert(ins == INS_push || ins == INS_push_hide);
    }

    switch (ins)
    {
        case INS_jge:
            assert((val >= -128) && (val <= 127));
            dst += emitOutputByte(dst, insCode(ins));
            dst += emitOutputByte(dst, val);
            break;

        case INS_loop:
            assert((val >= -128) && (val <= 127));
            dst += emitOutputByte(dst, insCodeMI(ins));
            dst += emitOutputByte(dst, val);
            break;

        case INS_ret:
            assert(val);
            dst += emitOutputByte(dst, insCodeMI(ins));
            dst += emitOutputWord(dst, val);
            break;

        case INS_push_hide:
        case INS_push:
            code = insCodeMI(ins);

            // Does the operand fit in a byte?
            if (valInByte)
            {
                dst += emitOutputByte(dst, code | 2);
                dst += emitOutputByte(dst, val);
            }
            else
            {
                if (TakesRexWPrefix(ins, size))
                {
                    code = AddRexWPrefix(ins, code);
                    dst += emitOutputRexOrVexPrefixIfNeeded(ins, dst, code);
                }

                dst += emitOutputByte(dst, code);
                dst += emitOutputLong(dst, val);
                if (id->idIsCnsReloc())
                {
                    emitRecordRelocation((void*)(dst - sizeof(INT32)), (void*)(size_t)val, IMAGE_REL_BASED_HIGHLOW);
                }
            }

            // Did we push a GC ref value?
            if (id->idGCref())
            {
#ifdef DEBUG
                printf("UNDONE: record GCref push [cns]\n");
#endif
            }

            break;

        default:
            assert(!"unexpected instruction");
    }

    return dst;
}

/*****************************************************************************
 *
 *  Output a local jump instruction.
 *  This function also handles non-jumps that have jump-like characteristics, like RIP-relative LEA of a label that
 *  needs to get bound to an actual address and processed by branch shortening.
 */

BYTE* emitter::emitOutputLJ(insGroup* ig, BYTE* dst, instrDesc* i)
{
    assert(false);
    return 0;
}

/*****************************************************************************
 *
 *  Append the machine code corresponding to the given instruction descriptor
 *  to the code block at '*dp'; the base of the code block is 'bp', and 'ig'
 *  is the instruction group that contains the instruction. Updates '*dp' to
 *  point past the generated code, and returns the size of the instruction
 *  descriptor in bytes.
 */

#ifdef _PREFAST_
#pragma warning(push)
#pragma warning(disable : 21000) // Suppress PREFast warning about overly large function
#endif
size_t emitter::emitOutputInstr(insGroup* ig, instrDesc* id, BYTE** dp)
{
    assert(false);
    return 0;
}
#ifdef _PREFAST_
#pragma warning(pop)
#endif

//emitter::insFormat emitter::getMemoryOperation(instrDesc* id)
//{
//    assert(false);
//}

#if defined(DEBUG) || defined(LATE_DISASM)

//----------------------------------------------------------------------------------------
// getInsExecutionCharacteristics:
//    Returns the current instruction execution characteristics
//
// Arguments:
//    id  - The current instruction descriptor to be evaluated
//
// Return Value:
//    A struct containing the current instruction execution characteristics
//
// Notes:
//    The instruction latencies and throughput values returned by this function
//    are for the Intel Skylake-X processor and are from either:
//      1.  Agner.org - https://www.agner.org/optimize/instruction_tables.pdf
//      2.  uops.info - https://uops.info/table.html
//
emitter::insExecutionCharacteristics emitter::getInsExecutionCharacteristics(instrDesc* id)
{
    assert(false);
    insExecutionCharacteristics result;
    result.insThroughput = PERFSCORE_THROUGHPUT_ILLEGAL;
    return result;
}

#endif // defined(DEBUG) || defined(LATE_DISASM)

/*****************************************************************************/
/*****************************************************************************/

#endif // defined(TARGET_XARCH)
