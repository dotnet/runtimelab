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
// SatoriRegion.inl
//

#ifndef __SATORI_REGION_INL__
#define __SATORI_REGION_INL__

#include "common.h"

#include "gcenv.h"
#include "../env/gcenv.os.h"
#include "../env/gcenv.ee.h"
#include "SatoriRegion.h"
#include "SatoriPage.h"
#include "SatoriWorkChunk.h"

inline bool SatoriRegion::CanSplitWithoutCommit(size_t size)
{
    size_t minCommitted = ALIGN_UP(m_end - size + offsetof(SatoriRegion, m_syncBlock) + SatoriUtil::MinZeroInitSize(), SatoriUtil::CommitGranularity());
    return m_committed >= min(minCommitted, m_end);
}

inline bool SatoriRegion::IsEscapeTracking()
{
    _ASSERTE(!m_ownerThreadTag || m_generation == 0);
    return m_ownerThreadTag;
}

inline bool SatoriRegion::MaybeEscapeTrackingAcquire()
{
    // must check reusable level before the owner tag, before doing whatever follows
    // the stale marks flag must be checked after the owner tag, see StopEscapeTrackingFromBarrier.
    return VolatileLoad((uint8_t*)&m_reusableFor) == (uint8_t)ReuseLevel::Gen0 ||
        VolatileLoad(&m_ownerThreadTag) ||
        VolatileLoad(&m_staleEscapeMarks);
}

inline bool SatoriRegion::IsEscapeTrackedByCurrentThread()
{
    return m_ownerThreadTag == SatoriUtil::GetCurrentThreadTag();
}

inline int SatoriRegion::Generation()
{
    return m_generation;
}

inline int SatoriRegion::GenerationAcquire()
{
    return VolatileLoad(&m_generation);
}

inline void SatoriRegion::SetGeneration(int generation)
{
    // Gen0 is controlled by thread-local tracking
    _ASSERTE(generation != 0);
    _ASSERTE(m_generation != 0);

    if (m_generation != generation)
    {
        // generation has changed, reset sweeps counter
        m_sweepsSinceLastAllocation = 0;
    }

    m_generation = generation;
}

inline void SatoriRegion::SetGenerationRelease(int generation)
{
    // at the time when we set generation to 0+, we should make it certain if
    // the region is attached, since 0+ detached regions are assumed parseable.
    _ASSERTE(generation != 0);
    _ASSERTE(m_generation == -1 || IsReusable());

    // No need to reset m_sweepsSinceLastAllocation like in SetGeneration.
    // SetGenerationRelease is always related to an alocation anyways.
    // (promoting/demoting does not need "Release")

    VolatileStore(&m_generation, generation);
}

inline void SatoriRegion::SetHasFinalizables()
{
    m_hasFinalizables = true;
}

inline bool SatoriRegion::IsAllocating()
{
    return m_allocEnd != 0;
}

inline size_t SatoriRegion::Start()
{
    return (size_t)this;
}

inline size_t SatoriRegion::End()
{
    return m_end;
}

inline size_t SatoriRegion::Size()
{
    return End() - Start();
}

inline bool SatoriRegion::IsLarge()
{
    return Size() > Satori::REGION_SIZE_GRANULARITY;
}

inline size_t SatoriRegion::GetAllocStart()
{
    return m_allocStart;
}

inline size_t SatoriRegion::GetAllocRemaining()
{
    // reserve Satori::MIN_FREE_SIZE to be able to make the unused space parseable
    ptrdiff_t diff = m_allocEnd - m_allocStart - Satori::MIN_FREE_SIZE;
    _ASSERTE(!IsAllocating() || diff >=0);
    return diff >= 0 ? (size_t)diff : 0;
}

inline size_t SatoriRegion::ComputeTlgcWindow()
{
    // Dyadic ("ruler-function") schedule: baseWindow << tzcnt(m_tlgcCount + 1).
    // Half of collections use baseWindow, 1/4 use 2 * baseWindow, 1/8 use 4 * baseWindow, etc.,
    // so every level of the cache hierarchy captures all scales that fit in it without knowing cache sizes.
    DWORD scale;
    BitScanForward64(&scale, (size_t)m_tlgcCount + 1);
    size_t baseWindow = 4 * SatoriUtil::MinZeroInitSize();
    size_t dyadicWindow = scale < 6 ? (baseWindow << scale) : Satori::REGION_SIZE_GRANULARITY;
    dyadicWindow = min(dyadicWindow, (size_t)Satori::REGION_SIZE_GRANULARITY);

    // Scale up by total unrecoverable/live bytes (survivors + escaped + sub-quantum fragments)
    // so at least ~75% of the window is free space (matching TLGC_MIN_FREE_PERCENT = 70),
    // and by stack depth / root count so deep stacks do not scan on small windows.
    size_t escapedSinceCollect = (size_t)m_escapedSize > m_escapedSizeAtCollect ?
        (size_t)m_escapedSize - m_escapedSizeAtCollect : 0;
    size_t unusableBytes = max((size_t)m_escapedSize, m_unusableInPrefix + escapedSinceCollect);
    size_t liveFloor = unusableBytes * 4;
    size_t stackFloor = max(CurrentStackDepth() * 8, (size_t)m_lastStackSlots * 256);
    return min((size_t)Satori::REGION_SIZE_GRANULARITY, max(dyadicWindow, max(liveFloor, stackFloor)));
}

