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
// SatoriPage.cpp
//

#include "common.h"

#include "gcenv.h"
#include "../env/gcenv.os.h"
#include "../gcenv.inl"

#include "SatoriUtil.h"
#include "SatoriPage.h"
#include "SatoriPage.inl"
#include "SatoriRegion.h"
#include "SatoriRegion.inl"
#include "SatoriRegionQueue.h"
#include <minipal/log.h>

// The bitmaps and the watermarks of the granules come after the region map, and must end before the line of the reader count, the last
// 64 bytes before the first useful card word. (see InitializeAt and UnownedHeaderReaders) The smallest page is the tightest fit:
// 128 bytes of slack at 1Gb, and 320 bytes more for every 1Gb more. ALIGN_UP is a function, so the rounding of the free bitmap's offset is written out.
static const size_t s_granulesInSmallestPage = Satori::PAGE_SIZE_GRANULARITY >> Satori::REGION_BITS;
static const size_t s_freeBitmapOffset =
    (128 + Satori::PAGE_SIZE_GRANULARITY / Satori::BYTES_PER_CARD_GROUP * 2 + s_granulesInSmallestPage + 63) & ~(size_t)63;
static_assert(
    s_freeBitmapOffset + 2 * s_granulesInSmallestPage + 3 * (s_granulesInSmallestPage / 8) <=
        Satori::PAGE_SIZE_GRANULARITY / Satori::BYTES_PER_CARD_BYTE / Satori::BYTES_PER_CARD_BYTE - 64,
    "the bitmaps and the watermarks of the granules must end before the line of the reader count.");

SatoriPage* SatoriPage::InitializeAt(size_t address, size_t pageSize, SatoriHeap* heap, InitStatus& status)
{
    _ASSERTE(pageSize % Satori::PAGE_SIZE_GRANULARITY == 0);

    VirtualReserveStatus reserveStatus = VirtualReserveStatus::Refused;
    SatoriPage* result = (SatoriPage*)GCToOSInterface::VirtualReserve((void*)address, pageSize, SatoriUtil::UseTHP(), &reserveStatus);
    if (result == nullptr)
    {
        status = reserveStatus == VirtualReserveStatus::Occupied ?
            InitStatus::Occupied :
            InitStatus::ReserveRefused;

        return nullptr;
    }

    size_t cardTableSize = pageSize / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(cardTableSize % Satori::REGION_SIZE_GRANULARITY == 0);

    // when commit granularity is larger than region granularity (1Gb), commit the entire page.
    // we can't have sparse commit coarser than regions.
    // besides we would end up comitting the entire page anyways once the obj size
    // is aligned up and committed.
    // Where the card slices are not decommitted (see SatoriUtil::CanDecommitCardSlices), the card table is committed in full and stays so.
    // It is 2Mb per 1Gb of the page and starts where the page does, so with THP huge pages can back it.
    // Otherwise only the slices of the card table's own granules are committed: the header and the metadata, and the slices that share
    // an OS page with them. The slices of the other granules are committed when the granules are claimed (see FormatClaimed).
    size_t firstGranule = cardTableSize >> Satori::REGION_BITS;
    size_t pinnedSliceGranules = ALIGN_UP(firstGranule, SatoriUtil::CardSliceGroupGranules());
    size_t commitSize = pinnedSliceGranules * Satori::CARD_SLICE_SIZE;
    if (SatoriUtil::CommitGranularity() > Satori::REGION_SIZE_GRANULARITY)
    {
        commitSize = pageSize;
    }
    else if (!SatoriUtil::CanDecommitCardSlices())
    {
        commitSize = cardTableSize;
    }

    // Where the slices are not decommitted, every granule has its cards from the start: no granule gets the WATERMARK_NO_CARDS bit below.
    _ASSERTE(SatoriUtil::CanDecommitCardSlices() ? commitSize < cardTableSize : commitSize >= cardTableSize);

    if (!GCToOSInterface::VirtualCommit((void*)address, commitSize))
    {
        GCToOSInterface::VirtualRelease(result, pageSize);
        status = InitStatus::CommitFailed;
        return nullptr;
    }

    heap->IncBytesCommitted(commitSize);
    result->m_end = address + pageSize;
    result->m_firstRegion = address + cardTableSize;
    result->m_initialCommit = address + commitSize;
    result->m_cardTableSize = cardTableSize;

    // conservatively assume the first useful card word to cover the start of the first region.
    size_t cardTableStart = (result->m_firstRegion - address) / Satori::BYTES_PER_CARD_BYTE;
    // this is also region map size
    size_t regionCount = pageSize >> Satori::REGION_BITS;
    size_t cardGroupSize = pageSize / Satori::BYTES_PER_CARD_GROUP * 2;

    // initializing cards to EPHEMERAL is optional. 0 is ok too.
    // for huge pages it is not as useful and may get expensive.
    // also if the huge obj contains references, its region will go to gen2 anyways
    // Only the committed cards are initialized here, the others when their granules are claimed (see TryCommitCardSlices).
    if (pageSize == Satori::PAGE_SIZE_GRANULARITY)
    {
        size_t cardsCommitted = min(commitSize, cardTableSize);
        size_t initialized = cardsCommitted > cardTableStart ? cardsCommitted - cardTableStart : 0;
#if _DEBUG
        // in debug we initialize only half the cards, since it is optional...
        memset(&result->m_cardTable[cardTableStart], Satori::CardState::EPHEMERAL, initialized / 2);
#else
        memset(&result->m_cardTable[cardTableStart], Satori::CardState::EPHEMERAL, initialized);
#endif
        // We leave card groups blank as we do not want to look at them when they may not even be covered by regions.
        // We maintain the invariant that groups are not set for allocator regions.
        // We do not use EPHEMERAL for groups.
    }

    result->m_cardTableStart = cardTableStart;
    result->m_heap = heap;

    // make sure offset of m_cardGroups is 128.
    _ASSERTE(offsetof(SatoriPage, m_cardGroups) == 128);
#if _DEBUG
    static_assert(offsetof(SatoriPage, m_snapshotStamp) + sizeof(size_t) <= 128,
        "the header fields must end before the card groups.");
#else
    static_assert(offsetof(SatoriPage, m_seenFreeBitmap) + sizeof(size_t*) <= 128,
        "the header fields must end before the card groups.");
#endif

    result->m_regionMap = (uint8_t*)(address + 128 + cardGroupSize);

    // The free bitmap is one cache line per 1Gb. The three bitmaps and the two watermarks need 19 bits per granule
    // on top of the region map, and the card table that covers itself has room for that at any page size.
    _ASSERTE(regionCount % 64 == 0);
    result->m_freeBitmap = (size_t*)ALIGN_UP((size_t)result->m_regionMap + regionCount, 64);
    result->m_warmBitmap = result->m_freeBitmap + regionCount / 64;
    result->m_commitWatermarks = (uint8_t*)(result->m_warmBitmap + regionCount / 64);
    result->m_dirtyWatermarks = result->m_commitWatermarks + regionCount;
    result->m_seenFreeBitmap = (size_t*)(result->m_dirtyWatermarks + regionCount);

    // make sure the first useful card word is beyond the header, and that the line of the reader count (the last 64 bytes before it,
    // see UnownedHeaderReaders) is not used by the bitmaps. The card words before the first useful one cover the card table itself, nothing uses them.
    // In a 1Gb page the seen bitmap ends two lines before the line of the reader count.
    _ASSERTE((size_t)(result->m_seenFreeBitmap + regionCount / 64) <= result->Start() + cardTableStart - 64);
    _ASSERTE(*result->UnownedHeaderReaders() == 0);

#if _DEBUG
    // The stamps do not fit into the page, they are for Checked builds only.
    result->m_freeStamps = new (nothrow) size_t[regionCount]();
    if (result->m_freeStamps == nullptr)
    {
        heap->DecBytesCommitted(commitSize);
        GCToOSInterface::VirtualRelease(result, pageSize);
        status = InitStatus::CommitFailed;
        return nullptr;
    }
#endif

    // Everything after the card table is free. If the commit granularity made us commit more than the card table,
    // that part is committed and clean.
    // The page is published in the heap only after this, so nobody can claim anything before it is ready.
    for (size_t granule = result->FirstGranule(); granule < regionCount; granule++)
    {
        size_t bit = (size_t)1 << (granule % 64);
        result->m_freeBitmap[granule / 64] |= bit;

        size_t granuleStart = result->GranuleStart(granule);
        size_t committedBytes = result->m_initialCommit > granuleStart ?
            min(result->m_initialCommit - granuleStart, Satori::REGION_SIZE_GRANULARITY) :
            0;

        size_t committedUnits = committedBytes >> Satori::WATERMARK_UNIT_BITS;
        result->m_commitWatermarks[granule] = (uint8_t)committedUnits;
        if (SatoriUtil::CanDecommitCardSlices() && granule >= pinnedSliceGranules)
        {
            _ASSERTE(committedUnits == 0);
            result->m_commitWatermarks[granule] = Satori::WATERMARK_NO_CARDS;
        }

        if (committedUnits == Satori::WATERMARK_UNITS_PER_GRANULE)
        {
            result->m_warmBitmap[granule / 64] |= bit;
        }
    }

    status = InitStatus::Success;
    return result;
}

