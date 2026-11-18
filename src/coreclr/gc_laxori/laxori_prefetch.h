// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_PREFETCH_H
#define LAXORI_PREFETCH_H

#include <stddef.h>
#include <stdint.h>

#if defined(_MSC_VER) && (defined(_M_X64) || defined(_M_IX86))
#include <xmmintrin.h>
#endif

// P28 (F10). The software prefetch of the loops over increments, decrements and trace entries, as in the Java LAXORI
// (mmtk-core plan/laxori/rc.rs: process_incs, process_decs; cm.rs; args.rs: PREFETCH = on, PREFETCH_STEP = 8, PREFETCH_HEADER = on,
// PREFETCH_RC = off). While the loop works on entry i, the object that entry i + 8 names is prefetched, with the hint for data
// that is read once (_MM_HINT_NTA: util/address.rs Address::prefetch_read). A prefetch never changes a result.
// -DLAXORI_NO_PREFETCH removes it.
namespace LaxoriPrefetch
{
    constexpr size_t Step = 8;

#ifdef LAXORI_F10_PREFETCH_TEST_HOOK
    typedef void (*Hook)(const void* address);
    inline Hook& HookRef()
    {
        static Hook hook = nullptr;
        return hook;
    }
#endif // LAXORI_F10_PREFETCH_TEST_HOOK

    inline void Read(const void* address)
    {
#ifdef LAXORI_F10_PREFETCH_TEST_HOOK
        if (HookRef() != nullptr)
        {
            HookRef()(address);
        }
#endif // LAXORI_F10_PREFETCH_TEST_HOOK
#if defined(LAXORI_NO_PREFETCH)
        (void)address;
#elif defined(_MSC_VER) && (defined(_M_X64) || defined(_M_IX86))
        _mm_prefetch(static_cast<const char*>(address), _MM_HINT_NTA);
#elif defined(_MSC_VER) && defined(_M_ARM64)
        __prefetch(address);
#else
        __builtin_prefetch(address, 0, 0);
#endif
    }

    // entries[index + Step] names an object (entriesAreSlots = false: the entry is the object, minus the flag bits in flagMask)
    // or a slot that holds the object (entriesAreSlots = true). Nothing is prefetched past the end of the entries, or for a null
    // reference. A slot is read here, as in the Java loop (s.load()); the object is not read.
    inline void Ahead(const uintptr_t* entries, size_t count, size_t index, bool entriesAreSlots, uintptr_t flagMask = 0)
    {
#ifdef LAXORI_F10_PREFETCH_OFFBYONE_CONTROL
        size_t ahead = index + Step + 1;
#else
        size_t ahead = index + Step;
#endif // LAXORI_F10_PREFETCH_OFFBYONE_CONTROL
        if (ahead >= count)
        {
            return;
        }
        uintptr_t entry = entries[ahead] & ~flagMask;
        uintptr_t object = entriesAreSlots ? (entry == 0 ? 0 : *reinterpret_cast<const uintptr_t*>(entry)) : entry;
        if (object != 0)
        {
            Read(reinterpret_cast<const void*>(object));
        }
    }

    // Two-stage prefetch of the loop over the slots of the child increments (ProcessChildIncrements). While the loop works on entry i,
    // the line of the slot of entry i + SlotStep is prefetched (the address is known: nothing is loaded), and the slot of entry
    // i + Step is loaded to prefetch its target. That slot line was prefetched SlotStep - Step entries earlier, so the load does not wait
    // for memory. Ahead loads the slot of entry i + Step without an earlier prefetch of its line.
    constexpr size_t SlotStep = 16;

    inline void SlotLineAhead(const uintptr_t* slots, size_t count, size_t index)
    {
        size_t ahead = index + SlotStep;
        if (ahead < count && slots[ahead] != 0)
        {
            Read(reinterpret_cast<const void*>(slots[ahead]));
        }
    }
}

#endif // LAXORI_PREFETCH_H