inline size_t SatoriRegion::ComputeTlgcAllocEnd(size_t minAllocSize, size_t allocBytes)
{
    if (!IsEscapeTracking())
    {
        return m_allocEnd;
    }

    size_t totalWindow = ComputeTlgcWindow();
    size_t alreadyAllocated = allocBytes > m_allocBytesAtCollect ?
        allocBytes - m_allocBytesAtCollect : 0;
    size_t remainingWindow = totalWindow > alreadyAllocated ? totalWindow - alreadyAllocated : 0;
    size_t spanWindow = max(minAllocSize, remainingWindow);
    size_t limit = ALIGN_UP(m_allocStart + spanWindow + Satori::MIN_FREE_SIZE, Satori::INDEX_GRANULARITY);
    return min(m_allocEnd, limit);
}

inline size_t SatoriRegion::GetTlgcAllocRemaining()
{
    size_t limit = IsEscapeTracking() ? m_tlgcAllocEnd : m_allocEnd;
    ptrdiff_t diff = limit - m_allocStart - Satori::MIN_FREE_SIZE;
    _ASSERTE(!IsAllocating() || diff >= 0);
    return diff >= 0 ? (size_t)diff : 0;
}

inline bool SatoriRegion::ShouldCollectAtTlgcLimit(size_t allocBytes)
{
    if (!IsEscapeTracking() || !IsAllocating())
    {
        return false;
    }

    size_t alreadyAllocated = allocBytes > m_allocBytesAtCollect ?
        allocBytes - m_allocBytesAtCollect : 0;

    if (m_tlgcAllocEnd < m_allocEnd)
    {
        // A large request may not fit before enough allocation has occurred to justify collecting.
        // Defer instead of letting ThreadLocalCollect reject the region and end escape tracking.
        if (alreadyAllocated < 2 * SatoriUtil::MinZeroInitSize())
        {
            return false;
        }

        size_t escapedSinceCollect = (size_t)m_escapedSize > m_escapedSizeAtCollect ?
            (size_t)m_escapedSize - m_escapedSizeAtCollect : 0;
        if (escapedSinceCollect * 4 > alreadyAllocated)
        {
            // Too much of the current window escaped; let the live-size floor extend it instead of collecting early.
            return false;
        }

        return true;
    }

    return alreadyAllocated >= ComputeTlgcWindow();
}

inline size_t SatoriRegion::ParseableBitmapLimit()
{
    size_t end = min(m_maxParseableEnd, Start() + Satori::REGION_SIZE_GRANULARITY);
    size_t limit = (end - Start() + Satori::BYTES_PER_CARD_BYTE - 1) / Satori::BYTES_PER_CARD_BYTE;
    _ASSERTE(limit <= SatoriRegion::BITMAP_LENGTH);
    return limit;
}

// The size of the zeroed run that starts at m_allocStart.
inline size_t SatoriRegion::GetAllocZeroedRemaining()
{
    _ASSERTE(m_allocZeroedEnd <= m_allocEnd);
    size_t zeroedEnd = max(m_allocStart, m_allocZeroedEnd);

    // everything at or above m_used is zeroed too, so if the run reaches m_used, it goes all the way.
    if (zeroedEnd >= m_used)
    {
        zeroedEnd = m_allocEnd;
    }

    return zeroedEnd - m_allocStart;
}

inline size_t SatoriRegion::RegionSizeForAlloc(size_t allocSize)
{
    return ALIGN_UP(allocSize + offsetof(SatoriRegion, m_firstObject) + Satori::MIN_FREE_SIZE, Satori::REGION_SIZE_GRANULARITY);
}

inline SatoriObject* SatoriRegion::FirstObject()
{
    return &m_firstObject;
}

inline void SatoriRegion::StartEscapeTrackingRelease(size_t threadTag, size_t allocBytes, size_t minAllocSize)
{
    _ASSERTE(m_generation == -1 || m_reusableFor == ReuseLevel::Gen0);
    _ASSERTE(!m_staleEscapeMarks);
    m_escapeFunc = EscapeFn;
    m_ownerThreadTag = threadTag;
    m_allocBytesAtCollect = allocBytes;
    m_tlgcCount = 0;
    m_escapedSizeAtCollect = (uint32_t)m_escapedSize;
    m_allocBytesAtAttach = allocBytes;
    m_lastStackSlots = 0;
    VolatileStore(&m_generation, 0);
    if (IsAllocating())
    {
        m_tlgcAllocEnd = ComputeTlgcAllocEnd(minAllocSize, allocBytes);
    }
}

inline void SatoriRegion::StopEscapeTracking()
{
    if (IsEscapeTracking())
    {
        _ASSERTE(!HasPinnedObjects());
        ClearMarks();

        // must clear ownership after clearing marks
        // to make sure concurrent marking does not use dirty mark table
        VolatileStore(&m_ownerThreadTag, (size_t)0);

        m_escapeFunc = nullptr;
        m_generation = 1;
        m_escapedSize = 0;
        m_allocBytesAtCollect = 0;
    }
    else
    {
        ClearStaleEscapeMarks();
    }
}