void SatoriPage::OnRegionInitialized(SatoriRegion* region)
{
    _ASSERTE((size_t)region > Start() && (size_t)region < End());
    size_t startIndex = (region->Start() - Start()) >> Satori::REGION_BITS;
    size_t mapCount = region->Size() >> Satori::REGION_BITS;

    VolatileStore(&RegionMap()[startIndex], (uint8_t)1);
    for (int i = 1; i < mapCount; i++)
    {
        DWORD log2;
        BitScanReverse64(&log2, i);
        RegionMap()[startIndex + i] = (uint8_t)(log2 + 2);
    }

    // The header and the map are stored before any card state of the region, also when one thread stores both.
    // (a card scanner finds the region through a card group state, see RegionForCardGroup)
    VolatileStoreBarrier();
}

// The reverse of OnRegionInitialized. A lookup reads a byte once, so it finds the region as it was or nothing, in whatever order
// the bytes are cleared. The set of the free bits is the release for the clear. (see PublishFreeGranules)
void SatoriPage::OnRegionFreed(SatoriRegion* region)
{
    size_t startIndex = (region->Start() - Start()) >> Satori::REGION_BITS;
    size_t mapCount = region->Size() >> Satori::REGION_BITS;
    _ASSERTE(RegionMap()[startIndex] == 1);

    RegionMap()[startIndex] = 0;
    memset(&RegionMap()[startIndex + 1], 0, mapCount - 1);
}

// The bits [lowBit, lowBit + width) of a bitmap word.
static size_t BitRange(size_t lowBit, size_t width)
{
    _ASSERTE(width > 0 && lowBit + width <= 64);
    size_t ones = width == 64 ? ~(size_t)0 : (((size_t)1 << width) - 1);
    return ones << lowBit;
}

#if _DEBUG
// The counter that the stamps of the trimmer's check come from, see m_freeStamps.
static volatile size_t s_freeClock;
#endif

// Turns the granules of a region that nobody uses any more into free granules.
// Nothing is decommitted or written into the freed memory. What the next owner needs to know about it,
// how much is committed and how much of that may be dirty, is recorded in the watermarks.
void SatoriPage::FreeRegion(SatoriRegion* region)
{
    _ASSERTE(region->ContainingPage() == this);
    _ASSERTE(region->Generation() == -1);
    _ASSERTE(region->ContainingQueue() == nullptr);

    // read everything that is needed from the header while the region is ours.
    size_t start = region->Start();
    size_t end = region->End();
    size_t committed = region->CommittedEnd();
    size_t used = region->UsedEnd();

    _ASSERTE(start % Satori::REGION_SIZE_GRANULARITY == 0 && end % Satori::REGION_SIZE_GRANULARITY == 0);
    _ASSERTE(start >= m_firstRegion && end <= End());
    _ASSERTE(committed >= start && committed <= end);
    _ASSERTE(used >= start && used <= committed);
    // every commit is a whole number of watermark units
    _ASSERTE((committed - start) % Satori::WATERMARK_UNIT == 0);

    size_t first = (start - Start()) >> Satori::REGION_BITS;
    size_t count = (end - start) >> Satori::REGION_BITS;

    // A free granule's cards may be decommitted (see DecommitCardSlices): no scan of the cards may look at them.
    _ASSERTE(CardGroupsAreWipedForRange(start, end));

#if _DEBUG
    // In the conservative mode a thread that scans a stack may have found the region in the region map and still be reading its header.
    // Nothing frees a region while a thread scans a stack. (see SatoriAllocator::FreeToBitmap)
    _ASSERTE(!SatoriUtil::IsConservativeMode() || !m_heap->Recycler()->IsConservativeScanInProgress());

    // before the seen bits are cleared and the free bits set, see TryTrimRun
    // A full interlocked increment on purpose: the check needs the stamp ordered with the bit operations around it. Do not relax it.
    size_t freeStamp = Interlocked::Increment(&s_freeClock);
#endif

    size_t runBits = 0;
    size_t warmBits = 0;
    for (size_t granule = first; granule < first + count; granule++)
    {
        size_t granuleStart = GranuleStart(granule);
        size_t committedBytes = committed > granuleStart ?
            min(committed - granuleStart, Satori::REGION_SIZE_GRANULARITY) :
            0;
        size_t usedBytes = used > granuleStart ?
            min(used - granuleStart, Satori::REGION_SIZE_GRANULARITY) :
            0;

        // rounding up the used part is the safe direction. It cannot get above the committed part.
        size_t committedUnits = committedBytes >> Satori::WATERMARK_UNIT_BITS;
        size_t dirtyUnits = (usedBytes + Satori::WATERMARK_UNIT - 1) >> Satori::WATERMARK_UNIT_BITS;
        _ASSERTE(dirtyUnits <= committedUnits);

        m_commitWatermarks[granule] = (uint8_t)committedUnits;
        m_dirtyWatermarks[granule] = (uint8_t)dirtyUnits;
#if _DEBUG
        m_freeStamps[granule] = freeStamp;
#endif

        size_t bit = BitRange(granule % 64, 1);
        runBits |= bit;
        if (committedUnits == Satori::WATERMARK_UNITS_PER_GRANULE)
        {
            warmBits |= bit;
        }

        // The warm bits of a word are written once its granules are done.
        if (granule % 64 == 63 || granule == first + count - 1)
        {
            // The other granules in the word may be changing, thus atomic. No order is needed: the set of the free bits below is the release for it.
            // Only the owner of a granule writes its warm bit, and the granules are ours, so the bits that we read are the last ones written.
            // A free that does not change the bits writes nothing.
            size_t word = granule / 64;
            size_t wasWarm = VolatileLoadWithoutBarrier(&m_warmBitmap[word]);
            size_t toSet = warmBits & ~wasWarm;
            size_t toClear = runBits & ~warmBits & wasWarm;
            if (toSet != 0)
            {
                Interlocked::OrUnordered(&m_warmBitmap[word], toSet);
            }

            if (toClear != 0)
            {
                Interlocked::AndUnordered(&m_warmBitmap[word], ~toClear);
            }

            runBits = 0;
            warmBits = 0;
        }
    }

    // These granules are not the ones that the trimmer saw free. The trimmer reads a free bit before it reads the seen bit,
    // and the interlocked operation that sets the free bits below is a release for this one.
    ForgetSeenFree(first, count);

    OnRegionFreed(region);

    // The interlocked operation is a release for everything above. The next owner reads
    // the watermarks and the region map only after it clears its free bit.
    PublishFreeGranules(first, count);
}

