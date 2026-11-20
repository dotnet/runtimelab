// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// FORCEINLINE implementation of the Interlocked class methods
//

#ifndef __GCENV_INTERLOCKED_INL__
#define __GCENV_INTERLOCKED_INL__

#ifdef _MSC_VER
#include <intrin.h>
#endif // _MSC_VER

#ifndef _MSC_VER
FORCEINLINE void Interlocked::InterlockedOperationBarrier()
{
#if defined(HOST_ARM64) || defined(HOST_LOONGARCH64) || defined(HOST_RISCV64)
    // See PAL_InterlockedOperationBarrier() in the PAL
    __sync_synchronize();
#endif
}
#endif // !_MSC_VER

#ifdef INTERLOCKED_ARM64_LSE

// The operations check it in __builtin_expect, so that the LSE instruction is the straight-line path and the loops of exclusive loads
// and stores are out of the way. The hint has to be at the check: in UseLseAtomics it is lost before UseLseAtomics is inlined.
FORCEINLINE bool Interlocked::UseLseAtomics()
{
#ifdef INTERLOCKED_ARM64_LSE_DETECTION
    // Written once, when the module is loaded, and either value is correct, so a plain read is enough.
    return s_arm64LseAtomics;
#else
    // The build requires a processor that has them.
    return true;
#endif
}

// The LSE instructions are in inline assembly, so that they are the same whether or not the compiler may emit them; ".arch_extension lse"
// lets the assembler accept them where it may not.
// "Q" is a memory operand addressed by a register alone, as the instructions need, and the "memory" clobber keeps the compiler from moving
// other memory accesses across them. A full operation is an instruction with both an acquire and a release, and no barrier after it,
// the way that the PAL does it (see Define_InterlockMethod).
// There is a form of each instruction for each size: on the x registers for 8 bytes, and on the w registers for 4, 2 ("h") and 1 ("b").

#define INTERLOCKED_LSE_FETCH_OP_FOR_SIZE(instruction, suffix, width, type)                                                         \
    {                                                                                                                               \
        type original;                                                                                                              \
        __asm__ __volatile__(".arch_extension lse\n\t" instruction suffix " %" width "[value], %" width "[original], %[destination]" \
            : [destination] "+Q" (*(type*)destination), [original] "=r" (original)                                                  \
            : [value] "r" ((type)value)                                                                                             \
            : "memory");                                                                                                            \
        return (T)original;                                                                                                         \
    }

// Does the operation of the instruction on the destination and the value, and returns what the destination had.
#define INTERLOCKED_LSE_FETCH_OP(name, instruction)                                                                                 \
    template <typename T>                                                                                                           \
    FORCEINLINE T Interlocked::name(T volatile *destination, T value)                                                               \
    {                                                                                                                               \
        static_assert(sizeof(T) == 1 || sizeof(T) == 2 || sizeof(T) == 4 || sizeof(T) == 8, "Unsupported size");                   \
        if constexpr (sizeof(T) == 8) INTERLOCKED_LSE_FETCH_OP_FOR_SIZE(instruction, "", "x", uint64_t)                              \
        else if constexpr (sizeof(T) == 4) INTERLOCKED_LSE_FETCH_OP_FOR_SIZE(instruction, "", "w", uint32_t)                         \
        else if constexpr (sizeof(T) == 2) INTERLOCKED_LSE_FETCH_OP_FOR_SIZE(instruction, "h", "w", uint16_t)                        \
        else INTERLOCKED_LSE_FETCH_OP_FOR_SIZE(instruction, "b", "w", uint8_t)                                                       \
    }

INTERLOCKED_LSE_FETCH_OP(LseFetchAdd, "ldaddal")
INTERLOCKED_LSE_FETCH_OP(LseFetchAddRel, "ldaddl")
INTERLOCKED_LSE_FETCH_OP(LseFetchClear, "ldclral")
INTERLOCKED_LSE_FETCH_OP(LseFetchClearUnordered, "ldclr")
INTERLOCKED_LSE_FETCH_OP(LseFetchOr, "ldsetal")
INTERLOCKED_LSE_FETCH_OP(LseFetchOrRel, "ldsetl")
INTERLOCKED_LSE_FETCH_OP(LseFetchOrUnordered, "ldset")
INTERLOCKED_LSE_FETCH_OP(LseSwap, "swpal")

