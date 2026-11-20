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
// SatoriPage.h
//

#ifndef __SATORI_PAGE_H__
#define __SATORI_PAGE_H__

#include "common.h"
#include "../gc.h"
#include "SatoriUtil.h"

class SatoriHeap;
class SatoriRegion;

// The Page is a memory reservation unit. Also manages cards.
class SatoriPage
{
public:
    SatoriPage() = delete;
    ~SatoriPage() = delete;

    enum class InitStatus
    {
        Success,
        // the address range is taken by another mapping, another location may work.
        Occupied,
        // the OS refused to reserve for other reasons (out of address space, limits, ...)
        ReserveRefused,
        // could not commit the initial part of the page.
        CommitFailed,
    };

    static SatoriPage* InitializeAt(size_t address, size_t pageSize, SatoriHeap* heap, InitStatus& status);

    void OnRegionInitialized(SatoriRegion* region);

    SatoriRegion* RegionForAddressChecked(size_t address);
    SatoriRegion* RegionForCardGroup(size_t group);

    SatoriRegion* NextInPage(SatoriRegion* region);

    // The page is divided into granules of REGION_SIZE_GRANULARITY. The granule i starts at Start() + i * REGION_SIZE_GRANULARITY.
    // The first granules hold the card table. The rest are covered by regions or are free.
    size_t GranuleCount();
    size_t FirstGranule();
    size_t GranuleStart(size_t granule);

    // Claiming free granules. The thread that claims a granule by clearing its free bit owns it,
    // until it frees the granule, or gives it back.
    //
    // Claims the free granule that has the lowest (or the highest) address. When asked, only among the warm ones.
    // Returns SIZE_MAX if there is none.
    size_t TryClaimGranule(bool warmOnly, bool fromTop);
    // Claims the granule if it is free.
    bool TryClaimGranuleAt(size_t granule);
    // Claims a run of free granules, the lowest (or the highest) one that fits. Returns its first granule or SIZE_MAX.
    // The threads that claim runs must not run this on the same page at the same time, but other threads
    // may claim single granules meanwhile. If one takes a granule from the run, the claim is rolled back and tried again.
    // When asked, only among the runs of warm granules.
    size_t TryClaimRun(size_t count, bool fromTop, bool warmOnly = false);
    // The granules are claimed and all of them are committed in full. The commit of a claimed granule does not change.
    // A granule that was warm when it was looked at may not be after it is claimed: the trimmer can claim it, decommit it
    // and publish it as free between the look and the claim.
    bool IsCommittedInFull(size_t first, size_t count);
    // Formats claimed granules as a region, with at least commitSize bytes from its start committed, (a whole number of watermark units)
    // for the memory that its first use needs. If that is not possible, gives the granules back and returns nullptr.
    SatoriRegion* FormatClaimed(size_t first, size_t count, size_t commitSize);

    // Gives back a page that was not published.
    void Release();

    // Trimming. Only the trimmer decommits the memory of free granules, and only of the ones that it has claimed.
    // A trimmed granule has nothing committed, not even its head, and that is safe because no thread reads the header of a region
    // that it does not own without being registered, see EnterUnownedHeaderRead.
    //
    // Finds the first free granule that has anything committed, not before `from`. When asked, only among the granules
    // that were also free when the trimmer last looked at this page.
    // Returns SIZE_MAX if there is none.
    size_t NextGranuleToTrim(size_t from, bool onlyIfSeenFree);
    // Claims the granule `first`, which NextGranuleToTrim found, and the free granules after it that have anything committed,
    // up to the end of the block of `maxCount` granules that `first` is in. Decommits them with one call and gives them back as free and cold.
    // When asked, only the granules that are still ones that were seen free: the caller may have found them before
    // they were claimed and freed again, and then the claim takes the new free bit.
    // `count` is the number of granules that the run covered (at least 1), the caller goes on after them.
    // Returns true if it decommitted anything. It decommits nothing, and gives the granules back, if a thread is registered as a reader
    // of an unowned header.
    bool TryTrimRun(size_t first, size_t maxCount, bool onlyIfSeenFree, size_t& count);
    // Remembers which granules are free now, for the next time (see m_seenFreeBitmap).
    void RememberFreeGranules();
    // Forgets the last look: no granule is seen free until the next one.
    void ForgetFreeGranules();
    // What the free granules have committed. A snapshot: the granules can be claimed and freed meanwhile.
    size_t FreeCommittedBytes();