// Called from the write barrier when the escape budget is exceeded.
// Clearing the marks is left to the owner thread, since the barrier does not preserve
// vector registers and clearing the bitmap is relatively expensive.
inline void SatoriRegion::StopEscapeTrackingFromBarrier()
{
    _ASSERTE(IsEscapeTracking());

    // the flag must be set before ownership is cleared, so that concurrent marking,
    // which checks ownership first, never sees neither and does not use dirty mark table.
    m_staleEscapeMarks = true;
    VolatileStore(&m_ownerThreadTag, (size_t)0);

    m_escapeFunc = nullptr;
    m_generation = 1;
    m_escapedSize = 0;
    m_allocBytesAtCollect = 0;
}

inline bool SatoriRegion::ClearStaleEscapeMarks()
{
    if (!m_staleEscapeMarks)
    {
        return false;
    }

    _ASSERTE(!IsEscapeTracking());
    _ASSERTE(!HasPinnedObjects());
    ClearMarks();

    // must reset the flag after clearing marks
    // to make sure concurrent marking does not use dirty mark table
    VolatileStore(&m_staleEscapeMarks, false);
    return true;
}

// Used to simulate writes when containing region is individually promoted.
inline void SatoriRegion::SetCardsForObject(SatoriObject* o, size_t size)
{
    _ASSERTE(this->Size() == Satori::REGION_SIZE_GRANULARITY);

    o->ForEachObjectRef(
        [&](SatoriObject** ppObject)
        {
            SatoriObject* child = VolatileLoadWithoutBarrier(ppObject);
            if (child &&
                !child->IsExternal() &&
                child->ContainingRegion()->Generation() < 2)
           {
                // This could run concurrenlty with mutator or in a blocking stage,
                // but not when the blocking stage does cleaning.
                // It also should be relatively rare, so we will just assume the mutator is running
                // for simplicity and call a concurrent helper.
                ContainingPage()->DirtyCardForAddressConcurrent((size_t)ppObject);
            }
        },
        size
    );

    if (o->HasEphemeralCollectibleAllocator())
    {
        // The loader-allocator edge is synthetic; its card belongs to the MethodTable slot.
        ContainingPage()->DirtyCardForAddressConcurrent(o->Start());
    }
}

template <typename F>
void SatoriRegion::ForEachFinalizable(F lambda)
{
    size_t items = 0;
    size_t nulls = 0;
    SatoriWorkChunk* chunk = m_finalizableTrackers;
    while (chunk)
    {
        size_t count = chunk->Count();
        items += count;
        for (size_t i = 0; i < count; i++)
        {
            SatoriObject* finalizable = chunk->Item(i);
            if (finalizable == nullptr)
            {
                nulls++;
                continue;
            }

            SatoriObject* newFinalizable = lambda(finalizable);
            if (newFinalizable != finalizable)
            {
                chunk->Item(i) = newFinalizable;
                if (newFinalizable == nullptr)
                {
                    nulls++;
                }
            }
        }

        // unfilled slots in the tail chunks count as nulls
        if (chunk != m_finalizableTrackers)
        {
            nulls += chunk->FreeSpace();
        }

        chunk = chunk->Next();
    }

    // typically the list is short, so having some dead entries is not a big deal.
    // compacting at 1/2 occupancy should be good enough.
    // (50% max overhead at O(N) maintenance cost)
    if (nulls * 2 > items)
    {
       CompactFinalizableTrackers();
    }
}

