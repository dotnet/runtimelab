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
// SatoriRecycler.cpp
//

#include "common.h"

#include "gcenv.h"
#include "../env/gcenv.os.h"
#include "../gceventstatus.h"

// TODO: fixme
#include "../../inc/enum_class_flags.h"

#include "SatoriHandlePartitioner.h"
#include "SatoriHeap.h"
#include "SatoriTrimmer.h"
#include "SatoriPage.h"
#include "SatoriPage.inl"
#include "SatoriRecycler.h"
#include "SatoriObject.h"
#include "SatoriObject.inl"
#include "SatoriRegion.h"
#include "SatoriRegion.inl"
#include "SatoriWorkChunk.h"
#include "SatoriPrefetchQueue.h"
#include "SatoriAllocationContext.h"
#include "SatoriFinalizationQueue.h"
#include "../gcscan.h"

#include <minipal/memorybarrierprocesswide.h>

//#pragma optimize("", off)

#ifdef memcpy
#undef memcpy
#endif //memcpy

static const int MIN_GEN1_BUDGET = 2 * Satori::REGION_SIZE_GRANULARITY;

extern "C" uint8_t *g_write_barrier_state = 0;

// Publishes the barrier state to the barriers via the patchable state values (see GetAssignRefFunctions).
void ToggleWriteBarrier(int barrierState, bool eeSuspended)
{
    // only the states that have a published counterpart could be set
    _ASSERTE(barrierState == SatoriRecycler::BARRIER_STATE_NOT_CONCURRENT ||
             barrierState == SatoriRecycler::BARRIER_STATE_CONCURRENT ||
             barrierState == SatoriRecycler::BARRIER_STATE_SKIPPING_CARDS);

    WriteBarrierParameters args = {};

    args.operation = WriteBarrierOp::StompCustom;
    args.parameters_mask = WriteBarrierParametersMask::WriteWatchTable | WriteBarrierParametersMask::IsRuntimeSuspended | WriteBarrierParametersMask::SwWwEnabledForGcHeap;

    args.write_watch_table = (uint8_t*)(size_t)barrierState;
    args.sw_ww_enabled_for_gc_heap = (barrierState == SatoriRecycler::BARRIER_STATE_CONCURRENT);

    g_write_barrier_state = args.write_watch_table;

    args.is_runtime_suspended = eeSuspended;
    GCToEEInterface::StompWriteBarrier(&args);

    if (barrierState == SatoriRecycler::BARRIER_STATE_CONCURRENT && !eeSuspended)
    {
        // If runtime is not suspended, force all threads to see the changed state before observing future allocations.
        minipal_memory_barrier_process_wide();
    }    
}

void SatoriRecycler::Initialize(SatoriHeap* heap)
{
    m_workerGate = new (nothrow) SatoriGate();
    m_gateSignaled = 0;
    m_workerWoken = 0;
    m_activeWorkers= 0;
    m_totalWorkers = 0;

    m_noWorkSince = 0;

    m_osTicksPerMilli = minipal_hires_tick_frequency() / 1000;
    m_osTicksPerMicro = minipal_hires_tick_frequency() / 1000000;
    m_timeStampTicksPerMilli = SatoriUtil::GetTimeStampFrequency() / 1000;

    m_heap = heap;
    m_trimmer = new (nothrow) SatoriTrimmer(heap);

    m_ephemeralRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerEphemeral, 2);
    m_ephemeralFinalizationTrackingRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerEphemeralFinalizationTracking, 4);
    m_tenuredRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerTenured, 4);
    m_tenuredFinalizationTrackingRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerTenuredFinalizationTracking, 8);

    m_finalizationPendingRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerFinalizationPending, 8);

    m_stayingRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerStaying, 2);
    m_relocatingRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerRelocating, 2);
    m_relocatedRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerRelocated, 4);
    m_relocatedToHigherGenRegions = SatoriRegionQueue::AllocAligned(QueueKind::RecyclerRelocatedToHigherGen);

    for (int i = 0; i < Satori::FREELIST_COUNT; i++)
    {
        int processorsPerShard = (i < 4) ? 8 : 4;
        m_relocationTargets[i] = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerRelocationTarget, processorsPerShard);
    }

    m_deferredSweepRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerDeferredSweep, 4);
    m_deferredSweepCount = 0;
    m_gen1AddedSinceLastCollection = 0;
    m_gen2AddedSinceLastCollection = 0;

    m_reusableRegions = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerReusable, 4);
    m_ephemeralWithUnmarkedDemoted = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerDemoted, 8);
    m_reusableRegionsAlternate = SatoriShardedRegionQueue::AllocAlignedForProcessors(QueueKind::RecyclerReusable, 4);

    m_workList = SatoriShardedWorkList::AllocAlignedForProcessors(4);
    m_gcState = GC_STATE_NONE;
    m_barrierState = BARRIER_STATE_NOT_CONCURRENT;

    m_gcCount[0] = m_gcCount[1] = m_gcCount[2] = 0;
    m_compactingGcCount[0] = m_compactingGcCount[1] = m_compactingGcCount[2] = 0;

    m_condemnedGeneration = 0;
    m_compactionRequests[0] = {};
    m_compactionRequests[1] = {};
    m_trimRequest = {};
    m_blockingRequests[0] = {};
    m_blockingRequests[1] = {};
    m_gcStartSequence = 0;
    m_forceCompact = false;
    m_forceTrim = false;
    m_forceBlocking = false;

    m_estimatedEphemeralReclaim = 0;
    m_estimatedTenuredReclaim = 0;
    m_promotionEstimate = 0;

    m_occupancy[0] = m_occupancyAcc[0] = 0;
    m_occupancy[1] = m_occupancyAcc[1] = 0;
    m_occupancy[2] = m_occupancyAcc[2] = 0;
    m_demotedOccupancyAcc = 0;
    m_occupancyReportingEnabled = false;

    m_gen1CountAtLastGen2 = 0;
    m_gen1Budget = MIN_GEN1_BUDGET;
    m_prevGen1Budget = MIN_GEN1_BUDGET;
    m_gen1OverrunPerCycle = 0;
    m_availableMemory = 0;
    m_reusableTargetPercent = SatoriUtil::ReusableTarget();
    m_reusableLimit = 0;
    m_totalLimit = MIN_GEN1_BUDGET;
    m_nextGcIsFullGc = true;
    m_prevCondemnedGeneration = 2;
    m_gcNextTimeTarget = 0;

    m_activeWorkerFn = nullptr;
    m_rootScanTicket = 0;
    m_cardScanTicket = 0;
    m_concurrentCardsDone = true;
    m_concurrentCleaningState = CC_CLEAN_STATE_NOT_READY;
    m_concurrentHandlesDone = true;
    m_ccStackMarkState = CC_MARK_STATE_NONE;
    m_ccStackMarkingThreadsNum = 0;
    m_reusableFilterThreadsNum = 0;

    m_isLowLatencyMode = SatoriUtil::IsLowLatencyMode();

    for (int i = 0; i < 3; i++)
    {
        m_gcStartMillis[i] = m_gcDurationUsecs[i] = m_gcAccmulatingDurationUsecs[i] = 0;
    }

    m_blockingWorkEndTicks = 0;

    m_lastEphemeralGcInfo = { 0 };
    m_lastTenuredGcInfo   = { 0 };
    m_CurrentGcInfo = nullptr;
    m_startMillis = GetNowMillis();
}

void SatoriRecycler::ShutDown()
{
    m_activeWorkerFn = nullptr;
}

/* static */
void SatoriRecycler::WorkerThreadMainLoop(void* param)
{
    SatoriRecycler* recycler = (SatoriRecycler*)param;
    Interlocked::Increment(&recycler->m_activeWorkers);

    for (;;)
    {
        Interlocked::Decrement(&recycler->m_activeWorkers);

        for (;;)
        {
            if (recycler->m_gateSignaled &&
               Interlocked::CompareExchange(&recycler->m_gateSignaled, 0, 1) == 1)
            {
                goto released;
            }

            // spin for ~10 microseconds (GcSpin)
            // the OS timer here - only the clock ends this wait, so it must not be the inline one
            const int64_t ticksPerMicro = recycler->m_osTicksPerMicro;
            int64_t lastTick = minipal_hires_ticks();
            int64_t limit = lastTick + ticksPerMicro * SatoriUtil::GcSpin();

            // We do not know how long YieldProcessor takes, it varies a lot between hardware.
            // Start with a short batch and keep doubling it, but only while batches take less than a microsecond.
            // After that the batch stays the same, so the clock is read about once per microsecond
            // and we overshoot the limit by no more than one batch.
            int i = 1;
            int64_t now;
            do
            {
                int j = (1 << i);
                while (--j > 0)
                {
                    YieldProcessor();

                    if (recycler->m_gateSignaled &&
                        Interlocked::CompareExchange(&recycler->m_gateSignaled, 0, 1) == 1)
                    {
                        goto released;
                    }
                }

                now = minipal_hires_ticks();
                if (now - lastTick < ticksPerMicro)
                {
                    i = min(30, i + 1);
                }

                lastTick = now;
            }
            while (now < limit);

            // Wait returns true if was woken up.
            if (!recycler->m_workerGate->TimedWait(10000))
            {
                // we timed out (very rare case)
                // we did not consume the wake, but there could be no more workers sleeping.
                Interlocked::Decrement(&recycler->m_totalWorkers);
                return;
            }

            // if there is a wake, clear it as it may no longer wake anything
            Interlocked::And(&recycler->m_workerWoken, 0);
        }

    released:
        Interlocked::Increment(&recycler->m_activeWorkers);

        auto activeWorkerFn = recycler->m_activeWorkerFn;
        if (activeWorkerFn)
        {
            (recycler->*activeWorkerFn)();
        }
    }
}

// not interlocked. this is not done concurrently. 
void SatoriRecycler::IncrementRootScanTicket()
{
    m_rootScanTicket++;
    // make sure the ticket is not 0
    if (!m_rootScanTicket)
    {
        m_rootScanTicket++;
    }
}

void SatoriRecycler::IncrementCardScanTicket()
{
    m_cardScanTicket++;
    // make sure the ticket is not 0
    if (!m_cardScanTicket)
    {
        m_cardScanTicket++;
    }
}

int SatoriRecycler::GetRootScanTicket()
{
    return m_rootScanTicket;
}

uint8_t SatoriRecycler::GetCardScanTicket()
{
    return m_cardScanTicket;
}

int64_t SatoriRecycler::GetCollectionCount(int gen)
{
    return m_gcCount[gen];
}

int SatoriRecycler::GetCondemnedGeneration()
{
    return m_condemnedGeneration;
}

size_t SatoriRecycler::Gen1RegionCount()
{
    return m_ephemeralFinalizationTrackingRegions->Count() +
        m_ephemeralRegions->Count() +
        m_ephemeralWithUnmarkedDemoted->Count();
}

size_t SatoriRecycler::Gen2RegionCount()
{
    return m_tenuredFinalizationTrackingRegions->Count() + m_tenuredRegions->Count();
}

size_t SatoriRecycler::RegionCount()
{
    return Gen1RegionCount() + Gen2RegionCount();
}

// Takes a reusable region that can serve an allocation of minFree bytes. Only the first few regions
// in each shard are considered, the rest stays where it is.
SatoriRegion* SatoriRecycler::TryGetReusable(size_t minFree)
{
    const size_t MAX_SCAN = 2;
    SatoriRegion* reusable = m_reusableRegions->TryPopFitting(minFree, MAX_SCAN);
    if (reusable == nullptr)
    {
        // concurrent filtering parks already filtered regions here until prep.
        // they are usable as-is, no reason to make the app wait for prep to hand them back.
        reusable = m_reusableRegionsAlternate->TryPopFitting(minFree, MAX_SCAN);
    }

    _ASSERTE(reusable == nullptr || reusable->Occupancy() < Satori::REGION_SIZE_GRANULARITY);

    return reusable;
}

SatoriRegion* SatoriRecycler::TryGetReusableForLarge()
{
    SatoriRegion* reusable = m_reusableRegions->TryDequeueIfHasFreeSpaceInTopBucket();
    if (reusable == nullptr)
    {
        reusable = m_reusableRegionsAlternate->TryDequeueIfHasFreeSpaceInTopBucket();
    }

    _ASSERTE(reusable == nullptr || reusable->Occupancy() < Satori::REGION_SIZE_GRANULARITY);

    return  reusable;
}

// NOTE: DrainReusableQueue implements the same routing as this, just batched.
//       Changes here likely need to be mirrored there.
void SatoriRecycler::PushToEphemeralQueues(SatoriRegion* region)
{
    if (region->HasUnmarkedDemotedObjects())
    {
        m_ephemeralWithUnmarkedDemoted->Push(region);
    }
    else
    {
        // we do not know, so conservatively assume that the next GC may promote
        size_t estimatedReclaim = region->ReclaimSizeIfRelocated(m_nextGcIsFullGc);
        if (estimatedReclaim  > 0)
        {
            Interlocked::ExchangeAdd64(&m_estimatedEphemeralReclaim, estimatedReclaim);
        }

        if (region->IsPromotionCandidate())
        {
            Interlocked::Increment(&m_promotionEstimate);
        }

        if (region->HasFinalizables())
        {
            m_ephemeralFinalizationTrackingRegions->Push(region);
        }
        else
        {
            m_ephemeralRegions->Push(region);
        }
    }
}

void SatoriRecycler::PushToTenuredQueues(SatoriRegion* region)
{
    // we do not know, so conservatively assume that the next GC may promote
    size_t estimatedReclaim = region->ReclaimSizeIfRelocated(m_nextGcIsFullGc);
    if (estimatedReclaim  > 0)
    {
        Interlocked::ExchangeAdd64(&m_estimatedTenuredReclaim, estimatedReclaim);
    }

    if (region->HasFinalizables())
    {
        m_tenuredFinalizationTrackingRegions->Push(region);
    }
    else
    {
        m_tenuredRegions->Push(region);
    }
}

// NOTE: recycler owns nursery regions only temporarily for the duration of GC when mutators are stopped.
//       we do not keep them because an active nursery region may change its finalizability classification
//       concurrently by allocating a finalizable object.
void SatoriRecycler::AddEphemeralRegion(SatoriRegion* region)
{
    _ASSERTE(region->GetAllocStart() == 0);
    _ASSERTE(region->GetAllocRemaining() == 0);
    _ASSERTE(region->Generation() == 0 || region->Generation() == 1);
    _ASSERTE(!region->HasMarksSet());
    _ASSERTE(!region->DoNotSweep());
    _ASSERTE(!region->IsAttachedToAllocatingOwner() || IsBlockingPhase());

    // Charge the gen1 budget for the usable capacity that the region took out of circulation.
    // The budget bounds the footprint, so allocated or surviving bytes are not the right measure.
    // Thread-local GC may reclaim most of a region, but leave it too fragmented to reuse, and the region
    // still takes all of its space until the next GC.
    //  - When the allocating owner retires the region, nothing can allocate from it until a GC sweeps it,
    //    so it is charged for all the usable capacity it had when attached, used or not.
    //  - When a GC is starting, the GC will reclaim what is left, so only what was used is charged.
    // Fragments too small to be usable are never charged, they were not usable to begin with.
    size_t charge = region->TakeBudgetCharge(/* gcIsStarting */ IsBlockingPhase());
    PushToEphemeralQueues(region);
    Interlocked::ExchangeAdd64(&m_gen1AddedSinceLastCollection, charge);
    if (region->IsEscapeTracking())
    {
        _ASSERTE(IsBlockingPhase());
        _ASSERTE(!region->HasPinnedObjects());
        if (region->IsDemoted())
        {
            // A demoted region reused for gen0. Its demoted objects will be marked as roots
            // and the region may get pinned in this GC, which escape tracking does not allow.
            // Stop tracking, the region continues as a regular gen1 region.
            region->StopEscapeTracking();
        }
        else
        {
            region->ClearMarks();
        }
    }
    else
    {
        region->ClearStaleEscapeMarks();
    }

    // When concurrent marking is allowed we may have marks already.
    // Demoted regions could be pre-marked
#if _DEBUG
    if (!region->MaybeAllocatingAcquire())
    {
        region->Verify(/* allowMarked */ region->IsDemoted() || SatoriUtil::IsConcurrentEnabled());
    }
#endif
}

void SatoriRecycler::AddTenuredRegion(SatoriRegion* region)
{
    _ASSERTE(region->GetAllocStart() == 0);
    _ASSERTE(region->GetAllocRemaining() == 0);
    _ASSERTE(!region->IsEscapeTracking());
    _ASSERTE(!region->HasMarksSet());
    _ASSERTE(!region->DoNotSweep());

    region->Verify(/* allowMarked */ SatoriUtil::IsConcurrentEnabled() && SatoriUtil::IsConservativeMode());
    PushToTenuredQueues(region);
    _ASSERTE(region->Generation() == 2);
    // only huge regions come here. Nothing else can use the space after the object, so charge the whole region.
    Interlocked::ExchangeAdd64(&m_gen2AddedSinceLastCollection, region->Size());
    region->RearmCardsForTenured();
}

size_t SatoriRecycler::GetNowMillis()
{
    int64_t t = minipal_hires_ticks();
    return (size_t)(t / m_osTicksPerMilli);
}

size_t SatoriRecycler::GetNowUsecs()
{
    int64_t t = minipal_hires_ticks();
    return (size_t)(t / m_osTicksPerMicro);
}

size_t SatoriRecycler::IncrementGen0Count()
{
    // duration of Gen0 is typically << msec, so we will not record that.
    m_gcStartMillis[0] = GetNowMillis();
    return Interlocked::Increment((size_t*)&m_gcCount[0]);
}

// if generation is too small we do not bother with concurrent marking.
// the extra costs of concurrent marking may not justify using it with tiny heaps
// unless we run in low-latency mode, then we always prefer concurrent.
#if _DEBUG
static const int concMarkThreshold = 0;
#else
// Empirical cutoff: on small heaps, concurrent-marking overhead may outweigh its benefit.
// Last-reported occupancy is only a proxy for collection cost, not a pause-time bound.
static const int concMarkThreshold = 1024  * 1024 * 1;
#endif

bool SatoriRecycler::ShouldDoConcurrent(int generation)
{
    if (IsLowLatencyMode())
    {
        return true;
    }

    if (this->GetTotalOccupancy() < concMarkThreshold)
    {
        return false;
    }

    return true;
}

void SatoriRecycler::TryStartGC(int generation, gc_reason reason)
{
    if (m_gcState != GC_STATE_NONE)
    {
        return;
    }

    size_t blockingGen1Requests = VolatileLoad(&m_blockingRequests[0].m_requested);
    size_t blockingGen2Requests = VolatileLoad(&m_blockingRequests[1].m_requested);
    size_t gen1Requests = VolatileLoad(&m_compactionRequests[0].m_requested);
    size_t gen2Requests = VolatileLoad(&m_compactionRequests[1].m_requested);
    size_t trimRequests = VolatileLoad(&m_trimRequest.m_requested);

    bool blockingGen1 = blockingGen1Requests > VolatileLoad(&m_blockingRequests[0].m_completed);
    bool blockingGen2 = blockingGen2Requests > VolatileLoad(&m_blockingRequests[1].m_completed);
    if (m_nextGcIsFullGc || blockingGen2 ||
        gen2Requests > VolatileLoad(&m_compactionRequests[1].m_completed) ||
        trimRequests > VolatileLoad(&m_trimRequest.m_completed) ||
        reason == gc_reason::reason_induced_aggressive)
    {
        generation = 2;
    }

    bool forceBlocking = blockingGen1 || blockingGen2;
    int newState = !forceBlocking && SatoriUtil::IsConcurrentEnabled() && ShouldDoConcurrent(generation) ?
        GC_STATE_CONCURRENT :
        GC_STATE_BLOCKING;

    if (Interlocked::CompareExchange(&m_gcState, newState, GC_STATE_NONE) == GC_STATE_NONE)
    {
        VolatileStore(&m_gcStartSequence, m_gcStartSequence + 1);

        CollectionRequest& gen1Request = m_compactionRequests[0];
        CollectionRequest& gen2Request = m_compactionRequests[1];
        // Another GC may have completed newer requests between the snapshot and our start claim.
        gen1Request.m_started = max(gen1Requests, gen1Request.m_completed);
        gen2Request.m_started = max(gen2Requests, gen2Request.m_completed);
        m_trimRequest.m_started = max(trimRequests, m_trimRequest.m_completed);
        m_blockingRequests[0].m_started = max(blockingGen1Requests, m_blockingRequests[0].m_completed);
        m_blockingRequests[1].m_started = max(blockingGen2Requests, m_blockingRequests[1].m_completed);

        bool compactGen1 = gen1Request.m_started != gen1Request.m_completed;
        bool compactGen2 = gen2Request.m_started != gen2Request.m_completed;
        blockingGen2 = m_blockingRequests[1].m_started != m_blockingRequests[1].m_completed;
        m_forceBlocking = m_blockingRequests[0].m_started != m_blockingRequests[0].m_completed ||
                          blockingGen2;
        // A racing GC can finish the requests before our claim, leaving newState blocking without m_forceBlocking.
        // Keeping that decision only costs an unnecessary blocking collection.
        m_forceTrim = reason == gc_reason::reason_induced_aggressive ||
                      m_trimRequest.m_started != m_trimRequest.m_completed;
        m_forceCompact = reason == gc_reason::reason_induced_compacting ||
                         compactGen1 || compactGen2 || m_forceTrim;

        m_CurrentGcInfo = generation == 2 ? &m_lastTenuredGcInfo :&m_lastEphemeralGcInfo;
        m_CurrentGcInfo->m_condemnedGeneration = (uint8_t)generation;
        m_CurrentGcInfo->m_concurrent = m_gcState == GC_STATE_CONCURRENT;
        m_CurrentGcInfo->m_pauseDurations[1] = 0;

        if (m_forceTrim)
        {
            reason = gc_reason::reason_induced_aggressive;
        }
        else if (m_forceCompact)
        {
            reason = gc_reason::reason_induced_compacting;
        }
        else if (m_forceBlocking)
        {
            reason = gc_reason::reason_induced;
        }
        FIRE_EVENT(GCTriggered, (uint32_t)reason);

        m_trimmer->SetStopSuggested();

        // Here we have a short window when other threads may notice that gc is in progress,
        // but will find no helping opportunities until we set m_condemnedGeneration.
        // that is ok.

        // in concurrent case there is an extra pass over stacks and handles
        // which happens before the blocking stage
        if (newState == GC_STATE_CONCURRENT)
        {
            m_ccStackMarkState = CC_MARK_STATE_NONE;
            IncrementRootScanTicket();
            m_concurrentCardsDone = (generation == 2);
            m_concurrentCleaningState = CC_CLEAN_STATE_NOT_READY;
            m_concurrentHandlesDone = false;
            SatoriHandlePartitioner::StartNextScan();
            m_activeWorkerFn = &SatoriRecycler::ConcurrentWorkerFn;
        }
        if (generation != 2)
        {
            IncrementCardScanTicket();
        }

        // publishing condemned generation will enable helping.
        // it should happen after the writes above.
        // just to make sure it is all published when the barrier is updated, which is fully synchronizing.
        VolatileStore(&m_condemnedGeneration, generation);

        // if we are doing blocking GC, just go and start it
        if (newState == GC_STATE_BLOCKING)
        {
            BlockingCollect();
        }
    }
}

