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
// SatoriObject.h
//

#ifndef __SATORI_OBJECT_H__
#define __SATORI_OBJECT_H__

#include "common.h"
#include "../gc.h"

// overwrite freed space with junk for debugging purposes.
#if _DEBUG
#define JUNK_FILL_FREE_SPACE
#endif

class SatoriRegion;

class SatoriObject : public Object
{
    friend class SatoriRegion;
    friend class SatoriGC;

public:
    SatoriObject() = delete;
    ~SatoriObject() = delete;

    // zeroedEnd, when above the location, tells that the memory from the location up to zeroedEnd
    // contains zeroes. That is recorded in the free object (see SatoriFreeListObject::ZeroedEnd)
    // and the memory after the header is kept that way (it matters for debug junk-filling).
    static SatoriObject* FormatAsFree(size_t location, size_t size, size_t zeroedEnd = 0);

    SatoriRegion* ContainingRegion();
    size_t Start();
    size_t End();
    SatoriObject* Next();

    size_t Size();
    size_t FreeObjSize();
    size_t FreeObjCapacity();
    bool SameRegion(SatoriRegion* otherRegion);
    bool IsFree();
    bool IsExternal();

    bool IsEscaped();
    bool IsMarked();
    bool IsMarkedOrOlderThan(int generation);
    void SetMarked();
    void SetMarkedAtomic();

    int GetMarkBitAndWord(size_t* bitmapIndex);

    void SetUnmovable();
    bool IsUnmovable();

    template <bool notExternal = false>
    bool IsRelocated();
    template <bool notExternal = false>
    bool IsRelocatedTo(SatoriObject** newLocation);
    SatoriObject * RelocatedToUnchecked();

    // #define BIT_SBLK_UNUSED                     0x80000000
    static const uint32_t BIT_SBLK_UNFINISHED = 0x80000000;

    // used by shared and huge allocations
    void CleanSyncBlockAndSetUnfinished()
    {
        *((size_t*)this - 1) = (size_t)BIT_SBLK_UNFINISHED << 32;
    }

    bool IsUnfinished()
    {
        return *((uint32_t*)this - 1) & BIT_SBLK_UNFINISHED;
    }

    void UnsetUnfinished()
    {
        *((uint32_t*)this - 1) &= ~BIT_SBLK_UNFINISHED;
    }

    bool HasEphemeralCollectibleAllocator();
    void DirtyCardsForContent();

    void EscapeCheckOnHandleCreation();

    bool IsFinalizationSuppressed();
    void SuppressFinalization();
    void UnSuppressFinalization();

    int32_t GetNextInLocalMarkStack();
    void SetNextInLocalMarkStack(int32_t);
    void ClearNextInLocalMarkStack();
    void ClearMarkCompactStateForRelocation();
    int32_t GetLocalReloc();
    void SetLocalReloc(int32_t);

    void CleanSyncBlock();

    void Validate();

    // Calls "lambda" with the location of every reference field.
    // The size, if given, must be the Size() of the object. The ranged version visits only the fields in [start, end).
    // NB: the ranged version takes the size too. The callers that walk objects one after another need it
    //     to get to the next object anyways, and the walk needs it too, so it is computed once.
    template <typename F>
    void ForEachObjectRef(F lambda, bool includeCollectibleAllocator = false);

    template <typename F>
    void ForEachObjectRef(F lambda, size_t size, bool includeCollectibleAllocator = false);

    template <typename F>
    void ForEachObjectRef(F lambda, size_t size, size_t start, size_t end, bool includeCollectibleAllocator);

    // The ranged version used to be "lambda, start, end". Make sure that a call written that way
    // does not quietly bind to the sized overload with "end" converted to bool.
    template <typename F> void ForEachObjectRef(F lambda, size_t, size_t) = delete;

private:
    // the walk of the whole object, shared by the overloads above that differ in how they get the method table and the size.
    template <typename F>
    void ForEachObjectRefCore(MethodTable* mt, size_t size, F lambda, bool includeCollectibleAllocator);

    template <typename F>
    void ReportCollectibleAllocator(MethodTable* mt, F lambda);

    static MethodTable* s_emptyObjectMt;
    static void Initialize();
};


class SatoriFreeListObject : public SatoriObject
{
private:
    uint32_t       m_Length;
    // Array alignment padding, not a part of the length.
    // FormatAsFree uses it to record how much of the free space is known to contain zeroes, if anything.
    uint32_t       m_zeroedSize;

public:
    SatoriFreeListObject() = delete;
    ~SatoriFreeListObject() = delete;

    // The memory after this header and up to ZeroedEnd() is known to contain zeroes.
    // When ZeroedEnd() is not above the header, nothing is known.
    size_t ZeroedEnd()
    {
        return (size_t)this + m_zeroedSize;
    }

    void SetZeroedEnd(size_t zeroedEnd)
    {
        m_zeroedSize = zeroedEnd > (size_t)this ? (uint32_t)(zeroedEnd - (size_t)this) : 0;
    }

    SatoriFreeListObject* m_nextInFreeList;
};

#endif