template <typename F>
bool SatoriRegion::PendFinalizables(F markFn, int condemnedGeneration)
{
    bool hasCF = false;
    size_t items = 0;
    size_t nulls = 0;

    SatoriFinalizationQueue* q = m_containingPage->Heap()->FinalizationQueue();
    SatoriWorkChunk* chunk = m_finalizableTrackers;
    while (chunk)
    {
        int32_t toPend = 0;
        int32_t toPendCF = 0;
        size_t count = chunk->Count();
        items += count;
        for (size_t i = 0; i < count; i++)
        {
            SatoriObject* finalizable = chunk->Item(i);
            _ASSERTE(((size_t)finalizable & Satori::FINALIZATION_PENDING_ANY) == 0);

            if (finalizable == nullptr)
            {
                nulls++;
                continue;
            }

            // reachable finalizables are not iteresting in any state.
            // finalizer can be suppressed and re-registered again without creating new trackers.
            // (this is preexisting behavior)
            if (finalizable->IsMarked())
            {
                continue;
            }

            // eager finalization does not respect suppression (preexisting behavior)
            if (GCToEEInterface::EagerFinalized(finalizable))
            {
                finalizable = nullptr;
                nulls++;
            }
            else if (finalizable->IsFinalizationSuppressed())
            {
                // Reset the bit so it will be put back on the queue
                // if resurrected and re-registered.
                // NOTE: if finalizer could run only once until re-registered,
                //       unreachable + suppressed object would not be able to resurrect.
                //       however, re-registering multiple times may result in multiple finalizer runs.
                // (this is preexisting behavior)
                finalizable->UnSuppressFinalization();
                finalizable = nullptr;
                nulls++;
            }
            else
            {
                // finalizable has become unreachable.
                // mark it as it needs to stay live until finalized and/or suppressed.
                markFn(finalizable);

                if (finalizable->RawGetMethodTable()->HasCriticalFinalizer())
                {
                    // can't pend just yet, because CriticalFinalizables must go
                    // after regular finalizables scheduled in the same GC
                    toPendCF++;
                    (size_t&)finalizable |= Satori::FINALIZATION_PENDING_CRITICAL;
                }
                else
                {
                    toPend++;
                    (size_t&)finalizable |= Satori::FINALIZATION_PENDING;
                }
            }

            chunk->Item(i) = finalizable;
        }

        if (toPend)
        {
            size_t pendIdx;
            bool overflow = !q->TryReserveSpace(toPend, &pendIdx);
            for (size_t i = 0; i < count; i++)
            {
                SatoriObject* finalizable = chunk->Item(i);
                if ((size_t)finalizable & Satori::FINALIZATION_PENDING)
                {
                    _ASSERTE(toPend > 0);
#if _DEBUG
                    toPend--;
#endif

                    (size_t&)finalizable &= ~Satori::FINALIZATION_PENDING;
                    if (!overflow)
                    {
                        q->ScheduleForFinalizationAt(pendIdx++, finalizable);
                        finalizable = nullptr;
                        nulls++;
                    }

                    chunk->Item(i) = finalizable;
                }
            }

            if (overflow)
            {
                q->SetOverflow(condemnedGeneration);
            }
        }

        _ASSERTE(toPend == 0);

        // unfilled slots in the tail chunks count as nulls
        if (chunk != m_finalizableTrackers)
        {
            nulls += chunk->FreeSpace();
        }

        if (toPendCF)
        {
            chunk->SetAux(toPendCF);
            hasCF = true;
        }

        chunk = chunk->Next();
    }

    // typically the list is short, so having some dead entries is not a big deal.
    // compacting at 1/2 occupancy should be good enough.
    // (50% max overhead at O(N) maintenance cost)
    if (!hasCF && nulls * 2 > items)
    {
       CompactFinalizableTrackers();
    }

    return hasCF;
}

inline void SatoriRegion::PendCfFinalizables(int condemnedGeneration)
{
    size_t items = 0;
    size_t nulls = 0;

    SatoriFinalizationQueue* q = m_containingPage->Heap()->FinalizationQueue();
    SatoriWorkChunk* chunk = m_finalizableTrackers;
    while (chunk)
    {
        int32_t toPend = chunk->GetAndClearAux();
        size_t count = chunk->Count();
        items += count;
        size_t pendIdx = 0;
        bool overflow = toPend &&
            (q->OverflowedGen() != 0 || !q->TryReserveSpace(toPend, &pendIdx));

        for (size_t i = 0; i < count; i++)
        {
            SatoriObject* finalizable = chunk->Item(i);
            if (finalizable == nullptr)
            {
                nulls++;
                continue;
            }

            if ((size_t)finalizable & Satori::FINALIZATION_PENDING_CRITICAL)
            {
                _ASSERTE(toPend > 0);
#if _DEBUG
                toPend--;
#endif

                (size_t&)finalizable &= ~Satori::FINALIZATION_PENDING_CRITICAL;
                if (!overflow)
                {
                    q->ScheduleForFinalizationAt(pendIdx++, finalizable);
                    finalizable = nullptr;
                    nulls++;
                }

                chunk->Item(i) = finalizable;
            }
        }

        _ASSERTE(toPend == 0);

        if (overflow)
        {
            q->SetOverflow(condemnedGeneration);
        }

        // unfilled slots in the tail chunks count as nulls
        if (chunk != m_finalizableTrackers)
        {
            nulls += chunk->FreeSpace();
        }

        chunk = chunk->Next();
    }

    // typically the list is short, so having some dead entries is not a big deal.
    // compacting at 1/2 occupancy should be good enough.
    // (50% max overhead at O(N) maintenance cost)
    if (nulls * 2 > items)
    {
       CompactFinalizableTrackers();
    }
}

// Used by threadlocal GC concurrently with user threads,
// thus must lock - in case a user thread tries to reregister an object for finalization
template <typename F>
void SatoriRegion::ForEachFinalizableThreadLocal(F lambda)
{
    LockFinalizableTrackers();
    ForEachFinalizable(lambda);
    UnlockFinalizableTrackers();
}

// Sweeping does little work per object and the next object is known only after reading the current one,
// thus the sweep is mostly waiting for object headers. The mark bitmap tells where the upcoming live
// objects are, so they can be prefetched some distance ahead.
// (8 lines ahead made sweeping about a quarter to a third cheaper per live object)
inline void SatoriRegion::SweepPrefetchStart(SweepPrefetcher& prefetcher, SatoriObject* from)
{
    // start with the mark bits at and after the given object
    int bitOffset = from->GetMarkBitAndWord(&prefetcher.m_index);
    prefetcher.m_word = m_bitmap[prefetcher.m_index] & ~(((size_t)1 << bitOffset) - 1);
    prefetcher.m_line = 0;
    for (int i = 0; i < SWEEP_PREFETCH_LINES; i++)
    {
        SweepPrefetchNext(prefetcher);
    }
}

// called when the sweep gets to a live object, keeps the prefetcher the same number of lines ahead.
inline void SatoriRegion::SweepPrefetchAdvance(SweepPrefetcher& prefetcher, SatoriObject* o)
{
    size_t line = o->Start() >> 6;
    if (line != prefetcher.m_line)
    {
        prefetcher.m_line = line;
        SweepPrefetchNext(prefetcher);
    }
}

