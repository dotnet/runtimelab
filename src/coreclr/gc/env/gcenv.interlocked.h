// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Interlocked operations
//

#ifndef __GCENV_INTERLOCKED_H__
#define __GCENV_INTERLOCKED_H__

#if defined(HOST_ARM64) && !defined(_MSC_VER)
// With GCC and Clang the operations are LSE instructions (ARMv8.1) in inline assembly when the processor has them,
// and loops of exclusive loads and stores otherwise (see UseLseAtomics).
#define INTERLOCKED_ARM64_LSE
#ifndef __ARM_FEATURE_ATOMICS
// The build does not require the LSE atomics (the compiler may not use them), so whether the processor has them is checked at run time.
// Where the build requires them (macOS, for one) the operations use them without a check.
#define INTERLOCKED_ARM64_LSE_DETECTION
#endif
#endif

// Interlocked operations
class Interlocked
{
private:

#ifndef _MSC_VER
    static void InterlockedOperationBarrier();
#endif // !_MSC_VER

#ifdef INTERLOCKED_ARM64_LSE
    static bool UseLseAtomics();

    // One LSE instruction each, see gcenv.interlocked.inl. They return the value that the destination had.
    // No suffix: both an acquire and a release. Acq, Rel and Unordered: as for the operations below.
    template<typename T> static T LseFetchAdd(T volatile *destination, T value);
    template<typename T> static T LseFetchAddRel(T volatile *destination, T value);
    template<typename T> static T LseFetchClear(T volatile *destination, T bits);
    template<typename T> static T LseFetchClearUnordered(T volatile *destination, T bits);
    template<typename T> static T LseFetchOr(T volatile *destination, T bits);
    template<typename T> static T LseFetchOrRel(T volatile *destination, T bits);
    template<typename T> static T LseFetchOrUnordered(T volatile *destination, T bits);
    template<typename T> static T LseSwap(T volatile *destination, T value);
    template<typename T> static T LseCompareExchange(T volatile *destination, T exchange, T comparand);
    template<typename T> static T LseCompareExchangeAcq(T volatile *destination, T exchange, T comparand);
    template<typename T> static T LseCompareExchangeUnordered(T volatile *destination, T exchange, T comparand);
    // CASPAL, see CompareExchange128.
    static bool LseCompareExchange128(int64_t volatile *destination, int64_t exchangeHigh, int64_t exchangeLow, int64_t *comparandResult);
#endif // INTERLOCKED_ARM64_LSE

public:

#ifdef INTERLOCKED_ARM64_LSE_DETECTION
    // Whether the processor has the LSE atomics. Set when the module is loaded, on Linux, FreeBSD, iOS and tvOS, see gcenv.unix.cpp. Elsewhere, and until then,
    // the operations use the exclusive loads and stores, which is correct as well: the two kinds of operations are atomic with respect to each other.
    static inline bool s_arm64LseAtomics = false;
#endif // INTERLOCKED_ARM64_LSE_DETECTION

    // Increment the value of the specified 32-bit variable as an atomic operation.
    // Parameters:
    //  addend - variable to be incremented
    // Return:
    //  The resulting incremented value
    template<typename T>
    static T Increment(T volatile *addend);

    // Decrement the value of the specified 32-bit variable as an atomic operation.
    // Parameters:
    //  addend - variable to be decremented
    // Return:
    //  The resulting decremented value
    template<typename T>
    static T Decrement(T volatile *addend);

    // Perform an atomic AND operation on the specified values values
    // Parameters:
    //  destination - the first operand and the destination
    //  value       - second operand
    template<typename T>
    static void And(T volatile *destination, T value);

    // Perform an atomic OR operation on the specified values values
    // Parameters:
    //  destination - the first operand and the destination
    //  value       - second operand
    template<typename T>
    static void Or(T volatile *destination, T value);