#define INTERLOCKED_LSE_COMPARE_EXCHANGE_FOR_SIZE(instruction, suffix, width, type)                                                 \
    {                                                                                                                               \
        type original = (type)comparand;                                                                                            \
        __asm__ __volatile__(".arch_extension lse\n\t" instruction suffix " %" width "[original], %" width "[exchange], %[destination]" \
            : [destination] "+Q" (*(type*)destination), [original] "+r" (original)                                                  \
            : [exchange] "r" ((type)exchange)                                                                                       \
            : "memory");                                                                                                            \
        return (T)original;                                                                                                         \
    }

// Stores the exchange if the destination has the comparand, and returns what the destination had.
#define INTERLOCKED_LSE_COMPARE_EXCHANGE(name, instruction)                                                                         \
    template <typename T>                                                                                                           \
    FORCEINLINE T Interlocked::name(T volatile *destination, T exchange, T comparand)                                               \
    {                                                                                                                               \
        static_assert(sizeof(T) == 1 || sizeof(T) == 2 || sizeof(T) == 4 || sizeof(T) == 8, "Unsupported size");                   \
        if constexpr (sizeof(T) == 8) INTERLOCKED_LSE_COMPARE_EXCHANGE_FOR_SIZE(instruction, "", "x", uint64_t)                      \
        else if constexpr (sizeof(T) == 4) INTERLOCKED_LSE_COMPARE_EXCHANGE_FOR_SIZE(instruction, "", "w", uint32_t)                 \
        else if constexpr (sizeof(T) == 2) INTERLOCKED_LSE_COMPARE_EXCHANGE_FOR_SIZE(instruction, "h", "w", uint16_t)                \
        else INTERLOCKED_LSE_COMPARE_EXCHANGE_FOR_SIZE(instruction, "b", "w", uint8_t)                                               \
    }

INTERLOCKED_LSE_COMPARE_EXCHANGE(LseCompareExchange, "casal")
INTERLOCKED_LSE_COMPARE_EXCHANGE(LseCompareExchangeAcq, "casa")
INTERLOCKED_LSE_COMPARE_EXCHANGE(LseCompareExchangeUnordered, "cas")

#undef INTERLOCKED_LSE_FETCH_OP_FOR_SIZE
#undef INTERLOCKED_LSE_FETCH_OP
#undef INTERLOCKED_LSE_COMPARE_EXCHANGE_FOR_SIZE
#undef INTERLOCKED_LSE_COMPARE_EXCHANGE

// CASPAL compares and exchanges pairs of registers, each an even-numbered register and the one after it, the first with the half at the lower
// address. Inline assembly cannot ask for such a pair, so the operands are in fixed registers, the way that Linux does it (see
// __lse__cmpxchg128). Nothing that could use those registers, a call for one, may be between their assignment and the instruction.
FORCEINLINE bool Interlocked::LseCompareExchange128(int64_t volatile *destination, int64_t exchangeHigh, int64_t exchangeLow, int64_t *comparandResult)
{
    uint64_t comparandLow = (uint64_t)comparandResult[0];
    uint64_t comparandHigh = (uint64_t)comparandResult[1];
    register uint64_t originalLow __asm__("x0") = comparandLow;
    register uint64_t originalHigh __asm__("x1") = comparandHigh;
    register uint64_t valueLow __asm__("x2") = (uint64_t)exchangeLow;
    register uint64_t valueHigh __asm__("x3") = (uint64_t)exchangeHigh;
    __asm__ __volatile__(".arch_extension lse\n\tcaspal %[originalLow], %[originalHigh], %[valueLow], %[valueHigh], %[destination]"
        : [destination] "+Q" (*(__uint128_t*)destination), [originalLow] "+&r" (originalLow), [originalHigh] "+&r" (originalHigh)
        : [valueLow] "r" (valueLow), [valueHigh] "r" (valueHigh)
        : "memory");
    comparandResult[0] = (int64_t)originalLow;
    comparandResult[1] = (int64_t)originalHigh;
    return ((originalLow ^ comparandLow) | (originalHigh ^ comparandHigh)) == 0;
}

