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
#include "../gcenv.inl"

#include "SatoriUtil.h"
#include "SatoriTrimmer.h"
#include "SatoriAllocator.h"
#include "SatoriHeap.h"
#include "SatoriPage.h"
#include "SatoriPage.inl"
#include "SatoriRegion.h"
#include "SatoriRegion.inl"

SatoriTrimmer::SatoriTrimmer(SatoriHeap* heap)
{
    m_lastGen2Count = 0;
    m_heap  = heap;
    m_state = TRIMMER_STATE_STOPPED;

    m_event = new (nothrow) GCEvent;
    m_event->CreateAutoEventNoThrow(false);

    m_sleepGate = new (nothrow) GCGate();

    if (SatoriUtil::IsTrimmingEnabled())
    {
        GCToEEInterface::CreateThread(LoopFn, this, false, "Satori GC Trimmer Thread");
    }
}

// A commit coarser than a region cannot be sparse, so then there is nothing to decommit.
static bool CanDecommitGranules()
{
    return SatoriUtil::CommitGranularity() <= Satori::REGION_SIZE_GRANULARITY;
}

void SatoriTrimmer::Pause(int msec)
{
    m_paused = true;
    m_sleepGate->TimedWait(msec);
    m_paused = false;
}

void SatoriTrimmer::Unpause()
{
    m_sleepGate->WakeOne();
}

void SatoriTrimmer::LoopFn(void* inst)
{
    SatoriTrimmer* ths = (SatoriTrimmer*)inst;
    ths->Loop();
}