    // Set a 32-bit variable to the specified value as an atomic operation.
    // Parameters:
    //  destination - value to be exchanged
    //  value       - value to set the destination to
    // Return:
    //  The previous value of the destination
    template<typename T>
    static T Exchange(T volatile *destination, T value);

    // Set a pointer variable to the specified value as an atomic operation.
    // Parameters:
    //  destination - value to be exchanged
    //  value       - value to set the destination to
    // Return:
    //  The previous value of the destination
    template <typename T>
    static T ExchangePointer(T volatile * destination, T value);

    template <typename T>
    static T ExchangePointer(T volatile * destination, std::nullptr_t value);

    // Perform an atomic addition of two 32-bit values and return the original value of the addend.
    // Parameters:
    //  addend - variable to be added to
    //  value  - value to add
    // Return:
    //  The previous value of the addend
    template<typename T>
    static T ExchangeAdd(T volatile *addend, T value);

    template<typename T>
    static T ExchangeAdd64(T volatile* addend, T value);

    template<typename T>
    static T ExchangeAddPtr(T volatile* addend, T value);

    // Performs an atomic compare-and-exchange operation on the specified values.
    // Parameters:
    //  destination - value to be exchanged
    //  exchange    - value to set the destination to
    //  comparand   - value to compare the destination to before setting it to the exchange.
    //                The destination is set only if the destination is equal to the comparand.
    // Return:
    //  The original value of the destination
    template<typename T>
    static T CompareExchange(T volatile *destination, T exchange, T comparand);

    // Performs an atomic compare-and-exchange operation on the specified pointers.
    // Parameters:
    //  destination - value to be exchanged
    //  exchange    - value to set the destination to
    //  comparand   - value to compare the destination to before setting it to the exchange.
    //                The destination is set only if the destination is equal to the comparand.
    // Return:
    //  The original value of the destination
    template <typename T>
    static T CompareExchangePointer(T volatile *destination, T exchange, T comparand);

    template <typename T>
    static T CompareExchangePointer(T volatile *destination, T exchange, std::nullptr_t comparand);

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
    static bool CompareExchange128(int64_t volatile *destination, int64_t exchangeHigh, int64_t exchangeLow, int64_t *comparandResult);
#endif // HOST_64BIT

    // The operations above order both ways: a locked instruction on x86 and x64; on ARM64 an atomic that is both an acquire and a release,
    // and with GCC and Clang, when it is a loop of exclusive loads and stores rather than an LSE instruction, a full barrier after it
    // (see InterlockedOperationBarrier).
    // A CompareExchange that fails does not write, except that a loop of exclusive loads and stores for CompareExchange128 may store back
    // the value that it read, which leaves the destination as it was. Where no barrier follows it (an LSE instruction, or with MSVC a loop
    // of exclusive loads and stores) it then orders as an acquire load only: what the thread does next is ordered after the read, but an
    // earlier store is not ordered before a later load.
    // The ones below order no more than their names say.
    //   Acq:       what the thread does after the operation is ordered after the read of the destination.
    //   Rel:       what the thread did before the operation is ordered before the write of the destination.
    //   Unordered: atomic, and nothing more.
    // On x86 and x64 they are the same locked instructions as the ones above.

    // Same as CompareExchange, but an acquire, whether the exchange happens or not.
    template<typename T>
    static T CompareExchangeAcq(T volatile *destination, T exchange, T comparand);

    // Same as CompareExchange, but unordered.
    template<typename T>
    static T CompareExchangeUnordered(T volatile *destination, T exchange, T comparand);

    // Same as Decrement, but a release.
    template<typename T>
    static T DecrementRel(T volatile *addend);

    // Same as And, but unordered.
    template<typename T>
    static void AndUnordered(T volatile *destination, T value);

    // Same as Or, but a release.
    template<typename T>
    static void OrRel(T volatile *destination, T value);

    // Same as Or, but unordered.
    template<typename T>
    static void OrUnordered(T volatile *destination, T value);
};

#endif // __GCENV_INTERLOCKED_H__
