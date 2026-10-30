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
// SatoriObject.inl
//

#ifndef __SATORI_OBJECT_INL__
#define __SATORI_OBJECT_INL__

#include "common.h"
#include "../gc.h"
#include "../gcdesc.h"

#include "SatoriUtil.h"
#include "SatoriObject.h"
#include "SatoriRegion.h"

FORCEINLINE size_t SatoriObject::Size()
{
    MethodTable* mt = RawGetMethodTable();
    size_t size = mt->GetBaseSize();
    if (mt->HasComponentSize())
    {
        size += (size_t)((ArrayBase*)this)->GetNumComponents() * mt->RawGetComponentSize();
        size = ALIGN_UP(size, Satori::OBJECT_ALIGNMENT);
    }

    return size;
}

FORCEINLINE size_t SatoriObject::FreeObjSize()
{
    _ASSERTE(IsFree());
    size_t size = Satori::MIN_FREE_SIZE;
    size += (size_t)((ArrayBase*)this)->GetNumComponents();
    _ASSERTE(size == ALIGN_UP(size, Satori::OBJECT_ALIGNMENT));
    return size;
}

FORCEINLINE size_t SatoriObject::FreeObjCapacity()
{
    _ASSERTE(IsFree());
    return (size_t)((ArrayBase*)this)->GetNumComponents();
}

inline size_t SatoriObject::Start()
{
    return (size_t)this;
}

FORCEINLINE size_t SatoriObject::End()
{
    return Start() + Size();
}

FORCEINLINE SatoriObject* SatoriObject::Next()
{
    return (SatoriObject*)End();
}

inline bool SatoriObject::IsExternal()
{
#if FEATURE_SATORI_EXTERNAL_OBJECTS
    return !SatoriHeap::IsInHeap(this->Start());
#else
    _ASSERTE(SatoriHeap::IsInHeap(this->Start()));
    return false;
#endif
}

inline SatoriRegion* SatoriObject::ContainingRegion()
{
#if FEATURE_SATORI_EXTERNAL_OBJECTS
    _ASSERTE(!IsExternal() || this == nullptr);
#endif

    return (SatoriRegion*)((size_t)this & ~(Satori::REGION_SIZE_GRANULARITY - 1));
}


inline bool SatoriObject::SameRegion(SatoriRegion* otherRegion)
{
    return ((size_t)this ^ (size_t)otherRegion) < Satori::REGION_SIZE_GRANULARITY;
}

inline bool SatoriObject::IsFree()
{
    return RawGetMethodTable() == s_emptyObjectMt;
}

FORCEINLINE bool SatoriObject::HasEphemeralCollectibleAllocator()
{
    if (!RawGetMethodTable()->Collectible())
    {
        return false;
    }

    SatoriObject* loaderAllocator = (SatoriObject*)GCToEEInterface::GetLoaderAllocatorObjectForGC(this);
    return loaderAllocator &&
        !loaderAllocator->IsExternal() &&
        loaderAllocator->ContainingRegion()->Generation() < 2;
}

#ifndef HOST_64BIT

32bit is NYI

#endif

inline bool SatoriObject::IsEscaped()
{
    return ContainingRegion()->IsEscaped(this);
}

inline bool SatoriObject::IsMarked()
{
    return ContainingRegion()->IsMarked(this);
}

FORCEINLINE bool SatoriObject::IsMarkedOrOlderThan(int generation)
{
    _ASSERTE(this->Size() > 0);
    SatoriRegion* r = ContainingRegion();
    return r->Generation() > generation || r->IsMarked(this);
}

inline void SatoriObject::SetMarked()
{
    ContainingRegion()->SetMarked(this);
}

inline void SatoriObject::SetMarkedAtomic()
{
    return ContainingRegion()->SetMarkedAtomic(this);
}

inline bool SatoriObject::IsFinalizationSuppressed()
{
    return GetHeader()->GetBits() & BIT_SBLK_FINALIZER_RUN;
}

inline void SatoriObject::SuppressFinalization()
{
    GetHeader()->SetBit(BIT_SBLK_FINALIZER_RUN);
}

inline void SatoriObject::UnSuppressFinalization()
{
    GetHeader()->ClrBit(BIT_SBLK_FINALIZER_RUN);
}

//
// Implementation note on mark overflow and relocation:
//    we could use temporary maps (we would have to on 32bit),
//    but on 64bit we will use unused bits in the syncblock instead.
//

inline int32_t SatoriObject::GetNextInLocalMarkStack()
{
    return *((int32_t*)this - 2);
}

inline void SatoriObject::SetNextInLocalMarkStack(int32_t next)
{
    _ASSERTE(GetNextInLocalMarkStack() == 0);
    *((int32_t*)this - 2) = next;
}