bool IsWorkerThread()
{
    return GCToEEInterface::WasCurrentThreadCreatedByGC();
}

int64_t SatoriRecycler::HelpQuantumTimeStampTicks()
{
        return m_timeStampTicksPerMilli /
            (IsWorkerThread() ?
                8:  // 125 usec
                64); // 15 usec
}

int64_t SatoriRecycler::HelpQuantumOsTicks()
{
        return m_osTicksPerMilli /
            (IsWorkerThread() ?
                8:  // 125 usec
                64); // 15 usec
}

bool SatoriRecycler::HelpOnceCoreInner(bool minQuantum)
{

    if (m_ccStackMarkState == CC_MARK_STATE_MARKING)
    {
        _ASSERTE(IsBarrierConcurrent());
        // help with marking stacks and f-queue, this is urgent since EE is stopped for this.
        BlockingMarkForConcurrentImpl();
    }

    int64_t timeStamp = SatoriUtil::GetTimeStamp();
    int64_t deadline = timeStamp + (minQuantum ? 0: HelpQuantumTimeStampTicks());

    // this should be done before scanning stacks or cards
    // since the regions must be swept before we can use FindObject
    if (m_ccStackMarkState == CC_MARK_STATE_NONE)
    {
        if (DrainDeferredSweepQueueConcurrent(deadline))
        {
            return true;
        }
    }

    // make sure the barrier is toggled to concurrent before marking
    int origBarrierState = m_barrierState;
    if (origBarrierState != BARRIER_STATE_CONCURRENT)
    {
        // Toggling is a ProcessWide fence, which is expensive, and on some platforms
        // is serialized with other process-wide fences. There is no point in every helper
        // doing it, so claim the right to toggle and let just one thread do the work.
        // NB: the barrier could be in either of the two non-concurrent states and
        //     switching to concurrent stops skipping cards in any case, so we claim
        //     the switch from whatever non-concurrent state we have observed.
        if (origBarrierState != BARRIER_STATE_SWITCHING &&
            Interlocked::CompareExchange(&m_barrierState, BARRIER_STATE_SWITCHING, origBarrierState) == origBarrierState)
        {
            ToggleWriteBarrier(BARRIER_STATE_CONCURRENT, /* eeSuspended */ false);
            // the toggle above is a fence, so the switch is published to everyone by now.
            VolatileStore((int*)&m_barrierState, BARRIER_STATE_CONCURRENT);
        }
        else if (m_barrierState != BARRIER_STATE_CONCURRENT)
        {
            // Another thread is switching the barrier and we may not mark until that is done.
            // Rather than waiting for it, leave and come back later - there is more work to do.
            return true;
        }

        // otherwise the barrier became concurrent while we were checking - just continue.
    }

    // Ahead of the drain: this is the bulk of the roots in low latency mode, and getting them
    // in early is worth ~2x on the prep pause. Whatever is left when prep starts costs
    // EE-stopped time, and prep has to wait for us to get out of here as well.
    if (MarkDemotedInReusableConcurrent(deadline))
    {
        return true;
    }

    if (m_ccStackMarkState != CC_MARK_STATE_DONE)
    {
        MarkOwnStackOrDrainQueuesConcurrent(deadline);
    }

    if (AppThreadsShouldSuspend() && !IsWorkerThread())
    {
        // this is a mutator thread and we are suspending them, leave and suspend.
        return true;
    }

    if (MarkDemotedAndDrainQueuesConcurrent(deadline))
    {
        return true;
    }

    // Handles are claimed per partition and cards per group - neither has an inner deadline,
    // so an app thread may overshoot its quantum. Only low latency cares: otherwise a
    // compacting blocking GC dwarfs anything an app thread might overshoot here.
    bool canTakeBigChunks = IsWorkerThread() || m_activeWorkers == 0 || !IsLowLatencyMode();

    if (canTakeBigChunks)
    {
        if (!m_concurrentHandlesDone)
        {
            if (MarkHandles(deadline))
            {
                return true;
            }
            else
            {
                m_concurrentHandlesDone = true;
            }
        }

        if (!m_concurrentCardsDone)
        {
            _ASSERTE(m_condemnedGeneration != 2);
            if (MarkThroughCardsConcurrent(deadline))
            {
                return true;
            }
            else
            {
                m_concurrentCardsDone = true;
            }
        }
    }

    // if stacks are not marked yet, start suspending EE
    if (m_ccStackMarkState == CC_MARK_STATE_NONE)
    {
        // drain what the handle and card scans have queued first, so that their closure
        // is marked while the EE still runs. If that does not finish, come back later.
        if (DrainMarkQueuesConcurrent(nullptr, deadline))
        {
            return true;
        }

        // we may have skipped handles or cards above - prep must not start before those are done
        if (!m_concurrentHandlesDone || !m_concurrentCardsDone)
        {
            return true;
        }

        // only one thread will win and drive this stage, others may help.
        BlockingMarkForConcurrent();
    }

    if (m_ccStackMarkState == CC_MARK_STATE_MARKING)
    {
        _ASSERTE(IsWorkerThread());
        // do not leave (and start blocking gc), come back and help
        return true;
    }

    // cleaning is per card group as well, but nothing below depends on it
    if (VolatileLoad(&m_concurrentCleaningState) == CC_CLEAN_STATE_CLEANING)
    {
        if (!canTakeBigChunks)
        {
            // the cleaning still has to happen, we just may not be the one doing it
            DrainMarkQueuesConcurrent(nullptr, deadline);
            return true;
        }

        if (CleanCardsConcurrent(deadline))
        {
            return true;
        }
        else
        {
            m_concurrentCleaningState = CC_CLEAN_STATE_DONE;
        }
    }

    // if queues are empty we see no more work.
    // whatever we skipped above is still work, even if not ours to do.
    return DrainMarkQueuesConcurrent(nullptr, deadline) ||
        !m_concurrentHandlesDone ||
        !m_concurrentCardsDone;
}

bool SatoriRecycler::HelpOnceCore(bool minQuantum)
{
    if (m_condemnedGeneration == 0)
    {
        // GC has not started yet, come again later.
        return true;
    }

    // paired with m_noWorkSince, which gates completion below - the OS timer
    // so that a stalled inline counter cannot suppress it
    int64_t start = minipal_hires_ticks();

    bool moreWork = !m_concurrentCardsDone ||
        !m_concurrentHandlesDone ||
        m_ccStackMarkState != CC_MARK_STATE_DONE ||
        m_concurrentCleaningState == CC_CLEAN_STATE_CLEANING ||
        !m_workList->IsEmpty();

    if (moreWork && m_concurrentCleaningState != CC_CLEAN_STATE_WAIT_FOR_HELPERS)
    {
        Interlocked::Increment(&m_ccHelpersNum);
        if (m_concurrentCleaningState != CC_CLEAN_STATE_WAIT_FOR_HELPERS)
        {
            moreWork = HelpOnceCoreInner(minQuantum);
        }
        Interlocked::Decrement(&m_ccHelpersNum);
    }

    if (!moreWork)
    {
        if (m_ccStackMarkState != CC_MARK_STATE_DONE)
        {
            // Stacks are about to be marked, or are being marked. We will be needed there, so stay around.
            // Do not tell new helpers to not enter - that would keep them out of marking stacks, and cleaning
            // could not start before stacks are done anyway, since the marking thread counts as a helper.
            moreWork = true;
        }
        else if (m_concurrentCleaningState == CC_CLEAN_STATE_NOT_READY)
        {
            // did not try to clean yet, tell new helpers to not enter.
            // there will be more work soon.
            moreWork = true;
            Interlocked::CompareExchange(&m_concurrentCleaningState, CC_CLEAN_STATE_WAIT_FOR_HELPERS, CC_CLEAN_STATE_NOT_READY);
        }
    }

    // we need the helpers to be out before cleaning as we need to switch scan ticket
    // and we need to read the state before reading the helper count
    if (VolatileLoad(&m_concurrentCleaningState) == CC_CLEAN_STATE_WAIT_FOR_HELPERS)
    {
        // we can't help at this time, but there is more work.
        moreWork = true;

        // if we see no workers after seeing the wait state, the worker count is effectively 0
        // as new worker will check the state after incrementing m_ccHelpersNum and not start helping if sees the state.
        int ccHelpersNum = m_ccHelpersNum;

        // last helper initiates cleaning
        if (ccHelpersNum == 0 &&
            Interlocked::CompareExchange(&m_concurrentCleaningState, CC_CLEAN_STATE_SETTING_UP, CC_CLEAN_STATE_WAIT_FOR_HELPERS) == CC_CLEAN_STATE_WAIT_FOR_HELPERS)
        {
            IncrementCardScanTicket();
            // Publish the ticket before a helper can start cleaning.
            VolatileStore((int*)&m_concurrentCleaningState, (int)CC_CLEAN_STATE_CLEANING);
        }
    }

    // If we are done cleaning and see no work, start blocking collection.
    // Never with minQuantum - that is the thread that suspended EE for marking stacks and is waiting for the other
    // markers. Cleaning cannot be done while that thread counts as a helper anyway, this just makes sure.
    if (!minQuantum &&
        m_concurrentCleaningState == CC_CLEAN_STATE_DONE &&
        m_workList->IsEmpty())
    {
        // was it long enough since last time we saw work?
        // Helpers that are still inside could be in the middle of something that produces more work, like a card group,
        // so then we wait longer. How long does not depend on which thread checks.
        int64_t quietTicks = m_ccHelpersNum > 0 ?
            m_osTicksPerMilli / 2 :     // 500 usec, 4 worker quanta
            m_osTicksPerMilli / 16;     // 62.5 usec, 4 app thread quanta
        if (start - m_noWorkSince > quietTicks)
        {
            // no work for a while, seems like we are done
            // we may have some helpers draining long chains and not sharing anything
            // in such degenerate case we still may want to wrap it up and and block.
            if (Interlocked::CompareExchange(&m_gcState, GC_STATE_BLOCKING, GC_STATE_CONCURRENT) == GC_STATE_CONCURRENT)
            {
                BlockingCollect();
            }
        }
    }

    return moreWork || !m_workList->IsEmpty();
}

bool SatoriRecycler::HelpOnce(bool waitForGC)
{
    _ASSERTE(!IsWorkerThread());

    bool moreWork = false;
    int gcState = m_gcState;
    if (gcState != GC_STATE_NONE)
    {
        if (gcState == GC_STATE_CONCURRENT &&
            (waitForGC || SatoriUtil::IsPacingEnabled() || m_activeWorkers == 0) &&
            m_condemnedGeneration != 0)
        {
            int64_t start = minipal_hires_ticks();

            moreWork = HelpOnceCore(/*minQuantum*/ false);
            if (moreWork)
            {
                m_noWorkSince = minipal_hires_ticks();
            }
            else
            {
                // TODO: VS is this possible? should we do something like block?
                //if (!moreWork && start - m_noWorkSince > m_osTicksPerMilli)
                //{
                //    printf("PANIC\n");
                //}
            }

            // Check if we need to pace. Even if we see more work, it could be not a timeout.
            // Only makes sense if we can use extra workers.
            if (!waitForGC && IsLowLatencyMode() && MaxWorkers() > 0 && m_gcState == GC_STATE_CONCURRENT)
            {
                // if we did not use all the quantum in Low Latency mode,
                // consume what roughly remains for pacing reasons.
                int64_t deadline = start + HelpQuantumOsTicks() / 2;
                int iters = 1;
                while (minipal_hires_ticks() < deadline &&
                    !AppThreadsShouldSuspend())
                {
                    iters *= 2;
                    for (int i = 0; i < iters; i++)
                    {
                        YieldProcessor();
                    }
                }
            }
        }

    }
    else if (!m_deferredSweepRegions->IsEmpty())
    {
        int64_t timeStamp = SatoriUtil::GetTimeStamp();
        int64_t deadline = timeStamp + HelpQuantumTimeStampTicks();
        moreWork = DrainDeferredSweepQueueConcurrent(deadline);
    }

    if (gcState != GC_STATE_NONE || waitForGC)
    {
        GCToEEInterface::GcPoll();
    }

    return moreWork;
}

void SatoriRecycler::ConcurrentWorkerFn()
{
    // workers have deadline too, just to come here and check the stage.
    while (m_gcState == GC_STATE_CONCURRENT)
    {
        if (HelpOnceCore(/*minQuantum*/ false))
        {
            m_noWorkSince = minipal_hires_ticks();
        }
        else if (m_concurrentCleaningState == CC_CLEAN_STATE_NOT_READY)
        {
            // we saw no work available and may not be coming soon, so will exit.
            // if more work is generated we will be invited back.
            break;
        }

        // past CC_CLEAN_STATE_NOT_READY stage we spin while GC_STATE_CONCURRENT - to check
        // for more work or for completion condition.
        MaybeAskForHelp();
        break;
    }
}

class MarkContext
{
    friend class SatoriRecycler;

public:
    MarkContext(SatoriRecycler* recycler)
        : m_WorkChunk()
    {
        m_recycler = recycler;
        m_condemnedGeneration = recycler->m_condemnedGeneration;
        m_heap = recycler->m_heap;
    }

    void PushToMarkQueues(SatoriObject* o)
    {
        if (m_WorkChunk && m_WorkChunk->TryPush(o))
        {
            return;
        }

        m_recycler->PushToMarkQueuesSlow(m_WorkChunk, o);
    }

private:
    int m_condemnedGeneration;
    SatoriWorkChunk* m_WorkChunk;
    SatoriHeap* m_heap;
    SatoriRecycler* m_recycler;
};

void SatoriRecycler::BlockingMarkForConcurrentImpl()
{
    Interlocked::Increment(&m_ccStackMarkingThreadsNum);
    // check state again it could have changed if there were no marking threads
    if (m_ccStackMarkState == CC_MARK_STATE_MARKING)
    {
        MarkAllStacksFinalizationAndDemotedRoots();
    }

    Interlocked::Decrement(&m_ccStackMarkingThreadsNum);
}

/* static */
void SatoriRecycler::ConcurrentPhasePrepFn(gc_alloc_context* gcContext, void* param)
{
    SatoriAllocationContext* context = (SatoriAllocationContext*)gcContext;
    MarkContext* markContext = (MarkContext*)param;
    SatoriRecycler* recycler = markContext->m_recycler;

    SatoriRegion* region = context->RegularRegion();
    if (region)
    {
        region->StopEscapeTracking();

        if (region->HasUnmarkedDemotedObjects())
        {
            recycler->MarkDemoted(region, markContext);
        }
    }

    region = context->LargeRegion();
    if (region && region->HasUnmarkedDemotedObjects())
    {
        recycler->MarkDemoted(region, markContext);
    }
}

void SatoriRecycler::BlockingMarkForConcurrent()
{
    int targetState = CC_MARK_STATE_SUSPENDING_EE;
    if (Interlocked::CompareExchange(&m_ccStackMarkState, CC_MARK_STATE_SUSPENDING_EE, CC_MARK_STATE_NONE) == CC_MARK_STATE_NONE)
    {
        // The state is published by the CAS above, so filtering that starts from now on will
        // see it and do nothing. Wait for the ones already inside to leave before touching the
        // queues. This is before suspending, so the wait is not a part of the pause.
        while (m_reusableFilterThreadsNum)
        {
            YieldProcessor();
        }

        size_t blockingStart = minipal_hires_ticks();
        GCToEEInterface::SuspendEE(SUSPEND_FOR_GC_PREP);

        // the concurrent pass has already filtered what is in the alternate, make it usable again.
        // whatever is left in m_reusableRegions it did not reach - the loop below will filter that.
        SatoriShardedRegionQueue* leftovers = m_reusableRegions;
        m_reusableRegions = m_reusableRegionsAlternate;
        m_reusableRegionsAlternate = leftovers;

        // signal to everybody to start marking roots
        VolatileStore((int*)&m_ccStackMarkState, CC_MARK_STATE_MARKING);
        MaybeAskForHelp();

        // mark demoted regions if any attached to thread contexts
        MarkContext c(this);
        GCToEEInterface::GcEnumAllocContexts(ConcurrentPhasePrepFn, &c);
        if (c.m_WorkChunk != nullptr)
        {
            m_workList->Push(c.m_WorkChunk);
        }

        // now join everybody else and mark some roots
        MarkAllStacksFinalizationAndDemotedRoots();

        // done, wait for marking to finish and restart EE
        Interlocked::Exchange(&m_ccStackMarkState, CC_MARK_STATE_DONE);
        while (m_ccStackMarkingThreadsNum)
        {
            // All stacks are claimed, so the other markers should be done soon - poll aggressively.
            // Help with a chunk of work to not spin uselessly, but only if there is some. Entering as a helper
            // is interlocked traffic, which could delay the markers that we are waiting for.
            // NB: the head of the work list is volatile, thus the check is not hoisted out of the loop.
            if (m_workList->IsEmpty() || !HelpOnceCore(/*minQuantum*/ true))
            {
                YieldProcessor();
            }
        }

        _ASSERTE(m_reusableRegionsAlternate->IsEmpty());

        size_t blockingDuration = (minipal_hires_ticks() - blockingStart);
        m_CurrentGcInfo->m_pauseDurations[1] = blockingDuration / m_osTicksPerMicro;
        m_gcAccmulatingDurationUsecs[m_condemnedGeneration] += blockingDuration / m_osTicksPerMicro;
        UpdateGcCounters(blockingStart);

        GCToEEInterface::RestartEE(false);
    }
}

int SatoriRecycler::MaxWorkers()
{
    int workerCount = SatoriUtil::MaxWorkersCount();
    if (workerCount < 0)
    {
        int cpuCount = GCToEEInterface::GetCurrentProcessCpuCount();

        // TUNING: should this be more dynamic? check CPU load and such.
        workerCount = cpuCount - 1;
        if (!IsBlockingPhase() && IsLowLatencyMode())
        {
            workerCount = (cpuCount + 1) / 2;
        }
    }

    return workerCount;
}

void SatoriRecycler::MaybeAskForHelp()
{
    if (m_activeWorkerFn && m_activeWorkers < MaxWorkers())
    {
        AskForHelp();
    }
}

void SatoriRecycler::AskForHelp()
{
    if (!m_gateSignaled && Interlocked::CompareExchange(&m_gateSignaled, 1, 0) == 0)
    {
        // we signaled the gate, but need to make sure a worker will see it, even if all workers have gone to sleep.
        // if there is no pending wake, lets wake one thread.
        if (!m_workerWoken && Interlocked::CompareExchange(&m_workerWoken, 1, 0) == 0)
        {
            // wake can fold with other wakes, but the presence of wake ensures that something will wake up and check for work.
            // except if a worker times out and exits, which is very rare, then we may have noone to wake.
            m_workerGate->WakeOne();
        }
    }

    // if all workers are busy, make another one, up to a limit.
    // if workers are spining/sleeping, we will not add more workers.
    int totalWorkers = m_totalWorkers;
    if (m_activeWorkers >= totalWorkers &&
        totalWorkers < MaxWorkers() &&
        Interlocked::CompareExchange(&m_totalWorkers, totalWorkers + 1, totalWorkers) == totalWorkers)
    {
        if (!GCToEEInterface::CreateThread(WorkerThreadMainLoop, this, false, "Satori GC Worker Thread"))
        {
            Interlocked::Decrement(&m_totalWorkers);
            return;
        }
    }
}

void SatoriRecycler::RequestCollection(CollectionRequest* request, size_t requestNumber)
{
    size_t requested = VolatileLoad(&request->m_requested);
    while (requested < requestNumber)
    {
        size_t previous = Interlocked::CompareExchange(&request->m_requested, requestNumber, requested);
        if (previous == requested)
        {
            return;
        }

        requested = previous;
    }
}

void SatoriRecycler::Collect(int generation, bool force, bool blocking, bool compact, bool aggressive)
{
    _ASSERTE(generation == 1 || generation == 2);

    size_t requestNumber = VolatileLoad(&m_gcStartSequence) + 1;

    // only blocked & forced GC is strictly observable - via finalization.
    // otherwise it is kind of a suggestion to maybe start collecting

    // same as in the stock GC, the request to compact or to trim is carried by the reason
    gc_reason reason = aggressive ?
        gc_reason::reason_induced_aggressive :
        compact ?
            gc_reason::reason_induced_compacting :
            (force ? gc_reason::reason_induced : gc_reason::reason_induced_noforce);

    if (!force)
    {
        // just check if it is time
        MaybeTriggerGC(reason);
        return;
    }

    CollectionRequest* blockingRequest = blocking ? &m_blockingRequests[generation - 1] : nullptr;
    CollectionRequest* request = aggressive ? &m_trimRequest :
        compact ? &m_compactionRequests[generation - 1] : blockingRequest;
    if (request != nullptr)
    {
        RequestCollection(request, requestNumber);
    }
    if (blockingRequest != nullptr && blockingRequest != request)
    {
        RequestCollection(blockingRequest, requestNumber);
    }

    auto requestsCompleted = [&]()
    {
        return request != nullptr && VolatileLoad(&request->m_completed) >= requestNumber &&
            (blockingRequest == nullptr || VolatileLoad(&blockingRequest->m_completed) >= requestNumber);
    };

    uint32_t idlePasses = 0;
    do
    {
        if (requestsCompleted())
        {
            break;
        }

        TryStartGC(generation, reason);
        if (!HelpOnce(/* waitForGC */ blocking) && blocking)
        {
            YieldProcessor();
            if ((++idlePasses & 127) == 0)
            {
                GCToOSInterface::YieldThread(0);
            }
        }
        else
        {
            idlePasses = 0;
        }
    } while (blocking && !requestsCompleted());
}

bool SatoriRecycler::IsBlockingPhase()
{
    return m_gcState == GC_STATE_BLOCKED;
}

void SatoriRecycler::WaitForTrimmerToStop()
{
    _ASSERTE(IsBlockingPhase());
    m_trimmer->WaitForStop();
}