    // A thread that reads the header of a region that it does not own (the region that follows its own, see TryGetRegularRegion)
    // registers with the page before it reads the region map for that region, and leaves after its last read of the header.
    // The region can be freed meanwhile, and the trimmer decommits the granule of a free region. The trimmer looks at the registrations
    // after it claims a granule, and does not decommit while there is one. A thread that registers after that look reads the region map
    // after the region was removed from it (the map is cleared before the free bit is set), and does not read the header.
    // So the header that the map has found is committed for as long as the thread is registered.
    // The reader's side is a store followed by a load (the registration, and then the map), and the two must be ordered:
    // the registration is a full interlocked increment, and the map is read with an acquire, see EnterUnownedHeaderRead.
    // The trimmer's side is a load followed by a load (the free bit that its claim reads, and then the registrations).
    // The claim is an acquire, and that is enough, see TryTrimRun.
    // The granule of the freed region can also be claimed as a part of a run, for a bigger region. The memory of the old header is then
    // the body of that region: it stays committed (a region in use decommits only above the start of its last granule, see SatoriRegion::TryDecommitAbove),
    // but it can be written. So the reader uses what it read only after it checks it. (see TryTakeReusableNext)
    void EnterUnownedHeaderRead();
    void LeaveUnownedHeaderRead();
#if _DEBUG
    // A registered thread is about to read the header of the region that starts at the granule.
    void AssertHeaderIsReadable(size_t granule);
#endif

    // What of the card table is committed: the slices of the card table's own granules (the header and the metadata), and the slices
    // of the granules that have their cards. (With OS pages larger than a slice the OS may have a little more, see DecommitCardSlices.)
    // All of it where the slices are not decommitted (see SatoriUtil::CanDecommitCardSlices).
    size_t CardTableCommittedBytes();

#if _DEBUG
    // Checks that the free bits, the watermarks, the region map and the regions agree with each other.
    // Reports the problems and returns their number. The page must be quiet: no claims or frees.
    // Adds up what the page has committed. (see SatoriHeap::VerifyGranules)
    size_t VerifyGranules(size_t& committedBytes, bool report);
#endif

    // Makes the granules of a region that nobody uses any more free for others to claim.
    void FreeRegion(SatoriRegion* region);
    void OnRegionFreed(SatoriRegion* region);
    // Sets the free bits of granules that are ready for the next owner.
    void PublishFreeGranules(size_t first, size_t count);

    size_t Start();
    size_t End();
    size_t RegionsStart();
    uint8_t* RegionMap();
    SatoriHeap* Heap();

    // Not for the write barrier: a blank page becomes REMEMBERED, which is right only while nothing dirties cards.
    inline void SetCardForAddress(size_t address)
    {
        size_t offset = address - (size_t)this;
        size_t cardByteOffset = offset / Satori::BYTES_PER_CARD_BYTE;

        _ASSERTE(cardByteOffset >= m_cardTableStart);
        _ASSERTE(cardByteOffset < m_cardTableSize);

        if (!m_cardTable[cardByteOffset])
        {
            m_cardTable[cardByteOffset] = Satori::CardState::REMEMBERED;

            size_t cardGroup = offset / Satori::BYTES_PER_CARD_GROUP;
            if (!m_cardGroups[cardGroup * 2])
            {
                m_cardGroups[cardGroup * 2] = Satori::CardState::REMEMBERED;
                if (!m_cardState)
                {
                    m_cardState = Satori::CardState::REMEMBERED;
                }
            }
        }
    }

