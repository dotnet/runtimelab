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
// SatoriRegion.h
//

#ifndef __SATORI_REGION_H__
#define __SATORI_REGION_H__

#include "common.h"
#include "../gc.h"
#include "SatoriHeap.h"
#include "SatoriUtil.h"
#include "SatoriObject.h"
#include "SatoriPrefetchQueue.h"

class SatoriAllocator;
class SatoriRegionQueue;
class SatoriObject;
class SatoriAllocationContext;
struct SatoriLocalRootCache;

// The Region contains objects and their metadata.
class SatoriRegion
{
    friend class SatoriObject;
    friend class SatoriRegionQueue;
    friend class SatoriQueue<SatoriRegion>;

public:
    SatoriRegion() = delete;
    ~SatoriRegion() = delete;

    static const int MAX_LARGE_OBJ_SIZE;

    static SatoriRegion* InitializeAt(SatoriPage* containingPage, size_t address, size_t regionSize, size_t committed, size_t used);
    void MakeBlank();
    bool ValidateBlank();

    void RearmCardsForTenured();
    bool RearmCardsForStillTenured();
    void ResetCardsForEphemeral();

    SatoriRegion* TrySplit(size_t regionSize);
    bool CanDecommit();
    bool TryDecommit();
    void TryCommit();
    bool CanCoalesceWithNext();
    bool TryCoalesceWithNext(bool drainsAreDone);

    void ZeroInitAndLink(SatoriRegion* prev);

    static size_t RegionSizeForAlloc(size_t allocSize);

    size_t GetAllocStart();
    size_t GetAllocRemaining();
    size_t GetTlgcAllocRemaining();
    bool ShouldCollectAtTlgcLimit(size_t allocBytes);
    size_t GetAllocZeroedRemaining();
    size_t Allocate(size_t size, bool zeroInitialize);
    size_t AllocateDirty(size_t size, size_t* dirtyStart, size_t* dirtyEnd);
    size_t AllocateHuge(size_t size, bool zeroInitialize);

    size_t StartAllocating(size_t minSize, size_t allocBytes = 0);
    // The caller may know that [allocPtr, zeroedEnd) contains zeroes,
    // like the unused part of an allocation context does.
    void StopAllocating(size_t allocPtr, size_t zeroedEnd = 0);
    void StopAllocating();
    bool IsAllocating();

    void AddFreeSpace(SatoriObject* freeObj, size_t size);
    void ReturnFreeSpace(SatoriObject * freeObj, size_t size);

    int GetMaxFreeBucket();
    bool HasFreeSpaceInTopBucket();
    size_t FreeSpaceInTopNBuckets(int n);

    // Whether an allocation of the given size can be served from the current allocation span
    // or is guaranteed to fit a free list object (a hint, no side effects).
    bool CanFitAllocation(size_t size);

    // Free space that allocations can use: the rest of the current allocation span and the free lists.
    // (fragments that are too small for the free lists are not usable)
    size_t UsableCapacity();

    void StartEscapeTrackingRelease(size_t threadTag, size_t allocBytes = 0, size_t minAllocSize = 0);
    void StopEscapeTracking();
    // NB: called from the write barrier - must not touch vector registers or TLS.
    void StopEscapeTrackingFromBarrier();
    // Clears marks left by StopEscapeTrackingFromBarrier, if any.
    // Returns true if there were such marks.
    bool ClearStaleEscapeMarks();
    bool IsEscapeTracking();
    bool MaybeEscapeTrackingAcquire();
    bool IsEscapeTrackedByCurrentThread();

    // Per-thread history of escape tracking outcomes. A thread whose tracked regions keep
    // ending without a productive thread-local GC backs off and allocates some regions untracked.
    // Called by the owning thread when attaching an eligible region.
    static bool ShouldStartEscapeTracking();
    // Called by the owning thread when detaching a region that ran out of space while it was
    // tracked, or when it finds that the barrier stopped tracking because of too many escapes.
    // NB: not from the write barrier - uses TLS.
    // When outOfSpace is true, the region ran out of space. If no thread-local GC was even attempted in it,
    // that tells nothing about whether tracking pays off, so the history is left as is.
    static void OnEscapeTrackingEnded(bool outOfSpace = false);

    // Thread-local GC paid off in this region: in the current accounting period it returned at least
    // REGION_SIZE_GRANULARITY / 4 of usable space to the free lists. (reclaimed fragments too small to reuse do not count)
    bool IsTlgcProductive(size_t allocBytes);