// Sets the free bits of the granules.
// The granules must be ready for the next owner: the watermarks written and the region map bytes zero.
// (A claim that did not format anything gives the granules back with the watermarks that it found.)
// The set is a release: what the caller wrote about the granules is ordered before it, and a claim, which is an acquire, reads it after.
// The trimmer's look at the readers of unowned headers relies on the same pair (see TryTrimRun). Neither may be weakened.
void SatoriPage::PublishFreeGranules(size_t first, size_t count)
{
    _ASSERTE(first >= FirstGranule() && first + count <= GranuleCount());
    for (size_t word = first / 64; word <= (first + count - 1) / 64; word++)
    {
        size_t lowGranule = max(first, word * 64);
        size_t highGranule = min(first + count, word * 64 + 64);
        size_t bits = BitRange(lowGranule - word * 64, highGranule - lowGranule);
        _ASSERTE((m_freeBitmap[word] & bits) == 0);
        Interlocked::OrRel(&m_freeBitmap[word], bits);
    }
}

// Claims the granule that has the lowest (or the highest) address of the free ones. When asked, only among the warm ones.
// Returns SIZE_MAX if there is none.
size_t SatoriPage::TryClaimGranule(bool warmOnly, bool fromTop)
{
    size_t wordCount = GranuleCount() / 64;
    for (size_t i = 0; i < wordCount; i++)
    {
        size_t word = fromTop ? wordCount - 1 - i : i;
        size_t freeBits = VolatileLoadWithoutBarrier(&m_freeBitmap[word]);
        while (true)
        {
            size_t candidates = warmOnly ? (freeBits & VolatileLoadWithoutBarrier(&m_warmBitmap[word])) : freeBits;
            if (candidates == 0)
            {
                break;
            }

            DWORD bit;
            if (fromTop)
            {
                BitScanReverse64(&bit, candidates);
            }
            else
            {
                BitScanForward64(&bit, candidates);
            }

            // an acquire: what the last owner wrote before it set the bit is read after this. (see PublishFreeGranules)
            size_t seen = Interlocked::CompareExchangeAcq(&m_freeBitmap[word], freeBits & ~((size_t)1 << bit), freeBits);
            if (seen == freeBits)
            {
                return word * 64 + bit;
            }

            // the word has changed. Whether our bit is still there is decided by the next round.
            freeBits = seen;
        }
    }

    return SIZE_MAX;
}

// Claims the granule, if it is free.
bool SatoriPage::TryClaimGranuleAt(size_t granule)
{
    _ASSERTE(granule >= FirstGranule() && granule < GranuleCount());

    size_t bit = (size_t)1 << (granule % 64);
    size_t freeBits = VolatileLoadWithoutBarrier(&m_freeBitmap[granule / 64]);
    while (freeBits & bit)
    {
        // an acquire: what the last owner wrote before it set the bit is read after this. (see PublishFreeGranules)
        size_t seen = Interlocked::CompareExchangeAcq(&m_freeBitmap[granule / 64], freeBits & ~bit, freeBits);
        if (seen == freeBits)
        {
            return true;
        }

        freeBits = seen;
    }

    return false;
}

// The free bits of a word, and when asked, only the free granules that are committed in full.
// The warm bit of a granule is written before its free bit is set, so a free granule has its warm bit.
size_t SatoriPage::FreeWord(size_t word, bool warmOnly)
{
    size_t bits = m_freeBitmap[word];
    return warmOnly ? bits & m_warmBitmap[word] : bits;
}

// Finds the first granule of a run of free granules that is the lowest (or the highest) among the runs that fit.
// When asked, only among the runs of free granules that are committed in full.
// The free bits are read as they are, claimers of single granules could change them meanwhile.
size_t SatoriPage::FindRun(size_t count, bool fromTop, bool warmOnly)
{
    _ASSERTE(count > 1);

    size_t first = FirstGranule();
    size_t end = GranuleCount();
    size_t runLength = 0;

    if (!fromTop)
    {
        size_t granule = first;
        while (granule < end)
        {
            size_t word = FreeWord(granule / 64, warmOnly);
            size_t bit = granule % 64;
            if (bit == 0 && word == 0)
            {
                // nothing is free in the word
                runLength = 0;
                granule += 64;
                continue;
            }

            if ((word >> bit) & 1)
            {
                // this is the end of the run if it is long enough.
                if (++runLength == count)
                {
                    return granule + 1 - count;
                }
            }
            else
            {
                runLength = 0;
            }

            granule++;
        }
    }
    else
    {
        // the granule before this one is the next to look at
        size_t granule = end;
        while (granule > first)
        {
            size_t current = granule - 1;
            size_t word = FreeWord(current / 64, warmOnly);
            size_t bit = current % 64;
            if (bit == 63 && word == 0)
            {
                runLength = 0;
                granule -= 64;
                continue;
            }

            if ((word >> bit) & 1)
            {
                // this is the start of the run if it is long enough.
                if (++runLength == count)
                {
                    return current;
                }
            }
            else
            {
                runLength = 0;
            }

            granule--;
        }
    }

    return SIZE_MAX;
}

// The bits of the run [first, first + count) that are in the word.
static size_t RunBitsInWord(size_t first, size_t count, size_t word)
{
    size_t lowGranule = max(first, word * 64);
    size_t highGranule = min(first + count, word * 64 + 64);
    return BitRange(lowGranule - word * 64, highGranule - lowGranule);
}

// Clears the free bits of the run, if all of them are set. Otherwise leaves everything as it was.
bool SatoriPage::TryClearRun(size_t first, size_t count)
{
    size_t firstWord = first / 64;
    size_t lastWord = (first + count - 1) / 64;
    for (size_t word = firstWord; word <= lastWord; word++)
    {
        size_t runBits = RunBitsInWord(first, count, word);
        size_t freeBits = VolatileLoadWithoutBarrier(&m_freeBitmap[word]);
        while (true)
        {
            if ((freeBits & runBits) != runBits)
            {
                // Someone has taken a granule of the run. Put back what we have cleared.
                // Unordered: nothing was written while the bits were ours.
                for (size_t cleared = firstWord; cleared < word; cleared++)
                {
                    Interlocked::OrUnordered(&m_freeBitmap[cleared], RunBitsInWord(first, count, cleared));
                }

                return false;
            }

            // an acquire: what the last owner wrote before it set the bit is read after this. (see PublishFreeGranules)
            size_t seen = Interlocked::CompareExchangeAcq(&m_freeBitmap[word], freeBits & ~runBits, freeBits);
            if (seen == freeBits)
            {
                break;
            }

            freeBits = seen;
        }
    }

    return true;
}