inline void SatoriRegion::SweepPrefetchNext(SweepPrefetcher& prefetcher)
{
    // NB: the sweep may clear marks behind us, but we are ahead and see the words before they are touched.
    while (prefetcher.m_word == 0)
    {
        if (prefetcher.m_index >= BITMAP_LENGTH - 1)
        {
            return;
        }

        prefetcher.m_word = m_bitmap[++prefetcher.m_index];
    }

    DWORD bit;
    BitScanForward64(&bit, prefetcher.m_word);
    // the rest of the mark byte is in the same cache line, skip it.
    prefetcher.m_word &= ~((size_t)0xFF << (bit & ~7));
    // prefetch the first object that starts in the line, rather than the line start,
    // in case the actual cache line is smaller.
    SatoriUtil::Prefetch(ObjectForMarkBit(prefetcher.m_index, (int)bit));
}

template <bool updatePointers, bool individuallyPromoted, bool isEscapeTracking>
bool SatoriRegion::Sweep()
{
    // we should only sweep when we have marks
    _ASSERTE(HasMarksSet());
    _ASSERTE(!DoNotSweep());

    m_sweepsSinceLastAllocation++;

    size_t objLimit = Start() + Satori::REGION_SIZE_GRANULARITY;

    // in a huge region there is only one real object. If it is unreachable the region is all empty.
    if (End() > objLimit)
    {
        SatoriObject* o = FirstObject()->IsFree() ? FirstObject()->Next() : FirstObject();
        if (!IsMarked(o))
        {
            _ASSERTE(this->NothingMarked());
            _ASSERTE(!this->HasPinnedObjects());
#if _DEBUG
            size_t fistObjStart = FirstObject()->Start();
            SatoriObject::FormatAsFree(fistObjStart, objLimit - fistObjStart);
            this->HasMarksSet() = false;
#endif
            this->DoNotSweep() = true;
            SetOccupancy(0, 0);
            return false;
        }
    }
    else
    {
        // we will be building new free lists
        ClearFreeLists();
    }

    m_escapedSize = 0;
    bool cannotRecycle = this->IsAttachedToAllocatingOwner();
    size_t occupancy = 0;
    int32_t objCount = 0;
    bool hasFinalizables = false;
    SatoriObject* o = FirstObject();
    SweepPrefetcher prefetcher;
    SweepPrefetchStart(prefetcher, o);
    SatoriRelocatedRefQueue<8> updateQueue;
    do
    {
        if (!CheckAndClearMarked(o))
        {
            size_t lastMarkedEnd = o->Start();
            o = SkipUnmarkedAndClear(o);
            size_t skipped = o->Start() - lastMarkedEnd;
            SatoriObject* free = SatoriObject::FormatAsFree(lastMarkedEnd, skipped);
            SetIndicesForObject(free, o->Start());
            AddFreeSpace(free, skipped);

            if (o->Start() >= objLimit)
            {
                _ASSERTE(o->Start() == objLimit);
                break;
            }
        }

        _ASSERTE(!o->IsFree());
        SweepPrefetchAdvance(prefetcher, o);

        size_t size = o->Size();
        if (isEscapeTracking)
        { 
            this->EscapeShallow(o, size);
        }

        if (updatePointers)
        {
            UpdatePointersInObject(o, size, updateQueue);
        }

        if (individuallyPromoted)
        {
            SetCardsForObject(o, size);
        }

        if (!hasFinalizables && o->RawGetMethodTable()->HasFinalizer())
        {
            hasFinalizables = true;
        }

        objCount++; 
        occupancy += size;
        o = (SatoriObject*)(o->Start() + size);
    } while (o->Start() < objLimit);

    if (updatePointers)
    {
        updateQueue.Flush();
    }

    _ASSERTE(o->Start() == objLimit || End() > objLimit);
    _ASSERTE(hasFinalizables || !m_finalizableTrackers);
#if _DEBUG
    this->HasMarksSet() = false;
#endif

    this->HasUnmarkedDemotedObjects() = IsDemoted();
    this->m_hasFinalizables = hasFinalizables;
    this->DoNotSweep() = true;
    this->HasPinnedObjects() = false;
    this->m_individuallyPromoted = false;
    this->m_maxParseableEnd = objLimit;

    SetOccupancy(occupancy, objCount);
    if (objCount !=0)
    {
        cannotRecycle = true;
    }

    return cannotRecycle;
}

template <bool updatePointers>
bool SatoriRegion::Sweep()
{
    return
        this->m_individuallyPromoted ?
            this->IsEscapeTracking() ?
                Sweep<updatePointers, true, true>() :
                Sweep<updatePointers, true, false>() :
            this->IsEscapeTracking() ?
                Sweep<updatePointers, false, true>() :
                Sweep<updatePointers, false, false>();
}

inline bool SatoriRegion::HasFinalizables()
{
    return m_hasFinalizables;
}

inline bool& SatoriRegion::HasPendingFinalizables()
{
    return m_hasPendingFinalizables;
}

inline size_t SatoriRegion::Occupancy()
{
    return m_occupancy;
}