    // Attaching starts an accounting period for the gen1 budget. (see TakeBudgetCharge)
    void AttachToAllocatingOwner(SatoriRegion** attachementPoint);
    void DetachFromAlocatingOwnerRelease();
    // Returns how much of the gen1 budget the region spent in the current accounting period.
    // Called when the region is handed to the recycler. (see SatoriRecycler::AddEphemeralRegion)
    size_t TakeBudgetCharge(bool gcIsStarting);
    // Called by the allocating owner before it resumes allocating from the region.
    // If a GC took the region while it stayed attached, starts a new accounting period.
    void RebaselineUsableIfStale(size_t allocBytes);
    bool IsAttachedToAllocatingOwner();
    bool MaybeAllocatingAcquire();
    void SetHasFinalizables();

    void ResetReusableForRelease();

    bool IsReuseCandidate();
    bool IsDemotionCandidate(bool nextGcIsFullGC);
    bool IsPromotionCandidate();
    size_t ReclaimSizeIfRelocated(bool assumeFullGC);

    bool TryDemote(bool nextGcIsFullGc);
    bool IsDemoted();
    SatoriWorkChunk* &DemotedObjects();
    bool& HasUnmarkedDemotedObjects();
    void FreeDemotedTrackers();

    int Generation();
    int GenerationAcquire();
    void SetGeneration(int generation);
    void SetGenerationRelease(int generation);

    size_t Start();
    size_t End();
    size_t Size();
    bool IsLarge();

    SatoriObject* FirstObject();
    SatoriObject* FindObject(size_t location, SatoriObject* hint = nullptr);
    // In a region that is being marked, finds the marked object that contains the location,
    // or the first marked object after it, up to the limit. Uses only the mark bitmap and the index,
    // except for reading the size of a candidate that starts before the location.
    SatoriObject* FindMarkedObjectFrom(size_t location, size_t limit, SatoriObject* hint);
    size_t LocationToIndex(size_t location);
    void SetIndicesForObject(SatoriObject* o, size_t end);
    void SetIndicesForObjectCore(size_t start, size_t end);
    void ClearIndicesForAllocRange();

    int IncrementUnfinishedAlloc();
    void DecrementUnfinishedAlloc();

    SatoriObject* SkipUnmarked(SatoriObject* from);
    SatoriObject* SkipUnmarkedThreadLocal(SatoriObject* from);
    SatoriObject* SkipUnmarkedAndClear(SatoriObject* from);
    SatoriObject* SkipUnmarked(SatoriObject* from, size_t upTo);

    // Walks the mark bitmap ahead of a sweep and prefetches upcoming live objects.
    // It moves by mark bytes - a mark byte covers one 64 byte cache line of the heap,
    // thus it prefetches every line that has live objects once, and counts the distance in such lines.
    struct SweepPrefetcher
    {
        size_t m_index;
        size_t m_word;
        // the heap cache line of the last live object the sweep has reached
        size_t m_line;
    };

    void SweepPrefetchStart(SweepPrefetcher& prefetcher, SatoriObject* from);
    void SweepPrefetchAdvance(SweepPrefetcher& prefetcher, SatoriObject* o);

    void TakeFinalizerInfoFrom(SatoriRegion* other);
    void IndividuallyPromote();
    void UpdateFinalizableTrackers();
    void UpdatePointers();
    void UpdatePointersInObject(SatoriObject* o, size_t size, SatoriRelocatedRefQueue<8>& updateQueue);
    void SetCardsForObject(SatoriObject* o, size_t size);

    template <bool promotingAllRegions>
    void UpdatePointersInPromotedObjects();

    template <bool updatePointers>
    bool Sweep();

    bool IsPreSweepCandidate(bool assumeFullGC);
    void PreSweep();
    void FinishSweepForPreSwept();

    bool IsExposed(SatoriObject** location);
    bool AnyExposed(size_t from, size_t length);
    bool CheckEscapeRange(size_t dst, size_t src, size_t len);
    void EscapeRecursively(SatoriObject* obj);
    void EscapeAll();
    void EscapeShallow(SatoriObject* o, size_t size);

    template <typename F>
    void ForEachFinalizable(F lambda);
    template <typename F>
    void ForEachFinalizableThreadLocal(F lambda);

    template <typename F>
    bool PendFinalizables(F markFn, int condemnedGeneration);
    void PendCfFinalizables(int condemnedGeneration);

    // used for exclusive access to trackers when accessing concurrently with user threads
    void LockFinalizableTrackers();
    void UnlockFinalizableTrackers();

    bool RegisterForFinalization(SatoriObject* finalizable);
    bool HasFinalizables();
    bool& HasPendingFinalizables();

    void SetOccupancy(size_t occupancy, int32_t objCount);
    void SetOccupancy(size_t occupancy);
    size_t Occupancy();
    int32_t ObjCount();
    size_t DemotedOccupancy();

    bool& HasPinnedObjects();
    void SetHasPinnedObjects();
    bool& DoNotSweep();
    bool& IsPreSwept();
    bool& IsRelocated();
    bool& AcceptedPromotedObjects();
    bool& IndividuallyPromoted();