    // For the write barrier, which runs concurrently with dirtying.
    inline void SetCardForAddressConcurrent(size_t address)
    {
        size_t offset = address - (size_t)this;
        size_t cardByteOffset = offset / Satori::BYTES_PER_CARD_BYTE;

        _ASSERTE(cardByteOffset >= m_cardTableStart);
        _ASSERTE(cardByteOffset < m_cardTableSize);

        if (!m_cardTable[cardByteOffset])
        {
            m_cardTable[cardByteOffset] = Satori::CardState::REMEMBERED;

            size_t cardGroup = offset / Satori::BYTES_PER_CARD_GROUP;
            if (!m_cardGroups[cardGroup * 2])
            {
                m_cardGroups[cardGroup * 2] = Satori::CardState::REMEMBERED;
                if (!m_cardState)
                {
                    // DIRTY on purpose: a racing REMEMBERED store could overwrite another thread's DIRTY.
                    m_cardState = Satori::CardState::DIRTY;
                }
            }
        }
    }

    // This is called to simulate a write concurrently with mutator.
    // For example when concurrent mark can't mark a child object so we need to keep
    // it logically gray, but can't keep it in the gray queue lest we never finish.
    // So we make the reference dirty as if it was written, but there was no actual write.
    inline void DirtyCardForAddressConcurrent(size_t address)
    {
        size_t offset = address - (size_t)this;
        size_t cardByteOffset = offset / Satori::BYTES_PER_CARD_BYTE;

        _ASSERTE(cardByteOffset >= m_cardTableStart);
        _ASSERTE(cardByteOffset < m_cardTableSize);

        // we dirty the card unconditionally, since it can be concurrently cleaned.
        // The card store is a release. ErectWriteBarrier calls this right after a reference store, and the markers call it after the object
        // that holds the reference was marked (with an unordered atomic). A cleaner that sees the dirty card visits the marked objects in it,
        // so it must see the reference and the mark too. (as the barriers do with stlrb, see WriteBarriers.S)
        VolatileStore(&m_cardTable[cardByteOffset], Satori::CardState::DIRTY);

        // we do not clean groups concurrently, so these can be conditional and unordered
        // only the eventual final state matters
        size_t cardGroup = offset / Satori::BYTES_PER_CARD_GROUP;
        if (m_cardGroups[cardGroup * 2] != Satori::CardState::DIRTY)
        {
            m_cardGroups[cardGroup * 2] = Satori::CardState::DIRTY;
            if (m_cardState != Satori::CardState::DIRTY)
            {
                m_cardState = Satori::CardState::DIRTY;
            }
        }
    }

    // Only sets the card, not the card group or the page state.
    // Used when refreshing cards, where the group and the page are already in the right state.
    inline void SetCardForAddressOnly(size_t address)
    {
        size_t offset = address - (size_t)this;
        size_t cardByteOffset = offset / Satori::BYTES_PER_CARD_BYTE;

        _ASSERTE(cardByteOffset >= m_cardTableStart);
        _ASSERTE(cardByteOffset < m_cardTableSize);

        m_cardTable[cardByteOffset] = Satori::CardState::REMEMBERED;
    }

    void DirtyCardForAddress(size_t address);
    void SetCardsForRangeConcurrent(size_t start, size_t end);
    void DirtyCardsForRange(size_t start, size_t length);
    void DirtyCardsForRangeConcurrent(size_t start, size_t end);

    void WipeGroupsForRange(size_t start, size_t end);
    void ResetCardsForRange(size_t start, size_t end, bool isTenured);
    bool CardGroupsAreWipedForRange(size_t start, size_t end);
#if _DEBUG
    bool CardsAreBlankForRange(size_t start, size_t end);
#endif

    volatile int8_t& CardState();
    volatile uint8_t& ScanTicket();
    volatile int8_t& CardGroupState(size_t i);
    volatile uint8_t& CardGroupScanTicket(size_t i);

    size_t CardGroupCount();
    int8_t* CardsForGroup(size_t i);
    size_t LocationForCard(int8_t* cardPtr);

    template <typename F>
    void ForEachRegion(F lambda);

private:
    size_t FindRun(size_t count, bool fromTop, bool warmOnly);
    size_t FreeWord(size_t word, bool warmOnly);
    bool TryClearRun(size_t first, size_t count);
    void ForgetSeenFree(size_t first, size_t count);
    bool IsTrimCandidate(size_t granule, bool onlyIfSeenFree);
    size_t PinnedCardSliceGranules();
    bool TryCommitCardSlices(size_t first, size_t count);
    void DecommitCardSlices(size_t first, size_t count);
    volatile int32_t* UnownedHeaderReaders();

