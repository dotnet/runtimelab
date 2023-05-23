// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"

#include "CommonMacros.inl"

//
// This file contains the implementation of a dynamic memory allocator used by codegen
// for 'localloc's that might be live in handlers and thus cannot use the native stack.
// The allocator is a simple pointer bump design, with a free list for pages and linked
// inline descriptors for allocations ("blocks"). We also impose an artificial limit on
// on the overall allocation size to help catch stack overflows. This could be made to
// be dynamically configurable if needed.
//
static const int DYN_STK_ALLOC_MAX_SIZE = 10 * 1024 * 1024; // 10MB.
static const int DYN_STK_ALLOC_MIN_PAGE_SIZE = 64 * 1024; // 64K.
static const int DYN_STK_ALLOC_ALIGNMENT = 8; // sizeof(double)

struct AllocatorBlock
{
    AllocatorBlock* Prev;
    void* ShadowFrameAddress;
};

struct AllocatorPage
{
    size_t Size; // Includes both the header and data.
    AllocatorBlock* LastBlock;
    AllocatorPage* Prev;
    alignas(DYN_STK_ALLOC_ALIGNMENT) unsigned char Data[];
};

struct AllocatorInstance
{
    unsigned char* Current = nullptr; // Points one byte past the end of the last allocated block.
    unsigned char* CurrentEnd = nullptr; // Points one byte past the end of the current page.
    AllocatorPage* BusyPages = nullptr; // Linked list, ordered first to current.
    AllocatorPage* FreePages = nullptr; // Linked list, LIFO.
    size_t TotalSize = 0; // Overall allocated memory size.
};

static bool IsSameOrCalleeFrame(void* pShadowFrame, void* pCallerShadowFrame)
{
    // Assumption: the shadow stack grows upwards.
    return pShadowFrame >= pCallerShadowFrame;
}

static AllocatorBlock* GetBlock(unsigned char* pBlockEnd)
{
    return reinterpret_cast<AllocatorBlock*>(pBlockEnd - sizeof(AllocatorBlock));
}

static unsigned char* GetBlockEnd(AllocatorBlock* pBlock)
{
    return reinterpret_cast<unsigned char*>(pBlock) + sizeof(AllocatorBlock);
}

static unsigned char* GetPageEnd(AllocatorPage* page)
{
    return reinterpret_cast<unsigned char*>(page) + page->Size;
}

static void FailFastWithStackOverflow()
{
    // Note: we cannot throw any sort of exception here as codegen assumes we don't call back into managed code.
    PalPrintFatalError("\nProcess is terminating due to StackOverflowException.\n");
    RhFailFast();
}

FORCEINLINE static unsigned char* AllocateBlock(unsigned char* pCurrent, size_t allocSize, AllocatorBlock* pCurrentBlock, void* pShadowFrame)
{
    ASSERT(IS_ALIGNED(allocSize, DYN_STK_ALLOC_ALIGNMENT));
    ASSERT((pCurrentBlock == nullptr) || IsSameOrCalleeFrame(pShadowFrame, pCurrentBlock->ShadowFrameAddress));

    unsigned char* pNextCurrent = pCurrent + allocSize;
    AllocatorBlock* pNextBlock = GetBlock(pNextCurrent);
    if ((pCurrentBlock != nullptr) && (pCurrentBlock->ShadowFrameAddress == pShadowFrame))
    {
        // Combine blocks from the same frame. This makes releasing them O(1).
        *pNextBlock = *pCurrentBlock;
    }
    else
    {
        pNextBlock->Prev = pCurrentBlock;
        pNextBlock->ShadowFrameAddress = pShadowFrame;
    }

    return pNextCurrent;
}