size_t SatoriPage::TryClaimRun(size_t count, bool fromTop, bool warmOnly)
{
    // Every failed attempt means that somebody claimed a granule of the run that we found. That is rare,
    // and it is not worth looking for long on this page if it keeps happening.
    for (int attempt = 0; attempt < 4; attempt++)
    {
        size_t first = FindRun(count, fromTop, warmOnly);
        if (first == SIZE_MAX || TryClearRun(first, count))
        {
            return first;
        }
    }

    return SIZE_MAX;
}

bool SatoriPage::IsCommittedInFull(size_t first, size_t count)
{
    for (size_t granule = first; granule < first + count; granule++)
    {
        if (m_commitWatermarks[granule] != Satori::WATERMARK_UNITS_PER_GRANULE)
        {
            return false;
        }
    }

    return true;
}

// Gives back a page that was not published. The only thing committed in it is the card table: its header, and the slices that a claim
// of the page's region (see SatoriHeap::AddLargePage) committed, or all of it where the slices are not decommitted.
void SatoriPage::Release()
{
#if _DEBUG
    delete[] m_freeStamps;
#endif
    m_heap->DecBytesCommitted(CardTableCommittedBytes() + (m_initialCommit > m_firstRegion ? m_initialCommit - m_firstRegion : 0));
    GCToOSInterface::VirtualRelease(this, m_end - Start());
}

// Clears the seen bits of the granules, see FreeRegion. The trimmer does not take them until a later look sees them free
// (see RememberFreeGranules).
void SatoriPage::ForgetSeenFree(size_t first, size_t count)
{
    for (size_t word = first / 64; word <= (first + count - 1) / 64; word++)
    {
        // atomic, no order: the free bits that are set after this are the release for it.
        Interlocked::AndUnordered(&m_seenFreeBitmap[word], ~RunBitsInWord(first, count, word));
    }
}

// The granules from 0 whose card slices are always committed: the card table's own granules, whose slices hold the header and the metadata,
// and the granules whose slices share an OS page with those.
size_t SatoriPage::PinnedCardSliceGranules()
{
    return ALIGN_UP(FirstGranule(), SatoriUtil::CardSliceGroupGranules());
}

size_t SatoriPage::CardTableCommittedBytes()
{
    if (!SatoriUtil::CanDecommitCardSlices())
    {
        return min(m_initialCommit, m_firstRegion) - Start();
    }

    size_t slices = PinnedCardSliceGranules();
    for (size_t granule = slices; granule < GranuleCount(); granule++)
    {
        if ((m_commitWatermarks[granule] & Satori::WATERMARK_NO_CARDS) == 0)
        {
            slices++;
        }
    }

    return slices * Satori::CARD_SLICE_SIZE;
}

// The claimer: commits the card slices of the claimed granules that do not have them, before the granules' memory is committed and
// before the region is published, and clears their WATERMARK_NO_CARDS bits. The granules are ours.
// A recommitted slice is zero, which is a valid state of cards (BLANK); it is set to EPHEMERAL, as the cards of a new page are, so that
// the write barrier does not mark the cards of an ephemeral region (it marks a card only when it is BLANK).
// With OS pages larger than a slice, the OS pages that cover the slices are committed (they may be committed already, for the
// neighbors that share them), and only the slices of our granules are written.
// Returns false if a commit failed. The slices that were committed stay so, and their bits say it.
bool SatoriPage::TryCommitCardSlices(size_t first, size_t count)
{
    size_t osPage = GCToOSInterface::GetPageSize();
    size_t granule = first;
    while (granule < first + count)
    {
        if ((m_commitWatermarks[granule] & Satori::WATERMARK_NO_CARDS) == 0)
        {
            granule++;
            continue;
        }

        size_t runEnd = granule + 1;
        while (runEnd < first + count && (m_commitWatermarks[runEnd] & Satori::WATERMARK_NO_CARDS))
        {
            runEnd++;
        }

        size_t sliceStart = Start() + granule * Satori::CARD_SLICE_SIZE;
        size_t sliceEnd = Start() + runEnd * Satori::CARD_SLICE_SIZE;
        size_t commitStart = ALIGN_DOWN(sliceStart, osPage);
        size_t commitEnd = ALIGN_UP(sliceEnd, osPage);
        if (!GCToOSInterface::VirtualCommit((void*)commitStart, commitEnd - commitStart))
        {
            return false;
        }

        m_heap->IncBytesCommitted(sliceEnd - sliceStart);

        for (size_t g = granule; g < runEnd; g++)
        {
#if _DEBUG
            // optional, so in debug only half of them, see InitializeAt
            if (g % 2 == 0)
#endif
            {
                memset((void*)(Start() + g * Satori::CARD_SLICE_SIZE), Satori::CardState::EPHEMERAL, Satori::CARD_SLICE_SIZE);
            }

            _ASSERTE(m_commitWatermarks[g] == Satori::WATERMARK_NO_CARDS);
            m_commitWatermarks[g] = 0;
        }

        granule = runEnd;
    }

    return true;
}

// The trimmer: decommits the card slices of the granules of a run that it has claimed and whose memory it has just decommitted, and sets
// their WATERMARK_NO_CARDS bits. Nobody reads or writes the cards of a free granule: its card groups are blank (every scan of the cards
// asserts that a group that is not blank belongs to a region in use), and the write barrier marks cards only of objects.
// With OS pages larger than a slice, an OS page is decommitted only when it covers the slices of granules of this run alone: the
// granules that share it are all free and claimed by us. Granules at the edges of the run keep their slices, and so do the granules
// that share an OS page with the header.
void SatoriPage::DecommitCardSlices(size_t first, size_t count)
{
    size_t group = SatoriUtil::CardSliceGroupGranules();
    size_t low = max(ALIGN_UP(first, group), PinnedCardSliceGranules());
    size_t high = ALIGN_DOWN(first + count, group);
    if (low >= high)
    {
        return;
    }

    size_t granule = low;
    while (granule < high)
    {
        // runs of groups that have any slice to decommit
        bool any = false;
        for (size_t g = granule; g < granule + group; g++)
        {
            _ASSERTE((m_commitWatermarks[g] & Satori::WATERMARK_UNITS_MASK) == 0);
            any |= (m_commitWatermarks[g] & Satori::WATERMARK_NO_CARDS) == 0;
        }

        if (!any)
        {
            granule += group;
            continue;
        }

        size_t runEnd = granule + group;
        while (runEnd < high)
        {
            bool next = false;
            for (size_t g = runEnd; g < runEnd + group; g++)
            {
                next |= (m_commitWatermarks[g] & Satori::WATERMARK_NO_CARDS) == 0;
            }

            if (!next)
            {
                break;
            }

            runEnd += group;
        }

        size_t start = Start() + granule * Satori::CARD_SLICE_SIZE;
        size_t end = Start() + runEnd * Satori::CARD_SLICE_SIZE;
        if (GCToOSInterface::VirtualDecommit((void*)start, end - start))
        {
            size_t had = 0;
            for (size_t g = granule; g < runEnd; g++)
            {
                if ((m_commitWatermarks[g] & Satori::WATERMARK_NO_CARDS) == 0)
                {
                    had++;
                }

                m_commitWatermarks[g] = Satori::WATERMARK_NO_CARDS;
            }

            m_heap->DecBytesCommitted(had * Satori::CARD_SLICE_SIZE);
        }

        granule = runEnd;
    }
}