    uint32_t SweepsSinceLastAllocation();

    enum class ReuseLevel : uint8_t
    {
        None,
        Gen1,
        Gen0,
    };

    ReuseLevel& ReusableFor();
    bool IsReusable();

    SatoriQueue<SatoriRegion>* ContainingQueue();

#if _DEBUG
    bool& HasMarksSet();
#endif

    bool NothingMarked();
    void ClearMarks();
    void ClearIndex();
    void ClearFreeLists();

    // we tell where we are in terms of alloc bytes, so we do not collect too soon
    // returns true if it actually did a collection.
    bool ThreadLocalCollect(size_t allocBytes, bool atTlgcLimit = false);

    SatoriPage* ContainingPage();
    SatoriRegion* NextInPage();

    void Verify(bool allowMarked = false);

    SatoriAllocator* Allocator();
    SatoriRecycler* Recycler();

private:
    static const int BITMAP_LENGTH = Satori::REGION_SIZE_GRANULARITY / sizeof(size_t) / sizeof(size_t) / 8;

    static const int SWEEP_PREFETCH_LINES = 8;

    void SweepPrefetchNext(SweepPrefetcher& prefetcher);

    // The first actually useful index is offsetof(m_firstObject) / sizeof(size_t) / 8,
    // which is the map itself (BITMAP_LENGTH + 1 words), the index and the syncblock.
    static const int BITMAP_START = (BITMAP_LENGTH + 1 + (Satori::INDEX_LENGTH + 2) / 2 + 1) / sizeof(size_t) / 8;

    union
    {
        // object metadata - one bit per size_t
        // due to the minimum size of an object we can store 3 bits per object: {Marked, Escaped, Pinned}
        // it may be possible to repurpose the bits for other needs as we see fit.
        //
        // we will overlap the map and the header for simplicity of map operations.
        // it is ok because the first BITMAP_START elements of the map cover the header/map itself and thus will not be used.
        // +1 to include End(), it will always be 0, but it is convenient to make it legal map index.
        volatile size_t m_bitmap[BITMAP_LENGTH + 1];

        // Header.(can be up to 72 size_t)
        struct
        {
            // just some thread-specific value that is easy to get.
            // TEB address could be used on Windows, for example
            size_t m_ownerThreadTag;
            void (*m_escapeFunc)(SatoriObject**, SatoriObject*, SatoriRegion*);
            int m_generation;
            // above fields are accessed from asm helpers
            // NB: the write barrier reads this cache line on nearly every reference store, and concurrent marking
            //     reads it for every child. Keep it free of frequently written fields, even though there is room
            //     after m_generation. (e.g. m_unfinishedAllocationCount, which is updated with interlocked operations
            //     while allocating in shared regions, would cause false sharing here)

            // the following fields change rarely or not at all.
            size_t m_end;
            SatoriPage* m_containingPage;

            ReuseLevel m_reusableFor;
            // escape tracking was stopped by the barrier and the mark bitmap still has escape bits.
            // concurrent marking must treat the region as escape tracking until the owner clears the marks.
            // NB: MaybeEscapeTrackingAcquire reads this, m_reusableFor and m_ownerThreadTag for every child that
            //     concurrent marking looks at. Keep them in the same cache line. (this one fits in the padding)
            //     It changes when escape tracking stops, together with m_ownerThreadTag.
            bool m_staleEscapeMarks;

            SatoriRegion** m_allocatingOwnerAttachmentPoint;
            SatoriWorkChunk* m_gen2Objects;

            // ===== 64 bytes boundary

            // Active allocation may happen in the following range.
            // The range may not be parseable as sequence of objects
            // The range is in terms of objects, there is embedded off-by-one error for syncblocks.
            size_t m_allocStart;
            size_t m_allocEnd;
            // While allocating, [m_allocStart, m_allocZeroedEnd) is known to contain zeroes
            // in addition to everything above m_used. Allocations do not need to zero that again.
            size_t m_allocZeroedEnd;

            // dirty and comitted watermarks
            size_t m_used;
            size_t m_committed;

            // counting escaped objects
            // when size goes too high, we stop escaping and do not do local GC.
            int32_t m_escapedSize;
            // misc uses in thread-local regions
            int32_t m_markStack;
            // alloc bytes at last threadlocal collect
            size_t m_allocBytesAtCollect;

            SatoriWorkChunk* m_finalizableTrackers;
            int m_finalizableTrackersLock;

            uint32_t m_sweepsSinceLastAllocation;
            // Bit N is set when free list N is not empty.
            uint16_t m_nonEmptyFreeLists;
            // Number of thread-local collections performed in this region, drives the dyadic window schedule.
            uint16_t m_tlgcCount;