//TUNING: We use a very simplistic approach for GC triggering here.
//        By default: 
//          SatoriUtil::Gen2Target() triggers Gen2 GC when heap size doubles.
//          SatoriUtil::Gen1Target() triggers Gen1 GC when ephemeral size quadruples.
// 
//        There could be a lot of room to improve in this area:
//        - could consider current CPU/memory load and adjust accordingly
//        - could collect and use past history of the program behavior
//        - could consider user input as to favor latency or throughput
//        - ??

void SatoriRecycler::MaybeTriggerGC(gc_reason reason)
{
    int generation = 0;

    if (GetNowMillis() < m_gcNextTimeTarget)
    {
        // too early
        return;
    }

    if (m_gen1AddedSinceLastCollection > m_gen1Budget)
    {
        generation = 1;
    }

    // gen2 allocations are rare and do not count towards gen1 budget since gen1 will not help with that
    // they can be big though, so check if by any chance gen2 allocs alone pushed us over the limit
    if (m_gen2AddedSinceLastCollection * SatoriUtil::Gen2Target() / 100 > m_totalLimit)
    {
        generation = 2;
    }

    if (generation != 0)
    {
        TryStartGC(generation, reason);
    }

    HelpOnce();
}

size_t GetAvailableMemory()
{
    bool isRestricted;
    uint64_t total = GCToOSInterface::GetPhysicalMemoryLimit(&isRestricted);
    uint64_t available;
#ifdef TARGET_WINDOWS
    uint64_t availableCommit;
    GCToOSInterface::GetMemoryStatus(0, nullptr, &available, &availableCommit);
#else
    GCToOSInterface::GetMemoryStatus(0, nullptr, &available, nullptr);
#endif

    if (isRestricted)
    {
        uint64_t restrictedAvailable;
        GCToOSInterface::GetMemoryStatus(total, nullptr, &restrictedAvailable, nullptr);
        available = min(available, restrictedAvailable);
    }

    // Keep at least 5% free, or more when the configured threshold is lower.
    uint64_t reserve = total * max(5u, 100u - SatoriUtil::HighMemoryPercent()) / 100;
    available = available > reserve ? available - reserve : 0;

#ifdef TARGET_WINDOWS
    // Windows reports commit headroom; Unix reports free swap, which is not a commit limit.
    available = min(available, availableCommit);
#endif

    return (size_t)available;
}

void SatoriRecycler::AdjustHeuristics()
{
    // All sweeping should be done by now and occupancies should reflect live set as of last GC.
    // The counters below track how much of the heap footprint was consumed since the last GC. They are not occupancy,
    // the objects allocated since may not be alive and the counts include space that is not occupied by anything.
    //  - m_gen1AddedSinceLastCollection is the usable capacity that regions passed to the recycler took out of
    //    circulation, including free space left in retired regions and fragments too small to reuse.
    //    (see AddEphemeralRegion)
    //  - m_gen2AddedSinceLastCollection is the size of huge regions allocated directly into gen2. It is not included
    //    in m_occupancy[2] and may accumulate across several collections, name is a bit misleading.
    // The purpose of these counters is to trigger GC. Budgets are computed from occupancy, since the cost of a GC
    // is mostly proportional to what is live, and spent in footprint, since the footprint is what they bound.
    // (thus occupancies and the counters do not add up to the occupancy of tenured and ephemeral lists)
    //
    // Regions attached to allocating owners are not counted until they are passed to the recycler. We do not detach
    // them until we are sure we will promote en-masse, but a GC deactivates them and charges what they used so far.
    // (thus the counts may lag by up to a region per attached region - a thread may have a regular and a large one,
    //  which seems ok)

    size_t ephemeralOccupancy = m_occupancy[1] + m_occupancy[0];
    size_t tenuredOccupancy = m_occupancy[2];
    size_t occupancy = tenuredOccupancy + ephemeralOccupancy;

    if (m_prevCondemnedGeneration == 2)
    {
        m_totalLimit = occupancy * SatoriUtil::Gen2Target() / 100;
    }

    // we trigger GC when the footprint consumed since the last GC reaches (SatoriUtil::Gen1Target() - 100)%
    // of the ephemeral occupancy, i.e. when the estimated ephemeral footprint grows to SatoriUtil::Gen1Target().
    // NOTE: after gen2 ephemeralOccupancy is 0. GC.Collect can also see small gen1 size.
    //       that is ok to happen occasionally as we have min limits and smoothing.
    size_t newGen1Budget = max((size_t)MIN_GEN1_BUDGET, ephemeralOccupancy * (SatoriUtil::Gen1Target() - 100) / 100);

    // TUNING: any reasons we might want smaller budget?
    // alternatively we allow gen1 allocs up to 1/16 of total limit.
    size_t altNewGen1Budget = max((size_t)MIN_GEN1_BUDGET, m_totalLimit / 16);

    // take max of both budgets
    newGen1Budget = max(newGen1Budget, altNewGen1Budget);

    m_prevGen1Budget = m_gen1Budget;

    // smooth the budget a bit
    // TUNING: using exponential smoothing with alpha == 1/2. is it a good smooth/lag balance?
    m_gen1Budget = (m_gen1Budget + newGen1Budget) / 2;

    // if the heap size will definitely be over the limit at next GC, make the next GC a full GC,
    // unless we already doing gen2 GC
    if ((m_condemnedGeneration != 2 && occupancy + m_gen1Budget > m_totalLimit) ||
        !SatoriUtil::IsGen1Enabled())
    {
        // NOTE: If gen1 is big, we will still use gen1 target, but then do gen2 GC.
        //       We may exceed the total goal temporarily.
        //       That is ok, but try not to overrun by too much.
        m_nextGcIsFullGc = true;
        if (occupancy > m_totalLimit)
        {
            m_gen1Budget = (size_t)MIN_GEN1_BUDGET;
        }
        else
        {
            m_gen1Budget = max(m_totalLimit - occupancy, (size_t)MIN_GEN1_BUDGET);
        }
    }
    else
    {
        m_nextGcIsFullGc = false;
    }

    // limit budget to available memory
    size_t available = GetAvailableMemory();
    m_availableMemory = available;
    if (available < m_gen1Budget)
    {
        m_gen1Budget = available;
    }

    // now figure if we will promote
    m_promoteAllRegions = false;
    size_t promotionEstimate = m_promotionEstimate;
    m_promotionEstimate = 0;

    if (m_condemnedGeneration == 2)
    {
        m_promoteAllRegions = true;
        m_gen1CountAtLastGen2 = (int)m_gcCount[1];
    }
    else
    {
        // ensure that sometimes we do gen2
        if ((int)m_gcCount[1] - m_gen1CountAtLastGen2 > 128)
        {
           m_nextGcIsFullGc = true;
        }

        if (promotionEstimate > Gen1RegionCount() / 2)
        {
            // will promote too many, just promote all
            m_promoteAllRegions = true;
        }
    }
}

void SatoriRecycler::BlockingCollect()
{
    if (m_condemnedGeneration == 2)
    {
        BlockingCollect2();
    }
    else
    {
        BlockingCollect1();
    }
}

NOINLINE
void SatoriRecycler::BlockingCollect1()
{
    size_t blockingStart = minipal_hires_ticks();

    // stop other threads.
    GCToEEInterface::SuspendEE(SUSPEND_FOR_GC);

    BlockingCollectImpl();

    size_t blockingDuration = ((size_t)m_blockingWorkEndTicks - blockingStart);
    m_CurrentGcInfo->m_pauseDurations[0] = blockingDuration / m_osTicksPerMicro;
    m_gcDurationUsecs[1] = blockingDuration / m_osTicksPerMicro;
    m_gcAccmulatingDurationUsecs[1] += blockingDuration / m_osTicksPerMicro;

    size_t fromStartMillis = GetNowMillis() - m_startMillis;
    int64_t totalPauseUsecs = m_gcAccmulatingDurationUsecs[1] + m_gcAccmulatingDurationUsecs[2];
    m_CurrentGcInfo->m_pausePercentage = fromStartMillis != 0 ? (uint32_t)(totalPauseUsecs * 10 / fromStartMillis) : 0;

    m_CurrentGcInfo = nullptr;
    UpdateGcCounters(blockingStart);

    // restart VM
    GCToEEInterface::RestartEE(true);
}

NOINLINE
void SatoriRecycler::BlockingCollect2()
{
    size_t blockingStart = minipal_hires_ticks();

    // stop other threads.
    GCToEEInterface::SuspendEE(SUSPEND_FOR_GC);

    BlockingCollectImpl();

    size_t blockingDuration = ((size_t)m_blockingWorkEndTicks - blockingStart);
    m_CurrentGcInfo->m_pauseDurations[0] = blockingDuration / m_osTicksPerMicro;
    m_gcDurationUsecs[2] = blockingDuration / m_osTicksPerMicro;
    m_gcAccmulatingDurationUsecs[2] += blockingDuration / m_osTicksPerMicro;

    size_t fromStartMillis = GetNowMillis() - m_startMillis;
    int64_t totalPauseUsecs = m_gcAccmulatingDurationUsecs[1] + m_gcAccmulatingDurationUsecs[2];
    m_CurrentGcInfo->m_pausePercentage = fromStartMillis != 0 ? (uint32_t)(totalPauseUsecs * 10 / fromStartMillis) : 0;

    m_CurrentGcInfo = nullptr;
    UpdateGcCounters(blockingStart);

    // restart VM
    GCToEEInterface::RestartEE(true);
}

NOINLINE
void SatoriRecycler::UpdateGenerationOccupancies()
{
    _ASSERTE(m_occupancyReportingEnabled);

    // We do not need to defer updating gen0, but we will defer for consistency,
    // So that all counts would appear as published at once, more or less.
    m_occupancy[0] = m_occupancyAcc[0];
    m_occupancyAcc[0] = 0;

    // Accumulators are cleared in Plan if corresponding generation is rebuilt in that GC,
    // then occupancy accumulates as GC adds to the gen.
    // Note: gen1 may add to gen2 if promotes, gen1 is always rebuilt.
    // When a GC is completely "done", which is possibly at the start of the next one,
    // we publish accumulated values as the new known generation sizes.
    // The value is not seen while it fills up or updated.
    m_occupancy[1] = m_occupancyAcc[1] - m_demotedOccupancyAcc;
    m_occupancy[2] = m_occupancyAcc[2] + m_demotedOccupancyAcc;

    // No more reporting or accumulating untill enabled again
    m_occupancyReportingEnabled = false;
}

void SatoriRecycler::BlockingCollectImpl()
{
    _ASSERTE(!m_nextGcIsFullGc || m_condemnedGeneration ==2);

    FIRE_EVENT(GCStart_V2, (uint32_t)GlobalGcIndex() + 1, (uint32_t)m_condemnedGeneration, (uint32_t)reason_empty, (uint32_t)gc_etw_type_ngc);
    m_gcStartMillis[m_condemnedGeneration] = GetNowMillis();
    m_activeWorkerFn = nullptr;
    if (IsWorkerThread())
    {
        // do not consider ourselves a worker to not wait forever when we need workers to leave.
        Interlocked::Decrement(&m_activeWorkers);
    }
    else
    {
        // make sure everyone sees the new Fn before waiting for workers to drain.
        MemoryBarrier();
    }

    while (m_activeWorkers > 0)
    {
        YieldProcessor();
    }

    m_gcState = GC_STATE_BLOCKED;

    // assume that we will relocate. we will rethink later.
    m_isRelocating = m_condemnedGeneration == 2 ? SatoriUtil::IsRelocatingInGen2() : SatoriUtil::IsRelocatingInGen1();

    // low latency mode does not relocate, unless asked to compact.
    // same as in the stock GC, where induced GCs are not restricted by low latency mode.
    if (IsLowLatencyMode() && !m_forceCompact)
    {
        m_isRelocating = false;
    }

    // regions swept below are drained a moment later and no allocation can use them.
    // Plan reopens this once the cycle's allocations and budget are known.
    m_reusableLimit = 0;
    RunWithHelp(&SatoriRecycler::DrainDeferredSweepQueue);
    m_deferredSweepRegions->ResetAfterUnsafeDrain();

    // Deliberately not RunWithHelp - the queue is a pointer chase, so discovery is serial
    // no matter how many threads we throw at it, and helpers only add contention on the head.
    DrainReusableQueue();
    m_reusableRegions->ResetAfterUnsafeDrain();

    // all sweeping should be done by now
    if (m_occupancyReportingEnabled)
    {
        UpdateGenerationOccupancies();
    }

    _ASSERTE(m_deferredSweepRegions->IsEmpty());

    // now we know survivorship after the last GC
    // and we can figure what we want to do in this GC and when we will do the next one
    // NOTE: AdjustHeuristics is in the context of previous GC, which is complete by now and m_occupancy
    //       was updated accordingly.
    //       That is intentional. We could glance into current state (i.e. m_gen1AddedSinceLastCollection + Nursery regions),
    //       but we have only vague idea of how much of that is alive until we mark. And if we
    //       need to guess, we'd guess "same as before" - thus counts at last GC are close enough.
    AdjustHeuristics();
    
    // Here we know if the next GC will surely be a full GC.
    int newBarrierState = m_nextGcIsFullGc ? BARRIER_STATE_SKIPPING_CARDS : BARRIER_STATE_NOT_CONCURRENT;
    ToggleWriteBarrier(newBarrierState, /* eeSuspended */ true);
    m_barrierState = newBarrierState;

    // tell EE that we are starting
    // this needs to be called on a "GC" thread while EE is stopped.
    GCToEEInterface::GcStartWork(m_condemnedGeneration, max_generation);

    // this will make the heap officially parseable.
    DeactivateAllocatingRegions();

    m_condemnedRegionsCount = m_condemnedGeneration == 2 ?
        RegionCount() :
        Gen1RegionCount();

    BlockingMark();
    Plan();
    Relocate();
    Update();

    // All regions are swept and every drain is done. When asked, release as much memory as we can before the world restarts.
    // This counts as GC work, same as in the stock GC.
    if (m_forceTrim)
    {
        m_trimmer->TrimNow();
    }

    bool walkHeapForDiagnostics = m_forceBlocking && m_condemnedGeneration == max_generation &&
        GCEventStatus::IsEnabled(GCEventProvider_Default, GCEventKeyword_GCHeapDump, GCEventLevel_Information);

    // we are done using workers.
    // undo the adjustment if we had to do one
    if (IsWorkerThread())
    {
        // interlocked because even though we saw no workers after m_activeWorkerFn was reset,
        // some delayed workers may later still come and look for work.
        Interlocked::Increment(&m_activeWorkers);
    }

    m_gcCount[0]++;
    m_gcCount[1]++;
    if (m_condemnedGeneration == 2)
    {
        m_gcCount[2]++;
        if (m_isRelocating)
        {
            m_compactingGcCount[2]++;
        }
    }
    else
    {
        if (m_isRelocating)
        {
            m_compactingGcCount[1]++;
        }
    }

    m_CurrentGcInfo->m_index = GlobalGcIndex();

    // The reported pause ends here, before telling EE and firing GCEnd/GCHeapStats - same as in the stock GC.
    m_blockingWorkEndTicks = minipal_hires_ticks();

    // we may still have some deferred sweeping to do, but
    // that is unobservable to EE, so tell EE that we are done
    GCToEEInterface::GcDone(m_condemnedGeneration);

    if (walkHeapForDiagnostics)
    {
        _ASSERTE(m_deferredSweepRegions->IsEmpty());
        GCToEEInterface::DiagGCEnd(GlobalGcIndex(), m_condemnedGeneration, reason_empty, /* fConcurrent */ false);
    }

    FIRE_EVENT(GCEnd_V1, (uint32_t)GlobalGcIndex(), (uint32_t)m_condemnedGeneration);

//void FireGCHeapStats_V2(uint64_t generationSize0,
//    uint64_t totalPromotedSize0,
//    uint64_t generationSize1,
//    uint64_t totalPromotedSize1,
//    uint64_t generationSize2,
//    uint64_t totalPromotedSize2,
//    uint64_t generationSize3,
//    uint64_t totalPromotedSize3,
//    uint64_t generationSize4,
//    uint64_t totalPromotedSize4,
//    uint64_t finalizationPromotedSize,
//    uint64_t finalizationPromotedCount,
//    uint32_t pinnedObjectCount,
//    uint32_t sinkBlockCount,
//    uint32_t gcHandleCount);

    FIRE_EVENT(GCHeapStats_V2,
        m_occupancy[0], m_occupancy[0],
        m_occupancy[1], m_occupancy[1],
        m_occupancy[2], m_occupancy[2],
        (size_t)0, (size_t)0,
        (size_t)0, (size_t)0,
        (size_t)0,
        (size_t)m_heap->FinalizationQueue()->Count(),
        (uint32_t)0,
        (uint32_t)0,
        (uint32_t)0);

    if (m_forceCompact)
    {
        VolatileStore(&m_compactionRequests[0].m_completed, m_compactionRequests[0].m_started);
        if (m_condemnedGeneration == 2)
        {
            VolatileStore(&m_compactionRequests[1].m_completed, m_compactionRequests[1].m_started);
        }
    }
    if (m_forceTrim)
    {
        VolatileStore(&m_trimRequest.m_completed, m_trimRequest.m_started);
    }
    if (m_forceBlocking)
    {
        VolatileStore(&m_blockingRequests[0].m_completed, m_blockingRequests[0].m_started);
        if (m_condemnedGeneration == 2)
        {
            VolatileStore(&m_blockingRequests[1].m_completed, m_blockingRequests[1].m_started);
        }
    }

    m_prevCondemnedGeneration = m_condemnedGeneration;
    m_condemnedGeneration = 0;
    m_gcState = GC_STATE_NONE;

    if (SatoriUtil::IsConcurrentEnabled() && !m_deferredSweepRegions->IsEmpty())
    {
        m_deferredSweepCount = m_deferredSweepRegions->Count();
        m_activeWorkerFn = &SatoriRecycler::DrainDeferredSweepQueueWorkerFn;
        MaybeAskForHelp();
    }
    else
    {
        // no deferred sweep, can update occupancy earlier (this is optional)
        UpdateGenerationOccupancies();
    }

    m_trimmer->SetOkToRun();

    m_gcNextTimeTarget = GetNowMillis() + SatoriUtil::GcRate();
}

void SatoriRecycler::RunWithHelp(void(SatoriRecycler::* method)())
{
    m_activeWorkerFn = method;
    do
    {
        (this->*method)();
        YieldProcessor();
    } while (m_activeWorkers > 0);

    m_activeWorkerFn = nullptr;
    // make sure everyone sees the new Fn before waiting for workers to drain.
    MemoryBarrier();
    while (m_activeWorkers > 0)
    {
        // TUNING: are we wasting too many cycles here?
        //         should we find something more useful to do than mmpause,
        //         or perhaps Sleep(0) after a few spins?
        YieldProcessor();
    }
}

void SatoriRecycler::BlockingMark()
{
    // tell EE we will be marking
    // NB: we may have done some marking concurrently already, but anything that watches
    //     for GC marking only cares about blocking/final marking phase.
    GCToEEInterface::BeforeGcScanRoots(m_condemnedGeneration, /* is_bgc */ false, /* is_concurrent */ false);

    MarkStrongReferences();
    ASSERT_NO_WORK();
    DependentHandlesScan();
    ASSERT_NO_WORK();

#ifdef FEATURE_JAVAMARSHAL
    MarkBridgeObjects();
    ASSERT_NO_WORK();
#endif

    // Tell EE we have done marking strong references before scanning finalizables.
    // What actually happens here is detaching COM wrappers when exposed object is not reachable.
    // The object may stay around for finalization and become F-reachable, so the check needs to happen here.
    ScanContext sc;
    GCToEEInterface::AfterGcScanRoots(m_condemnedGeneration, max_generation, &sc);

    ShortWeakPtrScan();
    ScanFinalizables();
    MarkNewReachable();
    ASSERT_NO_WORK();
    LongWeakPtrScan();
}

void SatoriRecycler::DrainAndCleanWorker()
{
    do
    {
        DrainMarkQueues();
        CleanCards();
    } while (!m_workList->IsEmpty() || HasDirtyCards());
}

void SatoriRecycler::MarkNewReachable()
{
    // it is nearly impossible to have dirty cards and empty work queue.
    // basically only if we completely ran out of chunks.
    // we will just check for simplicity.
    while (!m_workList->IsEmpty() || HasDirtyCards())
    {
        RunWithHelp(&SatoriRecycler::DrainAndCleanWorker);
        DependentHandlesRescan();
    }
}

void SatoriRecycler::DependentHandlesScan()
{
    DependentHandlesInitialScan();
    MarkNewReachable();
}

void SatoriRecycler::MarkStrongReferences()
{
    // stack and handles do not track dirtying writes,
    // therefore we must rescan even after concurrent mark completed its work
    IncrementRootScanTicket();
    SatoriHandlePartitioner::StartNextScan();

    RunWithHelp(&SatoriRecycler::MarkStrongReferencesWorker);

    // the demoted drain above is lock-free, restore the queue before it takes pushes again
    m_ephemeralWithUnmarkedDemoted->ResetAfterUnsafeDrain();
}

void SatoriRecycler::MarkStrongReferencesWorker()
{
    // in concurrent case the current stack is unlikely to have anything unmarked
    // it is still preferred to look at own stack on the same thread.
    // this will also ask for help.
    MarkOwnStackAndDrainQueues();
    MarkHandles();
    MarkAllStacksFinalizationAndDemotedRoots();

    if (m_condemnedGeneration == 1)
    {
        DrainMarkQueues();
        MarkThroughCards();
    }

    DrainAndCleanWorker();
}

void SatoriRecycler::ASSERT_NO_WORK()
{
    _ASSERTE(m_workList->IsEmpty());

    m_heap->ForEachPage(
        [&](SatoriPage* page)
        {
            _ASSERTE(page->CardState() < Satori::CardState::PROCESSING);
        }
    );
}

/* static */
void SatoriRecycler::DeactivateFn(gc_alloc_context* gcContext, void* param)
{
    SatoriAllocationContext* context = (SatoriAllocationContext*)gcContext;
    SatoriRecycler* recycler = (SatoriRecycler*)param;

    context->Deactivate(recycler, /*detach*/ recycler->m_promoteAllRegions);
}

void SatoriRecycler::DeactivateAllocatingRegions()
{
    GCToEEInterface::GcEnumAllocContexts(DeactivateFn, m_heap->Recycler());

    // make shared regions parseable, in case we have byrefs pointing to them.
    m_heap->Allocator()->DeactivateSharedRegions(m_promoteAllRegions);
}