// The granule is free and has anything committed. When asked, it was also free when the trimmer last looked at the page.
// The watermark of a free granule is stable, but the granule may be claimed any moment. This is only a hint,
// TryTrimRun looks at the watermark again when the granules are ours.
bool SatoriPage::IsTrimCandidate(size_t granule, bool onlyIfSeenFree)
{
    size_t word = granule / 64;
    size_t candidates = m_freeBitmap[word];
    if (onlyIfSeenFree)
    {
        // a hint: TryTrimRun reads the seen bit again after its claim
        candidates &= m_seenFreeBitmap[word];
    }

    return ((candidates >> (granule % 64)) & 1) && (m_commitWatermarks[granule] & Satori::WATERMARK_UNITS_MASK) != 0;
}

size_t SatoriPage::NextGranuleToTrim(size_t from, bool onlyIfSeenFree)
{
    size_t end = GranuleCount();
    size_t granule = max(from, FirstGranule());
    while (granule < end)
    {
        size_t word = granule / 64;
        size_t candidates = VolatileLoadWithoutBarrier(&m_freeBitmap[word]);
        if (onlyIfSeenFree)
        {
            // a hint: TryTrimRun reads the seen bit again after its claim
            candidates &= VolatileLoadWithoutBarrier(&m_seenFreeBitmap[word]);
        }

        // only the granules that are not before the one we were asked for
        candidates &= ~(size_t)0 << (granule % 64);
        while (candidates)
        {
            DWORD bit;
            BitScanForward64(&bit, candidates);
            size_t candidate = word * 64 + bit;

            // a hint, see IsTrimCandidate
            if ((m_commitWatermarks[candidate] & Satori::WATERMARK_UNITS_MASK) != 0)
            {
                return candidate;
            }

            candidates &= candidates - 1;
        }

        granule = (word + 1) * 64;
    }

    return SIZE_MAX;
}

bool SatoriPage::TryTrimRun(size_t first, size_t maxCount, bool onlyIfSeenFree, size_t& count)
{
    _ASSERTE(maxCount > 0);
    count = 1;

    size_t claimed = 0;
    // The run ends where a block of maxCount granules ends, so that adjacent runs meet at the boundaries of the groups of granules
    // whose card slices share an OS page, and no group is split between two runs, where the slices are decommitted. (see DecommitCardSlices)
    _ASSERTE(!SatoriUtil::CanDecommitCardSlices() || maxCount % SatoriUtil::CardSliceGroupGranules() == 0);
    size_t limit = min(ALIGN_DOWN(first, maxCount) + maxCount, GranuleCount());
    for (size_t granule = first; granule < limit; granule++)
    {
        // The first granule is the one that the caller found. The others join if they are next to the run and look like they can.
        if (granule != first && !IsTrimCandidate(granule, onlyIfSeenFree))
        {
            break;
        }

        if (!TryClaimGranuleAt(granule))
        {
            break;
        }

        // The granule could have been claimed and freed again after it was found, and then our claim took the new free bit.
        // FreeRegion clears the seen bit before it sets the free bit, so a granule that is seen now was not freed since it was seen.
        // The watermark is stable now that the granule is ours. A granule that has nothing committed is not worth a run.
        if ((onlyIfSeenFree && ((m_seenFreeBitmap[granule / 64] >> (granule % 64)) & 1) == 0) ||
            (m_commitWatermarks[granule] & Satori::WATERMARK_UNITS_MASK) == 0)
        {
            PublishFreeGranules(granule, 1);
            break;
        }

#if _DEBUG
        if (onlyIfSeenFree && m_freeStamps[granule] >= m_snapshotStamp)
        {
            minipal_log_print_error("Satori: page %p, granule %zu: the trimmer took a granule that was freed (stamp %zu) after the last look at its page (stamp %zu)\n",
                (void*)this, granule, m_freeStamps[granule], m_snapshotStamp);
            _ASSERTE(!"the trimmer took a granule that was freed after the last look at its page");
        }
#endif

        claimed++;
    }

    if (claimed == 0)
    {
        return false;
    }

    count = claimed;

    // The granules are ours. What is committed is what the watermarks say. No region is using the memory, but a thread that read the region map
    // before the region of a granule was freed may still be about to read its header, see EnterUnownedHeaderRead. This look comes after the claims,
    // a thread that registers after it finds the regions gone from the map. If there is a reader, the granules go back as they were, and are trimmed
    // when the trimmer comes again.
    // The look has to come after the claims have read the free bits. A free bit is set after the region map is cleared, and the set
    // is a release (see PublishFreeGranules), so a thread that registers after the look reads the map after the clear and finds the region gone.
    // A claim is an acquire, and that orders the look after it. No fence is needed on this side: it is a load after a load,
    // and the readers do not read the free bits. The reader's side is the one that needs a store ordered before a load (see EnterUnownedHeaderRead).
    // The count needs no acquire: a reader that left did its reads of the header before its release, and the decommit below is a system call
    // behind the branch on the count.
    bool trimmed = false;
    if (*UnownedHeaderReaders() == 0)
    {
        size_t committedBytes = 0;
        size_t decommitEnd = 0;
        for (size_t i = 0; i < claimed; i++)
        {
            size_t bytes = (size_t)m_commitWatermarks[first + i] << Satori::WATERMARK_UNIT_BITS;
            committedBytes += bytes;
            decommitEnd = GranuleStart(first + i) + bytes;
        }

        // One call covers the run. The granules before the last one are committed in full or not, and decommitting
        // what is not committed does nothing.
        // Until the commit count goes down, the claimed granules are neither free nor decommitted. (see SatoriHeap::InUseBytes)
        size_t decommitStart = GranuleStart(first);
        m_heap->SetTrimmingBytes(committedBytes);
        if (GCToOSInterface::VirtualDecommit((void*)decommitStart, decommitEnd - decommitStart))
        {
            m_heap->DecBytesCommitted(committedBytes);
            m_heap->SetTrimmingBytes(0);

            // What is committed next time is whatever the next owner commits, and all of it is zero. No granule is warm.
            for (size_t i = 0; i < claimed; i++)
            {
                m_commitWatermarks[first + i] = 0;
                m_dirtyWatermarks[first + i] = 0;
            }

            for (size_t word = first / 64; word <= (first + claimed - 1) / 64; word++)
            {
                // atomic, no order: the free bits that are set after this are the release for it.
                Interlocked::AndUnordered(&m_warmBitmap[word], ~RunBitsInWord(first, claimed, word));
            }

            // then the cards of the granules, now that nothing of them is committed
            if (SatoriUtil::CanDecommitCardSlices())
            {
                DecommitCardSlices(first, claimed);
            }

            trimmed = true;
        }
        else
        {
            m_heap->SetTrimmingBytes(0);
        }
    }

    PublishFreeGranules(first, claimed);

    return trimmed;
}

#if _DEBUG
void SatoriPage::AssertHeaderIsReadable(size_t granule)
{
    _ASSERTE(*UnownedHeaderReaders() > 0);

    // A region that was freed after the reader found it makes its granule free, with a commit that its header gives it: FreeRegion
    // records the commit of the region, and a region has its header committed. The trimmer does not decommit while we are registered.
    bool isFree = (VolatileLoad(&m_freeBitmap[granule / 64]) >> (granule % 64)) & 1;
    _ASSERTE(!isFree || (m_commitWatermarks[granule] & Satori::WATERMARK_UNITS_MASK) != 0);
}
#endif