#endif // INTERLOCKED_ARM64_LSE

// Increment the value of the specified 32-bit variable as an atomic operation.
// Parameters:
//  addend - variable to be incremented
// Return:
//  The resulting incremented value
template <typename T>
FORCEINLINE T Interlocked::Increment(T volatile *addend)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
    return _InterlockedIncrement((long*)addend);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return (T)(LseFetchAdd(addend, (T)1) + 1);
    }
#endif
    T result = __sync_add_and_fetch(addend, 1);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <>
FORCEINLINE size_t Interlocked::Increment(size_t volatile* addend)
{
#ifdef _MSC_VER
    return (size_t)_InterlockedIncrement64((volatile long long*)addend);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseFetchAdd(addend, (size_t)1) + 1;
    }
#endif
    size_t result = __sync_add_and_fetch(addend, 1);
    InterlockedOperationBarrier();
    return result;
#endif
}

// Decrement the value of the specified 32-bit variable as an atomic operation.
// Parameters:
//  addend - variable to be decremented
// Return:
//  The resulting decremented value
template <typename T>
FORCEINLINE T Interlocked::Decrement(T volatile *addend)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
    return _InterlockedDecrement((long*)addend);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return (T)(LseFetchAdd(addend, (T)-1) - 1);
    }
#endif
    T result = __sync_sub_and_fetch(addend, 1);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <>
FORCEINLINE size_t Interlocked::Decrement(size_t volatile* addend)
{
#ifdef _MSC_VER
    return (size_t)_InterlockedDecrement64((volatile long long*)addend);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseFetchAdd(addend, (size_t)-1) - 1;
    }
#endif
    size_t result = __sync_sub_and_fetch(addend, 1);
    InterlockedOperationBarrier();
    return result;
#endif
}

// Set a 32-bit variable to the specified value as an atomic operation.
// Parameters:
//  destination - value to be exchanged
//  value       - value to set the destination to
// Return:
//  The previous value of the destination
template <typename T>
FORCEINLINE T Interlocked::Exchange(T volatile *destination, T value)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
    return _InterlockedExchange((long*)destination, value);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseSwap(destination, value);
    }
#endif
    T result = __atomic_exchange_n(destination, value, __ATOMIC_ACQ_REL);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <>
FORCEINLINE int8_t Interlocked::Exchange<int8_t>(int8_t volatile* destination, int8_t value)
{
#ifdef _MSC_VER
    return (int8_t)_InterlockedExchange8((char*)destination, value);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseSwap(destination, value);
    }
#endif
    int8_t result = __atomic_exchange_n(destination, value, __ATOMIC_ACQ_REL);
    InterlockedOperationBarrier();
    return result;
#endif
}

// Performs an atomic compare-and-exchange operation on the specified values.
// Parameters:
//  destination - value to be exchanged
//  exchange    - value to set the destinaton to
//  comparand   - value to compare the destination to before setting it to the exchange.
//                The destination is set only if the destination is equal to the comparand.
// Return:
//  The original value of the destination
template <typename T>
FORCEINLINE T Interlocked::CompareExchange(T volatile *destination, T exchange, T comparand)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
    return _InterlockedCompareExchange((long*)destination, exchange, comparand);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseCompareExchange(destination, exchange, comparand);
    }
#endif
    T result = __sync_val_compare_and_swap(destination, comparand, exchange);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <>
FORCEINLINE size_t Interlocked::CompareExchange<size_t>(size_t volatile * destination, size_t exchange, size_t comparand)
{
#ifdef _MSC_VER
    return _InterlockedCompareExchange64((volatile long long*)destination, exchange, comparand);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseCompareExchange(destination, exchange, comparand);
    }
#endif
    size_t result = __sync_val_compare_and_swap(destination, comparand, exchange);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <>