            // ===== 128  bytes boundary
            SatoriRegion* m_prev;
            SatoriRegion* m_next;
            SatoriQueue<SatoriRegion>* m_containingQueue;

            size_t m_occupancy;
            size_t m_demotedOccupancy;
            // End of the current dyadic allocation window (<= m_allocEnd) when escape-tracking.
            size_t m_tlgcAllocEnd;
            // Upper bound on the end of all non-free objects in the region, bounds thread-local bitmap and index scans.
            size_t m_maxParseableEnd;
            // Bytes in [FirstObject(), m_maxParseableEnd) not in >= MinZeroInitSize() free spans after the last collect.
            size_t m_unusableInPrefix;
            // Usable capacity at the start of the current accounting period for the gen1 budget - when the region
            // was attached to an allocating owner, or when the owner resumed allocating after a GC.
            // (see TakeBudgetCharge)
            size_t m_usableAtAttach;
            // The owner's alloc bytes at the start of the current accounting period. (see IsTlgcProductive)
            size_t m_allocBytesAtAttach;
            uint32_t m_escapedSizeAtCollect;
            uint32_t m_lastStackSlots;
            int32_t m_objCount;

            int32_t m_unfinishedAllocationCount;

            bool m_hasPinnedObjects;
            bool m_hasFinalizables;
            bool m_hasPendingFinalizables;
            bool m_doNotSweep;
            bool m_isPreSwept;
            bool m_isRelocated;

            bool m_acceptedPromotedObjects;
            bool m_individuallyPromoted;
            bool m_hasUnmarkedDemotedObjects;
            // A GC took the region while it stayed attached to an allocating owner.
            // The owner starts a new accounting period when it resumes allocating. (see RebaselineUsableIfStale)
            bool m_usableBaselineStale;
            // TODO: VS can fold with m_doNotSweep?
            bool m_hasMarksSet;

            size_t m_freeListCapacities[Satori::FREELIST_COUNT];
            SatoriFreeListObject* m_freeLists[Satori::FREELIST_COUNT];
            SatoriFreeListObject* m_freeListTails[Satori::FREELIST_COUNT];
        };
    };

    volatile int m_index[Satori::INDEX_LENGTH + 2];

    size_t m_syncBlock;
    SatoriObject m_firstObject;

private:
    static size_t CurrentStackDepth();
    size_t ComputeTlgcWindow();
    size_t ComputeTlgcAllocEnd(size_t minAllocSize, size_t allocBytes);
    size_t ParseableBitmapLimit();
    bool CanSplitWithoutCommit(size_t size);
    void SplitCore(size_t regionSize, size_t& newStart, size_t& newCommitted, size_t& newZeroInitedAfter);
    void UndoSplitCore(size_t regionSize, size_t nextStart, size_t nextCommitted, size_t nextUsed);

    template <bool isConservative>
    static void MarkFn(PTR_PTR_Object ppObject, ScanContext* sc, uint32_t flags);

    template <bool isConservative>
    static void UpdateFn(PTR_PTR_Object ppObject, ScanContext* sc, uint32_t flags);

    static void EscapeFn(SatoriObject** dst, SatoriObject* src, SatoriRegion* region);
    NOINLINE void EscapeReachable(SatoriObject* o);

    bool ThreadLocalMark(SatoriLocalRootCache* rootCache);
    void ThreadLocalPropagateMarks(size_t maxSurv);
    void ThreadLocalPlan();
    void ThreadLocalUpdatePointers(SatoriLocalRootCache* rootCache);
    void ThreadLocalCompact();
    void ThreadLocalPendFinalizables();

    void PushToMarkStackIfHasPointers(SatoriObject* obj);
    SatoriObject* PopFromMarkStack();
    SatoriObject* ObjectForMarkBit(size_t bitmapIndex, int offset);
    void CompactFinalizableTrackers();

    enum MarkOffset: int
    {
        Marked,
        Escaped,
        Pinned,
    };

    bool IsMarked(SatoriObject* o);
    void SetMarked(SatoriObject* o);
    void SetMarkedAtomic(SatoriObject* o);
    void ClearMarked(SatoriObject* o);
    bool CheckAndClearMarked(SatoriObject* o);

    bool IsPinned(SatoriObject* o);
    void SetPinned(SatoriObject* o);
    void ClearMarkedAndPinned(SatoriObject* o);
    void ClearMarkedAndPinnedUnlessEscaped(SatoriObject* o);
    bool IsEscaped(SatoriObject* o);
    void SetEscaped(SatoriObject* o);
    bool IsEscapedOrPinned(SatoriObject* o);

    void SetExposed(SatoriObject** location);

    bool ValidateIndexEmpty();
    bool Coalesce(SatoriRegion* next);

    template <bool updatePointers, bool individuallyPromoted, bool isEscapeTracking>
    bool Sweep();
};

#endif