void SatoriRecycler::PushToMarkQueuesSlow(SatoriWorkChunk*& currentWorkChunk, SatoriObject* o)
{
    _ASSERTE(o->ContainingRegion()->Generation() <= m_condemnedGeneration);

    if (currentWorkChunk)
    {
        m_workList->Push(currentWorkChunk);
        MaybeAskForHelp();
    }

    currentWorkChunk = m_heap->Allocator()->TryGetWorkChunk();
    if (currentWorkChunk)
    {
        currentWorkChunk->Push(o);
    }
    else
    {
        // handle mark overflow by dirtying the cards
        o->DirtyCardsForContent();

        // since this o will not be popped from the work queue,
        // check for unmovable here
        if (o->IsUnmovable())
        {
            o->ContainingRegion()->SetHasPinnedObjects();
        }
    }
}

/* static */
template <bool isConservative>
void SatoriRecycler::MarkFn(PTR_PTR_Object ppObject, ScanContext* sc, uint32_t flags)
{
    size_t location = (size_t)*ppObject;
    if (location == 0)
    {
        return;
    }

    MarkContext* markContext = (MarkContext*)sc->_unused1;
    SatoriObject* o = (SatoriObject*)location;

    if (flags & GC_CALL_INTERIOR)
    {
        // byrefs may point to stack, use checked here
        SatoriRegion* containingRegion = markContext->m_heap->RegionForAddressChecked(location);
        if (!containingRegion ||
            (isConservative && (containingRegion->Generation() < 0)))
        {
            return;
        }

        o = containingRegion->FindObject(location);
        if (isConservative && o->IsFree())
        {
            return;
        }
    }
    else if (o->IsExternal())
    {
        return;
    }

    if (o->ContainingRegion()->Generation() <= markContext->m_condemnedGeneration)
    {
        if (!o->IsMarked())
        {
            o->SetMarkedAtomic();
            markContext->PushToMarkQueues(o);
        }

        if (flags & GC_CALL_PINNED)
        {
            o->ContainingRegion()->SetHasPinnedObjects();
        }
    }
};

/* static */
template <bool isConservative>
void SatoriRecycler::UpdateFn(PTR_PTR_Object ppObject, ScanContext* sc, uint32_t flags)
{
    size_t location = (size_t)*ppObject;
    if (location == 0)
    {
        return;
    }

    SatoriObject* o = (SatoriObject*)location;
    if (flags & GC_CALL_INTERIOR)
    {
        MarkContext* markContext = (MarkContext*)sc->_unused1;
        // byrefs may point to stack, use checked here
        SatoriRegion* containingRegion = markContext->m_heap->RegionForAddressChecked(location);
        if (!containingRegion ||
            (isConservative && (containingRegion->Generation() < 0)))
        {
            return;
        }

        o = containingRegion->FindObject(location);
        if (isConservative && o->IsFree())
        {
            return;
        }
    }

    SatoriObject* newLocation;
    if (o->IsRelocatedTo(&newLocation))
    {
        if (flags & GC_CALL_INTERIOR)
        {
            *ppObject = (PTR_Object)(location + ((size_t)newLocation - o->Start()));
        }
        else
        {
            *ppObject = (PTR_Object)newLocation;
        }
    }
};

template <bool isConservative>
void SatoriRecycler::MarkFnConcurrent(PTR_PTR_Object ppObject, ScanContext* sc, uint32_t flags)
{
    size_t location = (size_t)VolatileLoadWithoutBarrier(ppObject);
    if (location == 0)
    {
        return;
    }

#ifdef DEBUG_DestroyedHandleValue
    // we can race with destroy handle during concurrent scan
    if (location == (size_t)DEBUG_DestroyedHandleValue)
        return;
#endif //DEBUG_DestroyedHandleValue

    SatoriRegion* containingRegion;
    MarkContext* markContext = (MarkContext*)sc->_unused1;
    SatoriObject* o = (SatoriObject*)location;

    if (flags & GC_CALL_INTERIOR)
    {
        // byrefs may point to stack, use checked here
        containingRegion = markContext->m_heap->RegionForAddressChecked(location);
        if (!containingRegion)
        {
            return;
        }

        // since this is concurrent, in a conservative case an allocation could have caused a split
        // that shortened the found region and the region no longer contains the ref (which means the ref is not real).
        // Note that the split could only happen before we are done with allocator queue (which is synchronising)
        // and assign 0+ gen to the region.
        // So check here in the opposite order - first that region is 0+ gen and then that the size is still right.
        if (isConservative &&
            (containingRegion->GenerationAcquire() < 0 || location >= containingRegion->End()))
        {
            return;
        }

        // Concurrent FindObject is unsafe in active regions. While ref may be in a real obj,
        // the path to it from the first obj or prev indexed may cross unparsable ranges.
        // The check must acquire to be sure we check before actually doing FindObject.
        if (containingRegion->MaybeAllocatingAcquire())
        {
            return;
        }

        o = containingRegion->FindObject(location);
        if (isConservative && o->IsFree())
        {
            return;
        }
    }
    else
    {
        if (o->IsExternal())
        {
            return;
        }

        containingRegion = o->ContainingRegion();
        // can't mark in regions which are tracking escapes, bitmap is in use
        if (containingRegion->MaybeEscapeTrackingAcquire())
        {
            return;
        }
    }

    // no need for fences here. a published obj cannot become gen2 concurrently. 
    if (containingRegion->Generation() <= markContext->m_condemnedGeneration)
    {
        if (!o->IsMarked())
        {
            o->SetMarkedAtomic();
            markContext->PushToMarkQueues(o);
        }

        if (flags & GC_CALL_PINNED)
        {
            o->ContainingRegion()->SetHasPinnedObjects();
        }
    }
};

bool SatoriRecycler::MarkDemotedAndDrainQueuesConcurrent(int64_t deadline)
{
    _ASSERTE(!IsBlockingPhase());
    _ASSERTE(deadline > 0);

    MarkContext markContext = MarkContext(this);

    // Going through demoted could be contentious.
    // If there are workers, let workers do that.
    if (IsWorkerThread() || m_activeWorkers == 0)
    {
        // in blocking case we go through demoted together with marking all stacks
        // in concurrent case we do it here, since going through demoted does not need EE stopped.
        SatoriRegion* curRegion = m_ephemeralWithUnmarkedDemoted->TryPop();
        if (curRegion)
        {
            MaybeAskForHelp();
            do
            {
                MarkDemoted(curRegion, &markContext);
                PushToEphemeralQueues(curRegion);

                if ((SatoriUtil::GetTimeStamp() - deadline) > 0)
                {
                    if (markContext.m_WorkChunk != nullptr)
                    {
                        m_workList->Push(markContext.m_WorkChunk);
                    }

                    return true;
                }
            } while ((curRegion = m_ephemeralWithUnmarkedDemoted->TryPop()));
        }
    }

    return DrainMarkQueuesConcurrent(markContext.m_WorkChunk, deadline) ||
        !m_ephemeralWithUnmarkedDemoted->IsEmpty();
}

// Demoted regions that are reusable sit in m_reusableRegions, which the prep stage can only
// filter with EE stopped. Move them to the alternate queue and mark them here instead -
// prep will splice the alternate back and only deal with what we did not get to.
// Popping takes a lock, so this is for workers only - an app thread must not block here.
bool SatoriRecycler::MarkDemotedInReusableConcurrent(int64_t deadline)
{
    _ASSERTE(!IsBlockingPhase());
    _ASSERTE(deadline > 0);

    if (!IsWorkerThread())
    {
        return false;
    }

    Interlocked::Increment(&m_reusableFilterThreadsNum);

    // Paired with prep: it publishes the state, then reads this count. Both sides fence
    // between their write and their read, so if prep sees 0 here, we see a state != NONE.
    bool revisit = false;
    if (m_ccStackMarkState == CC_MARK_STATE_NONE)
    {
        MarkContext markContext = MarkContext(this);
        SatoriRegion* curRegion = m_reusableRegions->TryPop();
        if (curRegion)
        {
            MaybeAskForHelp();
            do
            {
                MarkDemotedInReusable(curRegion, &markContext);

                // reusables are kept with the top-bucket-free ones at the tail, which is the
                // end TryGetReusableForLarge takes from. re-derive that, same as KeepRegion does.
                if (curRegion->HasFreeSpaceInTopBucket())
                {
                    m_reusableRegionsAlternate->Enqueue(curRegion);
                }
                else
                {
                    m_reusableRegionsAlternate->Push(curRegion);
                }

                if ((SatoriUtil::GetTimeStamp() - deadline) > 0)
                {
                    revisit = !m_reusableRegions->IsEmpty();
                    break;
                }
            } while ((curRegion = m_reusableRegions->TryPop()));
        }

        if (markContext.m_WorkChunk != nullptr)
        {
            m_workList->Push(markContext.m_WorkChunk);
        }
    }

    Interlocked::Decrement(&m_reusableFilterThreadsNum);
    return revisit;
}

void SatoriRecycler::MarkOwnStackAndDrainQueues()
{
    MarkContext markContext = MarkContext(this);

    if (!IsWorkerThread())
    {
        gc_alloc_context* aContext = GCToEEInterface::GetAllocContext();
        int threadScanTicket = VolatileLoadWithoutBarrier(&aContext->alloc_count);
        int currentScanTicket = GetRootScanTicket();
        if (threadScanTicket != currentScanTicket)
        {
            // claim our own stack for scanning
            if (Interlocked::CompareExchange(&aContext->alloc_count, currentScanTicket, threadScanTicket) == threadScanTicket)
            {
                MaybeAskForHelp();
                MarkOwnStack(aContext, &markContext);
            }
        }
    }

    DrainMarkQueues(markContext.m_WorkChunk);
}

void SatoriRecycler::MarkOwnStackOrDrainQueuesConcurrent(int64_t deadline)
{
    MarkContext markContext = MarkContext(this);

    if (!IsWorkerThread())
    {
        gc_alloc_context* aContext = GCToEEInterface::GetAllocContext();
        int threadScanTicket = VolatileLoadWithoutBarrier(&aContext->alloc_count);
        int currentScanTicket = GetRootScanTicket();
        if (threadScanTicket != currentScanTicket)
        {
            // claim our own stack for scanning
            if (Interlocked::CompareExchange(&aContext->alloc_count, currentScanTicket, threadScanTicket) == threadScanTicket)
            {
                MaybeAskForHelp();
                MarkOwnStack(aContext, &markContext);

                // prep is about to suspend us, so hand off what we found rather than draining it here
                if (markContext.m_WorkChunk != nullptr)
                {
                    m_workList->Push(markContext.m_WorkChunk);
                }

                return;
            }
        }
    }

    DrainMarkQueuesConcurrent(markContext.m_WorkChunk, deadline);
}

void SatoriRecycler::MarkOwnStack(gc_alloc_context* aContext, MarkContext* markContext)
{
    bool isBlockingPhase = IsBlockingPhase();
    if (!isBlockingPhase)
    {
        SatoriRegion* region = ((SatoriAllocationContext*)aContext)->RegularRegion();
        if (region)
        {
            region->StopEscapeTracking();
        }
    }

    ScanContext sc;
    sc.promotion = TRUE;
    sc._unused1 = markContext;

    if (SatoriUtil::IsConservativeMode())
        GCToEEInterface::GcScanCurrentStackRoots(isBlockingPhase ? MarkFn<true> : MarkFnConcurrent<true>, &sc);
    else
        GCToEEInterface::GcScanCurrentStackRoots(isBlockingPhase ? MarkFn<false> : MarkFnConcurrent<false>, &sc);
}

void SatoriRecycler::MarkDemoted(SatoriRegion* curRegion, MarkContext* markContext)
{
    _ASSERTE(curRegion->Generation() == 1);
    curRegion->HasUnmarkedDemotedObjects() = false;

    if (m_condemnedGeneration == 1)
    {
        _ASSERTE(!curRegion->MaybeEscapeTrackingAcquire());
        curRegion->HasPinnedObjects() = true;

        SatoriWorkChunk* gen2Objects = curRegion->DemotedObjects();
        while (gen2Objects)
        {
            for (int i = 0; i < gen2Objects->Count(); i++)
            {
                SatoriObject* o = gen2Objects->Item(i);
                // demotion put these in a gen1 region, so they may already be marked
                if (!o->IsMarked())
                {
                    o->SetMarkedAtomic();
                    markContext->PushToMarkQueues(o);
                }
            }

            gen2Objects = gen2Objects->Next();
        }
    }
    else
    {
        curRegion->FreeDemotedTrackers();
    }
}

// Marks demoted objects of a region taken out of a reusable queue during concurrent marking.
// A gen0 candidate cannot be marked in, since its bitmap may be in use for escape tracking
// once an allocator takes it. We switch the region to gen1 first. That is ok without stopping EE:
//  - the region is not in a queue now, thus no allocator can take it while it is still a gen0 candidate.
//    When it goes back to a queue, the queue lock publishes the new level to whoever takes it next.
//  - concurrent markers that still see the old level just defer marking via dirty cards.
//    The bitmap is not in use either way, so marking in it right away is fine too.
void SatoriRecycler::MarkDemotedInReusable(SatoriRegion* curRegion, MarkContext* markContext)
{
    _ASSERTE(!IsBlockingPhase());
    _ASSERTE(!curRegion->IsAttachedToAllocatingOwner());
    _ASSERTE(!curRegion->IsEscapeTracking());

    if (curRegion->HasUnmarkedDemotedObjects())
    {
        if (curRegion->ReusableFor() == SatoriRegion::ReuseLevel::Gen0)
        {
            VolatileStore(&curRegion->ReusableFor(), SatoriRegion::ReuseLevel::Gen1);
        }

        MarkDemoted(curRegion, markContext);
    }
}

void SatoriRecycler::MarkAllStacksFinalizationAndDemotedRoots()
{
    // mark roots for all stacks
    ScanContext sc;
    sc.promotion = TRUE;
    MarkContext markContext = MarkContext(this);
    sc._unused1 = &markContext;

    bool isBlockingPhase = IsBlockingPhase();

    if (SatoriUtil::IsConservativeMode())
        //generations are meaningless here, so we pass -1
        GCToEEInterface::GcScanRoots(isBlockingPhase ? MarkFn<true> : MarkFnConcurrent<true>, -1, -1, &sc);
    else
        GCToEEInterface::GcScanRoots(isBlockingPhase ? MarkFn<false> : MarkFnConcurrent<false>, -1, -1, &sc);

    SatoriFinalizationQueue* fQueue = m_heap->FinalizationQueue();
    if (fQueue->TryUpdateScanTicket(this->GetRootScanTicket()) &&
        fQueue->HasItems())
    {
        fQueue->ForEachObjectRef(
            [&](SatoriObject** ppObject)
            {
                SatoriObject* o = *ppObject;

                // can't mark in regions which are tracking escapes, bitmap is in use
                if (!isBlockingPhase && o->ContainingRegion()->MaybeEscapeTrackingAcquire())
                {
                    return;
                }

                if (!o->IsMarkedOrOlderThan(markContext.m_condemnedGeneration))
                {
                    o->SetMarkedAtomic();
                    markContext.PushToMarkQueues(o);
                }
            }
        );
    }

    // EE is stopped here for root marking, but this could be either
    // a part of the blocking phase or a part of concurrent GC 
    if (isBlockingPhase)
    {
        SatoriRegion* curRegion = m_ephemeralWithUnmarkedDemoted->TryPopDrainOnly();
        if (curRegion)
        {
            MaybeAskForHelp();
            do
            {
                MarkDemoted(curRegion, &markContext);
                PushToEphemeralQueues(curRegion);
            } while ((curRegion = m_ephemeralWithUnmarkedDemoted->TryPopDrainOnly()));
        }
    }
    else
    {
        // going through demoted queue does not require EE stopped,
        // so in concurrent case we will do that later.
        // but there could be demoted regions in reusable queue
        // we can mark tenured objects in those.
        SatoriRegion* curRegion = m_reusableRegionsAlternate->TryPop();
        if (curRegion)
        {
            MaybeAskForHelp();
            do
            {
                MarkDemotedInReusable(curRegion, &markContext);

                if (curRegion->HasFreeSpaceInTopBucket())
                {
                    m_reusableRegions->Enqueue(curRegion);
                }
                else
                {
                    m_reusableRegions->Push(curRegion);
                }
            } while ((curRegion = m_reusableRegionsAlternate->TryPop()));
        }
    }

    if (markContext.m_WorkChunk != nullptr)
    {
        m_workList->Push(markContext.m_WorkChunk);
    }
}

void SatoriRecycler::PushOrReturnWorkChunk(SatoriWorkChunk * chunk)
{
    if (chunk)
    {
        if (chunk->Count() > 0)
        {
            m_workList->Push(chunk);
        }
        else
        {
            m_heap->Allocator()->ReturnWorkChunk(chunk);
        }
    }
}

// Draining a mark chunk visits objects in a known order, since new work goes to a different chunk.
// Prefetching the object this many pops ahead hides most of the latency of reading it.
// (1 -> 16 made concurrent marking ~15% cheaper per object on a large heap)
static const int MARK_PREFETCH_DISTANCE = 16;

bool SatoriRecycler::DrainMarkQueuesConcurrent(SatoriWorkChunk* srcChunk, int64_t deadline)
{
    _ASSERTE(deadline > 0);

    if (!srcChunk)
    {
        srcChunk = m_workList->TryPop();
    }

    if (!m_workList->IsEmpty())
    {
        MaybeAskForHelp();
    }

    SatoriWorkChunk* dstChunk = nullptr;
    SatoriObject* o = nullptr;

    // Newly marked children go through a prefetch queue. When a child comes out, its type is likely in cache.
    // Children without references are done at that point, only the rest go to dstChunk to be scanned.
    SatoriPrefetchQueue<8> childQueue;

    const int condemnedGeneration = m_condemnedGeneration;

    auto pushToChunk = [&](SatoriObject* child)
    {
        if (!dstChunk || !dstChunk->TryPush(child))
        {
            this->PushToMarkQueuesSlow(dstChunk, child);
        }
    };

    auto processChild = [&](SatoriObject* child)
    {
        if (child->RawGetMethodTable()->ContainsGCPointersOrCollectible())
        {
            pushToChunk(child);
        }
        else if (child->IsUnmovable())
        {
            child->ContainingRegion()->SetHasPinnedObjects();
        }
    };

    auto markChildFn = [&](SatoriObject** ref)
    {
        SatoriObject* child = VolatileLoadWithoutBarrier(ref);
        if (child && !child->IsExternal())
        {
            SatoriRegion* childRegion = child->ContainingRegion();
            if (!childRegion->MaybeEscapeTrackingAcquire())
            {
                if (!child->IsMarkedOrOlderThan(condemnedGeneration))
                {
                    child->SetMarkedAtomic();
                    if (SatoriObject* oldest = childQueue.Push(child))
                    {
                        processChild(oldest);
                    }
                }
                return;
            }

            // cannot mark the child in a thread local region. just mark the ref as dirty to visit later.
            // if ref is outside of the containing region, it is a fake ref to collectible allocator.
            // dirty the MT location as if it points to the allocator object.
            // technically it does reference the allocator, by indirection.
            SatoriRegion* parentRegion = o->ContainingRegion();
            if ((size_t)ref - parentRegion->Start() > parentRegion->Size())
            {
                ref = (SatoriObject**)o->Start();
            }

            parentRegion->ContainingPage()->DirtyCardForAddressConcurrent((size_t)ref);
        }
    };

    while (srcChunk)
    {
        if (srcChunk->IsRange())
        {
            size_t start, end;
            srcChunk = TakeObjectRange(srcChunk, o, start, end, Satori::MARK_RANGE_THRESHOLD);
            if (srcChunk)
            {
                if (!dstChunk)
                {
                    dstChunk = srcChunk;
                }
                else
                {
                    m_heap->Allocator()->ReturnWorkChunk(srcChunk);
                }
            }

            srcChunk = nullptr;
            // mark children in the range
            o->ForEachObjectRef(markChildFn, o->Size(), start, end, /* includeCollectibleAllocator */ true);
        }
        else
        {
            // share half of work if current work list shard has nothing to share
            if (srcChunk->Count() > Satori::SHARE_WORK_THRESHOLD &&
                m_workList->IsCurrentShardEmpty())
            {
                size_t half = srcChunk->Count() / 2;
                SatoriWorkChunk* halfChunk = m_heap->Allocator()->TryGetWorkChunk();
                if (halfChunk)
                {
                    halfChunk->TakeFrom(srcChunk, half);
                    m_workList->Push(srcChunk);
                    srcChunk = halfChunk;
                    MaybeAskForHelp();
                }
            }

            // drain srcChunk to dst chunk
            while (srcChunk->Count() > 0)
            {
                o = srcChunk->Pop();
                srcChunk->PrefetchNext(MARK_PREFETCH_DISTANCE);

                _ASSERTE(o->IsMarked());
                if (o->IsUnmovable())
                {
                    o->ContainingRegion()->SetHasPinnedObjects();
                }

                // do not get engaged with big objects, reschedule them as child ranges.
                size_t size = o->Size();
                if (size > Satori::MARK_RANGE_THRESHOLD)
                {
                    ScheduleMarkAsChildRanges(o);
                    continue;
                }

                o->ForEachObjectRef(markChildFn, size, /* includeCollectibleAllocator */ true);
            }
        }

        // finish the queued children, so that dstChunk has all the new work.
        while (SatoriObject* child = childQueue.Pop())
        {
            processChild(child);
        }

        _ASSERTE(srcChunk == nullptr || srcChunk->Count() == 0);

        // every once in a while check for the deadline.
        // check after processing one chunk to:
        // - amortize cost of reading the timestamp and
        // - establish the minimum amount of work per help quantum
        if ((SatoriUtil::GetTimeStamp() - deadline) > 0)
        {
            PushOrReturnWorkChunk(srcChunk);
            PushOrReturnWorkChunk(dstChunk);
            return true;
        }

        // done with srcChunk
        // if we have nonempty dstChunk (i.e. produced more work),
        // swap src and dst and continue
        if (dstChunk && dstChunk->Count() > 0)
        {
            SatoriWorkChunk* tmp = srcChunk;
            srcChunk = dstChunk;
            dstChunk = tmp;
        }
        else
        {
            if (srcChunk)
            {
                if (!dstChunk)
                {
                    dstChunk = srcChunk;
                }
                else
                {
                    m_heap->Allocator()->ReturnWorkChunk(srcChunk);
                }
            }

            srcChunk = m_workList->TryPop();
        }
    }

    if (dstChunk)
    {
        _ASSERTE(dstChunk->Count() == 0);
        m_heap->Allocator()->ReturnWorkChunk(dstChunk);
    }

    return false;
}