void SatoriPage::RememberFreeGranules()
{
    for (size_t word = 0; word < GranuleCount() / 64; word++)
    {
        // A granule that is claimed and freed again between the load and the store gets back the seen bit that the free cleared
        // (ForgetSeenFree). It is free, and it was freed during this look, which counts as before it.
        // Only atomicity is needed: nothing is published through this word.
        VolatileStoreWithoutBarrier(&m_seenFreeBitmap[word], m_freeBitmap[word]);
    }

#if _DEBUG
    // After the copy: a granule that the copy saw as free was freed before this stamp.
    // A full interlocked increment on purpose: the check needs the stamp ordered with the bit operations around it. Do not relax it.
    m_snapshotStamp = Interlocked::Increment(&s_freeClock);
#endif
}

void SatoriPage::ForgetFreeGranules()
{
    for (size_t word = 0; word < GranuleCount() / 64; word++)
    {
        // Only atomicity is needed, as in RememberFreeGranules. A free that clears seen bits meanwhile leaves them clear.
        VolatileStoreWithoutBarrier(&m_seenFreeBitmap[word], (size_t)0);
    }
}

size_t SatoriPage::FreeCommittedBytes()
{
    // The watermark of a free granule is stable, but the granule can be claimed any moment. A hint, as in NextGranuleToTrim.
    size_t units = 0;
    for (size_t word = 0; word < GranuleCount() / 64; word++)
    {
        size_t freeBits = VolatileLoadWithoutBarrier(&m_freeBitmap[word]);
        while (freeBits)
        {
            DWORD bit;
            BitScanForward64(&bit, freeBits);
            units += m_commitWatermarks[word * 64 + bit] & Satori::WATERMARK_UNITS_MASK;
            freeBits &= freeBits - 1;
        }
    }

    return units << Satori::WATERMARK_UNIT_BITS;
}

#if _DEBUG
static size_t GranuleProblem(SatoriPage* page, size_t granule, const char* what, bool report)
{
    if (report)
    {
        minipal_log_print_error("Satori: page %p, granule %zu: %s\n", (void*)page, granule, what);
    }

    return 1;
}

size_t SatoriPage::VerifyGranules(size_t& committedBytes, bool report)
{
    size_t problems = 0;
    size_t first = FirstGranule();
    size_t end = GranuleCount();

    // The card table is neither free nor covered by regions.
    for (size_t granule = 0; granule < first; granule++)
    {
        if ((m_freeBitmap[granule / 64] >> (granule % 64)) & 1)
        {
            problems += GranuleProblem(this, granule, "a granule of the card table is free", report);
        }

        if (RegionMap()[granule] != 0)
        {
            problems += GranuleProblem(this, granule, "a granule of the card table is in the region map", report);
        }
    }

    committedBytes += CardTableCommittedBytes();

    // Every other granule is either free or covered by exactly one region.
    size_t granule = first;
    while (granule < end)
    {
        bool isFree = (m_freeBitmap[granule / 64] >> (granule % 64)) & 1;
        uint8_t mapValue = RegionMap()[granule];

        if (isFree)
        {
            if (mapValue != 0)
            {
                problems += GranuleProblem(this, granule, "a free granule is in the region map", report);
            }

            size_t committedUnits = m_commitWatermarks[granule] & Satori::WATERMARK_UNITS_MASK;
            size_t dirtyUnits = m_dirtyWatermarks[granule];
            bool isWarm = (m_warmBitmap[granule / 64] >> (granule % 64)) & 1;
            if (dirtyUnits > committedUnits || committedUnits > Satori::WATERMARK_UNITS_PER_GRANULE)
            {
                problems += GranuleProblem(this, granule, "the watermarks of a free granule are not in order", report);
            }

            if ((m_commitWatermarks[granule] & Satori::WATERMARK_NO_CARDS) &&
                (committedUnits != 0 || granule < PinnedCardSliceGranules() || !SatoriUtil::CanDecommitCardSlices()))
            {
                problems += GranuleProblem(this, granule, "a free granule has no cards, but has memory, or must have its cards", report);
            }

            if (isWarm != (committedUnits == Satori::WATERMARK_UNITS_PER_GRANULE))
            {
                problems += GranuleProblem(this, granule, "the warm bit of a free granule does not match its commit", report);
            }

            committedBytes += committedUnits << Satori::WATERMARK_UNIT_BITS;
            granule++;
            continue;
        }

        if (mapValue != 1)
        {
            problems += GranuleProblem(this, granule, "a granule is not free and is not the start of a region", report);
            granule++;
            continue;
        }

        SatoriRegion* region = (SatoriRegion*)GranuleStart(granule);
        size_t size = region->Size();
        size_t count = size >> Satori::REGION_BITS;
        if (region->Start() != GranuleStart(granule) ||
            region->ContainingPage() != this ||
            size == 0 ||
            size % Satori::REGION_SIZE_GRANULARITY != 0 ||
            granule + count > end)
        {
            problems += GranuleProblem(this, granule, "a region does not fit where the region map has it", report);
            granule++;
            continue;
        }

        if (region->CommittedEnd() < region->Start() ||
            region->CommittedEnd() > region->End() ||
            region->UsedEnd() > region->CommittedEnd())
        {
            problems += GranuleProblem(this, granule, "a region has its commit or used part out of order", report);
        }
        else
        {
            committedBytes += region->CommittedEnd() - region->Start();
        }

        for (size_t i = 1; i < count; i++)
        {
            DWORD log2;
            BitScanReverse64(&log2, i);
            if (RegionMap()[granule + i] != (uint8_t)(log2 + 2))
            {
                problems += GranuleProblem(this, granule + i, "the region map does not continue the region", report);
            }

            if ((m_freeBitmap[(granule + i) / 64] >> ((granule + i) % 64)) & 1)
            {
                problems += GranuleProblem(this, granule + i, "a granule of a region is free", report);
            }
        }

        granule += count;
    }

    return problems;
}
#endif

// Calls the function for the ranges of a claimed run that must be committed to make its commit a prefix.
// The commit of the granules is whatever the last owners left. The function returns false to stop.
template <typename F>
static bool ForEachCommitGap(SatoriPage* page, size_t first, size_t count, size_t committed, uint8_t* commitWatermarks, F fn)
{
    size_t gapStart = 0;
    size_t gapEnd = 0;
    for (size_t granule = first; granule < first + count; granule++)
    {
        size_t granuleStart = page->GranuleStart(granule);
        if (granuleStart >= committed)
        {
            break;
        }

        // what is committed in this granule and what must be
        size_t have = granuleStart + ((size_t)commitWatermarks[granule] << Satori::WATERMARK_UNIT_BITS);
        size_t need = min(granuleStart + Satori::REGION_SIZE_GRANULARITY, committed);
        if (have < need)
        {
            if (gapEnd == have)
            {
                gapEnd = need;
            }
            else
            {
                if (gapStart != gapEnd && !fn(gapStart, gapEnd))
                {
                    return false;
                }

                gapStart = have;
                gapEnd = need;
            }
        }
    }

    return gapStart == gapEnd || fn(gapStart, gapEnd);
}