inline void SatoriObject::ClearNextInLocalMarkStack()
{
    *((int32_t*)this - 2) = 0;
}

inline int32_t SatoriObject::GetLocalReloc()
{
    // since local relocation does not intersect with uses of mark stack
    // we will use the same bits.
    return GetNextInLocalMarkStack();
}

inline void SatoriObject::SetLocalReloc(int32_t next)
{
    // since local relocation does not intersect with uses of mark stack
    // we will use the same bits.
    SetNextInLocalMarkStack(next);
}

inline void SatoriObject::ClearMarkCompactStateForRelocation()
{
    // since local relocation does not intersect with uses of mark stack
    // we will use the same bits.
    ClearNextInLocalMarkStack();

    SatoriRegion* r = ContainingRegion();
    _ASSERTE(!r->IsEscaped(this));
    r->ClearMarkedAndPinned(this);
}

inline SatoriObject* SatoriObject::RelocatedToUnchecked()
{
    _ASSERTE(this->ContainingRegion()->IsRelocated());
    SatoriObject* newLocation = *((SatoriObject**)this - 1);
    _ASSERTE(this->RawGetMethodTable() == newLocation->RawGetMethodTable());
    return newLocation;
}

template <bool notExternal>
FORCEINLINE bool SatoriObject::IsRelocated()
{
    if (notExternal)
    {
        _ASSERTE(!this->IsExternal());
    }
    else if (this->IsExternal())
    {
        return false;
    }

    return this->ContainingRegion()->IsRelocated();
}

template <bool notExternal>
FORCEINLINE bool SatoriObject::IsRelocatedTo(SatoriObject** newLocation)
{
    if (IsRelocated<notExternal>())
    {
        *newLocation = RelocatedToUnchecked();
        return true;
    }

    return false;
}

inline void SatoriObject::CleanSyncBlock()
{
    *((size_t*)this - 1) = 0;
}

inline int SatoriObject::GetMarkBitAndWord(size_t* bitmapIndex)
{
    size_t start = Start();
    *bitmapIndex = (start >> 9) & (SatoriRegion::BITMAP_LENGTH - 1); // % words in the bitmap
    return (start >> 3) & 63;     // % bits in a word
}

// used by pinned allocations
inline void SatoriObject::SetUnmovable()
{
    *((DWORD*)this - 1) |= BIT_SBLK_GC_RESERVE;
}

inline bool SatoriObject::IsUnmovable()
{
    return *((DWORD*)this - 1) & BIT_SBLK_GC_RESERVE;
}

template <typename F>
FORCEINLINE void SatoriObject::ReportCollectibleAllocator(MethodTable* mt, F lambda)
{
    if (mt->Collectible())
    {
        uint8_t* loaderAllocator = GCToEEInterface::GetLoaderAllocatorObjectForGC(this);
        // NB: Allocator ref location is fake. The allocator is accessed via a handle indirection.
        //     It is ok to "update" the ref, but it will have no effect.
        //     The real update is when the handle is updated, which should happen separately.
        //     Callers that do arithmetic on the ref location must not ask for this.
        lambda((SatoriObject**)&loaderAllocator);
    }
}

template <typename F>
inline void SatoriObject::ForEachObjectRef(F lambda, bool includeCollectibleAllocator)
{
    // The Java bridge tags method tables while walking the unreachable graph.
    MethodTable* mt = GetGCSafeMethodTable();

    // NB: not Size(), that reads the method table without removing the tag.
    size_t size = mt->GetBaseSize();
    if (mt->HasComponentSize())
    {
        size += (size_t)((ArrayBase*)this)->GetNumComponents() * mt->RawGetComponentSize();
        size = ALIGN_UP(size, Satori::OBJECT_ALIGNMENT);
    }

    ForEachObjectRefCore(mt, size, lambda, includeCollectibleAllocator);
}

template <typename F>
inline void SatoriObject::ForEachObjectRef(F lambda, size_t size, bool includeCollectibleAllocator)
{
    _ASSERTE(size == Size());
    ForEachObjectRefCore(RawGetMethodTable(), size, lambda, includeCollectibleAllocator);
}