SatoriWorkChunk* SatoriRecycler::TakeObjectRange(SatoriWorkChunk* chunk, SatoriObject*& o, size_t& start, size_t& end, size_t scanSize)
{
    chunk->GetRange(o, start, end);
    size_t next = start + min(scanSize, end - start);
    if (next < end)
    {
        // Expose more parallel work without materializing every scan range.
        // Round the split to scan units so only the final range has a short tail.
        size_t units = (end - next) / scanSize;
        if (units > 1)
        {
            SatoriWorkChunk* half = m_heap->Allocator()->TryGetWorkChunk();
            if (half)
            {
                size_t middle = next + (units / 2) * scanSize;
                half->SetRange(o, middle, end);
                m_workList->Push(half);
                end = middle;
            }
        }

        // Publish the remainder before scanning so other workers can claim it.
        // Reuse the original chunk even if allocating a second chunk failed.
        chunk->SetRange(o, next, end);
        m_workList->Push(chunk);
        MaybeAskForHelp();
        end = next;
        return nullptr;
    }

    chunk->Clear();
    return chunk;
}

void SatoriRecycler::ScheduleMarkAsChildRanges(SatoriObject* o)
{
    if (o->RawGetMethodTable()->ContainsGCPointersOrCollectible())
    {
        size_t start = o->Start();
        size_t end = start + o->Size();
        SatoriWorkChunk* chunk = m_heap->Allocator()->TryGetWorkChunk();
        if (chunk == nullptr)
        {
            o->ContainingRegion()->ContainingPage()->DirtyCardsForRange(start, end);
            return;
        }

        chunk->SetRange(o, start, end);
        m_workList->Push(chunk);
    }
}

bool SatoriRecycler::ScheduleUpdateAsChildRanges(SatoriObject* o)
{
    _ASSERTE(o->RawGetMethodTable()->ContainsGCPointers());

    SatoriWorkChunk* chunk = m_heap->Allocator()->TryGetWorkChunk();
    if (chunk == nullptr)
    {
        return false;
    }

    // Skip the method table, it is not a reference.
    // TakeObjectRange splits the range into pieces that do not overlap, so every reference is updated once.
    // NB: that is not needed for correctness. Updating a reference again would find it already updated,
    //     since the new location is not in a relocated region. It would just be wasted work.
    chunk->SetRange(o, o->Start() + sizeof(size_t), o->Start() + o->Size());
    m_workList->Push(chunk);
    return true;
}

void SatoriRecycler::DrainMarkQueues(SatoriWorkChunk* srcChunk)
{
    if (!srcChunk)
    {
        srcChunk = m_workList->TryPop();
    }

    if (!m_workList->IsEmpty())
    {
        MaybeAskForHelp();
    }

    SatoriWorkChunk* dstChunk = nullptr;

    // Newly marked children go through a prefetch queue. When a child comes out, its type is likely in cache.
    // Children without references are done at that point, only the rest go to dstChunk to be scanned.
    SatoriPrefetchQueue<8> childQueue;

    const int condemnedGeneration = m_condemnedGeneration;

    auto pushToChunk = [&](SatoriObject* child)
    {
        if (!dstChunk || !dstChunk->TryPush(child))
        {
            this->PushToMarkQueuesSlow(dstChunk, child);
        }
    };

    auto processChild = [&](SatoriObject* child)
    {
        if (child->RawGetMethodTable()->ContainsGCPointersOrCollectible())
        {
            pushToChunk(child);
        }
        else if (child->IsUnmovable())
        {
            child->ContainingRegion()->SetHasPinnedObjects();
        }
    };

    auto markChildFn = [&](SatoriObject** ref)
    {
        SatoriObject* child = *ref;
        if (child &&
            !child->IsExternal() &&
            !child->IsMarkedOrOlderThan(condemnedGeneration))
        {
            child->SetMarkedAtomic();
            if (SatoriObject* oldest = childQueue.Push(child))
            {
                processChild(oldest);
            }
        }
    };

    while (srcChunk)
    {
        if (srcChunk->IsRange())
        {
            SatoriObject* o;
            size_t start, end;
            srcChunk = TakeObjectRange(srcChunk, o, start, end, Satori::MARK_RANGE_THRESHOLD);
            if (srcChunk)
            {
                if (!dstChunk)
                {
                    dstChunk = srcChunk;
                }
                else
                {
                    m_heap->Allocator()->ReturnWorkChunk(srcChunk);
                }
            }

            srcChunk = nullptr;
            // mark children in the range
            o->ForEachObjectRef(markChildFn, o->Size(), start, end, /* includeCollectibleAllocator */ true);
        }
        else
        {
            // share half of work if current work list shard has nothing to share
            if (srcChunk->Count() > Satori::SHARE_WORK_THRESHOLD &&
                m_workList->IsCurrentShardEmpty())
            {
                size_t half = srcChunk->Count() / 2;
                SatoriWorkChunk* halfChunk = m_heap->Allocator()->TryGetWorkChunk();
                if (halfChunk)
                {
                    halfChunk->TakeFrom(srcChunk, half);
                    m_workList->Push(srcChunk);
                    srcChunk = halfChunk;
                    MaybeAskForHelp();
                }
            }

            // mark objects in the chunk
            while (srcChunk->Count() > 0)
            {
                SatoriObject* o = srcChunk->Pop();
                srcChunk->PrefetchNext(MARK_PREFETCH_DISTANCE);

                _ASSERTE(o->IsMarked());
                if (o->IsUnmovable())
                {
                    o->ContainingRegion()->SetHasPinnedObjects();
                }

                // do not get engaged with big objects, reschedule them as child ranges.
                size_t size = o->Size();
                if (size > Satori::MARK_RANGE_THRESHOLD)
                {
                    ScheduleMarkAsChildRanges(o);
                    continue;
                }

                o->ForEachObjectRef(markChildFn, size, /* includeCollectibleAllocator */ true);
            }
        }

        // finish the queued children, so that dstChunk has all the new work.
        while (SatoriObject* child = childQueue.Pop())
        {
            processChild(child);
        }

        _ASSERTE(!srcChunk || srcChunk->Count() == 0);

        // done with srcChunk
        // if we have nonempty dstChunk (i.e. produced more work),
        // swap src and dst and continue
        if (dstChunk && dstChunk->Count() > 0)
        {
            SatoriWorkChunk* tmp = srcChunk;
            srcChunk = dstChunk;
            dstChunk = tmp;
        }
        else
        {
            if (srcChunk)
            {
                m_heap->Allocator()->ReturnWorkChunk(srcChunk);
            }

            srcChunk = m_workList->TryPop();
        }
    }

    if (dstChunk)
    {
        _ASSERTE(dstChunk->Count() == 0);
        m_heap->Allocator()->ReturnWorkChunk(dstChunk);
    }
}

// Just a number that is likely be different for different threads making the same call.
// Mix in the gcId to not get stuck with the same combination across threads, in case
// it is not a good combination. (most combinations are good, but there can be rare bad ones)
size_t ThreadSpecificNumber(int64_t gcId)
{
    size_t result = (((size_t)&result ^ (size_t)gcId) * 11400714819323198485llu) >> 32;
    return result;
}

int64_t SatoriRecycler::GlobalGcIndex()
{
    return m_gcCount[0];
}

struct ConcurrentCardsRestart
{
    int64_t gcId;
    SatoriPage* page;
    size_t ii;
    size_t rndOffset;
};

thread_local
ConcurrentCardsRestart t_concurrentCardState;

bool SatoriRecycler::MarkThroughCardsConcurrent(int64_t deadline)
{
    SatoriWorkChunk* dstChunk = nullptr;
    bool revisit = false;

    // Use Gen1 count to identify the current GC. Not Gen0 as that could be changing concurrently.
    // Multiply by 2 to not intersect with concurrent card cleaner, which uses the same restart state.
    int64_t gcId = m_gcCount[1] * 2;
    ConcurrentCardsRestart* pRestart = &t_concurrentCardState;
    if (pRestart->gcId != gcId)
    {
        pRestart->gcId = gcId;
        pRestart->page = nullptr;
        pRestart->ii = 0;
        pRestart->rndOffset = ThreadSpecificNumber(gcId);
    }

    m_heap->ForEachPageUntil(
        [&](SatoriPage* page)
        {
            int8_t pageState = page->CardState();
            // Visit and advance tickets of all non-blank pages and groups, not just remembered as that also sets them for
            // revisit in concurrent clearing. Anything non-blank can be or become dirty, so clear will take a look.
            // Blank ones will not match any tickets and will be visited by clearing pass, if become dirty.
            if (pageState != Satori::CardState::BLANK)
            {
                uint8_t currentScanTicket = GetCardScanTicket();
                if (page->ScanTicket() == currentScanTicket)
                {
                    // this is not a timeout, continue to next page
                    return false;
                }

                // there is unfinished page. Maybe should ask for help
                MaybeAskForHelp();

                size_t groupCount = page->CardGroupCount();

                // if restarting with the same page, continue with the last ii and offset
                size_t offset = pRestart->rndOffset;
                size_t ii = pRestart->ii;
                if (pRestart->page != page)
                {
                    // it is a new page
                    pRestart->page = page;
                    ii = 0;
                }

                while (ii < groupCount)
                {
                    size_t i = (ii + offset) % groupCount;
                    ii++;

                    int8_t groupState = page->CardGroupState(i);
                    // Visit and advance tickets of all non-blank groups, not just remembered as that also
                    // sets them for clearing later.
                    if (groupState != Satori::CardState::BLANK)
                    {
                        uint8_t groupTicket = page->CardGroupScanTicket(i);
                        if (groupTicket == currentScanTicket)
                        {
                            continue;
                        }

                        // claim the group as complete
                        page->CardGroupScanTicket(i) = currentScanTicket;

                        // now we have to finish, since we have clamed the group
                        // NB: two threads may claim the same group and do overlapping work
                        //     that is correct, but redundant. We could claim using interlocked operation
                        //     and avoid that, but such collisions appear to be too rare to worry about.
                        //     It may be worth watching this in the future though.

                        // NB: It is safe to get a region even if region map may be changing because
                        //    a region with remembered/dirty marks must be there and cannot be destroyed.
                        SatoriRegion* region = page->RegionForCardGroup(i);
                        _ASSERTE(region->Generation() >= 0);

                        // we should not be marking when there could be dead objects
                        _ASSERTE(!region->HasMarksSet());

                        // sometimes we set cards without knowing dst generation, but REMEMBERED only has meaning in tenured
                        if (region->Generation() < 2)
                        {
                            // Wiping the group if ephemeral+remembered is an optimization.
                            // If the group is not covered by a tenured region, we wipe the group, to not look at it again in the next scans.
                            // The actual cards do not matter, we will look at cards in ephemeral regions only if group is dirty,
                            // and then cleaner will reset them appropriately.
                            // There is no marking work in this region, so ticket is also irrelevant. It will be wiped if region gets promoted.
                            // As a result cards that do not belong to tenured regions may occasionally have remembered state set.
                            // It is ignorable and we are too lazy to clean that.until the region is promoted.
                            if (groupState == Satori::CardState::REMEMBERED)
                            {
                                Interlocked::CompareExchange(&page->CardGroupState(i), Satori::CardState::BLANK, Satori::CardState::REMEMBERED);
                            }

                            continue;
                        }

                        // invariant check: when marking through cards the gen2 remset stays remset, thus should be marked through on every GC
                        // and should not fall far behind the tickets
                        _ASSERTE(groupTicket == 0 || groupTicket == 0xff || (uint8_t)(currentScanTicket - groupTicket) == 1);
                        int8_t resetValue = Satori::CardState::REMEMBERED;
                        int8_t* cards = page->CardsForGroup(i);
                        // where the previous walk in this group stopped, see FindObject
                        SatoriObject* hint = nullptr;
                        for (size_t j = 0; j < Satori::CARD_BYTES_IN_CARD_GROUP; j++)
                        {
                            // cards are often sparsely set, if j is aligned, check the entire size_t for unset value
                            if (((j & (sizeof(size_t) - 1)) == 0) && *((size_t*)&cards[j]) == 0)
                            {
                                j += sizeof(size_t) - 1;
                                continue;
                            }

                            // skip unset cards
                            int8_t origValue = cards[j];
                            if (origValue <= Satori::CardState::BLANK)
                            {
                                continue;
                            }

                            size_t start = page->LocationForCard(&cards[j]);
                            bool cleaned = false;
                            do
                            {
                                // since we are going to look at the card, we can clean its dirty state.
                                // just need to make sure we do the state change before looking at objects - in case it gets dirty again.
                                if (cards[j] == Satori::CardState::DIRTY)
                                {
                                    cleaned = true;
                                    cards[j] = resetValue;
                                }
                            } while (++j < Satori::CARD_BYTES_IN_CARD_GROUP && cards[j] > Satori::CardState::BLANK);

                            size_t end = page->LocationForCard(&cards[j]);
                            size_t objLimit = min(end, region->Start() + Satori::REGION_SIZE_GRANULARITY);
                            SatoriObject* o = region->FindObject(start, hint);

                            if (cleaned)
                            {
                                // do not allow card cleaning to delay until after checking IsMarked
                                MemoryBarrier();
                            }

                            do
                            {
                                size_t size = o->Size();

                                // everything is effectively marked in gen2 remembered set
                                // if (o->IsMarked())
                                {
                                    o->ForEachObjectRef(
                                    [&](SatoriObject** ref)
                                    {
                                        SatoriObject* child = VolatileLoadWithoutBarrier(ref);
                                        if (child && !child->IsExternal())
                                        {
                                            SatoriRegion* childRegion = child->ContainingRegion();
                                            if (!childRegion->MaybeEscapeTrackingAcquire())
                                            {
                                                if (!child->IsMarkedOrOlderThan(1))
                                                {
                                                    child->SetMarkedAtomic();
                                                    if (!dstChunk || !dstChunk->TryPush(child))
                                                    {
                                                        this->PushToMarkQueuesSlow(dstChunk, child);
                                                    }
                                                }
                                                return;
                                            }

                                            // cannot mark stuff in thread local regions. just mark as dirty to visit later.
                                            // if ref is outside of the containing region, it is a fake ref to collectible allocator.
                                            // dirty the MT location as if it points to the allocator object
                                            // technically it does reference the allocator, by indirection.
                                            SatoriRegion* parentRegion = o->ContainingRegion();
                                            if ((size_t)ref - parentRegion->Start() > parentRegion->Size())
                                            {
                                                ref = (SatoriObject**)o->Start();
                                            }

                                            parentRegion->ContainingPage()->DirtyCardForAddressConcurrent((size_t)ref);
                                        }
                                    }, size, start, end, /* includeCollectibleAllocator */ true);
                                }
                                o = (SatoriObject*)(o->Start() + size);
                                hint = o;
                            } while (o->Start() < objLimit);
                        }

                        _ASSERTE(deadline != 0);
                        if (SatoriUtil::GetTimeStamp() - deadline > 0)
                        {
                            // timed out, there could be more work
                            // save where we would restart if we see this page again
                            pRestart->ii = ii;
                            revisit = true;
                            return true;
                        }
                    }
                }

                // All groups/cards are accounted in this page - either visited or claimed.
                // No marking work is left here, set the ticket to indicate that.
                page->ScanTicket() = currentScanTicket;
            }

            // not a timeout, continue iterating
            return false;
        }
    );

    if (dstChunk)
    {
        m_workList->Push(dstChunk);
    }

    return revisit;
}

bool SatoriRecycler::CleanCardsConcurrent(int64_t deadline)
{
    SatoriWorkChunk* dstChunk = nullptr;
    bool revisit = false;
    const int condemnedGeneration = m_condemnedGeneration;

    // Use Gen1 count to identify the current GC. Not Gen0 as that could be changing concurrently.
    // Multiply by 2 and add 1 to not intersect with concurrent marking, which uses the same restart state.
    int64_t gcId = m_gcCount[1] * 2 + 1;
    ConcurrentCardsRestart* pRestart = &t_concurrentCardState;
    if (pRestart->gcId != gcId)
    {
        pRestart->gcId = gcId;
        pRestart->page = nullptr;
        pRestart->ii = 0;
        pRestart->rndOffset = ThreadSpecificNumber(gcId);
    }

    m_heap->ForEachPageUntil(
        [&](SatoriPage* page)
        {
            int8_t pageState = page->CardState();
            // visit and advance all non-blank pages/groups, not just dirty as that would leave remembered for nonconcurrent visit.
            // the completion criteria is all non-blank pages/groups checked for dirty, cleaned if needed and advanced to current ticket
            // Effectively we want to look at each dirty thing to have less work in blocking stage, but only once.
            // If it gets dirty again - blocking stage will have to deal with it.
            if (pageState != Satori::CardState::BLANK)
            {
                uint8_t currentScanTicket = GetCardScanTicket();
                if (page->ScanTicket() == currentScanTicket)
                {
                    // this is not a timeout, continue to next page
                    return false;
                }

                // there is unfinished page. Maybe should ask for help
                MaybeAskForHelp();

                size_t groupCount = page->CardGroupCount();

                // if restarting with the same page, continue with the last ii and offset
                size_t offset = pRestart->rndOffset;
                size_t ii = pRestart->ii;
                if (pRestart->page != page)
                {
                    // it is a new page
                    pRestart->page = page;
                    ii = 0;
                }

                while (ii < groupCount)
                {
                    size_t i = (ii + offset) % groupCount;
                    ii++;

                    int8_t groupState = page->CardGroupState(i);
                    // visit and advance all non-blank GROUPS, not just dirty as we must advance remembered as well.
                    if (groupState != Satori::CardState::BLANK)
                    {
                        uint8_t groupTicket = page->CardGroupScanTicket(i);
                        if (groupTicket == currentScanTicket)
                        {
                            continue;
                        }

                        // claim the group as complete, now it is ours
                        page->CardGroupScanTicket(i) = currentScanTicket;

                        // Unlike marking we do not have to actually clean cards or commit to finish anything.
                        // The blocking cleaner does not use tickets for completion and will deal with what we might miss here.
                        // For our completion tracking we need to ensure that all non-blank gen2 groups advance
                        // and we have just done that for this group.
                        // Other than that we would like to make a good effort at cleaning cards if the group is dirty.
                        // The race with dirtying is obviously unavoidable, but unimportant here.
                        if (groupState != Satori::CardState::DIRTY)
                        {
                            continue;
                        }

                        // NB: two threads may claim the same group and do overlapping work
                        //     that is correct, but redundant. We could claim using interlocked operation
                        //     and avoid that, but such collisions appear to be too rare to worry about.
                        //     It may be worth watching this in the future though.

                        // NB: It is safe to get a region even if region map may be changing because
                        //    a region with remembered/dirty marks must be there and cannot be destroyed.
                        SatoriRegion* region = page->RegionForCardGroup(i);
                        _ASSERTE(region->Generation() >= 0);

                        // we should not be marking when there could be dead objects
                        _ASSERTE(!region->HasMarksSet());

                        int8_t resetValue = Satori::CardState::REMEMBERED;
                        if (region->Generation() < 2)
                        {
                            // We will not wipe remembered+ephemeral here.
                            // If we are going gen2 GC, we will not care about rememebered set after this and cards will be reset in the end,
                            // otherwise the wiping must have been done already.
                            if (groupState == Satori::CardState::REMEMBERED)
                            {
                                _ASSERTE(m_condemnedGeneration == 2);
                                // we will not find anything dirty here.
                                continue;
                            }

                            if (region->MaybeAllocatingAcquire())
                            {
                                // Allocating region is not parseable. Can't do much here.
                                continue;
                            }

                            resetValue = Satori::CardState::EPHEMERAL;
                        }

                        // invariant check: when marking through cards the gen2 remset stays remset, thus should be marked through on every GC
                        // and should not fall far behind the tickets
                        _ASSERTE(region->Generation() != 2 || groupTicket == 0 || groupTicket == 0xff || (uint8_t)(currentScanTicket - groupTicket) == 1);

                        bool considerAllMarked = region->Generation() > condemnedGeneration;
                        int8_t* cards = page->CardsForGroup(i);
                        // where the previous walk in this group stopped, see FindObject
                        SatoriObject* hint = nullptr;

                        _ASSERTE(Satori::CardState::DIRTY == (int8_t)0x04);
                        const size_t dirtyBits = 0x0404040404040404;
                        for (size_t j = 0; j < Satori::CARD_BYTES_IN_CARD_GROUP; j++)
                        {
                            // cards are often sparsely set, if j is aligned, check if any card in entire size_t is dirty
                            if (((j & (sizeof(size_t) - 1)) == 0) && (*((size_t*)&cards[j]) & dirtyBits) == 0)
                            {
                                j += sizeof(size_t) - 1;
                                continue;
                            }

                            // skip nondirty cards
                            if (cards[j] != Satori::CardState::DIRTY)
                            {
                                continue;
                            }

                            size_t start = page->LocationForCard(&cards[j]);
                            do
                            {
                                // clean the card since it is going to be visited and marked through.
                                cards[j++] = resetValue;
                            } while (j < Satori::CARD_BYTES_IN_CARD_GROUP && cards[j] == Satori::CardState::DIRTY);

                            size_t end = page->LocationForCard(&cards[j]);
                            size_t objLimit = min(end, region->Start() + Satori::REGION_SIZE_GRANULARITY);

                            // do not allow card cleaning to delay until after checking IsMarked or fetching children
                            MemoryBarrier();

                            // Only marked objects need visiting, those can be found via the mark bitmap.
                            // That is much cheaper than walking objects from a known object start (FindObject),
                            // since that touches memory of every object on the way.
                            SatoriObject* o = considerAllMarked ?
                                region->FindObject(start, hint) :
                                region->FindMarkedObjectFrom(start, objLimit, hint);
                            if (o->Start() < objLimit)
                            {
                                hint = o;
                            }

                            // the lookup may find nothing in the range
                            while (o->Start() < objLimit)
                            {
                                if (!considerAllMarked)
                                {
                                    o = region->SkipUnmarked(o, objLimit);
                                    if (o->Start() == objLimit)
                                    {
                                        break;
                                    }
                                }

                                size_t size = o->Size();

                                // if (considerAllMarked || o->IsMarked())
                                {
                                    o->ForEachObjectRef(
                                        [&](SatoriObject** ref)
                                        {
                                            SatoriObject* child = VolatileLoadWithoutBarrier(ref);
                                            if (child &&
                                                !child->IsExternal())
                                            {
                                                SatoriRegion* childRegion = child->ContainingRegion();
                                                if (!childRegion->MaybeEscapeTrackingAcquire())
                                                {
                                                    if (!child->IsMarkedOrOlderThan(condemnedGeneration))
                                                    {
                                                        child->SetMarkedAtomic();
                                                        if (!dstChunk || !dstChunk->TryPush(child))
                                                        {
                                                            this->PushToMarkQueuesSlow(dstChunk, child);
                                                        }
                                                    }

                                                    return;
                                                }

                                                // cannot mark stuff in thread local regions. just mark as dirty to visit later.
                                                // if ref is outside of the containing region, it is a fake ref to collectible allocator.
                                                // dirty the MT location as if it points to the allocator object
                                                // technically it does reference the allocator, by indirection.
                                                SatoriRegion* parentRegion = o->ContainingRegion();
                                                if ((size_t)ref - parentRegion->Start() > parentRegion->Size())
                                                {
                                                    ref = (SatoriObject**)o->Start();
                                                }

                                                parentRegion->ContainingPage()->DirtyCardForAddressConcurrent((size_t)ref);
                                            }
                                        }, size, start, end, /* includeCollectibleAllocator */ true);
                                }
                                o = (SatoriObject*)(o->Start() + size);
                                hint = o;
                            }
                        }

                        _ASSERTE(deadline != 0);
                        if (SatoriUtil::GetTimeStamp() - deadline > 0)
                        {
                            // timed out, there could be more work
                            // save where we would restart if we see this page again
                            pRestart->ii = ii;
                            revisit = true;
                            return true;
                        }
                    }
                }

                // All groups/cards are accounted in this page - either visited or claimed.
                // No marking work is left here, set the ticket to indicate that.
                page->ScanTicket() = currentScanTicket;
            }

            // not a timeout, continue iterating
            return false;
        }
    );

    if (dstChunk)
    {
        m_workList->Push(dstChunk);
    }

    return revisit;
}