// Formats claimed granules as a region.
// The granules are ours, so what the last owners left in the watermarks is stable.
// If that can't be done, gives the granules back as they were and returns nullptr.
SatoriRegion* SatoriPage::FormatClaimed(size_t first, size_t count, size_t commitSize)
{
    _ASSERTE(count > 0);
    _ASSERTE(commitSize <= (count << Satori::REGION_BITS) && commitSize % Satori::WATERMARK_UNIT == 0);
    size_t start = GranuleStart(first);

    // The cards first: they must be there before the region's memory is, and before the region is published.
    // After this no claimed granule has the WATERMARK_NO_CARDS bit, and the watermarks below are counts of units.
    if (!TryCommitCardSlices(first, count))
    {
        PublishFreeGranules(first, count);
        return nullptr;
    }

    // The region is committed as a prefix, so it is the highest committed byte that matters.
    // Same for the dirty part, the rest of the committed part is zeroed.
    size_t committed = start;
    size_t used = start;
    for (size_t granule = first; granule < first + count; granule++)
    {
        size_t committedUnits = m_commitWatermarks[granule];
        size_t dirtyUnits = m_dirtyWatermarks[granule];
        _ASSERTE(dirtyUnits <= committedUnits && committedUnits <= Satori::WATERMARK_UNITS_PER_GRANULE);

        if (committedUnits)
        {
            committed = GranuleStart(granule) + (committedUnits << Satori::WATERMARK_UNIT_BITS);
        }

        if (dirtyUnits)
        {
            used = GranuleStart(granule) + (dirtyUnits << Satori::WATERMARK_UNIT_BITS);
        }
    }

#if _DEBUG
    // The region does not zero what is above its used part when it allocates, so it had better be zero.
    for (size_t granule = first; granule < first + count; granule++)
    {
        size_t granuleStart = GranuleStart(granule);
        size_t* p = (size_t*)(granuleStart + ((size_t)m_dirtyWatermarks[granule] << Satori::WATERMARK_UNIT_BITS));
        size_t* end = (size_t*)(granuleStart + ((size_t)m_commitWatermarks[granule] << Satori::WATERMARK_UNIT_BITS));
        for (; p < end; p++)
        {
            if (*p != 0)
            {
                minipal_log_print_error("Satori: page %p, granule %zu: nonzero memory at %p, above the dirty watermark of %u units\n",
                    (void*)this, granule, (void*)p, (unsigned)m_dirtyWatermarks[granule]);
                _ASSERTE(!"nonzero memory above the dirty watermark");
                break;
            }
        }
    }
#endif

    // What the caller asked for: the memory of a huge object, or of the array of the finalization queue. It is committed below
    // with the gaps, before the region is published, so a commit that fails gives the granules back before anything can see the region.
    committed = max(committed, start + commitSize);

    // Fill the gaps in the commit, the object that needed the run would commit them anyway.
    size_t failedAt = SIZE_MAX;
    bool committedGaps = ForEachCommitGap(this, first, count, committed, m_commitWatermarks,
        [&](size_t gapStart, size_t gapEnd)
        {
            if (!GCToOSInterface::VirtualCommit((void*)gapStart, gapEnd - gapStart))
            {
                failedAt = gapStart;
                return false;
            }

            m_heap->IncBytesCommitted(gapEnd - gapStart);
            return true;
        }
    );

    SatoriRegion* region = nullptr;
    if (committedGaps)
    {
        region = SatoriRegion::InitializeAt(this, start, count << Satori::REGION_BITS, committed, used);
    }

    if (region == nullptr)
    {
        // out of memory. Decommit what we have committed and give the granules back.
        ForEachCommitGap(this, first, count, committed, m_commitWatermarks,
            [&](size_t gapStart, size_t gapEnd)
            {
                if (gapStart >= failedAt)
                {
                    return false;
                }

                if (GCToOSInterface::VirtualDecommit((void*)gapStart, gapEnd - gapStart))
                {
                    m_heap->DecBytesCommitted(gapEnd - gapStart);
                    return true;
                }

                // It stays committed, and it is still zero, nothing has written into it. The granules go back as they are,
                // except that the watermarks must tell the next owner about this commit, and the heap keeps counting it.
                for (size_t granule = (gapStart - Start()) >> Satori::REGION_BITS; GranuleStart(granule) < gapEnd; granule++)
                {
                    size_t granuleStart = GranuleStart(granule);
                    size_t units = (min(gapEnd, granuleStart + Satori::REGION_SIZE_GRANULARITY) - granuleStart) >> Satori::WATERMARK_UNIT_BITS;
                    if (units > m_commitWatermarks[granule])
                    {
                        m_commitWatermarks[granule] = (uint8_t)units;
                        if (units == Satori::WATERMARK_UNITS_PER_GRANULE)
                        {
                            // atomic, no order: the free bits that are set after this are the release for it.
                            Interlocked::OrUnordered(&m_warmBitmap[granule / 64], (size_t)1 << (granule % 64));
                        }
                    }
                }

                return true;
            }
        );

        PublishFreeGranules(first, count);
    }

    return region;
}

SatoriRegion* SatoriPage::RegionForAddressChecked(size_t address)
{
    _ASSERTE(address >= Start() && address < End());
    size_t mapIndex = (address - Start()) >> Satori::REGION_BITS;

    // Regions are freed meanwhile, and that zeroes the map. A byte is read once, and the byte that ends the search
    // is the one that decides. Reading it again could find zero where there was a continuation, and shift by a negative count.
    uint8_t mapValue;
    while ((mapValue = VolatileLoad(&RegionMap()[mapIndex])) > 1)
    {
        mapIndex -= ((size_t)1 << (mapValue - 2));
    }

    if (mapValue != 1)
    {
        // there is no region here.
        return nullptr;
    }

    return (SatoriRegion*)((mapIndex << Satori::REGION_BITS) + Start());
}

SatoriRegion* SatoriPage::NextInPage(SatoriRegion* region)
{
    _ASSERTE(region->Start() > Start());

    size_t address = region->End();
    if (address >= End())
    {
        // this is the last region on this page, no next one.
        return nullptr;
    }

    size_t mapIndex = (address - Start()) >> Satori::REGION_BITS;

    // An acquire load of the map byte, so that the loads of the header that the caller does when the map has a region are not satisfied before
    // it (see EnterUnownedHeaderRead: the registration, then this load, and then the reads of the header, in this order). With MSVC on
    // a processor without the LSE atomics it is also what orders this load after the registration.
    if (VolatileLoad(&RegionMap()[mapIndex]) != 1)
    {
        // there is no region here yet.
        return nullptr;
    }

    return (SatoriRegion*)address;
}

SatoriRegion* SatoriPage::RegionForCardGroup(size_t group)
{
    size_t mapIndex = group * Satori::BYTES_PER_CARD_GROUP / Satori::REGION_SIZE_GRANULARITY;

    // The caller comes here on a card group state that it read with a plain load. Order that load before the loads of the map
    // and of the region header that follow, or they can see the granule as it was before the region was put there.
    VolatileLoadBarrier();

    // An acquire, as in RegionForAddressChecked and NextInPage: the byte that ends the search is the one that OnRegionInitialized
    // stored with a release after it wrote the header, and the caller reads the header next. The address of the region is computed
    // from the index, not loaded, so nothing else orders those reads after this load. Do not relax it.
    // (The region is live, a group that is not blank has one, so the bytes do not change under us.)
    uint8_t mapValue;
    while ((mapValue = VolatileLoad(&RegionMap()[mapIndex])) > 1)
    {
        mapIndex -= ((size_t)1 << (mapValue - 2));
    }

    // card groups are never set for free granules.
    _ASSERTE(mapValue == 1);
    return (SatoriRegion*)((mapIndex << Satori::REGION_BITS) + Start());
}