inline size_t SatoriRegion::DemotedOccupancy()
{
    return m_demotedOccupancy;
}

inline int32_t SatoriRegion::ObjCount()
{
    return m_objCount;
}

inline bool& SatoriRegion::HasPinnedObjects()
{
    return m_hasPinnedObjects;
}

// Markers call this for every pinned object they see, possibly on many threads at once.
// Store only if not set yet, so that repeated calls do not take the cache line exclusive.
inline void SatoriRegion::SetHasPinnedObjects()
{
    if (!m_hasPinnedObjects)
    {
        m_hasPinnedObjects = true;
    }
}

#if _DEBUG
inline bool& SatoriRegion::HasMarksSet()
{
    return m_hasMarksSet;
}
#endif

inline bool& SatoriRegion::DoNotSweep()
{
    return m_doNotSweep;
}

inline bool& SatoriRegion::IsPreSwept()
{
    return m_isPreSwept;
}

inline bool& SatoriRegion::IsRelocated()
{
    return m_isRelocated;
}

inline bool& SatoriRegion::AcceptedPromotedObjects()
{
    return m_acceptedPromotedObjects;
}

inline bool& SatoriRegion::IndividuallyPromoted()
{
    return m_individuallyPromoted;
}

inline uint32_t SatoriRegion::SweepsSinceLastAllocation()
{
    return m_sweepsSinceLastAllocation;
}

inline bool SatoriRegion::IsReusable()
{
    return m_reusableFor != ReuseLevel::None;
}

inline SatoriRegion::ReuseLevel& SatoriRegion::ReusableFor()
{
    return m_reusableFor;
}

inline SatoriQueue<SatoriRegion>* SatoriRegion::ContainingQueue()
{
    return VolatileLoadWithoutBarrier(&m_containingQueue);
}

inline void SatoriRegion::AttachToAllocatingOwner(SatoriRegion** attachementPoint)
{
    _ASSERTE(!m_allocatingOwnerAttachmentPoint);
    _ASSERTE(!*attachementPoint);

    *attachementPoint = this;
    m_allocatingOwnerAttachmentPoint = attachementPoint;

    // start an accounting period for the gen1 budget. (see TakeBudgetCharge)
    m_usableAtAttach = UsableCapacity();
    m_usableBaselineStale = false;
}

inline void SatoriRegion::RebaselineUsableIfStale(size_t allocBytes)
{
    if (m_usableBaselineStale)
    {
        // the GC deactivated the region, the owner cannot be in the middle of allocating from it.
        _ASSERTE(!IsAllocating());
        m_usableAtAttach = UsableCapacity();
        m_allocBytesAtAttach = allocBytes;
        m_usableBaselineStale = false;
    }
}

inline void SatoriRegion::DetachFromAlocatingOwnerRelease()
{
    _ASSERTE(*m_allocatingOwnerAttachmentPoint == this);

    StopEscapeTracking();

    *m_allocatingOwnerAttachmentPoint = nullptr;
    // all allocations must be committed prior to detachement.
    VolatileStore(&m_allocatingOwnerAttachmentPoint, (SatoriRegion**)nullptr);
}

inline bool SatoriRegion::IsAttachedToAllocatingOwner()
{
    return m_allocatingOwnerAttachmentPoint;
}

inline bool SatoriRegion::MaybeAllocatingAcquire()
{
    // must check reusable level before the attach point, before doing whatever follows
    // The unfinished allocation count must be read with acquire as well. In a detached shared region it can be
    // the only thing that keeps the caller from parsing the region, and the parsing must not read objects ahead
    // of seeing the count drop to zero. (possible on weak memory models)
    return VolatileLoad((uint8_t*)&m_reusableFor) ||
        VolatileLoad(&m_allocatingOwnerAttachmentPoint) ||
        VolatileLoad(&m_unfinishedAllocationCount);
}

inline void SatoriRegion::ResetReusableForRelease()
{
    VolatileStore(&m_reusableFor, SatoriRegion::ReuseLevel::None);
}

inline bool SatoriRegion::IsDemoted()
{
    return m_gen2Objects;
}

inline SatoriWorkChunk* &SatoriRegion::DemotedObjects()
{
    return m_gen2Objects;
}

inline bool& SatoriRegion::HasUnmarkedDemotedObjects()
{
    return m_hasUnmarkedDemotedObjects;
}

inline void SatoriRegion::SetMarked(SatoriObject* o)
{
    _ASSERTE(o->SameRegion(this));

    size_t word = o->Start();
    size_t bitmapIndex = (word >> 9) & (SatoriRegion::BITMAP_LENGTH - 1);
    size_t mask = (size_t)1 << ((word >> 3) & 63);

    m_bitmap[bitmapIndex] |= mask;
}

inline void SatoriRegion::SetMarkedAtomic(SatoriObject* o)
{
    _ASSERTE(o->SameRegion(this));
    _ASSERTE(!o->IsFree());

    size_t word = o->Start();
    size_t bitmapIndex = (word >> 9) & (SatoriRegion::BITMAP_LENGTH - 1);
    size_t mask = (size_t)1 << ((word >> 3) & 63);

    //NB: this does not need to be a full fence, if possible. 
    //    just need to set a bit atomically.
    //    ordering WRT other writes is unimportant, as long as the bit is set.
#ifdef _MSC_VER
#if defined(TARGET_AMD64)
    _InterlockedOr64((long long*)&m_bitmap[bitmapIndex], mask);
#else
    _InterlockedOr64_nf((long long*)&m_bitmap[bitmapIndex], mask);
#endif
#else
    __atomic_or_fetch(&m_bitmap[bitmapIndex], mask, __ATOMIC_RELAXED);
#endif
}