void SatoriRecycler::MarkThroughCards()
{
    SatoriWorkChunk* dstChunk = nullptr;

    m_heap->ForEachPage(
        [&](SatoriPage* page)
        {
            int8_t pageState = page->CardState();
            if (pageState != Satori::CardState::BLANK)
            {
                uint8_t currentScanTicket = GetCardScanTicket();
                if (page->ScanTicket() == currentScanTicket)
                {
                    // continue to next page
                    return;
                }

                // there is unfinished page. Maybe should ask for help
                MaybeAskForHelp();

                size_t groupCount = page->CardGroupCount();
                // add thread specific offset, to separate somewhat what threads read
                size_t offset = ThreadSpecificNumber(GlobalGcIndex());
                for (size_t ii = 0; ii < groupCount; ii++)
                {
                    size_t i = (offset + ii) % groupCount;
                    int8_t groupState = page->CardGroupState(i);
                    if (groupState != Satori::CardState::BLANK)
                    {
                        uint8_t groupTicket = page->CardGroupScanTicket(i);
                        if (groupTicket == currentScanTicket)
                        {
                            continue;
                        }

                        // claim the group as complete, now we have to finish
                        page->CardGroupScanTicket(i) = currentScanTicket;

                        //NB: It is safe to get a region even if region map may be changing because
                        //    a region with remembered/dirty mark must be there and cannot be destroyed.
                        SatoriRegion* region = page->RegionForCardGroup(i);
                        _ASSERTE(region->Generation() >= 0);

                        // we should not be marking when there could be dead objects
                        _ASSERTE(!region->HasMarksSet());

                        // sometimes we set cards without knowing dst generation, but REMEMBERED only has meaning in tenured
                        if (region->Generation() < 2)
                        {
                            // Wiping the group if ephemeral+remembered is an optimization.
                            // If the group is not covered by a tenured region, we wipe the group, to not look at it again in the next scans.
                            // It must use interlocked, even if not concurrent - in case it gets dirty by mark ovrflow (it can since it is < gen2)
                            // The actual cards do not matter, we will look at cards in ephemeral regions only if group is dirty,
                            // and then cleaner will reset them appropriately.
                            // There is no marking work in this region, so ticket is also irrelevant. It will be wiped if region gets promoted.
                            // As a result cards that do not belong to tenured regions may occasionally have remembered state set.
                            // It is ignorable and we are too lazy to clean that.until the region is promoted.

                            if (groupState == Satori::CardState::REMEMBERED)
                            {
                                Interlocked::CompareExchange(&page->CardGroupState(i), Satori::CardState::BLANK, Satori::CardState::REMEMBERED);
                            }

                            continue;
                        }

                        // invariant check: when marking through cards the gen2 remset stays remset, thus should be marked through on every GC
                        // and should not fall far behind the tickets
                        _ASSERTE(groupTicket == 0 || groupTicket == 0xff || (uint8_t)(currentScanTicket - groupTicket) == 1);
                        const int8_t resetValue = Satori::CardState::REMEMBERED;
                        int8_t* cards = page->CardsForGroup(i);
                        // where the previous walk in this group stopped, see FindObject
                        SatoriObject* hint = nullptr;

                        // clean the group if dirty, but must do that before reading the cards.
                        if (groupState == Satori::CardState::DIRTY)
                        {
                            Interlocked::CompareExchange(&page->CardGroupState(i), resetValue, Satori::CardState::DIRTY);
                        }

                        for (size_t j = 0; j < Satori::CARD_BYTES_IN_CARD_GROUP; j++)
                        {
                            // cards are often sparsely set, if j is aligned, check the entire size_t for 0
                            if (((j & (sizeof(size_t) - 1)) == 0) && *((size_t*)&cards[j]) == 0)
                            {
                                j += sizeof(size_t) - 1;
                                continue;
                            }

                            // skip empty cards
                            if (!cards[j])
                            {
                                continue;
                            }

                            size_t start = page->LocationForCard(&cards[j]);
                            do
                            {
                                if (cards[j] != resetValue)
                                {
                                    // clean the card since it is going to be visited and marked through.
                                    // order relative to visiting the objects is unimportant since fields are not changing
                                    cards[j] = resetValue;
                                }
                                j++;
                            } while (j < Satori::CARD_BYTES_IN_CARD_GROUP && cards[j]);

                            size_t end = page->LocationForCard(&cards[j]);
                            size_t objLimit = min(end, region->Start() + Satori::REGION_SIZE_GRANULARITY);
                            SatoriObject* o = region->FindObject(start, hint);
                            do
                            {
                                size_t size = o->Size();
                                o->ForEachObjectRef(
                                    [&](SatoriObject** ref)
                                    {
                                        SatoriObject* child = VolatileLoadWithoutBarrier(ref);
                                        if (child &&
                                            !child->IsExternal() &&
                                            !child->IsMarkedOrOlderThan(1))
                                        {
                                            child->SetMarkedAtomic();
                                            if (!dstChunk || !dstChunk->TryPush(child))
                                            {
                                                this->PushToMarkQueuesSlow(dstChunk, child);
                                            }
                                        }
                                    }, size, start, end, /* includeCollectibleAllocator */ true);
                                o = (SatoriObject*)(o->Start() + size);
                                hint = o;
                            } while (o->Start() < objLimit);
                        }
                    }
                }

                // All groups/cards are accounted in this page - either visited or claimed.
                // No updating work is left for this page, set the ticket to indicate that.
                page->ScanTicket() = currentScanTicket;
            }
        }
    );

    if (dstChunk)
    {
        m_workList->Push(dstChunk);
    }
}

bool SatoriRecycler::HasDirtyCards()
{
    bool isDirty = false;
    m_heap->ForEachPageUntil([&](SatoriPage* page) { return page->CardState() == Satori::CardState::DIRTY ? (isDirty = true) : false; });
    return isDirty;
}

// cleaning is not concurrent, but could be parallel
void SatoriRecycler::CleanCards()
{
    SatoriWorkChunk* dstChunk = nullptr;
    const int condemnedGeneration = m_condemnedGeneration;

    m_heap->ForEachPage(
        [&](SatoriPage* page)
        {
            // NOTE: we may concurrently make cards dirty due to mark stack overflow.
            // Thus the page must be checked first, then group, then cards.
            // Dirtying due to overflow will have to do writes in the opposite order. (the barrier dirtying only orders cards after assignments)
            int8_t pageState = page->CardState();
            if (pageState >= Satori::CardState::PROCESSING)
            {
                // move the page to PROCESSING state and make sure it does not reorder past reading the card group states
                pageState = Interlocked::CompareExchange(& page->CardState(), Satori::CardState::PROCESSING, Satori::CardState::DIRTY);
                if (pageState < Satori::CardState::PROCESSING)
                {
                    return;
                }

                // there is unfinished page. Maybe should ask for help
                MaybeAskForHelp();

                size_t groupCount = page->CardGroupCount();
                // add thread specific offset, to separate somewhat what threads read
                size_t offset = ThreadSpecificNumber(GlobalGcIndex());

                for (size_t ii = 0; ii < groupCount; ii++)
                {
                    size_t i = (offset + ii) % groupCount;
                    int8_t groupState = page->CardGroupState(i);
                    if (groupState == Satori::CardState::DIRTY)
                    {
                        SatoriRegion* region = page->RegionForCardGroup(i);
                        _ASSERTE(region->Generation() >= 0);

                        // clean the group, but must do that before reading the cards.
                        const int8_t groupResetValue = region->Generation() >= 2 ? Satori::CardState::REMEMBERED : Satori::CardState::BLANK;
                        if (Interlocked::CompareExchange(&page->CardGroupState(i), groupResetValue, Satori::CardState::DIRTY) != Satori::CardState::DIRTY)
                        {
                            // at this point in time the card group is no longer dirty, try the next one
                            continue;
                        }

                        bool considerAllMarked = region->Generation() > condemnedGeneration;

                        int8_t* cards = page->CardsForGroup(i);
                        // where the previous walk in this group stopped, see FindObject
                        SatoriObject* hint = nullptr;
                        const int8_t resetValue = region->Generation() >= 2 ? Satori::CardState::REMEMBERED : Satori::CardState::EPHEMERAL;

                        _ASSERTE(Satori::CardState::DIRTY == (int8_t)0x04);
                        const size_t dirtyBits = 0x0404040404040404;
                        for (size_t j = 0; j < Satori::CARD_BYTES_IN_CARD_GROUP; j++)
                        {
                            // cards are often sparsely set, if j is aligned, check if any card in entire size_t is dirty
                            if (((j & (sizeof(size_t) - 1)) == 0) && (*((size_t*)&cards[j]) & dirtyBits) == 0)
                            {
                                j += sizeof(size_t) - 1;
                                continue;
                            }

                            // skip nondirty
                            if (cards[j] != Satori::CardState::DIRTY)
                            {
                                continue;
                            }

                            size_t start = page->LocationForCard(&cards[j]);
                            do
                            {
                                // clean the card since it is going to be visited and marked through.
                                cards[j++] = resetValue;
                            } while (j < Satori::CARD_BYTES_IN_CARD_GROUP && cards[j] == Satori::CardState::DIRTY);

                            size_t end = page->LocationForCard(&cards[j]);
                            size_t objLimit = min(end, region->Start() + Satori::REGION_SIZE_GRANULARITY);

                            // do not allow card cleaning to delay until after checking IsMarked
                            if (!considerAllMarked)
                            {
                                MemoryBarrier();
                            }

                            // Only marked objects need visiting, those can be found via the mark bitmap.
                            // That is much cheaper than walking objects from a known object start (FindObject),
                            // since that touches memory of every object on the way.
                            SatoriObject* o = considerAllMarked ?
                                region->FindObject(start, hint) :
                                region->FindMarkedObjectFrom(start, objLimit, hint);
                            if (o->Start() < objLimit)
                            {
                                hint = o;
                            }

                            // the lookup may find nothing in the range
                            while (o->Start() < objLimit)
                            {
                                if (!considerAllMarked)
                                {
                                    o = region->SkipUnmarked(o, objLimit);
                                    if (o->Start() == objLimit)
                                    {
                                        break;
                                    }
                                }

                                size_t size = o->Size();

                                // if (considerAllMarked || o->IsMarked())
                                {
                                    o->ForEachObjectRef(
                                        [&](SatoriObject** ref)
                                        {
                                            SatoriObject* child = *ref;
                                            if (child &&
                                                !child->IsExternal() &&
                                                !child->IsMarkedOrOlderThan(condemnedGeneration))
                                            {
                                                child->SetMarkedAtomic();
                                                if (!dstChunk || !dstChunk->TryPush(child))
                                                {
                                                    this->PushToMarkQueuesSlow(dstChunk, child);
                                                }
                                            }
                                        }, size, start, end, /* includeCollectibleAllocator */ true);
                                }
                                o = (SatoriObject*)(o->Start() + size);
                                hint = o;
                            }
                        }
                    }
                }

                // we do not see more cleaning work so clean the page state, use interlocked in case the page went dirty while we were working on it
                int8_t origState = Interlocked::CompareExchange(&page->CardState(), Satori::CardState::REMEMBERED, Satori::CardState::PROCESSING);
                _ASSERTE(origState != Satori::CardState::BLANK);
            }
        }
    );

    if (dstChunk)
    {
        m_workList->Push(dstChunk);
    }
}

void SatoriRecycler::UpdatePointersThroughCards()
{
    SatoriWorkChunk* dstChunk = nullptr;
    m_heap->ForEachPage(
        [&](SatoriPage* page)
        {
            int8_t pageState = page->CardState();
            _ASSERTE(pageState < Satori::CardState::PROCESSING);
            if (pageState == Satori::CardState::REMEMBERED)
            {
                uint8_t currentScanTicket = GetCardScanTicket();
                if (page->ScanTicket() == currentScanTicket)
                {
                    // continue to next page
                    return;
                }

                // there is unfinished page. Maybe should ask for help
                MaybeAskForHelp();

                size_t groupCount = page->CardGroupCount();
                // add thread specific offset, to separate somewhat what threads read
                size_t offset = ThreadSpecificNumber(GlobalGcIndex());
                for (size_t ii = 0; ii < groupCount; ii++)
                {
                    size_t i = (offset + ii) % groupCount;
                    int8_t groupState = page->CardGroupState(i);
                    _ASSERTE(groupState < Satori::CardState::PROCESSING);
                    if (groupState == Satori::CardState::REMEMBERED)
                    {
                        uint8_t groupTicket = page->CardGroupScanTicket(i);
                        if (groupTicket == currentScanTicket)
                        {
                            continue;
                        }

                        // claim the group as complete, now we have to finish
                        page->CardGroupScanTicket(i) = currentScanTicket;

                        SatoriRegion* region = page->RegionForCardGroup(i);
                        _ASSERTE(region->Generation() >= 0);

                        // Wiping the group if ephemeral+remembered is an optimization,
                        // but, since we do that, we expect that it is done by now.
                        _ASSERTE(region->Generation() >= 2);

                        // invariant check: when marking/updating through cards the gen2 remset stays remset,
                        // thus should be marked through on every GC and should not fall far behind the tickets
                        _ASSERTE(groupTicket == 0 || groupTicket == 0xff || (uint8_t)(currentScanTicket - groupTicket) == 1);
                        int8_t* cards = page->CardsForGroup(i);
                        // where the previous walk in this group stopped, see FindObject
                        SatoriObject* hint = nullptr;
                        SatoriRelocatedRefQueue<8> updateQueue;
                        for (size_t j = 0; j < Satori::CARD_BYTES_IN_CARD_GROUP; j++)
                        {
                            // cards are often sparsely set, if j is aligned, check the entire size_t for 0
                            if (((j & (sizeof(size_t) - 1)) == 0) && *((size_t*)&cards[j]) == 0)
                            {
                                j += sizeof(size_t) - 1;
                                continue;
                            }

                            _ASSERTE(cards[j] < Satori::CardState::PROCESSING);
                            if (cards[j] != Satori::CardState::REMEMBERED)
                            {
                                continue;
                            }

                            size_t start = page->LocationForCard(&cards[j]);
                            do
                            {
                                _ASSERTE(cards[j] < Satori::CardState::PROCESSING);
                                // wipe the card. if the card is still valid we will set it back shortly.
                                // this is an optimization. we do not need this for correctness.
                                cards[j] = Satori::CardState::BLANK;
                                j++;
                            } while (j < Satori::CARD_BYTES_IN_CARD_GROUP && cards[j] == Satori::CardState::REMEMBERED);

                            size_t end = page->LocationForCard(&cards[j]);
                            size_t objLimit = min(end, region->Start() + Satori::REGION_SIZE_GRANULARITY);
                            SatoriObject* o = region->FindObject(start, hint);

                            // Refs are visited in address order, so refs in the same card come one after another
                            // and a card often has several that need it. Set each card once, when the first one is seen.
                            // (cards are aligned in absolute addresses, since pages are, thus address / card size
                            //  identifies the card)
                            size_t lastRememberedCard = 0;
                            do
                            {
                                size_t size = o->Size();
                                o->ForEachObjectRef(
                                    [&](SatoriObject** ppObject)
                                    {
                                        // prevent re-reading o, someone else could be doing the same update.
                                        SatoriObject* child = VolatileLoadWithoutBarrier(ppObject);
                                        // ignore common cases - child is null or in the same region, also skip externals
                                        if (child &&
                                            (((size_t)ppObject ^ (size_t)child) >= Satori::REGION_SIZE_GRANULARITY) &&
                                            !child->IsExternal())
                                        {
                                            if (child->IsRelocated</*notExternal*/true>())
                                            {
                                                updateQueue.Push(ppObject, child);
                                            }

                                            if (child->ContainingRegion()->Generation() < 2)
                                            {
                                                size_t card = (size_t)ppObject / Satori::BYTES_PER_CARD_BYTE;
                                                if (card != lastRememberedCard)
                                                {
                                                    page->SetCardForAddressOnly((size_t)ppObject);
                                                    lastRememberedCard = card;
                                                }
                                            }
                                        }
                                    },
                                    size,
                                    start,
                                    end,
                                    // The allocator is updated through its handle; its synthetic ref location
                                    // must not be used as a card address.
                                    /* includeCollectibleAllocator */ false
                                );

                                // The synthetic loader-allocator edge is remembered at the MethodTable slot.
                                if (start <= o->Start() && o->HasEphemeralCollectibleAllocator())
                                {
                                    page->SetCardForAddressOnly(o->Start());
                                }

                                o = (SatoriObject*)(o->Start() + size);
                                hint = o;
                            } while (o->Start() < objLimit);
                        }

                        updateQueue.Flush();
                    }
                }

                // All groups/cards are accounted in this page - either visited or claimed.
                // No updating work is left for this page, set the ticket to indicate that.
                page->ScanTicket() = currentScanTicket;
            }
        }
    );
}

bool SatoriRecycler::MarkHandles(int64_t deadline)
{
    ScanContext sc;
    sc.promotion = TRUE;
    sc.concurrent = !IsBlockingPhase();
    MarkContext c = MarkContext(this);
    sc._unused1 = &c;

    bool revisit = SatoriHandlePartitioner::ForEachUnscannedPartition(
        [&](int p)
        {
            // there is work for us, so maybe there is more
            MaybeAskForHelp();
            _ASSERTE(m_condemnedGeneration > 0);

            sc.thread_number = p;
            GCScan::GcScanHandles(
                IsBlockingPhase() ? MarkFn</*isConservative*/ false> : MarkFnConcurrent</*isConservative*/ false>,
                m_condemnedGeneration,
                2,
                &sc);
        },
        deadline
    );

    if (c.m_WorkChunk != nullptr)
    {
        m_workList->Push(c.m_WorkChunk);
    }

    return revisit;
}

#ifdef FEATURE_JAVAMARSHAL
void SatoriRecycler::MarkBridgeObjects()
{
    _ASSERTE(IsBlockingPhase());

    ScanContext sc;
    sc.promotion = TRUE;

    // Analyze the unreachable graph with all marking workers stopped. The bridge
    // temporarily borrows object headers and restores them before returning.
    size_t count;
    uint8_t** objects = GCScan::GcProcessBridgeObjects(m_condemnedGeneration, max_generation, &sc, &count);

    // Keep every bridge alive until the client releases its handle, including
    // when it is still processing the results of a previous collection.
    MarkContext c = MarkContext(this);
    sc._unused1 = &c;
    for (size_t i = 0; i < count; i++)
    {
        MarkFn</*isConservative*/ false>((Object**)&objects[i], &sc, 0);
    }

    if (c.m_WorkChunk != nullptr)
    {
        m_workList->Push(c.m_WorkChunk);
    }

    // Bridge roots can also make dependent-handle secondaries reachable.
    MarkNewReachable();
}
#endif

void SatoriRecycler::ShortWeakPtrScan()
{
    SatoriHandlePartitioner::StartNextScan();
    RunWithHelp(&SatoriRecycler::ShortWeakPtrScanWorker);
}

void SatoriRecycler::ShortWeakPtrScanWorker()
{
    ScanContext sc;
    sc.promotion = TRUE;
    SatoriHandlePartitioner::ForEachUnscannedPartition(
        [&](int p)
        {
            sc.thread_number = p;
            GCScan::GcShortWeakPtrScan(m_condemnedGeneration, 2, &sc);
        }
    );
}

void SatoriRecycler::LongWeakPtrScan()
{
    SatoriHandlePartitioner::StartNextScan();
    m_syncBlockCacheScanDone = 0;
    RunWithHelp(&SatoriRecycler::LongWeakPtrScanWorker);
}

void SatoriRecycler::LongWeakPtrScanWorker()
{
    ScanContext sc;
    sc.promotion = TRUE;
    SatoriHandlePartitioner::ForEachUnscannedPartition(
        [&](int p)
        {
            sc.thread_number = p;
            GCScan::GcWeakPtrScan(m_condemnedGeneration, 2, &sc);
        }
    );

    if (!m_syncBlockCacheScanDone &&
        Interlocked::CompareExchange(&m_syncBlockCacheScanDone, 1, 0) == 0)
    {
        // scan for deleted entries in the syncblk cache
        // sc is not really used, but must have "promotion == TRUE" if HeapVerify is on.
        GCScan::GcWeakPtrScanBySingleThread(m_condemnedGeneration, 2, &sc);
    }
}

void SatoriRecycler::ScanFinalizables()
{
    SatoriFinalizationQueue* queue = m_heap->FinalizationQueue();
    queue->ResetOverflow(m_condemnedGeneration);
    RunWithHelp(&SatoriRecycler::ScanAllFinalizableRegionsWorker);

    // the drains above are lock-free, restore what was actually drained
    m_ephemeralFinalizationTrackingRegions->ResetAfterUnsafeDrain();
    if (m_condemnedGeneration == 2)
    {
        m_tenuredFinalizationTrackingRegions->ResetAfterUnsafeDrain();
    }

    RunWithHelp(&SatoriRecycler::QueueCriticalFinalizablesWorker);
    m_finalizationPendingRegions->ResetAfterUnsafeDrain();

    // Marking-work allocation can fail; an empty overflowed queue also needs a finalizer retry.
    if (queue->HasItems() || queue->OverflowedGen() != 0)
    {
        GCToEEInterface::EnableFinalization(true);
    }
}

