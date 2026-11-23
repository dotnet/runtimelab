// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef LAXORI_BLOCK_ZEROER_H
#define LAXORI_BLOCK_ZEROER_H

#include "laxori_immix_allocation.h"

#ifdef byte
#pragma push_macro("byte")
#define LAXORI_BLOCK_ZEROER_RESTORE_BYTE
#undef byte
#endif
#ifdef __valid
#pragma push_macro("__valid")
#define LAXORI_BLOCK_ZEROER_RESTORE_VALID
#undef __valid
#endif

#include <chrono>
#include <condition_variable>
#include <mutex>
#include <thread>

#ifdef LAXORI_BLOCK_ZEROER_RESTORE_BYTE
#pragma pop_macro("byte")
#undef LAXORI_BLOCK_ZEROER_RESTORE_BYTE
#endif
#ifdef LAXORI_BLOCK_ZEROER_RESTORE_VALID
#pragma pop_macro("__valid")
#undef LAXORI_BLOCK_ZEROER_RESTORE_VALID
#endif

#ifdef __linux__
#include <pthread.h>
#endif

// The background thread that clears free Immix blocks in front of the allocators (see laxori_immix_zeroing.cpp). It runs while the
// program runs. A pause holds it back (ImmixAllocationManager::SuspendPreZero). It sleeps when the window of blocks in front
// of the allocation cursor is clean, and polls the cursor with a growing interval while nothing allocates.
class LaxoriBlockZeroer final
{
public:
    LaxoriBlockZeroer() = default;
    LaxoriBlockZeroer(const LaxoriBlockZeroer&) = delete;
    LaxoriBlockZeroer& operator=(const LaxoriBlockZeroer&) = delete;
    ~LaxoriBlockZeroer()
    {
        Stop();
    }

    bool Start(ImmixAllocationManager* manager, size_t marginBlocks, size_t windowBlocks)
    {
        if (manager == nullptr || m_running || !manager->IsPreZeroEnabled() || windowBlocks == 0)
        {
            return false;
        }
        m_manager = manager;
        m_margin = marginBlocks;
        m_window = windowBlocks;
        m_stop = false;
        m_wake = false;
        manager->SetPreZeroWake(&LaxoriBlockZeroer::WakeCallback, this);
        try
        {
            m_thread = std::thread([this]() { Run(); });
        }
        catch (...)
        {
            manager->SetPreZeroWake(nullptr, nullptr);
            return false;
        }
        m_running = true;
        return true;
    }

    void Stop()
    {
        if (!m_running)
        {
            return;
        }
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            m_stop = true;
        }
        m_condition.notify_one();
        m_thread.join();
        m_running = false;
        if (m_manager != nullptr)
        {
            m_manager->SetPreZeroWake(nullptr, nullptr);
        }
    }

    // Called after a pause: the cursor of the allocators is back at the start of the arena.
    void Wake()
    {
        if (!m_running)
        {
            return;
        }
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            m_wake = true;
        }
        m_condition.notify_one();
    }

    bool IsRunning() const
    {
        return m_running;
    }

private:
    static constexpr size_t PassBudgetBlocks = 8;
    static constexpr int SleepMilliseconds = 20;

    void Run()
    {
#ifdef __linux__
        pthread_setname_np(pthread_self(), "LAXORI zero");
#endif
        while (true)
        {
            ImmixAllocationManager::PreZeroPass pass;
            m_manager->PreZeroStep(m_margin, m_window, PassBudgetBlocks, &pass);
            if (pass.cleared == PassBudgetBlocks)
            {
                std::lock_guard<std::mutex> lock(m_mutex);
                if (m_stop)
                {
                    return;
                }
                continue;
            }
            // Nothing more to clear now: sleep until an allocator takes a block near the end of the clean window, or until a
            // pause is over. The timeout only bounds the effect of a lost wake-up.
            std::unique_lock<std::mutex> lock(m_mutex);
            if (m_stop)
            {
                return;
            }
            if (!m_wake && m_manager->ArmPreZeroWake(pass.wakeIndex))
            {
                m_condition.wait_for(lock, std::chrono::milliseconds(SleepMilliseconds), [this]() { return m_stop || m_wake; });
            }
            m_manager->DisarmPreZeroWake();
            m_wake = false;
            if (m_stop)
            {
                return;
            }
        }
    }

    static void WakeCallback(void* context)
    {
        static_cast<LaxoriBlockZeroer*>(context)->Wake();
    }

    ImmixAllocationManager* m_manager = nullptr;
    size_t m_margin = 0;
    size_t m_window = 0;
    bool m_running = false;
    bool m_stop = false;
    bool m_wake = false;
    std::thread m_thread;
    std::mutex m_mutex;
    std::condition_variable m_condition;
};

#endif // LAXORI_BLOCK_ZEROER_H