FORCEINLINE int8_t Interlocked::CompareExchange<int8_t>(int8_t volatile* destination, int8_t exchange, int8_t comparand)
{
#ifdef _MSC_VER
    return (int8_t)_InterlockedCompareExchange8((char*)destination, exchange, comparand);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseCompareExchange(destination, exchange, comparand);
    }
#endif
    int8_t result = __sync_val_compare_and_swap(destination, comparand, exchange);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <>
FORCEINLINE uint8_t Interlocked::CompareExchange<uint8_t>(uint8_t volatile* destination, uint8_t exchange, uint8_t comparand)
{
#ifdef _MSC_VER
    return (uint8_t)_InterlockedCompareExchange8((char*)destination, exchange, comparand);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseCompareExchange(destination, exchange, comparand);
    }
#endif
    uint8_t result = __sync_val_compare_and_swap(destination, comparand, exchange);
    InterlockedOperationBarrier();
    return result;
#endif
}

// Perform an atomic addition of two 32-bit values and return the original value of the addend.
// Parameters:
//  addend - variable to be added to
//  value  - value to add
// Return:
//  The previous value of the addend
template <typename T>
FORCEINLINE T Interlocked::ExchangeAdd(T volatile *addend, T value)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
    return _InterlockedExchangeAdd((long*)addend, value);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseFetchAdd(addend, value);
    }
#endif
    T result = __sync_fetch_and_add(addend, value);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <typename T>
FORCEINLINE T Interlocked::ExchangeAdd64(T volatile* addend, T value)
{
#ifdef _MSC_VER
    static_assert(sizeof(int64_t) == sizeof(T), "Size of LONGLONG must be the same as size of T");
    return _InterlockedExchangeAdd64((int64_t*)addend, value);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseFetchAdd(addend, value);
    }
#endif
    T result = __sync_fetch_and_add(addend, value);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <typename T>
FORCEINLINE T Interlocked::ExchangeAddPtr(T volatile* addend, T value)
{
#ifdef _MSC_VER
#ifdef HOST_64BIT
    static_assert(sizeof(int64_t) == sizeof(T), "Size of LONGLONG must be the same as size of T");
    return _InterlockedExchangeAdd64((int64_t*)addend, value);
#else
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
    return _InterlockedExchangeAdd((long*)addend, value);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseFetchAdd(addend, value);
    }
#endif
    T result = __sync_fetch_and_add(addend, value);
    InterlockedOperationBarrier();
    return result;
#endif
}

// Perform an atomic AND operation on the specified values values
// Parameters:
//  destination - the first operand and the destination
//  value       - second operand
template <typename T>
FORCEINLINE void Interlocked::And(T volatile *destination, T value)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
    _InterlockedAnd((long*)destination, value);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchClear(destination, (T)~value);
        return;
    }
#endif
    __sync_and_and_fetch(destination, value);
    InterlockedOperationBarrier();
#endif
}

template <>
FORCEINLINE void Interlocked::And<size_t>(size_t volatile *destination, size_t value)
{
#ifdef _MSC_VER
    _InterlockedAnd64((volatile long long*)destination, value);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchClear(destination, ~value);
        return;
    }
#endif
    __sync_and_and_fetch(destination, value);
    InterlockedOperationBarrier();
#endif
}

// Perform an atomic OR operation on the specified values values
// Parameters:
//  destination - the first operand and the destination
//  value       - second operand
template <typename T>
FORCEINLINE void Interlocked::Or(T volatile *destination, T value)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
    _InterlockedOr((long*)destination, value);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchOr(destination, value);
        return;
    }
#endif
    __sync_or_and_fetch(destination, value);
    InterlockedOperationBarrier();
#endif
}

template <>
FORCEINLINE void Interlocked::Or<size_t>(size_t volatile *destination, size_t value)
{
#ifdef _MSC_VER
    _InterlockedOr64((volatile long long*)destination, value);
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchOr(destination, value);
        return;
    }
#endif
    __sync_or_and_fetch(destination, value);
    InterlockedOperationBarrier();
#endif
}