// setting cards is never concurent with unsetting (unlike dirtying)
// so there are no ordering implications.
void SatoriPage::SetCardsForRangeConcurrent(size_t start, size_t end)
{
    _ASSERTE(end > start);

    size_t firstByteOffset = start - Start();
    size_t lastByteOffset = end - Start() - 1;

    size_t firstCard = firstByteOffset / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(firstCard >= m_cardTableStart);
    _ASSERTE(firstCard < m_cardTableSize);

    if (m_cardTable[firstCard] == Satori::CardState::EPHEMERAL)
    {
        // this is optimization
        // marking cards in an ephemeral region is ok, but pointless
        return;
    }

    size_t lastCard = lastByteOffset / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(lastCard >= m_cardTableStart);
    _ASSERTE(lastCard < m_cardTableSize);

    memset((void*)(m_cardTable + firstCard), Satori::CardState::REMEMBERED, lastCard - firstCard + 1);

    size_t firstGroup = firstByteOffset / Satori::BYTES_PER_CARD_GROUP;
    size_t lastGroup = lastByteOffset / Satori::BYTES_PER_CARD_GROUP;
    for (size_t i = firstGroup; i <= lastGroup; i++)
    {
        if (!m_cardGroups[i * 2])
        {
            m_cardGroups[i * 2] = Satori::CardState::REMEMBERED;
        }
    }

    if (!m_cardState)
    {
        // DIRTY on purpose: a racing REMEMBERED store could overwrite another thread's DIRTY.
        m_cardState = Satori::CardState::DIRTY;
    }
}

// 
void SatoriPage::DirtyCardForAddress(size_t address)
{
    size_t offset = address - Start();
    size_t cardByteOffset = offset / Satori::BYTES_PER_CARD_BYTE;

    _ASSERTE(cardByteOffset >= m_cardTableStart);
    _ASSERTE(cardByteOffset < m_cardTableSize);

    // we dirty the card unconditionally, since it can be concurrently cleaned
    // however this does not get called after field writes (unlike in barriers),
    // so the card dirtying write can be unordered.
    m_cardTable[cardByteOffset] = Satori::CardState::DIRTY;

    size_t cardGroup = offset / Satori::BYTES_PER_CARD_GROUP;
    VolatileStore(&this->m_cardGroups[cardGroup * 2], Satori::CardState::DIRTY);
    VolatileStore(&this->m_cardState, Satori::CardState::DIRTY);
}

void SatoriPage::DirtyCardsForRange(size_t start, size_t end)
{
    size_t firstByteOffset = start - Start();
    size_t lastByteOffset = end - Start() - 1;

    size_t firstCard = firstByteOffset / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(firstCard >= m_cardTableStart);
    _ASSERTE(firstCard < m_cardTableSize);

    size_t lastCard = lastByteOffset / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(lastCard >= m_cardTableStart);
    _ASSERTE(lastCard < m_cardTableSize);

    // we dirty these cards unconditionally, since they can be concurrently cleaned.
    // This is called on mark overflow, after the object was marked with an unordered atomic (SetMarkedAtomic).
    // A cleaner that sees a dirty card visits only the marked objects in it, so the mark must be visible first.
    VolatileStoreBarrier();
    memset((void*)(m_cardTable + firstCard), Satori::CardState::DIRTY, lastCard - firstCard + 1);

    // if dirtying can be concurrent with cleaning, we must ensure order
    // of writes: cards, then groups, then page.
    // cleaning will read in the opposite order
    VolatileStoreBarrier();

    size_t firstGroup = firstByteOffset / Satori::BYTES_PER_CARD_GROUP;
    size_t lastGroup = lastByteOffset / Satori::BYTES_PER_CARD_GROUP;
    for (size_t i = firstGroup; i <= lastGroup; i++)
    {
        this->m_cardGroups[i * 2] = Satori::CardState::DIRTY;
    }

    VolatileStore(&this->m_cardState, Satori::CardState::DIRTY);
}

// this is called concurently with mutator after bulk writes
// we may be concurrently cleaning cards, but not groups and pages
void SatoriPage::DirtyCardsForRangeConcurrent(size_t start, size_t end)
{
    size_t firstByteOffset = start - Start();
    size_t lastByteOffset = end - Start() - 1;

    size_t firstCard = firstByteOffset / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(firstCard >= m_cardTableStart);
    _ASSERTE(firstCard < m_cardTableSize);

    size_t lastCard = lastByteOffset / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(lastCard >= m_cardTableStart);
    _ASSERTE(lastCard < m_cardTableSize);

    // this must be ordered after bulk writes and cannot be conditional
    // since we may be cleaning concurrently
    VolatileStoreBarrier();
    memset((void*)(m_cardTable + firstCard), Satori::CardState::DIRTY, lastCard - firstCard + 1);

    // we do not clean groups concurrently, so these can be conditional and unordered
    // only the eventual final state matters
    size_t firstGroup = firstByteOffset / Satori::BYTES_PER_CARD_GROUP;
    size_t lastGroup = lastByteOffset / Satori::BYTES_PER_CARD_GROUP;
    for (size_t i = firstGroup; i <= lastGroup; i++)
    {
        if (m_cardGroups[i * 2] != Satori::CardState::DIRTY)
        {
            m_cardGroups[i * 2] = Satori::CardState::DIRTY;
        }
    }

    if (m_cardState != Satori::CardState::DIRTY)
    {
        m_cardState = Satori::CardState::DIRTY;
    }
}

void SatoriPage::WipeGroupsForRange(size_t start, size_t end)
{
    size_t firstByteOffset = start - Start();
    size_t lastByteOffset = end - Start() - 1;

    size_t firstGroup = firstByteOffset / Satori::BYTES_PER_CARD_GROUP;
    size_t lastGroup = lastByteOffset / Satori::BYTES_PER_CARD_GROUP;
    // this resets both states and tickets
    memset((void*)&m_cardGroups[firstGroup * 2], Satori::CardState::BLANK, (lastGroup - firstGroup + 1) * 2);
}

void SatoriPage::ResetCardsForRange(size_t start, size_t end, bool isTenured)
{
    size_t firstByteOffset = start - Start();
    size_t lastByteOffset = end - Start() - 1;

    size_t firstCard = firstByteOffset / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(firstCard >= m_cardTableStart);
    _ASSERTE(firstCard < m_cardTableSize);

    size_t lastCard = lastByteOffset / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(lastCard >= m_cardTableStart);
    _ASSERTE(lastCard < m_cardTableSize);
    int8_t resetValue = isTenured ? Satori::CardState::BLANK : Satori::CardState::EPHEMERAL;
    memset((void*)(m_cardTable + firstCard), resetValue, lastCard - firstCard + 1);

    WipeGroupsForRange(start, end);
}

// Tells if the card groups for the range are as WipeGroupsForRange leaves them - blank and with no scan ticket.
bool SatoriPage::CardGroupsAreWipedForRange(size_t start, size_t end)
{
    size_t firstGroup = (start - Start()) / Satori::BYTES_PER_CARD_GROUP;
    size_t lastGroup = (end - Start() - 1) / Satori::BYTES_PER_CARD_GROUP;
    for (size_t i = firstGroup; i <= lastGroup; i++)
    {
        if (m_cardGroups[i * 2] != Satori::CardState::BLANK || m_cardGroups[i * 2 + 1] != 0)
        {
            return false;
        }
    }

    return true;
}

#if _DEBUG
bool SatoriPage::CardsAreBlankForRange(size_t start, size_t end)
{
    size_t firstCard = (start - Start()) / Satori::BYTES_PER_CARD_BYTE;
    size_t lastCard = (end - Start() - 1) / Satori::BYTES_PER_CARD_BYTE;
    for (size_t i = firstCard; i <= lastCard; i++)
    {
        if (m_cardTable[i] != Satori::CardState::BLANK)
        {
            return false;
        }
    }

    return true;
}
#endif
