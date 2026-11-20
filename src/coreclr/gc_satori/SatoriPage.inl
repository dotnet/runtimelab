// Copyright (c) 2025 Vladimir Sadov
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
// SatoriPage.inl
//

#ifndef __SATORI_PAGE_INL__
#define __SATORI_PAGE_INL__

#include "SatoriUtil.h"
#include "SatoriPage.h"
#include "SatoriRegion.h"

inline size_t SatoriPage::Start()
{
    return (size_t)this;
}

inline size_t SatoriPage::End()
{
    return m_end;
}

inline size_t SatoriPage::RegionsStart()
{
    return m_firstRegion;
}

inline size_t SatoriPage::GranuleCount()
{
    return (End() - Start()) >> Satori::REGION_BITS;
}

inline size_t SatoriPage::FirstGranule()
{
    return (m_firstRegion - Start()) >> Satori::REGION_BITS;
}

inline size_t SatoriPage::GranuleStart(size_t granule)
{
    return Start() + (granule << Satori::REGION_BITS);
}

inline uint8_t* SatoriPage::RegionMap()
{
    return m_regionMap;
}

inline SatoriHeap* SatoriPage::Heap()
{
    return m_heap;
}

// The count of the registered readers of unowned headers, see EnterUnownedHeaderRead. It has a cache line of its own: the last 64 bytes
// before the first useful card word, in the part of the page that the card table has for covering itself. Every thread that takes the next
// region of its own writes it twice, when it registers and when it leaves. It is not in the header, where the line has the card state and
// the scan ticket (the GC writes them for every scan) and most of the fields that every claim reads (m_end, m_heap, m_regionMap).
// It is not next to the bitmaps that FreeRegion writes (the seen bits are the last of them) either: in a 1Gb page the two lines in front of it
// are not used, and in bigger pages more.
// It is 0 from the start, as is all the committed memory of the card table.
inline volatile int32_t* SatoriPage::UnownedHeaderReaders()
{
    return (volatile int32_t*)(Start() + m_cardTableStart - 64);
}

inline void SatoriPage::EnterUnownedHeaderRead()
{
    // The registration is a store, and the caller's next step is a load: the region map, in NextInPage.
    // Either the trimmer's look at the count sees the registration, or the caller reads the map after that look
    // and finds the region gone. That holds only if the registration is ordered before the read of the map,
    // and an acquire or a release alone does not order a store before a later load. What does:
    //  - x64: the locked instruction is a full fence.
    //  - ARM64 with the LSE atomics: ldaddal, one instruction that is both an acquire and a release. No barrier follows, and none
    //    is needed: such an instruction is a full barrier by itself (the PAL and the Linux kernel rely on the same).
    //  - ARM64 without them, GCC and Clang: a full barrier ends the ldaxr/stlxr loop (see InterlockedOperationBarrier).
    //  - ARM64 without them, MSVC: the intrinsic is an ldaxr/stlxr loop with no barrier after it, so only its store is a release.
    //    It is ordered before the read of the map because the map is read with an acquire (see NextInPage), and a store-release
    //    is ordered before a later load-acquire.
    // So this stays the full interlocked increment (an acquire or a release form of it is not enough), and the map stays an acquire load.
    Interlocked::Increment(UnownedHeaderReaders());
}

inline void SatoriPage::LeaveUnownedHeaderRead()
{
    // A release: the reads of the header are done before the trimmer can see that we are gone. No fence after it is needed,
    // nothing that we do next depends on the trimmer.
    Interlocked::DecrementRel(UnownedHeaderReaders());
}

// order is unimportant, but we want to read it only once when we read it, thus volatile.
inline volatile int8_t& SatoriPage::CardState()
{
    return m_cardState;
}

// order is unimportant, but we want to read it only once when we read it, thus volatile.
inline volatile uint8_t& SatoriPage::ScanTicket()
{
    return m_scanTicket;
}

// order is unimportant, but we want to read it once when we read it, thus volatile.
inline volatile int8_t& SatoriPage::CardGroupState(size_t i)
{
    return m_cardGroups[i * 2];
}

// order is unimportant, but we want to read it once when we read it, thus volatile.
inline volatile uint8_t& SatoriPage::CardGroupScanTicket(size_t i)
{
    return (uint8_t&)m_cardGroups[i * 2 + 1];
}

inline size_t SatoriPage::CardGroupCount()
{
    return (End() - Start()) / Satori::BYTES_PER_CARD_GROUP;
}

inline int8_t* SatoriPage::CardsForGroup(size_t i)
{
    return &m_cardTable[i * Satori::CARD_BYTES_IN_CARD_GROUP];
}

inline size_t SatoriPage::LocationForCard(int8_t* cardPtr)
{
    return Start() + ((size_t)cardPtr - Start()) * Satori::BYTES_PER_CARD_BYTE;
}

// Visits the regions of the page from the end of the page down. Free granules are skipped.
template <typename F>
    void SatoriPage::ForEachRegion(F lambda)
    {
        size_t first = FirstGranule();
        size_t granule = GranuleCount();

        while (granule > first)
        {
            granule--;
            if (RegionMap()[granule] == 0)
            {
                // a free granule.
                continue;
            }

            SatoriRegion* region = RegionForAddressChecked(GranuleStart(granule));
            if (!region)
            {
                // the region is gone.
                continue;
            }

            // continue below the region. The lambda may change it.
            granule = (region->Start() - Start()) >> Satori::REGION_BITS;
            lambda(region);
        }
    }

#endif