// Set a pointer variable to the specified value as an atomic operation.
// Parameters:
//  destination - value to be exchanged
//  value       - value to set the destination to
// Return:
//  The previous value of the destination
template <typename T>
FORCEINLINE T Interlocked::ExchangePointer(T volatile * destination, T value)
{
#ifdef _MSC_VER
#ifdef HOST_64BIT
    return (T)(TADDR)_InterlockedExchangePointer((void* volatile *)destination, value);
#else
    return (T)(TADDR)_InterlockedExchange((long volatile *)(void* volatile *)destination, (long)(void*)value);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return (T)(TADDR)LseSwap((size_t volatile *)destination, (size_t)(void*)value);
    }
#endif
    T result = (T)(TADDR)__atomic_exchange_n((void* volatile *)destination, value, __ATOMIC_ACQ_REL);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <typename T>
FORCEINLINE T Interlocked::ExchangePointer(T volatile * destination, std::nullptr_t value)
{
#ifdef _MSC_VER
#ifdef HOST_64BIT
    return (T)(TADDR)_InterlockedExchangePointer((void* volatile *)destination, value);
#else
    return (T)(TADDR)_InterlockedExchange((long volatile *)(void* volatile *)destination, (long)(void*)value);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return (T)(TADDR)LseSwap((size_t volatile *)destination, (size_t)0);
    }
#endif
    T result = (T)(TADDR)__atomic_exchange_n((void* volatile *)destination, value, __ATOMIC_ACQ_REL);
    InterlockedOperationBarrier();
    return result;
#endif
}

// Performs an atomic compare-and-exchange operation on the specified pointers.
// Parameters:
//  destination - value to be exchanged
//  exchange    - value to set the destinaton to
//  comparand   - value to compare the destination to before setting it to the exchange.
//                The destination is set only if the destination is equal to the comparand.
// Return:
//  The original value of the destination
template <typename T>
FORCEINLINE T Interlocked::CompareExchangePointer(T volatile *destination, T exchange, T comparand)
{
#ifdef _MSC_VER
#ifdef HOST_64BIT
    return (T)(TADDR)_InterlockedCompareExchangePointer((void* volatile *)destination, exchange, comparand);
#else
    return (T)(TADDR)_InterlockedCompareExchange((long volatile *)(void* volatile *)destination, (long)(void*)exchange, (long)(void*)comparand);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return (T)(TADDR)LseCompareExchange((size_t volatile *)destination, (size_t)(void*)exchange, (size_t)(void*)comparand);
    }
#endif
    T result = (T)(TADDR)__sync_val_compare_and_swap((void* volatile *)destination, comparand, exchange);
    InterlockedOperationBarrier();
    return result;
#endif
}

template <typename T>
FORCEINLINE T Interlocked::CompareExchangePointer(T volatile *destination, T exchange, std::nullptr_t comparand)
{
#ifdef _MSC_VER
#ifdef HOST_64BIT
    return (T)(TADDR)_InterlockedCompareExchangePointer((void* volatile *)destination, (void*)exchange, (void*)comparand);
#else
    return (T)(TADDR)_InterlockedCompareExchange((long volatile *)(void* volatile *)destination, (long)(void*)exchange, (long)(void*)comparand);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return (T)(TADDR)LseCompareExchange((size_t volatile *)destination, (size_t)(void*)exchange, (size_t)0);
    }
#endif
    T result = (T)(TADDR)__sync_val_compare_and_swap((void* volatile *)destination, (void*)comparand, (void*)exchange);
    InterlockedOperationBarrier();
    return result;
#endif
}