void SatoriRecycler::ScanAllFinalizableRegionsWorker()
{
    MarkContext c = MarkContext(this);

    // every scanned region gets rerouted, so stage the pushes and splice once per destination
    SatoriShardedRegionQueue::Batch pending(m_finalizationPendingRegions);
    SatoriShardedRegionQueue::Batch eph(m_ephemeralRegions);
    SatoriShardedRegionQueue::Batch ten(m_tenuredRegions);

    ScanFinalizableRegions(m_ephemeralFinalizationTrackingRegions, &c, &pending, &eph, &ten);
    _ASSERTE(m_reusableRegions->IsEmpty());
    
    if (m_condemnedGeneration == 2)
    {
        ScanFinalizableRegions(m_tenuredFinalizationTrackingRegions, &c, &pending, &eph, &ten);
    }

    m_finalizationPendingRegions->Append(&pending);
    m_ephemeralRegions->Append(&eph);
    m_tenuredRegions->Append(&ten);

    if (c.m_WorkChunk != nullptr)
    {
        m_workList->Push(c.m_WorkChunk);
    }
}

void SatoriRecycler::ScanFinalizableRegions(SatoriShardedRegionQueue* queue, MarkContext* markContext, SatoriShardedRegionQueue::Batch* pending, SatoriShardedRegionQueue::Batch* eph, SatoriShardedRegionQueue::Batch* ten)
{
    SatoriRegion* region = queue->TryPopDrainOnly();
    if (region)
    {
        MaybeAskForHelp();
        do
        {
            _ASSERTE(region->Generation() <= m_condemnedGeneration);
            bool hasCF = region->PendFinalizables(
                [&](SatoriObject* finalizable)
                {
                    // Only us look at finalizables from this region right now
                    // and no other marking is happening.
                    // No need for this to be atomic.
                    finalizable->SetMarked();
                    markContext->PushToMarkQueues(finalizable);
                },
                m_condemnedGeneration
            );

            if (hasCF)
            {
                pending->Push(region);
            }
            else
            {
                if (region->Generation() == 2)
                {
                    ten->Push(region);
                }
                else
                {
                    eph->Push(region);
                }
            }
        } while ((region = queue->TryPopDrainOnly()));
    }
}

void SatoriRecycler::QueueCriticalFinalizablesWorker()
{
    SatoriShardedRegionQueue::Batch eph(m_ephemeralRegions);
    SatoriShardedRegionQueue::Batch ten(m_tenuredRegions);

    SatoriRegion* region = m_finalizationPendingRegions->TryPopDrainOnly();
    if (region)
    {
        MaybeAskForHelp();
        do
        {
            region->PendCfFinalizables(m_condemnedGeneration);
            if (region->Generation() == 2)
            {
                ten.Push(region);
            }
            else
            {
                eph.Push(region);
            }
        } while ((region = m_finalizationPendingRegions->TryPopDrainOnly()));

        m_ephemeralRegions->Append(&eph);
        m_tenuredRegions->Append(&ten);
    }
}

void SatoriRecycler::DependentHandlesInitialScan()
{
    SatoriHandlePartitioner::StartNextScan();
    RunWithHelp(&SatoriRecycler::DependentHandlesInitialScanWorker);
}

void SatoriRecycler::DependentHandlesInitialScanWorker()
{
    ScanContext sc;
    sc.promotion = TRUE;
    MarkContext c = MarkContext(this);
    sc._unused1 = &c;

    SatoriHandlePartitioner::ForEachUnscannedPartition(
        [&](int p)
        {
            sc.thread_number = p;
            GCScan::GcDhInitialScan(MarkFn</*isConservative*/ false>, m_condemnedGeneration, 2, &sc);
        }
    );

    if (c.m_WorkChunk != nullptr)
    {
        m_workList->Push(c.m_WorkChunk);
    }
}

void SatoriRecycler::DependentHandlesRescan()
{
    SatoriHandlePartitioner::StartNextScan();
    RunWithHelp(&SatoriRecycler::DependentHandlesRescanWorker);
}

void SatoriRecycler::DependentHandlesRescanWorker()
{
    ScanContext sc;
    sc.promotion = TRUE;
    MarkContext c = MarkContext(this);
    sc._unused1 = &c;

    SatoriHandlePartitioner::ForEachUnscannedPartition(
        [&](int p)
        {
            sc.thread_number = p;
            if (GCScan::GcDhUnpromotedHandlesExist(&sc))
            {
                GCScan::GcDhReScan(&sc);
            }
        }
    );

    if (c.m_WorkChunk != nullptr)
    {
        m_workList->Push(c.m_WorkChunk);
    }
}

void SatoriRecycler::PromoteHandlesAndFreeRelocatedRegions()
{
    // NB: we may promote some objects in gen1 gc, but it is ok if handles stay young.
    //     promote handles only after en-masse promotion.
    if (m_promoteAllRegions)
    {
        SatoriHandlePartitioner::StartNextScan();
    }

    RunWithHelp(&SatoriRecycler::PromoteSurvivedHandlesAndFreeRelocatedRegionsWorker);

    // the relocated drain above is lock-free
    m_relocatedRegions->ResetAfterUnsafeDrain();
}

void SatoriRecycler::PromoteSurvivedHandlesAndFreeRelocatedRegionsWorker()
{
    if (m_promoteAllRegions)
    {
        ScanContext sc;
        sc.promotion = TRUE;
        // no need for scan context. we do not create more work here.
        sc._unused1 = nullptr;

        SatoriHandlePartitioner::ForEachUnscannedPartition(
            [&](int p)
            {
                sc.thread_number = p;
                // promote gen1 handles
                GCScan::GcPromotionsGranted(1, 2, &sc);
            }
        );
    }

    FreeRelocatedRegionsWorker();
}

void SatoriRecycler::FreeLogicallyEmptyRegion(SatoriRegion* curRegion, bool hasMarks)
{
    // Blank and return an unoccupied region.
    // The biggest cost here could be clearing marks, which is often is not needed
    // as logically empty regions would have nothing marked. (one exception: evacuated regions)
    // The rest is a fairly relatively cheap wiping of metadata fields.
    // Thus we do not defer freeing - once we know a region is empty, we free it.

    _ASSERTE(!curRegion->HasPinnedObjects());

    // NOTE: MakeBlank will assert that nothing is marked.
    if (hasMarks)
    {
        curRegion->ClearMarks();
    }

    bool isNurseryRegion = curRegion->IsAttachedToAllocatingOwner();
    if (isNurseryRegion)
    {
        curRegion->DetachFromAlocatingOwnerRelease();
    }

    curRegion->MakeBlank();
    // GC work-chunk allocations and the non-suspendable trimmer can use allocator queues even during blocking GC.
    m_heap->Allocator()->ReturnRegion(curRegion);
}

void SatoriRecycler::FreeRelocatedRegionsWorker()
{
    SatoriRegion* curRegion = m_relocatedRegions->TryPopDrainOnly();
    if (curRegion)
    {
        MaybeAskForHelp();
        do
        {
            FreeLogicallyEmptyRegion(curRegion, /* hasMarks */ true);
        } while ((curRegion = m_relocatedRegions->TryPopDrainOnly()));
    }
}

NOINLINE
void SatoriRecycler::Plan()
{
    _ASSERTE(m_ephemeralFinalizationTrackingRegions->IsEmpty());
    if (m_condemnedGeneration == 2)
    {
        _ASSERTE(m_tenuredFinalizationTrackingRegions->IsEmpty());
    }

#if _DEBUG
    auto setHasMarksFn = [&](SatoriRegion* region)
    {
        _ASSERTE(!region->HasMarksSet());
        region->HasMarksSet() = true;
    };

    // we have just marked all condemened generations
    m_ephemeralRegions->ForEachRegion(setHasMarksFn);
    if (m_condemnedGeneration == 2)
    {
        m_tenuredRegions->ForEachRegion(setHasMarksFn);
    }
#endif

    // Gen1 allocations are accounted for by every blocking collection.
    size_t allocatedInCycle = m_gen1AddedSinceLastCollection;
    m_gen1AddedSinceLastCollection = 0;

    // The budget says when a GC is requested, not how much is allocated before it completes.
    // Include what mutators allocated past the previous budget while waiting for GcRate and concurrent marking.
    size_t overrun = allocatedInCycle > m_prevGen1Budget ? allocatedInCycle - m_prevGen1Budget : 0;
    m_gen1OverrunPerCycle = max(overrun, (m_gen1OverrunPerCycle * 7 + overrun) / 8);
    size_t expectedGen1 = min(m_availableMemory, m_gen1Budget + m_gen1OverrunPerCycle);
    m_reusableLimit = (int64_t)(expectedGen1 / 100 * m_reusableTargetPercent);

    size_t estimatedReclaim = m_estimatedEphemeralReclaim;
    if (m_condemnedGeneration == 2)
    {
        estimatedReclaim += m_estimatedTenuredReclaim;
    }

    // gen1 is always rebuilt, clear counters
    m_occupancyAcc[1] = 0;
    m_estimatedEphemeralReclaim = 0;

    if (m_promoteAllRegions)
    {
        // Direct Gen2 allocations enter occupancy when Gen2 is rebuilt.
        m_gen2AddedSinceLastCollection = 0;
        m_occupancyAcc[2] = 0;
        m_estimatedTenuredReclaim = 0;
        m_demotedOccupancyAcc = 0;
    }

    // If we do relocation, we are committed to do pointer updates.
    // At an extreme we do not want to relocate one region
    // and then go through 100 regions and update pointers.
    //
    // As crude criteria, we will do relocations if at least some fraction
    // of condemned regions wants to participate. And at least 2 total.
    // And an average region would yield 1/2 region free space.
    // NOTE: Regions must be no more than 1/2 full to be considered candidates,
    //       but for presweepable we only guess that they would have that much.
    //       After presweep we may find otherwise. Too many mispredicted cases
    //       could add up to denying relocation after planning. That is not a
    //       problem, just some regions would be swept in STW and the rest
    //       concurrently. It is actually when we compact, then presweeping is
    //       a bit of a waste if we end up relocating the swept region.
    //       We pay presweep price for the quality of relocation. Relocation
    //       is not cheap, so better be effective.
    size_t desiredReclaim = 0;
    // forced compaction relocates whatever can be relocated.
    if (!m_forceCompact && !SatoriUtil::IsForceCompact())
    {
        // Note: this is relative to region count not occupancy,
        //       since relocation has no effect on occipancy.
        if (m_condemnedGeneration == 2)
        {
            // gen2 free space may hang around for a while, so we compact if can reclaim ~1/6 of space
            desiredReclaim = (m_condemnedRegionsCount / 3 + 2) * Satori::REGION_SIZE_GRANULARITY / 2;
        }
        else
        {
            // gen1 can reuse most of free space, so compact only if ~1/2 can be reclaimed
            desiredReclaim = (m_condemnedRegionsCount / 1 + 2) * Satori::REGION_SIZE_GRANULARITY / 2;
        }
    }

    if (m_isRelocating == false || estimatedReclaim < desiredReclaim)
    {
        DenyRelocation();
        return;
    }

    // plan relocations
    RunWithHelp(&SatoriRecycler::PlanWorker);

    // the drains above are lock-free, restore what was actually drained
    m_ephemeralRegions->ResetAfterUnsafeDrain();
    if (m_condemnedGeneration == 2)
    {
        m_tenuredRegions->ResetAfterUnsafeDrain();
    }

    // The actual relocatable number could be less than the estimate due to pinning
    // or if this is gen1, which can reuse more.
    // Check again if it we are still meeting the relocation criteria.
    size_t relocatableActual = m_estimatedEphemeralReclaim;
    m_estimatedEphemeralReclaim = 0;

    // This can happen when we are too optimistic about preswept candidates
    // and they did not result in 1/2 reclaim as we thought.
    // We could have skipped presweeping, if we could know.
    //    _ASSERTE(relocatableActual <= relocatableEstimate);
    if (relocatableActual <= desiredReclaim)
    {
        m_stayingRegions->AppendUnsafe(m_relocatingRegions);
        DenyRelocation();
    }
}

void SatoriRecycler::DenyRelocation()
{
    m_isRelocating = false;

    // put all affected regions into staying queue
    if (m_promoteAllRegions)
    {
        m_stayingRegions->AppendUnsafe(m_ephemeralRegions);
        m_stayingRegions->AppendUnsafe(m_tenuredRegions);
        m_stayingRegions->AppendUnsafe(m_tenuredFinalizationTrackingRegions);
    }
    else
    {
        m_stayingRegions->AppendUnsafe(m_ephemeralRegions);
    }
}

void SatoriRecycler::PlanWorker()
{
    PlanRegions(m_ephemeralRegions);

    if (m_condemnedGeneration == 2)
    {
        PlanRegions(m_tenuredRegions);
    }
}

void SatoriRecycler::PlanRegions(SatoriShardedRegionQueue* regions)
{
    _ASSERTE(m_isRelocating);

    SatoriRegion* curRegion = regions->TryPopDrainOnly();
    if (curRegion)
    {
        MaybeAskForHelp();

        do
        {
            _ASSERTE(curRegion->Generation() <= m_condemnedGeneration);

            if (curRegion->IsPreSweepCandidate(m_condemnedGeneration == 2))
            {
                curRegion->PreSweep();
            }

            if (curRegion->Occupancy() == 0)
            {
                FreeLogicallyEmptyRegion(curRegion, /* hasMarks */ false);
            }
            // select relocation candidates and relocation targets according to sizes.
            else
            {
                size_t reclaim = curRegion->ReclaimSizeIfRelocated(m_promoteAllRegions);
                if (reclaim)
                {
                    // we use m_estimatedEphemeralReclaim to accumulate current estimate
                    // which is likely to come up with a better estimate if we did any presweeping.
                    Interlocked::ExchangeAdd64(&m_estimatedEphemeralReclaim, reclaim);

                    // When relocating, we want to start with larger regions
                    // They are harder to fit and if cannot fit, we get extra targets.
                    // It is better to get extra targets early, to have more ways to use them.
                    if (curRegion->Occupancy() > Satori::REGION_SIZE_GRANULARITY * 2 / 5)
                    {
                        m_relocatingRegions->Push(curRegion);
                    }
                    else
                    {
                        m_relocatingRegions->Enqueue(curRegion);
                    }

                    continue;
                }
                else
                {
                    // no point to relocate
                    AddRelocationTarget(curRegion);
                }
            }
        } while ((curRegion = regions->TryPopDrainOnly()));
    }
}

void SatoriRecycler::AddRelocationTarget(SatoriRegion* region)
{
    int maxFreeBucket = region->GetMaxFreeBucket();
    if (maxFreeBucket < 0)
    {
        m_stayingRegions->Push(region);
    }
    else
    {
        _ASSERTE(maxFreeBucket < Satori::FREELIST_COUNT);
        // within the same bucket, we'd prefer to fill up pinned ones first
        if (region->HasPinnedObjects())
        {
            m_relocationTargets[maxFreeBucket]->Push(region);
        }
        else
        {
            m_relocationTargets[maxFreeBucket]->Enqueue(region);
        }
    }
}

SatoriRegion* SatoriRecycler::GetOrAddRelocationTarget(SatoriRegion* region, size_t allocSize)
{
    int maxFreeBucket = region->GetMaxFreeBucket();
    _ASSERTE(maxFreeBucket >= 0);
    _ASSERTE(maxFreeBucket < Satori::FREELIST_COUNT);
    _ASSERTE(!region->HasPinnedObjects());

    SatoriRegion* result = m_relocationTargets[maxFreeBucket]->PopOrPush(region);
    if (result)
    {
        size_t allocStart = result->StartAllocating(allocSize);
        _ASSERTE(allocStart);
    }

    return result;
}

SatoriRegion* SatoriRecycler::TryGetRelocationTarget(size_t allocSize, bool existingRegionOnly)
{
    //make this occasionally fail in debug to be sure we can handle low memory case.
#if _DEBUG
    if (allocSize % 1024 == 0)
    {
        return nullptr;
    }
#endif

    int32_t bucket = SatoriUtil::BucketForAlloc(allocSize);
    _ASSERTE(bucket >= 0);

    // Note: If we ever want to relocate more than half region (to force compaction or whatever),
    // we will not find any suitable buckets.
    // That is ok, it will have to be to a fresh region, which will always fit.
    for (; bucket < Satori::FREELIST_COUNT; bucket++)
    {
        SatoriShardedRegionQueue* queue = m_relocationTargets[bucket];
        if (queue)
        {
            SatoriRegion* region = queue->TryPop();
            if (region)
            {
                size_t allocStart = region->StartAllocating(allocSize);
                _ASSERTE(allocStart);
                return region;
            }
        }
    }

    if (existingRegionOnly)
    {
        return nullptr;
    }

    _ASSERTE(SatoriRegion::RegionSizeForAlloc(allocSize) == Satori::REGION_SIZE_GRANULARITY);
    SatoriRegion* newRegion = m_heap->Allocator()->GetRegion(Satori::REGION_SIZE_GRANULARITY);
    if (newRegion)
    {
        newRegion->SetGeneration(m_condemnedGeneration);
        newRegion->DoNotSweep() = true;
        if (newRegion->Generation() == 2)
        {
            newRegion->RearmCardsForTenured();
        }
    }

    return newRegion;
}

void SatoriRecycler::Relocate()
{
    if (m_isRelocating)
    {
        RunWithHelp(&SatoriRecycler::RelocateWorker);

        // the drains above are lock-free, restore what was actually drained
        m_relocatingRegions->ResetAfterUnsafeDrain();
        if (m_condemnedGeneration != 2 && m_promoteAllRegions)
        {
            m_tenuredRegions->ResetAfterUnsafeDrain();
            m_tenuredFinalizationTrackingRegions->ResetAfterUnsafeDrain();
        }
    }
}

void SatoriRecycler::AddTenuredRegionsToPlan(SatoriShardedRegionQueue* regions)
{
    _ASSERTE(m_isRelocating);

    SatoriRegion* curRegion = regions->TryPopDrainOnly();
    if (curRegion)
    {
        MaybeAskForHelp();
        do
        {
            // we mark only condemened generations
            _ASSERTE(!curRegion->HasMarksSet());
            // condemned regions should go through Plan
            _ASSERTE(curRegion->Generation() > m_condemnedGeneration);
            AddRelocationTarget(curRegion);
        } while ((curRegion = regions->TryPopDrainOnly()));
    }
}

void SatoriRecycler::RelocateWorker()
{
    _ASSERTE(m_isRelocating);

    if (m_condemnedGeneration != 2 && m_promoteAllRegions)
    {
        AddTenuredRegionsToPlan(m_tenuredRegions);
        AddTenuredRegionsToPlan(m_tenuredFinalizationTrackingRegions);
    }

    SatoriRegion* curRegion = m_relocatingRegions->TryPopDrainOnly();
    if (curRegion)
    {
        MaybeAskForHelp();
        do
        {
            RelocateRegion(curRegion);
        } while ((curRegion = m_relocatingRegions->TryPopDrainOnly()));
    }
}

void SatoriRecycler::RelocateRegion(SatoriRegion* relocationSource)
{
    // the region must be in after marking and before sweeping state
    _ASSERTE(relocationSource->HasMarksSet());
    // TODO: VS we could relocate demoted if next GC is Full GC,
    //       but would need to free the gen2 trackers.
    //       Also see ReclaimSizeIfRelocated, which assumes these do not relocate.
    //       Think if that is worth the trouble.
    _ASSERTE(!relocationSource->IsDemoted());

    relocationSource->Verify(true);

    size_t maxBytesToCopy = relocationSource->Occupancy();

    // If the region could be used as a target for itself,
    // there could be more like this, do not ask for a free region.
    // We would rather make this one a target of relocations.
    bool existingRegionOnly = relocationSource->GetMaxFreeBucket() >= SatoriUtil::BucketForAlloc(maxBytesToCopy);
    SatoriRegion* relocationTarget = TryGetRelocationTarget(maxBytesToCopy, existingRegionOnly);

    if (!relocationTarget)
    {
        if (existingRegionOnly)
        {
            // we did not found a target, but the region could fit into its own free.
            // we will make it a target instead.
            // however, there could be be more like this, and we do not want all become targets,
            // so we use GetOrAdd.
            relocationTarget = GetOrAddRelocationTarget(relocationSource, maxBytesToCopy);
            if (!relocationTarget)
            {
                return;
            }
        }
        else
        {
            // we could not get a region. we must be low on available memory.
            // we can still try using the source region as a target for other relocations.
            AddRelocationTarget(relocationSource);
            return;
        }
    }

    // allocate space for relocated objects
    size_t dst = relocationTarget->Allocate(maxBytesToCopy, /*zeroInitialize*/ false);
    if (dst == 0)
    {
        // Restore the target's free span and keep the source intact for sweeping.
        relocationTarget->StopAllocating();
        AddRelocationTarget(relocationTarget);
        AddRelocationTarget(relocationSource);
        return;
    }

    // transfer finalization trackers only after the destination is secured
    relocationTarget->TakeFinalizerInfoFrom(relocationSource);

    // actually relocate src objects into the allocated space.
    size_t dstOrig = dst;
    size_t objLimit = relocationSource->Start() + Satori::REGION_SIZE_GRANULARITY;
    SatoriObject* o = relocationSource->FirstObject();

    // the target is typically marked, unless it is freshly allocated or in a higher generation
    // preserve marked state by copying marks
    bool relocationIsPromotion = relocationTarget->Generation() > m_condemnedGeneration;
    bool needToCopyMarks = !(relocationIsPromotion || relocationTarget->DoNotSweep());
    _ASSERTE(relocationTarget->HasMarksSet() == needToCopyMarks);

    int32_t objectsRelocated = 0;
    SatoriRegion::SweepPrefetcher prefetcher;
    relocationSource->SweepPrefetchStart(prefetcher, o);
    do
    {
        o = relocationSource->SkipUnmarked(o);
        if (o->Start() >= objLimit)
        {
            _ASSERTE(o->Start() == objLimit);
            break;
        }

        _ASSERTE(o->IsMarked());
        _ASSERTE(!o->IsFree());
        _ASSERTE(!o->IsUnmovable());
        relocationSource->SweepPrefetchAdvance(prefetcher, o);

        size_t size = o->Size();
        memcpy((void*)(dst - sizeof(size_t)), (void*)(o->Start() - sizeof(size_t)), size);
        // record the new location of the object by storing it in the syncblock space.
        *((size_t*)o - 1) = dst;

        if (needToCopyMarks)
        {
            ((SatoriObject*)dst)->SetMarked();
        }

        relocationTarget->SetIndicesForObject((SatoriObject*)dst, dst + size);

        dst += size;
        objectsRelocated++;
        o = (SatoriObject*)(o->Start() + size);
    } while (o->Start() < objLimit);

    size_t used = dst - dstOrig;
    // we have new marks, but have not swept at this point, so could use less.
    _ASSERTE(used <= maxBytesToCopy);

    relocationTarget->StopAllocating(dst);

    // just update the obj count, if target is not marked this will be correct
    // otherwise sweep will update anyways.
    relocationTarget->SetOccupancy(relocationTarget->Occupancy(), relocationTarget->ObjCount() + objectsRelocated);

    // the target may yet have more space and be a target for more relocations.
    AddRelocationTarget(relocationTarget);

    if (objectsRelocated > 0)
    {
        relocationSource->IsRelocated() = true;
        if (relocationIsPromotion)
        {
            // Targets in a higher generation are added to the plan only when all regions are promoted.
            // NB: UpdatePointersInPromotedObjects relies on that and does not remember the loader allocator edge
            //     of collectible objects - the allocator is promoted as well.
            _ASSERTE(m_promoteAllRegions);
            relocationTarget->AcceptedPromotedObjects() = true;
            m_relocatedToHigherGenRegions->Push(relocationSource);
        }
        else
        {
            m_relocatedRegions->Push(relocationSource);
        }
    }
    else
    {
        // relocationSource happened to be empty
        FreeLogicallyEmptyRegion(relocationSource, /* hasMarks */ false);
    }
}