// The coming GC cycles take the memory that a GC frees: the next one about its gen1 budget, less what the sweeping parked in reusable
// regions, and the ones before the next gen2 also what they promote, up to the gen2 limit (see SatoriRecycler::GetTrimBudget).
// The trimmer keeps that much of the free committed memory, and decommits the rest, the surplus. A process that runs steady,
// with its GCs at about the same occupancy, has about no surplus, and does not decommit memory only to commit it again.
// A process whose live set grows and shrinks over many cycles needs again at its next peak what it needed at the last one,
// so the trimmer also keeps what brings the heap back to the most memory that it had in use at a GC in the last hold period
// (DOTNET_gcTrimPeakHold). That is what the heap needed, whatever the fragmentation and the floating garbage were then. (see SatoriRecycler::NoteInUse)
// A pass comes shortly after every blocking GC, and the next GC ends it. It decommits the surplus at a pace: decommit has a cost
// for the whole process, and the process may need the memory again soon. When the memory is low the surplus goes without the pace.
// A heap that committed memory in the last few seconds (TRIM_COMMIT_HOLD_SEC) is growing, and a pass does not decommit then.
// An idle process (no GC for a round interval) needs less: every round of idleness halves the budget, until all the free memory
// is decommitted.
// The granules that a pass decommits were free on the pass before too, so they were not taken by a whole GC cycle,
// and the memory that a GC frees, which is what the process takes again first, is not decommitted right after the GC.
void SatoriTrimmer::Loop()
{
    SatoriRecycler* recycler = m_heap->Recycler();
    const int64_t freq = SatoriUtil::GetTimeStampFrequency();
    const int64_t roundInterval = freq * 5;
    const int64_t gcDelay = freq * Satori::TRIM_GC_DELAY_MS / 1000;
    const int64_t commitHold = freq * Satori::TRIM_COMMIT_HOLD_SEC;

    // The blocking GCs, which free granules, and the GCs of any kind, which say that the process is not idle.
    int64_t lastBlockingGcCount = recycler->GetCollectionCount(1);
    int64_t lastGcCount = recycler->GetCollectionCount(0);
    int64_t lastBlockingGcTs = SatoriUtil::GetTimeStamp();
    int64_t lastGcSeenTs = lastBlockingGcTs;
    int64_t lastPassTs = lastBlockingGcTs;
    bool passDue = false;
    int idleRounds = 0;

    // The most memory that the heap had in use at a GC, in two periods of the hold. A peak is held for between one and two periods.
    const int64_t holdPeriod = freq * (int64_t)SatoriUtil::TrimPeakHoldSeconds();
    size_t peaks[2] = { 0, 0 };
    int64_t peakRotationTs = lastBlockingGcTs;

    // The last pass left free committed memory. If no GC comes, a later pass will decommit it, as the budget shrinks.
    bool leftWork = false;

    while (true)
    {
        while (true)
        {
            int64_t now = SatoriUtil::GetTimeStamp();
            int64_t blockingGcCount = recycler->GetCollectionCount(1);
            if (blockingGcCount != lastBlockingGcCount)
            {
                lastBlockingGcCount = blockingGcCount;
                lastBlockingGcTs = now;
                passDue = true;
            }

            int64_t gcCount = recycler->GetCollectionCount(0);
            if (gcCount != lastGcCount)
            {
                lastGcCount = gcCount;
                lastGcSeenTs = now;
                idleRounds = 0;
            }

            int64_t dueTs = INT64_MAX;
            if (passDue)
            {
                dueTs = lastBlockingGcTs + gcDelay;
            }
            else if (leftWork)
            {
                dueTs = max(lastPassTs, lastGcSeenTs) + roundInterval;
            }

            if (now >= dueTs)
            {
                break;
            }

            int64_t waitFor = min(dueTs - now, roundInterval);
            int waitMsec = (int)(waitFor * 1000 / freq + 1);
            Pause(waitMsec);
            StopAndWait();
        }

        passDue = false;
        leftWork = false;
        lastPassTs = SatoriUtil::GetTimeStamp();

        if (!CanDecommitGranules())
        {
            continue;
        }

        // The pass starts with the permission to run. The wait above can end without asking for it (when the last pass took longer than
        // a round interval), and a GC could be in progress then. In the conservative mode a GC marks only while the trimmer is not active.
        StopAndWait();
        if (lastPassTs - lastGcSeenTs >= roundInterval)
        {
            idleRounds = min(idleRounds + 1, 63);

            // Regions in the cache are not free granules, and the trimmer only decommits free granules.
            // A busy process takes them from the cache, an idle one has them decommitted by the next pass.
            m_heap->Allocator()->DrainRegionCache();
        }

        if (holdPeriod != 0)
        {
            int64_t sinceRotation = lastPassTs - peakRotationTs;
            if (sinceRotation >= holdPeriod)
            {
                peaks[1] = sinceRotation >= holdPeriod * 2 ? 0 : peaks[0];
                peaks[0] = 0;
                peakRotationTs = lastPassTs;
            }

            if (idleRounds == 0)
            {
                peaks[0] = max(peaks[0], recycler->TakeInUsePeak());
            }
        }

        size_t held = max(peaks[0], peaks[1]);

        // The pace is set by the surplus at the start of the pass, and a pass that is behind does not pause.
        int64_t paceStart = SatoriUtil::GetTimeStamp();
        size_t paceRate = 0;
        size_t pacedBytes = 0;
        bool trimming = true;

        // A GC ends the pass, and the next one starts after the delay, with the budget for the next cycle. A GC asks the trimmer to stop
        // when it starts, and counts itself before it lets the trimmer run again. In the precise mode it does not wait for the trimmer to stop.
        // (an acquire: after a state that the GC set when it ended, the count is the new one)
        bool gcCame = false;
        auto checkForGc = [&]()
        {
            gcCame = gcCame ||
                VolatileLoad(&m_state) != TRIMMER_STATE_RUNNING ||
                recycler->GetCollectionCount(1) != lastBlockingGcCount;

            return gcCame;
        };

        m_heap->ForEachPage(
            [&](SatoriPage* page)
            {
                if (m_state != TRIMMER_STATE_RUNNING)
                {
                    StopAndWait();
                }

                size_t granule = trimming ? page->NextGranuleToTrim(0, /* onlyIfSeenFree */ true) : SIZE_MAX;
                while (granule != SIZE_MAX)
                {
                    if (m_state != TRIMMER_STATE_RUNNING)
                    {
                        StopAndWait();
                    }

                    if (checkForGc())
                    {
                        trimming = false;
                        break;
                    }

                    // Every run looks at the surplus again, since the GC sweeping parks free space and the threads take free granules meanwhile.
                    // The free memory that brings what is in use back to the held peak is kept.
                    size_t free = m_heap->FreeCommittedBytes();
                    size_t committed = m_heap->GetBytesCommitted();
                    size_t inUse = committed > free ? committed - free : 0;
                    bool underPressure;
                    size_t budget = recycler->GetTrimBudget(held > inUse ? held - inUse : 0, underPressure);
                    if (idleRounds > 0)
                    {
                        budget >>= idleRounds;
                        if (budget < Satori::TRIM_RUN_GRANULES * Satori::REGION_SIZE_GRANULARITY)
                        {
                            budget = 0;
                        }
                    }

                    if (free <= budget)
                    {
                        trimming = false;
                        break;
                    }

                    // A heap that committed memory lately is growing, and would commit again what the pass decommits. The pass leaves it alone
                    // then, and the next one, after a GC or a round interval, looks again. When the memory is low the surplus goes anyway.
                    if (!underPressure && SatoriUtil::GetTimeStamp() - m_heap->GetLastCommitTimeStamp() < commitHold)
                    {
                        trimming = false;
                        break;
                    }

                    size_t surplus = free - budget;
                    if (paceRate == 0)
                    {
                        paceRate = max(Satori::TRIM_MIN_RATE, surplus / Satori::TRIM_SURPLUS_PART_PER_SEC);
                    }

                    // A run of up to the surplus, in a power of 2 granules, so that the runs stay aligned. (see TryTrimRun)
                    // Where the card slices are decommitted, no less than the granules whose slices share an OS page.
                    size_t surplusGranules = max((size_t)1, surplus / Satori::REGION_SIZE_GRANULARITY);
                    size_t minCount = SatoriUtil::CanDecommitCardSlices() ? SatoriUtil::CardSliceGroupGranules() : 1;
                    size_t maxCount = max(minCount, min(Satori::TRIM_RUN_GRANULES, SatoriUtil::RoundDownPwr2(surplusGranules)));

                    size_t before = page->FreeCommittedBytes();
                    size_t count;
                    if (page->TryTrimRun(granule, maxCount, /* onlyIfSeenFree */ true, count) && !underPressure)
                    {
                        size_t after = page->FreeCommittedBytes();
                        pacedBytes += before > after ? before - after : 0;
                        // in floating point: the bytes times the frequency would overflow
                        int64_t aheadTicks = paceStart + (int64_t)((double)pacedBytes / (double)paceRate * (double)freq) - SatoriUtil::GetTimeStamp();
                        if (aheadTicks > 0)
                        {
                            Pause((int)(aheadTicks * 1000 / freq + 1));
                        }
                    }

                    granule = page->NextGranuleToTrim(granule + count, /* onlyIfSeenFree */ true);
                }

                // Every pass looks at all the free granules, so that the next pass can tell which ones have stayed free.
                // Not after a GC: the next pass comes right after it, and would take the granules that the GC freed for ones that stayed free.
                // A look that a GC overlapped could have seen some of them, so the page forgets it, and has no granule seen free until the next look.
                if (!checkForGc())
                {
                    page->RememberFreeGranules();
                    if (checkForGc())
                    {
                        page->ForgetFreeGranules();
                    }
                }
            }
        );

        leftWork = m_heap->FreeCommittedBytes() != 0;
    }
}