template <typename F>
FORCEINLINE void SatoriObject::ForEachObjectRefCore(MethodTable* mt, size_t size, F lambda, bool includeCollectibleAllocator)
{
    if (includeCollectibleAllocator)
    {
        ReportCollectibleAllocator(mt, lambda);
    }

    if (!mt->ContainsGCPointers())
    {
        return;
    }

    CGCDesc* map = CGCDesc::GetCGCDescFromMT(mt);
    CGCDescSeries* cur = map->GetHighestSeries();

    // GetNumSeries is actually signed.
    // Negative value means the pattern repeats componentNum times (struct arrays)
    ptrdiff_t numSeries = (ptrdiff_t)map->GetNumSeries();
    if (numSeries >= 0)
    {
        CGCDescSeries* last = map->GetLowestSeries();

        do
        {
            size_t refPtr = (size_t)this + cur->GetSeriesOffset();
            // series size is offset by the object size, so need to compensate for that.
            size_t refPtrStop = refPtr + size + cur->GetSeriesSize();

            // top check loop. this could be a zero-element array
            while (refPtr < refPtrStop)
            {
                lambda((SatoriObject**)refPtr);
                refPtr += sizeof(size_t);
            }

            cur--;
        } while (cur >= last);
    }
    else
    {
        // repeating pattern - an array of structs
        ptrdiff_t componentNum = ((ArrayBase*)this)->GetNumComponents();
        size_t refPtr = (size_t)this + cur->GetSeriesOffset();
        while (componentNum-- > 0)
        {
            for (ptrdiff_t i = 0; i > numSeries; i--)
            {
                val_serie_item item = *(cur->val_serie + i);
                size_t refPtrStop = refPtr + item.nptrs * sizeof(size_t);
                do
                {
                    lambda((SatoriObject**)refPtr);
                    refPtr += sizeof(size_t);
                } while (refPtr < refPtrStop);

                refPtr += item.skip;
            }
        }
    }
}

template <typename F>
inline void SatoriObject::ForEachObjectRef(F lambda, size_t size, size_t start, size_t end, bool includeCollectibleAllocator)
{
    _ASSERTE(size == Size());
    MethodTable* mt = RawGetMethodTable();

    // report the allocator once - with the range that includes the start of the object.
    if (includeCollectibleAllocator && start <= Start())
    {
        ReportCollectibleAllocator(mt, lambda);
    }

    if (!mt->ContainsGCPointers())
    {
        return;
    }

    CGCDesc* map = CGCDesc::GetCGCDescFromMT(mt);
    CGCDescSeries* cur = map->GetHighestSeries();

    // GetNumSeries is actually signed.
    // Negative value means the pattern repeats componentNum times (struct arrays)
    ptrdiff_t numSeries = (ptrdiff_t)map->GetNumSeries();
    if (numSeries >= 0)
    {
        CGCDescSeries* last = map->GetLowestSeries();

        // NB: the series are sorted by offset, and walking from the highest series down visits them
        //     in address order. Once a series reaches "end", there is nothing more in the range.
        do
        {
            size_t refPtr = (size_t)this + cur->GetSeriesOffset();
            // series size is offset by the object size, so need to compensate for that.
            size_t refPtrStop = refPtr + size + cur->GetSeriesSize();

            bool reachedEnd = refPtrStop >= end;
            if (reachedEnd)
            {
                refPtrStop = end;
            }

            refPtr = max(refPtr, start);

            // top check loop. this could be a zero-element array
            while (refPtr < refPtrStop)
            {
                lambda((SatoriObject**)refPtr);
                refPtr += sizeof(size_t);
            }

            if (reachedEnd)
            {
                return;
            }

            cur--;
        } while (cur >= last);
    }
    else
    {
        // repeating pattern - an array of structs
        ptrdiff_t componentNum = ((ArrayBase*)this)->GetNumComponents();
        size_t elementSize = mt->RawGetComponentSize();
        size_t refPtr = (size_t)this + cur->GetSeriesOffset();

        if (refPtr < start)
        {
            size_t skip = (start - refPtr) / elementSize;
            componentNum -= skip;
            refPtr += skip * elementSize;
        }

        while (componentNum-- > 0)
        {
            for (ptrdiff_t i = 0; i > numSeries; i--)
            {
                val_serie_item item = *(cur->val_serie + i);
                size_t refPtrStop = refPtr + item.nptrs * sizeof(size_t);

                if (refPtrStop <= start)
                {
                    // serie does not intersect with the range
                    refPtr = refPtrStop;
                }
                else
                {
                    bool reachedEnd = refPtrStop >= end;
                    if (reachedEnd)
                    {
                        refPtrStop = end;
                    }

                    refPtr = max(refPtr, start);

                    // top check loop. the range could end before this serie
                    while (refPtr < refPtrStop)
                    {
                        lambda((SatoriObject**)refPtr);
                        refPtr += sizeof(size_t);
                    }

                    if (reachedEnd)
                    {
                        return;
                    }
                }

                refPtr += item.skip;
            }
        }
    }
}

#endif