inline void SatoriRegion::ClearMarked(SatoriObject* o)
{
    _ASSERTE(o->SameRegion(this));

    size_t word = o->Start();
    size_t bitmapIndex = (word >> 9) & (SatoriRegion::BITMAP_LENGTH - 1);
    size_t mask = (size_t)1 << ((word >> 3) & 63);

    m_bitmap[bitmapIndex] &= ~mask;
}

inline bool SatoriRegion::IsMarked(SatoriObject* o)
{
    _ASSERTE(o->SameRegion(this));

    size_t word = o->Start();
    size_t bitmapIndex = (word >> 9) & (SatoriRegion::BITMAP_LENGTH - 1);
    size_t mask = (size_t)1 << ((word >> 3) & 63);

    return m_bitmap[bitmapIndex] & mask;
}

inline bool SatoriRegion::CheckAndClearMarked(SatoriObject* o)
{
    _ASSERTE(o->SameRegion(this));

    size_t word = o->Start();
    size_t bitmapIndex = (word >> 9) & (SatoriRegion::BITMAP_LENGTH - 1);
    size_t mask = (size_t)1 << ((word >> 3) & 63);

    volatile size_t& bitmapWord = m_bitmap[bitmapIndex];
    bool wasMarked = bitmapWord & mask;
    bitmapWord &= ~mask;
    return wasMarked;
}

inline bool SatoriRegion::IsPinned(SatoriObject* o)
{
    return IsMarked(o + MarkOffset::Pinned);
}

inline void SatoriRegion::SetPinned(SatoriObject* o)
{
    SetMarked(o + MarkOffset::Pinned);
}

// NB: the mark, escaped and pinned bits of an object are adjacent, thus usually in the same bitmap word.
//     In escaped objects the bit in the pinned position is the "exposed" bit of the second field,
//     so it can be treated as the pinned bit only when the object is not escaped.

inline void SatoriRegion::ClearMarkedAndPinned(SatoriObject* o)
{
    _ASSERTE(o->SameRegion(this));
    _ASSERTE(!IsEscaped(o));

    size_t word = o->Start();
    size_t bitmapIndex = (word >> 9) & (SatoriRegion::BITMAP_LENGTH - 1);
    size_t bit = (word >> 3) & 63;
    if (bit < 62)
    {
        // ClearMarked(o) and ClearMarked(o + MarkOffset::Pinned) at once
        m_bitmap[bitmapIndex] &= ~((size_t)5 << bit);
    }
    else
    {
        ClearMarked(o);
        ClearMarked(o + MarkOffset::Pinned);
    }
}

// escaped objects stay marked (and cannot be pinned), see SetEscaped
inline void SatoriRegion::ClearMarkedAndPinnedUnlessEscaped(SatoriObject* o)
{
    _ASSERTE(o->SameRegion(this));

    size_t word = o->Start();
    size_t bitmapIndex = (word >> 9) & (SatoriRegion::BITMAP_LENGTH - 1);
    size_t bit = (word >> 3) & 63;
    if (bit < 62)
    {
        size_t bits = m_bitmap[bitmapIndex];
        if (!(bits & ((size_t)2 << bit)))
        {
            // not escaped, ClearMarked(o) and ClearMarked(o + MarkOffset::Pinned) at once
            m_bitmap[bitmapIndex] = bits & ~((size_t)5 << bit);
        }
    }
    else if (!IsEscaped(o))
    {
        ClearMarked(o);
        ClearMarked(o + MarkOffset::Pinned);
    }
}

inline bool SatoriRegion::IsEscaped(SatoriObject* o)
{
    return IsMarked(o + MarkOffset::Escaped);
}

inline void SatoriRegion::SetEscaped(SatoriObject* o)
{
    // Escaped objects are always live as far as thread-local GC is concerned, so they are also marked.
    // Thread-local GC clears marks when done, except marks of escaped objects.
    // NB: both bits are cleared together when tracking stops or when GC takes over the region.
    _ASSERTE(o->SameRegion(this));

    size_t word = o->Start();
    size_t bitmapIndex = (word >> 9) & (SatoriRegion::BITMAP_LENGTH - 1);
    size_t bit = (word >> 3) & 63;
    if (bit != 63)
    {
        // the mark and escape bits are adjacent and in the same word, set both at once.
        // SetMarked(o) and SetMarked(o + MarkOffset::Escaped)
        m_bitmap[bitmapIndex] |= (size_t)3 << bit;
    }
    else
    {
        // the mark bit is the last in the word, the escape bit is the first in the next word.
        // NB: o is not at the end of the region, so the next word is still in the bitmap.
        m_bitmap[bitmapIndex] |= (size_t)1 << 63;    // SetMarked(o)
        m_bitmap[bitmapIndex + 1] |= 1;              // SetMarked(o + MarkOffset::Escaped)
    }
}