#ifdef HOST_64BIT
// Performs an atomic compare-and-exchange operation on a 16-byte value, which has to be aligned to 16 bytes.
// Parameters:
//  destination     - value to be exchanged, two 64-bit halves with the low one first
//  exchangeHigh    - high half of the value to set the destination to
//  exchangeLow     - low half of the value to set the destination to
//  comparandResult - value to compare the destination to before setting it to the exchange, two halves with the low one first.
//                    The destination is set only if the destination is equal to the comparand.
//                    Receives the original value of the destination.
// Return:
//  true if the destination was set
FORCEINLINE bool Interlocked::CompareExchange128(int64_t volatile *destination, int64_t exchangeHigh, int64_t exchangeLow, int64_t *comparandResult)
{
#ifdef _MSC_VER
    return _InterlockedCompareExchange128(destination, exchangeHigh, exchangeLow, comparandResult) != 0;
#elif defined(HOST_AMD64)
    // CMPXCHG16B in inline assembly: the compiler expands a 16-byte __sync_val_compare_and_swap inline only when it may emit the
    // instruction (-mcx16), and otherwise calls a library function, which may not be lock-free. This keeps the operation lock-free
    // without raising the baseline ISA for the rest of the build.
    bool exchanged;
    __asm__ __volatile__(
        "lock cmpxchg16b %[destination]"
        : [destination] "+m" (*destination),
          "+d" (comparandResult[1]),    // RDX:RAX is the comparand, and receives
          "+a" (comparandResult[0]),    // the current value when the exchange fails
          [exchanged] "=@ccz" (exchanged)
        : "c" (exchangeHigh),           // RCX:RBX is the value to store
          "b" (exchangeLow)
        : "cc", "memory");
    return exchanged;
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseCompareExchange128(destination, exchangeHigh, exchangeLow, comparandResult);
    }
#endif
    __uint128_t comparand = ((__uint128_t)(uint64_t)comparandResult[1] << 64) | (uint64_t)comparandResult[0];
    __uint128_t exchange = ((__uint128_t)(uint64_t)exchangeHigh << 64) | (uint64_t)exchangeLow;
    __uint128_t result = __sync_val_compare_and_swap((__uint128_t volatile *)destination, comparand, exchange);
    InterlockedOperationBarrier();
    comparandResult[0] = (int64_t)(uint64_t)result;
    comparandResult[1] = (int64_t)(uint64_t)(result >> 64);
    return result == comparand;
#endif
}
#endif // HOST_64BIT

template <typename T>
FORCEINLINE T Interlocked::CompareExchangeAcq(T volatile *destination, T exchange, T comparand)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
#if defined(HOST_ARM64)
    return (T)_InterlockedCompareExchange_acq((long*)destination, (long)exchange, (long)comparand);
#else
    return (T)_InterlockedCompareExchange((long*)destination, (long)exchange, (long)comparand);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseCompareExchangeAcq(destination, exchange, comparand);
    }
#endif
    __atomic_compare_exchange_n(destination, &comparand, exchange, /* weak */ false, __ATOMIC_ACQUIRE, __ATOMIC_ACQUIRE);
    return comparand;
#endif
}

template <>
FORCEINLINE size_t Interlocked::CompareExchangeAcq<size_t>(size_t volatile *destination, size_t exchange, size_t comparand)
{
#ifdef _MSC_VER
#if defined(HOST_ARM64)
    return (size_t)_InterlockedCompareExchange64_acq((volatile long long*)destination, (long long)exchange, (long long)comparand);
#else
    return (size_t)_InterlockedCompareExchange64((volatile long long*)destination, (long long)exchange, (long long)comparand);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseCompareExchangeAcq(destination, exchange, comparand);
    }
#endif
    __atomic_compare_exchange_n(destination, &comparand, exchange, /* weak */ false, __ATOMIC_ACQUIRE, __ATOMIC_ACQUIRE);
    return comparand;
#endif
}

template <typename T>
FORCEINLINE T Interlocked::CompareExchangeUnordered(T volatile *destination, T exchange, T comparand)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
#if defined(HOST_ARM64)
    return (T)_InterlockedCompareExchange_nf((long*)destination, (long)exchange, (long)comparand);
#else
    return (T)_InterlockedCompareExchange((long*)destination, (long)exchange, (long)comparand);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseCompareExchangeUnordered(destination, exchange, comparand);
    }
#endif
    __atomic_compare_exchange_n(destination, &comparand, exchange, /* weak */ false, __ATOMIC_RELAXED, __ATOMIC_RELAXED);
    return comparand;
#endif
}

