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
// SatoriUtil.cpp
//

#include "common.h"

#include "gcenv.h"
#include "../env/gcenv.os.h"
#include "SatoriUtil.h"

#if defined(HOST_X86) || defined(HOST_AMD64)
#include <minipal/cpuid.h>
#endif

int64_t SatoriUtil::s_timeStampFrequency = 0;
int64_t SatoriUtil::s_osTimeStampFrequency = 0;
int64_t SatoriUtil::s_ticksPerUsec = 1;

// a counter that ticks slower than this is too coarse for the intervals that we measure.
static const int64_t MIN_USABLE_FREQUENCY = 1000 * 1000;

// a HW read that costs more than this is trapped, emulated or otherwise not what we are after.
static const int64_t MAX_READ_NSEC = 200;

// calibration reads per one check of the OS timer, so that its cost does not hide the cost of ours.
static const int READ_BATCH = 64;

// a measurement is inconclusive only if something interfered, so a few tries are plenty.
static const int MAX_CALIBRATION_ATTEMPTS = 4;

#if defined(HOST_X86) || defined(HOST_AMD64)

// The TSC is only useful to us if it ticks at a constant rate regardless of the
// core frequency and power state.
static bool HasConstantRateTimeStamp()
{
    int regs[4];

    __cpuid(regs, (int)0x80000000);
    if ((uint32_t)regs[0] < 0x80000007)
    {
        return false;
    }

    __cpuid(regs, (int)0x80000007);
    const int INVARIANT_TSC = 1 << 8;
    return (regs[3] & INVARIANT_TSC) != 0;
}

#elif defined(HOST_ARM64)

static bool HasConstantRateTimeStamp()
{
    // the counter is required to run at a constant rate.
    return true;
}

#else

static bool HasConstantRateTimeStamp()
{
    return false;
}

#endif

// keeps the calibration reads from being optimized away.
static volatile int64_t s_readSink;

// Measures the counter rate and checks that reading it is actually cheap.
// Returns 0 if the measurement was inconclusive.
int64_t SatoriUtil::MeasureTimeStampFrequency()
{
    // ~100 usec gets the rate within ~0.05%, which is far more than deadlines need.
    int64_t window = s_osTimeStampFrequency / 10000;

    // Read the OS timer on the outside of the counter at both ends, so that the interval
    // attributed to the counter is contained in the interval measured with the OS timer.
    // Bracketing it the other way round makes the counter's interval the larger of the
    // two and reports its rate high by whatever the two gaps cost - measured at +0.2%
    // typical, and always in the same direction.
    int64_t osStart = minipal_hires_ticks();
    int64_t hwStart = ReadHwTimeStamp();
    int64_t reads = 0;
    int64_t sink = 0;
    do
    {
        for (int i = 0; i < READ_BATCH; i++)
        {
            sink += ReadHwTimeStamp();
        }
        reads += READ_BATCH;
    } while (minipal_hires_ticks() - osStart < window);

    int64_t hwEnd = ReadHwTimeStamp();
    int64_t osNow = minipal_hires_ticks();
    s_readSink = sink;

    int64_t hwElapsed = hwEnd - hwStart;
    int64_t osElapsed = osNow - osStart;

    // Overshooting the window by this much means we were interrupted, so the numbers
    // do not describe this machine. Bounding it also keeps the math below in range.
    if (osElapsed > window * 4)
    {
        return 0;
    }

    if (osElapsed * 1000000000 / s_osTimeStampFrequency / reads > MAX_READ_NSEC)
    {
        return 0;
    }

    int64_t frequency = hwElapsed * s_osTimeStampFrequency / osElapsed;
    return frequency >= MIN_USABLE_FREQUENCY ? frequency : 0;
}

void SatoriUtil::Initialize()
{
    s_osTimeStampFrequency = minipal_hires_tick_frequency();

    // The choice is made once and for all - switching timers later would leave
    // deadlines that were taken on one of them to be checked against the other.
    if (HasConstantRateTimeStamp())
    {
        for (int i = 0; i < MAX_CALIBRATION_ATTEMPTS; i++)
        {
            s_timeStampFrequency = MeasureTimeStampFrequency();
            if (s_timeStampFrequency != 0)
            {
                break;
            }
        }
    }

    s_ticksPerUsec = GetTimeStampFrequency() / (1000 * 1000);
    if (s_ticksPerUsec < 1)
    {
        s_ticksPerUsec = 1;
    }
}

int64_t SatoriUtil::GetTimeStampSlow()
{
    return minipal_hires_ticks();
}