inline bool SatoriRegion::IsEscapedOrPinned(SatoriObject* o)
{
    _ASSERTE(o->SameRegion(this));

    size_t word = o->Start();
    size_t bitmapIndex = (word >> 9) & (SatoriRegion::BITMAP_LENGTH - 1);
    size_t bit = (word >> 3) & 63;
    if (bit < 62)
    {
        // IsEscaped(o) || IsPinned(o) with one read
        if (m_bitmap[bitmapIndex] & ((size_t)6 << bit))
        {
            return true;
        }
    }
    else if (IsEscaped(o) || IsPinned(o))
    {
        return true;
    }

    return o->IsUnmovable();
}

inline void SatoriRegion::SetExposed(SatoriObject** location)
{
    // set the mark bit corresponding to the location to indicate that it is globally exposed
    // same as: SetMarked((SatoriObject*)location);
    SetMarked((SatoriObject*)location);
}

inline bool SatoriRegion::IsExposed(SatoriObject** location)
{
    // check the mark bit corresponding to the location
    //same as: return IsMarked((SatoriObject*)location);
    return IsMarked((SatoriObject*)location);
}

inline SatoriObject* SatoriRegion::ObjectForMarkBit(size_t bitmapIndex, int offset)
{
    size_t objOffset = bitmapIndex * sizeof(size_t) * 8 + offset;
    return (SatoriObject*)(Start()) + objOffset;
}

inline size_t SatoriRegion::LocationToIndex(size_t location)
{
    return (location - Start()) >> Satori::INDEX_GRANULARITY_BITS;
}

inline void SatoriRegion::SetIndicesForObject(SatoriObject* o, size_t end)
{
    _ASSERTE(end > o->Start());
    _ASSERTE(end <= Start() + Satori::REGION_SIZE_GRANULARITY);

    size_t start = o->Start();

    // if object straddles index granules, record the object in corresponding indices
    if ((start ^ end) >> Satori::INDEX_GRANULARITY_BITS)
    {
        SetIndicesForObjectCore(start, end);
    }
}

inline void SatoriRegion::ClearIndicesForAllocRange()
{
    _ASSERTE(m_allocStart < m_allocEnd);
    _ASSERTE(m_allocStart > Start());

    size_t start = m_allocStart;
    size_t end = min(m_allocEnd, Start() + Satori::REGION_SIZE_GRANULARITY);

    // if range straddles index granules, clear corresponding indices
    if ((start ^ end) >> Satori::INDEX_GRANULARITY_BITS)
    {
        size_t firstIndex = LocationToIndex(start) + 1;
        // if first is clean, all are clean
        if (m_index[firstIndex] != 0)
        {
            size_t lastIndex = LocationToIndex(end);
            memset((void*)&m_index[firstIndex], 0, (lastIndex - firstIndex + 1) * sizeof(int));
        }

#if _DEBUG
        for(size_t i = firstIndex, last = LocationToIndex(end); i <= last; i++)
        {
            _ASSERTE(m_index[i] == 0);
        }
#endif
    }
}

// NB: objects are relocated to a higher generation only when all regions are promoted (see RelocateRegion),
//     so promotingAllRegions is always true here at the moment.
//     Otherwise the loader allocator edge of collectible objects would need a card as well.
template <bool promotingAllRegions>
void SatoriRegion::UpdatePointersInPromotedObjects()
{
    _ASSERTE(HasMarksSet());

    size_t objLimit = Start() + Satori::REGION_SIZE_GRANULARITY;
    SatoriObject* o = FirstObject();
    SatoriRelocatedRefQueue<8> updateQueue;
    do
    {
        o = SkipUnmarked(o);
        if (o->Start() >= objLimit)
        {
            _ASSERTE(o->Start() == objLimit);
            break;
        }

        _ASSERTE(IsMarked(o));

        SatoriObject* promoted = o->RelocatedToUnchecked();
        _ASSERTE(!promoted->IsFree());

        SatoriPage* page = promoted->ContainingRegion()->ContainingPage();
        size_t size = promoted->Size();
        promoted->ForEachObjectRef(
            [&](SatoriObject** ppObject)
            {
                // prevent re-reading o, UpdatePointersThroughCards could be doing the same update.
                SatoriObject* child = VolatileLoadWithoutBarrier(ppObject);
                if (child &&
                    !child->IsExternal())
                {
                    if (child->IsRelocated</*notExternal*/true>())
                    {
                        updateQueue.Push(ppObject, child);
                    }

                    // update the card as if the relocated object got a child assigned
                    if (!promotingAllRegions)
                    {
                        if (child->ContainingRegion()->Generation() < 2)
                        {
                            page->SetCardForAddress((size_t)ppObject);
                        }
                    }
                }
            },
            size
        );

        o = (SatoriObject*)(o->Start() + size);
    } while (o->Start() < objLimit);

    updateQueue.Flush();
}

inline int SatoriRegion::IncrementUnfinishedAlloc()
{
    return (int)Interlocked::Increment(&m_unfinishedAllocationCount);
}

inline void SatoriRegion::DecrementUnfinishedAlloc()
{
    Interlocked::Decrement(&m_unfinishedAllocationCount);
    _ASSERTE((int)m_unfinishedAllocationCount >= 0);
}

#endif