    union
    {
        // 1bit  - 64  bytes
        // 1byte - 512 bytes   i.e cards add ~ 0.002 overhead
        // 8byte - 4k
        // 4K    - 2Mb (region granularity)
        // 2Mb   - 1Gb (region granule can store cards for 1Gb page)
        // We can start card table at the beginning of the page for simplicity
        // The first 4K cover the card itself, so that space will be unused and we can use it for other metadata.
        int8_t m_cardTable[1];

        // header (can be up to 2Kb for 1Gb page)
        struct
        {
            int8_t m_cardState;
            uint8_t m_scanTicket;
            // The count of the registered readers of unowned headers is not in this line: the GC writes the two fields above for every scan,
            // and the claim path reads most of the ones below. It has a line of its own, see UnownedHeaderReaders.
            size_t m_end;
            size_t m_initialCommit;
            size_t m_firstRegion;

            SatoriHeap* m_heap;

            // the following is useful when scanning/clearing cards
            // it can be computed from the page size, but we have space, so we will store.
            size_t m_cardTableSize;
            size_t m_cardTableStart;

            // -----  we can have a few more fields above as long as m_cardGroups starts at offset 128.
            //        that can be adjusted if needed

            // computed size, located after card groups
            // 1byte per region
            // 512 bytes per 1Gb
            // 0 - the granule is free, 1 - the granule is the start of a region,
            // 2 and up - a continuation of a region, the distance to its start is 1 << (value - 2) granules.
            uint8_t* m_regionMap;

            // Free space is tracked per granule, in the following computed sizes located after the region map.
            // It all fits into the unused part of the card table that covers the card table itself.
            //
            // A granule is free if and only if its bit is set, and then its region map byte is 0.
            // 1 bit per granule, 8 words (one cache line) per 1Gb.
            // The thread that clears the bit owns the granule, until it sets the bit again.
            // The set is a release and the clear is an acquire, see PublishFreeGranules.
            size_t* m_freeBitmap;
            // 1 bit per granule. 1 means the granule is committed in full. Meaningful only while the granule is free.
            size_t* m_warmBitmap;
            // 1 byte per granule, meaningful only while the granule is free.
            // How much of the granule from its start is committed, in WATERMARK_UNITs (0 - 64), and the bit WATERMARK_NO_CARDS, which
            // says that the granule's slice of the card table is decommitted too (only with 0 units). A granule that is in use has its cards,
            // whatever its byte says: the claim commits them (FormatClaimed), and the trimmer decommits them with the free granule's memory,
            // where the slices are decommitted at all (see SatoriUtil::CanDecommitCardSlices).
            uint8_t* m_commitWatermarks;
            // 1 byte per granule, meaningful only while the granule is free.
            // How much of the granule from its start may hold nonzero data, rounded up, in WATERMARK_UNITs (0 - 64).
            // Never above the commit watermark. The rest of the committed part is zeroed.
            // The watermarks are written by the thread that frees the granule, before it sets the free bit,
            // and read by the thread that claims the granule, after it clears the bit.
            uint8_t* m_dirtyWatermarks;
            // 1 bit per granule. The granules that were free when the trimmer last looked at the page, and were not freed since.
            // FreeRegion clears the bits of the granules that it frees, before it sets their free bits.
            // Only the trimmer sets bits (and a GC that trims on request, while the trimmer is stopped).
            // The bitmaps end before the line of the reader count (see UnownedHeaderReaders).
            size_t* m_seenFreeBitmap;
#if _DEBUG
            // Checked builds only. The trimmer asserts that it never decommits a granule that was freed after the last look at its page.
            // 1 stamp per granule: when FreeRegion freed it (0 if it was never used). The stamps and the one below come from one counter.
            // A granule that the last look saw as free was freed before the look, because the look takes its stamp after the copy.
            size_t* m_freeStamps;
            // The stamp of the last look at the page.
            size_t m_snapshotStamp;
#endif

            // computed size,
            // 2 byte per card group (4 per region granule)
            //   one signed byte for a state, which has negative values.
            //   and one unsigned for a ticket, which is a wrap-around counter.
            // 2048 bytes per 1Gb
            DECLSPEC_ALIGN(128)
            int8_t m_cardGroups[1];
        };
    };
};

#endif