static void* AllocatePage(AllocatorInstance* alloc, size_t allocSize, void* pShadowFrame)
{
    ASSERT(IS_ALIGNED(allocSize, DYN_STK_ALLOC_ALIGNMENT));

    // Need to allocate a new page.
    allocSize += ALIGN_UP(sizeof(AllocatorPage), DYN_STK_ALLOC_ALIGNMENT);
    size_t allocPageSize = ALIGN_UP(allocSize, DYN_STK_ALLOC_MIN_PAGE_SIZE);

    // Do we have a free one available?
    AllocatorPage* allocPage = nullptr;
    for (AllocatorPage** link = &alloc->FreePages, *page = *link; page != nullptr; link = &page->Prev, page = *link)
    {
        if (page->Size >= allocPageSize)
        {
            *link = page->Prev;
            allocPage = page;
            break;
        }
    }

    if (allocPage == nullptr)
    {
        size_t newTotalAllocSize = alloc->TotalSize + allocPageSize;
        if (newTotalAllocSize > DYN_STK_ALLOC_MAX_SIZE)
        {
            FailFastWithStackOverflow();
        }

        allocPage = static_cast<AllocatorPage*>(aligned_alloc(DYN_STK_ALLOC_ALIGNMENT, allocPageSize));
        if (allocPage == nullptr)
        {
            FailFastWithStackOverflow();
        }

        alloc->TotalSize = newTotalAllocSize;
        allocPage->Size = allocPageSize;
    }

    // Thread the page onto the busy list.
    AllocatorPage* currentPage = alloc->BusyPages;
    if (currentPage != nullptr)
    {
        currentPage->LastBlock = GetBlock(alloc->Current);
    }
    allocPage->Prev = currentPage;
    alloc->BusyPages = allocPage;

    // Finally, allocate the block and update current allocator state.
    alloc->Current = AllocateBlock(allocPage->Data, allocSize, nullptr, pShadowFrame);
    alloc->CurrentEnd = GetPageEnd(allocPage);
    return allocPage->Data;
}

static void ReleaseBlocks(AllocatorInstance* alloc, void* pShadowFrame)
{
    ASSERT(alloc->Current != nullptr);
    AllocatorBlock* block = GetBlock(alloc->Current);
    AllocatorPage* page = alloc->BusyPages;
    while (IsSameOrCalleeFrame(block->ShadowFrameAddress, pShadowFrame))
    {
        AllocatorBlock* prevBlock = block->Prev;

        if (prevBlock == nullptr)
        {
            // We have reached the beginning of a page.
            AllocatorPage* prevPage = page->Prev;
            if (prevPage == nullptr)
            {
                // If this is the very first page, leave it in the busy list - nulling it out would
                // would slow the down the allocation path unnecessarily. But do release the first block.
                block = nullptr;
                break;
            }

            // Transfer "page" to the free list.
            ASSERT(page == alloc->BusyPages);
            alloc->BusyPages = prevPage;
            page->Prev = alloc->FreePages;
            alloc->FreePages = page;

            page = prevPage;
            prevBlock = prevPage->LastBlock;
            ASSERT(prevBlock != nullptr);
        }

        block = prevBlock;
    }

    alloc->Current = (block != nullptr) ? GetBlockEnd(block) : page->Data;
    alloc->CurrentEnd = GetPageEnd(page);
}

thread_local AllocatorInstance t_dynamicStackAlloc;

COOP_PINVOKE_HELPER(void*, RhpDynamicStackAlloc, (unsigned size, void* pShadowFrame))
{
    ASSERT((size != 0) && IS_ALIGNED(pShadowFrame, sizeof(void*)));
    size_t allocSize = ALIGN_UP(size + sizeof(AllocatorBlock), DYN_STK_ALLOC_ALIGNMENT);

    AllocatorInstance* alloc = &t_dynamicStackAlloc;
    unsigned char* pCurrent = alloc->Current;
    unsigned char* pCurrentEnd = alloc->CurrentEnd;
    ASSERT(IS_ALIGNED(pCurrent, DYN_STK_ALLOC_ALIGNMENT));

    // Note that if we haven't yet allocated any pages, this test will always fail, as intended.
    if ((pCurrent + allocSize) < pCurrentEnd)
    {
        alloc->Current = AllocateBlock(pCurrent, allocSize, GetBlock(pCurrent), pShadowFrame);
        return pCurrent;
    }

    return AllocatePage(alloc, allocSize, pShadowFrame);
}

COOP_PINVOKE_HELPER(void, RhpDynamicStackRelease, (void* pShadowFrame))
{
    AllocatorInstance* alloc = &t_dynamicStackAlloc;
    unsigned char* pCurrent = alloc->Current;
    if (pCurrent == nullptr)
    {
        // No pages allocated (yet).
        return;
    }

    // The most common case is that we release from the same frame we just allocated on.
    AllocatorBlock* currentBlock = GetBlock(pCurrent);
    if (currentBlock->ShadowFrameAddress == pShadowFrame)
    {
        // The previous block hay have been part of the previous page. Fall back to the slower path if so.
        AllocatorBlock* prevBlock = currentBlock->Prev;
        if (prevBlock != nullptr)
        {
            alloc->Current = GetBlockEnd(prevBlock);
            ASSERT(!IsSameOrCalleeFrame(prevBlock->ShadowFrameAddress, pShadowFrame));
            return;
        }
    }

    ReleaseBlocks(alloc, pShadowFrame);
}