template <>
FORCEINLINE size_t Interlocked::CompareExchangeUnordered<size_t>(size_t volatile *destination, size_t exchange, size_t comparand)
{
#ifdef _MSC_VER
#if defined(HOST_ARM64)
    return (size_t)_InterlockedCompareExchange64_nf((volatile long long*)destination, (long long)exchange, (long long)comparand);
#else
    return (size_t)_InterlockedCompareExchange64((volatile long long*)destination, (long long)exchange, (long long)comparand);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return LseCompareExchangeUnordered(destination, exchange, comparand);
    }
#endif
    __atomic_compare_exchange_n(destination, &comparand, exchange, /* weak */ false, __ATOMIC_RELAXED, __ATOMIC_RELAXED);
    return comparand;
#endif
}

template <typename T>
FORCEINLINE T Interlocked::DecrementRel(T volatile *addend)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
#if defined(HOST_ARM64)
    return (T)_InterlockedDecrement_rel((long*)addend);
#else
    return (T)_InterlockedDecrement((long*)addend);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        return (T)(LseFetchAddRel(addend, (T)-1) - 1);
    }
#endif
    return __atomic_sub_fetch(addend, 1, __ATOMIC_RELEASE);
#endif
}

template <typename T>
FORCEINLINE void Interlocked::AndUnordered(T volatile *destination, T value)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
#if defined(HOST_ARM64)
    _InterlockedAnd_nf((long*)destination, (long)value);
#else
    _InterlockedAnd((long*)destination, (long)value);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchClearUnordered(destination, (T)~value);
        return;
    }
#endif
    __atomic_fetch_and(destination, value, __ATOMIC_RELAXED);
#endif
}

template <>
FORCEINLINE void Interlocked::AndUnordered<size_t>(size_t volatile *destination, size_t value)
{
#ifdef _MSC_VER
#if defined(HOST_ARM64)
    _InterlockedAnd64_nf((volatile long long*)destination, (long long)value);
#else
    _InterlockedAnd64((volatile long long*)destination, (long long)value);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchClearUnordered(destination, ~value);
        return;
    }
#endif
    __atomic_fetch_and(destination, value, __ATOMIC_RELAXED);
#endif
}

template <typename T>
FORCEINLINE void Interlocked::OrRel(T volatile *destination, T value)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
#if defined(HOST_ARM64)
    _InterlockedOr_rel((long*)destination, (long)value);
#else
    _InterlockedOr((long*)destination, (long)value);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchOrRel(destination, value);
        return;
    }
#endif
    __atomic_fetch_or(destination, value, __ATOMIC_RELEASE);
#endif
}

template <>
FORCEINLINE void Interlocked::OrRel<size_t>(size_t volatile *destination, size_t value)
{
#ifdef _MSC_VER
#if defined(HOST_ARM64)
    _InterlockedOr64_rel((volatile long long*)destination, (long long)value);
#else
    _InterlockedOr64((volatile long long*)destination, (long long)value);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchOrRel(destination, value);
        return;
    }
#endif
    __atomic_fetch_or(destination, value, __ATOMIC_RELEASE);
#endif
}

template <typename T>
FORCEINLINE void Interlocked::OrUnordered(T volatile *destination, T value)
{
#ifdef _MSC_VER
    static_assert(sizeof(long) == sizeof(T), "Size of long must be the same as size of T");
#if defined(HOST_ARM64)
    _InterlockedOr_nf((long*)destination, (long)value);
#else
    _InterlockedOr((long*)destination, (long)value);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchOrUnordered(destination, value);
        return;
    }
#endif
    __atomic_fetch_or(destination, value, __ATOMIC_RELAXED);
#endif
}

template <>
FORCEINLINE void Interlocked::OrUnordered<size_t>(size_t volatile *destination, size_t value)
{
#ifdef _MSC_VER
#if defined(HOST_ARM64)
    _InterlockedOr64_nf((volatile long long*)destination, (long long)value);
#else
    _InterlockedOr64((volatile long long*)destination, (long long)value);
#endif
#else
#ifdef INTERLOCKED_ARM64_LSE
    if (__builtin_expect(UseLseAtomics(), 1))
    {
        LseFetchOrUnordered(destination, value);
        return;
    }
#endif
    __atomic_fetch_or(destination, value, __ATOMIC_RELAXED);
#endif
}

#endif // __GCENV_INTERLOCKED_INL__