// Decommits all free granules, and the regions from the cache with them.
void SatoriTrimmer::TrimNow()
{
    _ASSERTE(m_heap->Recycler()->IsBlockingPhase());

    if (!CanDecommitGranules())
    {
        return;
    }

    // The trimmer was asked to stop when the GC started. Make sure it did, so that we do not run two things at once.
    WaitForStop();

    m_heap->Allocator()->DrainRegionCache();

    m_heap->ForEachPage(
        [&](SatoriPage* page)
        {
            // the world is stopped, there is no use in waiting for the granules to stay free.
            size_t granule = page->NextGranuleToTrim(0, /* onlyIfSeenFree */ false);
            while (granule != SIZE_MAX)
            {
                size_t count;
                page->TryTrimRun(granule, Satori::TRIM_RUN_GRANULES, /* onlyIfSeenFree */ false, count);
                granule = page->NextGranuleToTrim(granule + count, /* onlyIfSeenFree */ false);
            }

            page->RememberFreeGranules();
        }
    );
}

void SatoriTrimmer::StopAndWait()
{
    while (true)
    {
        tryAgain:

        int state = m_state;
        switch (state)
        {
        case TRIMMER_STATE_STOP_SUGGESTED:
            Interlocked::CompareExchange(&m_state, TRIMMER_STATE_STOPPED, state);
            continue;
        case TRIMMER_STATE_OK_TO_RUN:
            Interlocked::CompareExchange(&m_state, TRIMMER_STATE_RUNNING, state);
            continue;
        case TRIMMER_STATE_STOPPED:
            for (int i = 0; i < 10; i++)
            {
                Pause(100);
                if (m_state != state)
                {
                    goto tryAgain;
                }
            }

            if (Interlocked::CompareExchange(&m_state, TRIMMER_STATE_BLOCKED, state) == state)
            {
                m_event->Wait(INFINITE, false);
            }
            continue;
        case TRIMMER_STATE_RUNNING:
            return;
        default:
            UNREACHABLE();
            return;
        }
    }
}