NOINLINE
void SatoriRecycler::Update()
{
    // if we ended up not moving anything, this is no longer a relocating GC.
    m_isRelocating = m_relocatedRegions->Count() > 0 ||
        m_relocatedToHigherGenRegions->Count() > 0;

    m_CurrentGcInfo->m_compaction = m_isRelocating;

    if (m_isRelocating)
    {
        IncrementRootScanTicket();
        SatoriHandlePartitioner::StartNextScan();
        if (m_condemnedGeneration != 2)
        {
            IncrementCardScanTicket();
        }

        m_workerGate->WakeAll();
    }

    RunWithHelp(&SatoriRecycler::UpdateRootsWorker);

    // the promoted-region drain above is lock-free
    m_relocatedToHigherGenRegions->ResetAfterUnsafeDrain();

    // regions must me updated 
    // after updating through cards since region update may change generations

    _ASSERTE(m_ephemeralRegions->IsEmpty());
    _ASSERTE(m_ephemeralFinalizationTrackingRegions->IsEmpty());

    // The following must be reset in Plan, just want to be sure
    // it did not change since then.
    // We will be rebuilding these counters, possibly in deferred sweep.
    _ASSERTE(m_occupancyAcc[0] == 0);
    _ASSERTE(m_occupancyAcc[1] == 0);
    _ASSERTE(m_estimatedEphemeralReclaim == 0);
    _ASSERTE(m_gen1AddedSinceLastCollection == 0);

    _ASSERTE(m_promoteAllRegions || m_condemnedGeneration != 2);
    if (m_promoteAllRegions)
    {
        _ASSERTE(m_tenuredRegions->IsEmpty());
        _ASSERTE(m_tenuredFinalizationTrackingRegions->IsEmpty());
        _ASSERTE(m_gen2AddedSinceLastCollection == 0);
        _ASSERTE(m_occupancyAcc[2] == 0);
        _ASSERTE(m_demotedOccupancyAcc == 0);
        _ASSERTE(m_estimatedTenuredReclaim == 0);
    }

    // enable accumulating and reporting
    m_occupancyReportingEnabled = true;

    RunWithHelp(&SatoriRecycler::UpdateRegionsWorker);

    // the drain above leaves tails stale, the queues are reusable only after this
    for (int i = 0; i < Satori::FREELIST_COUNT; i++)
    {
        m_relocationTargets[i]->ResetAfterUnsafeDrain();
    }

    m_stayingRegions->ResetAfterUnsafeDrain();

    // promoting handles must be after handles are updated (since update needs to know unpromoted generations).
    // relocated regions can be freed after live regions are updated (since update gets new locations from relocated regions).
    // we will combine these two passes here after both prerequisites are complete.
    PromoteHandlesAndFreeRelocatedRegions();

    if (m_promoteAllRegions)
    {
        // the following must be after cards are reset
        m_heap->ForEachPage(
            [](SatoriPage* page)
            {
                page->CardState() = Satori::CardState::BLANK;
                page->ScanTicket() = 0;
            }
        );
    }

}

void SatoriRecycler::UpdateRootsWorker()
{
    if (m_isRelocating)
    {
        ScanContext sc;
        sc.promotion = FALSE;
        MarkContext c = MarkContext(this);
        sc._unused1 = &c;

        SatoriHandlePartitioner::ForEachUnscannedPartition(
            [&](int p)
            {
                // there is work for us, so maybe there is more
                MaybeAskForHelp();

                sc.thread_number = p;
                GCScan::GcScanHandles(UpdateFn</*isConservative*/ false>, m_condemnedGeneration, 2, &sc);
            }
        );

        if (SatoriUtil::IsConservativeMode())
            //generations are meaningless here, so we pass -1
            GCToEEInterface::GcScanRoots(UpdateFn<true>, -1, -1, &sc);
        else
            GCToEEInterface::GcScanRoots(UpdateFn<false>, -1, -1, &sc);

        SatoriFinalizationQueue* fQueue = m_heap->FinalizationQueue();
        if (fQueue->TryUpdateScanTicket(this->GetRootScanTicket()) &&
            fQueue->HasItems())
        {
            fQueue->ForEachObjectRef(
                [&](SatoriObject** ppObject)
                {
                    SatoriObject* o = *ppObject;
                    SatoriObject* newLocation;
                    if (o->IsRelocatedTo</*notExternal*/true>(&newLocation))
                    {
                        *ppObject = newLocation;
                    }
                }
            );
        }

        _ASSERTE(c.m_WorkChunk == nullptr);

        UpdatePointersInPromotedObjects();

        if (m_condemnedGeneration != 2)
        {
            UpdatePointersThroughCards();
        }
   }
}

void SatoriRecycler::UpdateRegionsWorker()
{
    // nearly every region here ends up in the deferred sweep queue, so stage the pushes
    // off to the side and splice them in once rather than taking its lock per region.
    SatoriShardedRegionQueue::Batch deferredFirst(m_deferredSweepRegions, /* prepend */ true);
    SatoriShardedRegionQueue::Batch deferredRest(m_deferredSweepRegions);

    // update and return target regions
    for (int i = 0; i < Satori::FREELIST_COUNT; i++)
    {
        UpdateRegions(m_relocationTargets[i], &deferredFirst, &deferredRest);
    }

    // update and return staying regions
    UpdateRegions(m_stayingRegions, &deferredFirst, &deferredRest);

    // reuse candidates are swept first, so they go in at the head
    m_deferredSweepRegions->Append(&deferredRest);
    m_deferredSweepRegions->Prepend(&deferredFirst);

    // if we saw large objects we may have ranges to update
    if (!m_workList->IsEmpty())
    {
        UpdatePointersInObjectRanges();
    }
}

void SatoriRecycler::UpdatePointersInObjectRanges()
{
    SatoriWorkChunk* srcChunk = m_workList->TryPop();
    if (srcChunk)
    {
        MaybeAskForHelp();
        SatoriRelocatedRefQueue<8> updateQueue;
        do
        {
            _ASSERTE(srcChunk->IsRange());
            SatoriObject* o;
            size_t start, end;
            srcChunk = TakeObjectRange(srcChunk, o, start, end, Satori::MARK_RANGE_THRESHOLD);
            if (srcChunk)
            {
                m_heap->Allocator()->ReturnWorkChunk(srcChunk);
            }

            // update children in the range
            o->ForEachObjectRef(
                [&updateQueue](SatoriObject** ppObject)
                {
                    SatoriObject* child = *ppObject;
                    // ignore common cases - child is null or in the same region
                    if (child &&
                        (((size_t)ppObject ^ (size_t)child) >= Satori::REGION_SIZE_GRANULARITY) &&
                        child->IsRelocated())
                    {
                        updateQueue.Push(ppObject, child);
                    }
                },
                o->Size(),
                start,
                end,
                /* includeCollectibleAllocator */ false);
        } while ((srcChunk = m_workList->TryPop()));

        updateQueue.Flush();
    }
}

void SatoriRecycler::UpdatePointersInPromotedObjects()
{
    SatoriRegion* curRegion;
    while ((curRegion = m_relocatedToHigherGenRegions->TryPopDrainOnly()))
    {
        m_promoteAllRegions ?
            curRegion->UpdatePointersInPromotedObjects<true>():
            curRegion->UpdatePointersInPromotedObjects<false>();

        m_relocatedRegions->Push(curRegion);
    }
}

void SatoriRecycler::UpdateRegions(SatoriShardedRegionQueue* queue, SatoriShardedRegionQueue::Batch* deferredFirst, SatoriShardedRegionQueue::Batch* deferredRest)
{
    // These queues are filled in Plan and Relocate and only drained here, so the pop needs
    // no lock. ResetAfterUnsafeDrain below puts them back into a usable state.
    SatoriRegion* curRegion = queue->TryPopDrainOnly();
    if (curRegion)
    {
        MaybeAskForHelp();
        do
        {
            if (!m_promoteAllRegions && curRegion->IsPromotionCandidate())
            {
                RecordDemotedOccupancy(-(ptrdiff_t)curRegion->DemotedOccupancy());
                curRegion->IndividuallyPromote();
            }

            if (curRegion->IsPreSwept())
            {
                _ASSERTE (curRegion->Generation() <= m_condemnedGeneration);
                curRegion->FinishSweepForPreSwept();
            }

            if (curRegion->Generation() > m_condemnedGeneration &&
                !curRegion->IndividuallyPromoted())
            {
                // this can only happen when promoting in gen1
                _ASSERTE(m_promoteAllRegions);
                curRegion->DoNotSweep() = true;
                if (curRegion->AcceptedPromotedObjects())
                {
                    curRegion->UpdateFinalizableTrackers();
                    curRegion->AcceptedPromotedObjects() = false;
                }
            }
            else if (m_isRelocating)
            {
                // this can happen when relocation adds a region.
                if (curRegion->DoNotSweep())
                {
                    curRegion->UpdatePointers();
                }
                else
                {
                    if (!curRegion->Sweep</*updatePointers*/ true>())
                    {
                        FreeLogicallyEmptyRegion(curRegion, /* hasMarks */ false);
                        continue;
                    }
                }

                curRegion->UpdateFinalizableTrackers();
            }

            // recycler owns nursery regions only temporarily, we should not keep them.
            if (curRegion->IsAttachedToAllocatingOwner())
            {
                // when promoting, all nursery regions should be detached
                _ASSERTE(!m_promoteAllRegions);
                if (!m_isRelocating)
                {
                    curRegion->Sweep</*updatePointers*/ false>();
                }

                if (curRegion->Occupancy() == 0)
                {
                    FreeLogicallyEmptyRegion(curRegion, /* hasMarks */ false);
                }
                else
                {
                    RecordOccupancy(curRegion->Generation(), curRegion->Occupancy());
                    curRegion->DoNotSweep() = false;
                }

                continue;
            }

            if (m_promoteAllRegions)
            {
                if (curRegion->Generation() == 2)
                {
                    curRegion->RearmCardsForStillTenured();
                }
                else
                {
                    curRegion->SetGeneration(2);
                    curRegion->RearmCardsForTenured();
                }
            }

            // make sure the region is swept and returned now, or later.
            // Forced blocking and compaction requests leave the heap swept even if nothing could be relocated.
            if (!SatoriUtil::IsConcurrentEnabled() || m_forceCompact || m_forceBlocking)
            {
                SweepAndReturnRegion(curRegion);
                continue;
            }

            // this happens when relocation allocates a region or promotes.         
            if (curRegion->DoNotSweep() && curRegion->Occupancy() > 0)
            {
                // no point to defer these, we will not do anything more with them
                KeepRegion(curRegion);
                continue;
            }

            // these we can sweep/return later
            if (curRegion->IsReuseCandidate())
            {
                deferredFirst->Push(curRegion);
            }
            else
            {
                deferredRest->Enqueue(curRegion);
            }
        } while ((curRegion = queue->TryPopDrainOnly()));
    }
}

// Decides if a swept region should be parked for reuse rather than returned to the
// ephemeral queues. Reusables stay usable until a blocking GC starts and drains them,
// so parking more than the mutator can consume before then only adds to that drain.
bool SatoriRecycler::ShouldReuse(SatoriRegion* curRegion)
{
    size_t freeSpace = curRegion->FreeSpaceInTopNBuckets(Satori::FREELIST_COUNT);
    return Interlocked::ExchangeAdd64(&m_reusableLimit, -(int64_t)freeSpace) > 0;
}

void SatoriRecycler::KeepRegion(SatoriRegion* curRegion)
{
    _ASSERTE(curRegion->Occupancy() > 0);
    _ASSERTE(curRegion->Generation() > 0);

    curRegion->DoNotSweep() = false;
    curRegion->ReusableFor() = SatoriRegion::ReuseLevel::None;
    if (curRegion->IsReuseCandidate())
    {
        _ASSERTE(curRegion->Size() == Satori::REGION_SIZE_GRANULARITY);
        if ((curRegion->Generation() == 2) && curRegion->TryDemote(m_nextGcIsFullGc))
        {
            RecordDemotedOccupancy(curRegion->DemotedOccupancy());
        }

        if (curRegion->Generation() == 1 && ShouldReuse(curRegion))
        {
#if _DEBUG
            // just split 50%/50% for testing purposes.
            curRegion->ReusableFor() = curRegion->ObjCount() % 2 == 0 ?
                SatoriRegion::ReuseLevel::Gen0 :
                SatoriRegion::ReuseLevel::Gen1;

#else
            // TUNING: heuristic for gen0
            //         the cost here is inability to trace concurrently at all.
            curRegion->ReusableFor() = curRegion->Occupancy() < (Satori::MAX_ESCAPE_SIZE / 4)?
                                            SatoriRegion::ReuseLevel::Gen0 :
                                            SatoriRegion::ReuseLevel::Gen1;
#endif
            // Demoted regions can be gen0 candidates too. When a region starts escape tracking,
            // all its objects, including the demoted ones, are escaped, so thread-local GC keeps
            // them alive and in place.
            // We can't mark in gen0 candidates concurrently, but demoted objects must be marked as roots,
            // so when concurrent marking starts, it switches demoted gen0 candidates to gen1.
            // See MarkDemotedInReusable.
            // NB: sweeping completes before concurrent marking starts, so no new candidates appear after that.
            if (!SatoriUtil::IsGen0Enabled())
            {
                curRegion->ReusableFor() = SatoriRegion::ReuseLevel::Gen1;
            }
        }
    }

    //
    // finally return the region back into recycler queues
    //

    RecordOccupancy(curRegion->Generation(), curRegion->Occupancy());
    if (curRegion->Generation() >= 2)
    {
        PushToTenuredQueues(curRegion);
    }
    else
    {
        if (curRegion->IsReusable())
        {
            _ASSERTE(curRegion->Size() <= Satori::REGION_SIZE_GRANULARITY);
            if (curRegion->HasFreeSpaceInTopBucket())
            {
                m_reusableRegions->Enqueue(curRegion);
            }
            else
            {
                m_reusableRegions->Push(curRegion);
            }
        }
        else
        {
            PushToEphemeralQueues(curRegion);
        }
    }
}

void SatoriRecycler::DrainDeferredSweepQueue()
{
    SatoriRegion* curDeferredRegion = m_deferredSweepRegions->TryPopDrainOnly();
    if (curDeferredRegion)
    {
        MaybeAskForHelp();
        do
        {
            SweepAndReturnRegion(curDeferredRegion);
            Interlocked::Decrement(&m_deferredSweepCount);
        } while ((curDeferredRegion = m_deferredSweepRegions->TryPopDrainOnly()));
    }

    if (SatoriUtil::IsConservativeMode())
    {
        m_trimmer->WaitForStop();
    }
}

// EE is stopped and sweeping is done, so nothing adds to the reusable queue here
// and these are just regular gen1 regions now. Runs on one thread - see the call site.
// NOTE: the routing below is a batched PushToEphemeralQueues.
//       Changes here likely need to be mirrored there.
void SatoriRecycler::DrainReusableQueue()
{
    _ASSERTE(IsBlockingPhase());

    // sort into local batches and counters and publish each destination in one splice,
    // rather than taking a queue lock per region.
    SatoriShardedRegionQueue::Batch demotedLocal(m_ephemeralWithUnmarkedDemoted);
    SatoriShardedRegionQueue::Batch finalizationTrackingLocal(m_ephemeralFinalizationTrackingRegions);
    SatoriShardedRegionQueue::Batch ephemeralLocal(m_ephemeralRegions);
    size_t estimatedReclaim = 0;
    size_t promotionEstimate = 0;

    SatoriRegion* curReusableRegion = m_reusableRegions->TryPopUnsafe();
    while (curReusableRegion)
    {
        curReusableRegion->ReusableFor() = SatoriRegion::ReuseLevel::None;
        if (curReusableRegion->HasUnmarkedDemotedObjects())
        {
            demotedLocal.Push(curReusableRegion);
        }
        else
        {
            // no need to assume - we are in the GC and know its generation
            estimatedReclaim += curReusableRegion->ReclaimSizeIfRelocated(m_condemnedGeneration == 2);

            if (curReusableRegion->IsPromotionCandidate())
            {
                promotionEstimate++;
            }

            if (curReusableRegion->HasFinalizables())
            {
                finalizationTrackingLocal.Push(curReusableRegion);
            }
            else
            {
                ephemeralLocal.Push(curReusableRegion);
            }
        }

        curReusableRegion = m_reusableRegions->TryPopUnsafe();
    }

    m_ephemeralWithUnmarkedDemoted->Append(&demotedLocal);
    m_ephemeralFinalizationTrackingRegions->Append(&finalizationTrackingLocal);
    m_ephemeralRegions->Append(&ephemeralLocal);

    if (estimatedReclaim > 0)
    {
        Interlocked::ExchangeAdd64(&m_estimatedEphemeralReclaim, estimatedReclaim);
    }

    if (promotionEstimate > 0)
    {
        Interlocked::ExchangeAdd64(&m_promotionEstimate, promotionEstimate);
    }
}

bool SatoriRecycler::DrainDeferredSweepQueueConcurrent(int64_t deadline)
{
    bool isWorkerGCThread = IsWorkerThread();

    if (isWorkerGCThread)
    {
        SatoriFinalizationQueue* q = m_heap->FinalizationQueue();
        if (q->OverflowedGen() != 0)
        {
            MaybeAskForHelp();
            q->TryResizeIfOverflowing();
        }
    }

    // Sweeping and returning could be contentious.
    // If there are workers and we are in a low latency mode, do not participate.
    if (isWorkerGCThread || m_activeWorkers <= 0 || !m_isLowLatencyMode)
    {
        SatoriRegion* curRegion = m_deferredSweepRegions->TryPop();
        if (curRegion)
        {
            MaybeAskForHelp();
            do
            {
                SweepAndReturnRegion(curRegion);
                Interlocked::Decrement(&m_deferredSweepCount);

                // ignore deadline on worker threads, we can't do anything else anyways.
                if (!isWorkerGCThread && deadline && (SatoriUtil::GetTimeStamp() - deadline > 0))
                {
                    break;
                }
            } while ((curRegion = m_deferredSweepRegions->TryPop()));
        }
    }

    // no work that we can claim, but we must wait for sweeping to finish and
    // in conservative mode we can't start marking while trimming may change regions,
    // let other threads do their things.
    int cycles = 0;
    while (m_deferredSweepCount ||
        (SatoriUtil::IsConservativeMode() && m_trimmer->IsActive()))
    {
        // user threads should not wait, just say we have more work
        if (!isWorkerGCThread)
        {
            return true;
        }

        YieldProcessor();
        if ((++cycles & 127) == 0)
        {
            GCToOSInterface::YieldThread(0);
        }
    }

    _ASSERTE(m_deferredSweepCount == 0 &&
        (!SatoriUtil::IsConservativeMode() || !m_trimmer->IsActive()));

    return false;
}

void SatoriRecycler::DrainDeferredSweepQueueWorkerFn()
{
    _ASSERTE(IsWorkerThread());

    SatoriRegion* curRegion = m_deferredSweepRegions->TryPop();
    if (curRegion)
    {
        MaybeAskForHelp();
        do
        {
            SweepAndReturnRegion(curRegion);
            Interlocked::Decrement(&m_deferredSweepCount);
        } while (m_activeWorkerFn && (curRegion = m_deferredSweepRegions->TryPop()));
    }
}

void SatoriRecycler::SweepAndReturnRegion(SatoriRegion* curRegion)
{
    if (!curRegion->DoNotSweep())
    {
        curRegion->Sweep</*updatePointers*/ false>();
    }

    if (curRegion->Occupancy() == 0)
    {
        FreeLogicallyEmptyRegion(curRegion, /* hasMarks */ false);
    }
    else
    {
        KeepRegion(curRegion);
    }
}

void SatoriRecycler::RecordOccupancy(int generation, size_t occupancy)
{
    _ASSERTE(m_occupancyReportingEnabled);
    Interlocked::ExchangeAdd64(&m_occupancyAcc[generation], occupancy);
}

void SatoriRecycler::RecordDemotedOccupancy(ptrdiff_t demotedOccupancy)
{
    _ASSERTE(m_occupancyReportingEnabled);
    if (demotedOccupancy != 0)
    {
        Interlocked::ExchangeAdd64(&m_demotedOccupancyAcc, (size_t)demotedOccupancy);
    }
}

size_t SatoriRecycler::GetOccupancy(int i)
{
    if (i < 0 || i > 2)
        return 0;

    return m_occupancy[i];
}

size_t SatoriRecycler::GetTotalOccupancy()
{
    return m_occupancy[0] +
           m_occupancy[1] +
           m_occupancy[2];
}

size_t SatoriRecycler::GetGcStartMillis(int generation)
{
    return m_gcStartMillis[generation];
}

size_t SatoriRecycler::GetGcDurationMillis(int generation)
{
    return m_gcDurationUsecs[generation] / 1000;
}

size_t SatoriRecycler::GetGcAccumulatingDurationMillis(int generation)
{
    return m_gcAccmulatingDurationUsecs[generation];
}

bool& SatoriRecycler::IsLowLatencyMode()
{
    return m_isLowLatencyMode;
}

void SatoriRecycler::UpdateGcCounters(int64_t blockingStart)
{
    // Compute Time in GC
    int64_t osNow = minipal_hires_ticks();

    int64_t totalTimeInCurrentGc = osNow - blockingStart;
    int64_t timeSinceLastGcEnded = osNow - m_totalTimeAtLastGcEnd;

    // should always hold unless we switch to a nonmonotonic timer.
    _ASSERTE(timeSinceLastGcEnded >= totalTimeInCurrentGc);

    m_percentTimeInGcSinceLastGc = timeSinceLastGcEnded != 0 ? (int)(totalTimeInCurrentGc * 100 / timeSinceLastGcEnded) : 0;
    m_totalTimeAtLastGcEnd = osNow;
}