void SatoriTrimmer::SetOkToRun()
{
    while (true)
    {
        int state = m_state;
        switch (state)
        {
        case TRIMMER_STATE_BLOCKED:
            // trimmer can't get out of BLOCKED by itself, ordinary assignment is ok
            m_state = TRIMMER_STATE_OK_TO_RUN;
            m_event->Set();
            return;
        case TRIMMER_STATE_STOPPED:
            if (Interlocked::CompareExchange(&m_state, TRIMMER_STATE_OK_TO_RUN, state) == state)
            {
                // The trimmer is pausing in StopAndWait, wake it up, so that a pass after the GC starts in time.
                Unpause();
                return;
            }
            break;
        case TRIMMER_STATE_STOP_SUGGESTED:
            if (Interlocked::CompareExchange(&m_state, TRIMMER_STATE_RUNNING, state) == state)
            {
                return;
            }
            break;
        default:
            return;
        }
    }
}

void SatoriTrimmer::SetStopSuggested()
{
    while (true)
    {
        int state = m_state;
        switch (state)
        {
        case TRIMMER_STATE_OK_TO_RUN:
            if (Interlocked::CompareExchange(&m_state, TRIMMER_STATE_STOPPED, state) == state)
            {
                Unpause();
                return;
            }
            break;
        case TRIMMER_STATE_RUNNING:
            if (Interlocked::CompareExchange(&m_state, TRIMMER_STATE_STOP_SUGGESTED, state) == state)
            {
                Unpause();
                return;
            }
            break;
        default:
            _ASSERTE(m_state <= TRIMMER_STATE_STOP_SUGGESTED);
            Unpause();
            return;
        }
    }
}

void SatoriTrimmer::WaitForStop()
{
    _ASSERTE(m_state <= TRIMMER_STATE_STOP_SUGGESTED);

    int cycles = 0;
    // A conservative marker reads the region map after this. The load must be an acquire (a plain load is not one on arm64),
    // so that the marker sees every free that the trimmer did before its acknowledgement.
    while (VolatileLoad(&m_state) == TRIMMER_STATE_STOP_SUGGESTED)
    {
        if (m_paused)
        {
            Unpause();
        }

        YieldProcessor();
        if ((++cycles & 127) == 0)
        {
            GCToOSInterface::YieldThread(0);
        }
    }

    _ASSERTE(!IsActive());
}

bool SatoriTrimmer::IsActive()
{
    // an acquire, as in WaitForStop: a conservative marker that sees the trimmer inactive reads the region map after this.
    return VolatileLoad(&m_state) > TRIMMER_STATE_STOPPED;
}
