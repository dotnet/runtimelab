// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

/*
 * This is a simulator for managed memory behaviors to test the GC performance. It has the following aspects:

1) It provides general things you'd want to do when testing perf, eg, # of threads, running time, allocated bytes.

2) It includes different building blocks to simulate different relevant behaviors for GC, eg, survival rates,
   flat or pointer-rich object graphs, different SOH/LOH ratio.
   
The idea is when we see a relevant behavior we want this simulator to be able to generate that behavior but we
also want to have a dial (ie, how intense that behavior is) and mix it with other behaviors.

Each run writes out a log named pid-output.txt that includes some general info such as parameters used and execution time.



Right now it's fairly simple - during initialization it allocates an array that holds onto byte 
arrays that are placed on SOH and LOH based on the alloc ratio given; and during steady state it 
allocates byte arrays and survives based on survival intervals given.

It has the following characteristics:

It has a very flat object graph and very simple lifetime. It's a simple test that's good for things that
mostly just care about # of bytes allocated/promoted like BGC scheduling.

It allows you to specify the following commandline args. Notes on commandline args -

1) they are NOT case sensitive;

2) always specify a number as the value for an arg. For boolean values use 1 for true and 0 for false.

3) if you want to still specify an arg but want to use the default (Why? Maybe you want to run this with a lot of different
configurations and it's easier to compare if you have all the configs specified), just specify something that can't be parsed
as an integer. For example you could specify "defArgName". That way you could replace all defArgName with a value if you needed.

-testKind/-tk: testKind
    Either "time" (default) or "highsurvival"
    For "highsurvival", -totalMins should be set, -sohSurvInterval and -lohSurvInterval should not

-threadCount/-tc: threadCount
allocating thread count (usually I use half of the # of CPUs on the machine, this is just to reduce the OS scheduler effect 
so we can test the GC effect better)

-lohAllocRatio/-lohar: lohAllocRatio
LOH alloc ratio (this controls the bytes we allocate on LOH out of all allocations we do)
It's in in per thousands (not percents! even though in the output it says %). So if it's 5, that means 
5‰ of the allocations will be on LOH.

-pohAllocRatio/-pohar: pohAllocRatio
(.NET 5.0 or later)
POH alloc ratio (this controls the bytes we allocate on POH out of all allocations we do)
It's in in per thousands (not percents! even though in the output it says %). So if it's 5, that means 
5‰ of the allocations will be on POH.

-totalLiveGB/-tlgb: totalLiveBytesGB
this is the total live data size in GB

-totalAllocGB/-tagb: totalAllocBytesGB
this is the total allocated size in GB, instead of accepting an arg like # of iterations where you don't really know what 
an iteration does, we use the allocated bytes to indicate how much work the threads do.

-requestAllocMB/-ramb: requestAllocBytes
this is used to simulate "request processing" in servers. we allocate this much and keep a fraction of it live 
until we've reached the total. Then we let go of all the objects allocated for this request. Multiple threads
may be working in parallel on separate requests. The idea is to keep a certain amount of memory live for requests in flight.
for the GC, this creates objects with an intermediate lifetime, so the percentage of surviving objects in gen 1 goes
down (otherwise, it would be very close to 100%, because most objects would either die in gen 0, or survive long enough so they
get promoted to gen 2).

-requestLiveMB/-rlmb: requestLiveBytes
how much memory to keep live during a request.

-reqSohSurvInterval/-rsohsi:
meaning every Nth SOH object allocated during a request survives

-reqLohSurvInterval/-rlohsi":
meaning every Nth LOH object allocated during a request survives

-reqPohSurvInterval/-rpohsi":
(.NET 5.0 or later)
meaning every Nth POH object allocated during a request survives

-totalMins/-tm: totalMinutesToRun
time to run in minutes (for things that need long term effects like scheduling you want to run for 
a while, eg, a few hours to see how stable it is)

Note that if neither -totalAllocMB nor -totalMins is specified, it will run for the default for -totalMins.
If both are specified, we take whichever one that's met first. 

-sohSizeRange/-sohsr: sohAllocLow, sohAllocHigh
eg: -sohSizeRange 100-4000 will set sohAllocLow and sohAllocHigh to 100 and 4000
we allocate SOH that's randomly chosen between this range.

-lohSizeRange/-lohsr: lohAllocLow, lohAllocHigh
we allocate LOH that's randomly chosen between this range.

-pohSizeRange/-pohsr: pohAllocLow, pohAllocHigh
(.NET 5.0 or later)
we allocate POH that's randomly chosen between this range.

-sizeDistribution/-sizeDist: 1/0
1 means use the built-in size distributions to generate bucket specs
0 (default) means generate simple ones from values (or defaults) for -sohsr and related arguments

-sohSurvInterval/-sohsi: sohSurvInterval
meaning every Nth SOH object allocated will survive. This is something we will consider changing to survival rate
later. When the allocated objects are of similar sizes the surv rate is 1/sohSurvInterval but we may not want them
to all be similar sizes.

-lohSurvInterval/-lohsi: lohSurvInterval
meaning every Nth LOH object allocated will survive. 

-pohSurvInterval/-pohsi:
(.NET 5.0 or later)
meaning every Nth POH object allocated will survive.

Note that -sohSurvInterval/-lohSurvInterval are only applicable for steady state.
During initialization everything survives.

-sohPinningInterval/-sohpi: sohPinningInterval
meaning every Nth SOH object survived will be pinned. 

-sohFinalizableInterval/-sohfi: sohFinalizableInterval
meaning every Nth SOH object survived will be finalizable.

-lohPinningInterval/-lohpi: lohPinningInterval
meaning every Nth LOH object survived will be pinned. 

-lohFinalizableInterval/-lohfi: lohFinalizableInterval
meaning every Nth LOH object survived will be finalizable.

-pohFinalizableInterval/-pohfi: pohFinalizableInterval
(.NET 5.0 or later)
meaning every Nth POH object survived will be finalizable.

-allocType/-at: allocType
What kind of objects are we allocating? Current supported types: 
0 means SimpleItem - a byte array (implemented by the Item class)
1 means ReferenceItem - contains refs and can form linked list (implemented by the ReferenceItemWithSize class)

-verifyLiveSize: 1/0
Perform some verification that the live object size matches the expected size

-printEveryNthIter: printEveryNthIter
Display a summary every N iterations

-handleTest - NOT IMPLEMENTED other than pinned handles. Should write some interesting cases for weak handles.

-lohPauseMeasure/-lohpm: lohPauseMeasure
measure the time it takes to do a LOH allocation. When turned on the top 10 longest pauses will be included in the log file.
TODO: The longest pauses are interesting but we should also include all pauses by pause buckets.

-endException/-ee: endException
induces an exception at the end so you can do some post mortem debugging.

-compute/-c: Do some extra computation between allocations, 1000 will reduce the allocation rate by a factor of 2-4

-finishWithFullCollect: Do collections until all allocated finalizable objects have been finalized

The default for these args are specified in "Default parameters".

---

Other things worth noting:

1) There's also a lohPauseMeasure var that you could make into a config - for now I just always measure pauses for LOH 
   allocs (since it was used for measuring LOH alloc pause wrt BGC sweep).

2) At the end of the test I do an EmptyWorkingSet - the reason for this is when I run the test in a loop if I don't 
   empty the WS for an iteration it's common to observe that it heavily affects the beginning of the next iteration.


For perf runs I usually run with GC internal logging on which means I'd want to get the last (in memory) part of the log 
at the end. So this provides a config that make it convenient for that purpose by intentionally inducing an exception at 
the end so you can get the last part of the logging by setting this

   for your post mortem debugging in the registry: 
   under HKLM\Software\Microsoft\Windows NT\CurrentVersion\AeDebug 
   you can add a reg value called Debugger of REG_SZ type and it should be
   "d:\debuggers\amd64\windbg" -p %ld -e %ld -c ".jdinfo 0x%p; !DumpGCLog c:\temp;q"
   (obviously make sure the directory for windbg is correct for the machine you run the test on and
   replace c:\temp with whatever directory that you want to put the last part of the log in)

This is by default off and can be turned on via the -endException/-ee config.

**********************************************************************************************************************

When working on this please KEEP THE FOLLOWING IN MIND:

1) Do not use anything fancier than needed. In other words, use very simple code in this project. I am not against 
   using cool/rich lang/framework features normally but for this purpose we want something that's as easy to reason about 
   as possible, eg, using things like linq is forbidden because you don't know how much allocations/survivals it 
   does and even if you do understand perfectly (which is doubtful) it might change and it's a burden for other 
   people who need to work on this. 
   
2) Clearly document your intentions for each building block you write. This is important for others because a building
   block is supposed to be easy to use so in general another person shouldn't need to understand how the code works
   in order to use it. For example, if a building block is supposed to allocate X bytes with Y links (where the user
   specifies X and Y) it should say that in the comments.

3) Only add new files if there's a good reason to. Do NOT create tons of little files.
*/

#define PRINT_ITER_INFO

#if DEBUG
#define STATISTICS
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;

#if STATISTICS
class Statistics
{
    public ulong sohAllocatedBytes;
    public ulong lohAllocatedBytes;
    public ulong pohAllocatedBytes;

    private static List<Statistics> allStatistics = new List<Statistics>();
    [ThreadStatic]
    private static Statistics? threadLocalStatistics = null;

    public static Statistics GetStatistics()
    {
        if (threadLocalStatistics == null)
        {
            lock (allStatistics)
            {
                if (threadLocalStatistics == null)
                {
                    threadLocalStatistics = new Statistics();
                    allStatistics.Add(threadLocalStatistics);
                }
            }
        }
        return threadLocalStatistics;
    }

    public Statistics()
    {
        sohAllocatedBytes = 0;
        lohAllocatedBytes = 0;
        pohAllocatedBytes = 0;
    }

    public static TestResult Aggregate()
    {
        ulong sohAllocatedBytes = 0;
        ulong lohAllocatedBytes = 0;
        ulong pohAllocatedBytes = 0;
        foreach (Statistics statistics in allStatistics)
        {
            sohAllocatedBytes += statistics.sohAllocatedBytes;
            lohAllocatedBytes += statistics.lohAllocatedBytes;
            pohAllocatedBytes += statistics.pohAllocatedBytes;
        }
        return new TestResult
        {
            sohAllocatedBytes = sohAllocatedBytes,
            lohAllocatedBytes = lohAllocatedBytes,
            pohAllocatedBytes = pohAllocatedBytes,
        };
    }
}
#endif

static class Util
{
    public static void AlwaysAssert(bool condition, string? message = null)
    {
        if (!condition)
            throw new Exception(message);
    }

    private const ulong BYTES_IN_KB = 1024;
    private const ulong BYTES_IN_MB = 1024 * BYTES_IN_KB;
    private const ulong BYTES_IN_GB = 1024 * BYTES_IN_MB;

    public static double BytesToGB(ulong bytes) =>
        ((double)bytes) / BYTES_IN_GB;

    public static ulong GBToBytes(double GB) =>
        (ulong)Math.Round(GB * BYTES_IN_GB);

    public static double BytesToMB(ulong bytes) =>
        ((double)bytes) / BYTES_IN_MB;

    public static ulong MBToBytes(double MB) =>
        (ulong)Math.Round(MB * BYTES_IN_MB);

    public static ulong Mean(ulong a, ulong b) =>
        (a + b) / 2;

    public static bool AboutEquals(double a, double b)
    {
        if (b == 0)
            return a == 0;
        else
            return Math.Abs((a / b) - 1.0) < 0.1;
    }

    public static void AssertAboutEqual(ulong a, ulong b)
    {
        if (!AboutEquals((double)a, (double)b))
        {
            throw new Exception($"Values are not close: {a}, {b}");
        }
    }

    public static T NonNull<T>(T? value) where T : class
    {
        if (value == null)
        {
            throw new NullReferenceException();
        }
        else
        {
            return value;
        }
    }

    public static T NonNull<T>(T? value) where T : struct
    {
        if (value == null)
        {
            throw new NullReferenceException();
        }
        else
        {
            return value.Value;
        }
    }

    static uint GetPointerSize() => (uint)IntPtr.Size;

    public static readonly uint POINTER_SIZE = GetPointerSize();

    public static readonly uint OBJECT_HEADER_SIZE = 2 * Util.POINTER_SIZE;

    public static readonly uint ARRAY_HEADER_SIZE = 3 * Util.POINTER_SIZE;

    public static ulong ArraySize(ITypeWithPayload?[] a) =>
        (((ulong)a.Length) + 3) * POINTER_SIZE;

    public static bool isNth(uint interval, ulong count) =>
        interval != 0 && (count % interval) == 0;
}

sealed class Rand
{
    private uint x = 0;

    private uint GetRand()
    {
        unchecked
        {
            x = (314159269 * x + 278281) & 0x7FFFFFFF;
        }
        return x;
    }

    // obtain random number in the range 0 .. r-1
    public uint GetRand(uint r) =>
        (uint)(((ulong)GetRand() * r) >> 31);

    public uint GetRand(uint low, uint high) =>
        low + GetRand(high - low);

    public uint GetRand(SizeRange range) =>
        GetRand(range.low, range.high);

    public double GetFloat() =>
        (double)GetRand() / (double)0x80000000ul;
};

interface ITypeWithPayload
{
    class Totals
    {
        public static long NumCreatedWithFinalizers
            => Item.NumCreatedWithFinalizers + ReferenceItemWithSize.NumCreatedWithFinalizers;
        public static long NumFinalized
            => Item.NumFinalized + ReferenceItemWithSize.NumFinalized;
    }

    byte[] GetPayload();
    ulong TotalSize { get; }
    void Free();
}

enum TestKind
{
    time,
    highsurvival,
}

enum ItemType
{
    SimpleItem = 0,
    ReferenceItem = 1
};

enum ItemState
{
    // there isn't a handle associated with this object.
    NoHandle = 0,
    Pinned = 1,
    Strong = 2,
    WeakShort = 3,
    WeakLong = 4
};

abstract class Item : ITypeWithPayload
{
#if DEBUG
    public static long NumConstructed = 0;
    public static long NumFreed = 0;
#endif
    public static long NumCreatedWithFinalizers = 0;
    public static long NumFinalized = 0;

    public byte[]? payload; // Only null if this item has been freed
    public ItemState state;
    public GCHandle h;

    public static readonly uint FieldSize = 3 * Util.POINTER_SIZE;
    public static readonly uint ItemObjectSize = Util.OBJECT_HEADER_SIZE + FieldSize;

    public static uint ArrayOverhead = ItemObjectSize + Util.ARRAY_HEADER_SIZE;
    public static uint SohOverhead = ItemObjectSize;

    // TODO: isWeakLong never used
    public static Item New(uint size, bool isPinned, bool isFinalizable, bool isWeakLong = false, bool isPoh = false)
        => isFinalizable
            ? (Item) new ItemFinalizable(size, isPinned, isWeakLong, isPoh)
            : new ItemNonFinalizable(size, isPinned, isWeakLong, isPoh);

    private Item(uint size, bool isPinned, bool isWeakLong, bool isPoh)
    {
#if DEBUG
        Interlocked.Increment(ref NumConstructed);
#endif
#if STATISTICS
        Statistics statistics = Statistics.GetStatistics();
        statistics.sohAllocatedBytes += ItemObjectSize;
#endif
        if (size <= ArrayOverhead)
        {
            Console.WriteLine("allocating objects <= {0} is not supported for the Item class", size);
            throw new InvalidOperationException("Item class does not support allocating an object of this size");
        }
        uint payloadSize = size - ArrayOverhead;
        uint remainingSize = size - ItemObjectSize;

        if (isPoh)
        {
#if NET5_0_OR_GREATER
#if STATISTICS
            statistics.pohAllocatedBytes += remainingSize;
#endif
            payload = GC.AllocateArray<byte>((int)payloadSize, pinned: true);
#else
            throw new Exception("UNREACHABLE: POH allocations require netcoreapp5.0 or higher");
#endif
        }
        else
        {
#if STATISTICS
            if (size >= MemoryAlloc.LohThreshold)
            {
                statistics.lohAllocatedBytes += remainingSize;
            }
            else
            {
                statistics.sohAllocatedBytes += remainingSize;
            }
#endif
            payload = new byte[payloadSize];
        }

        // We only support these 3 states right now.
        state = (isPinned ? ItemState.Pinned : (isWeakLong ? ItemState.WeakLong : ItemState.NoHandle));

        // We can consider supporting multiple handles pointing to the same object. For now 
        // I am only doing at most one handle per item.
        if (isPinned || isWeakLong)
        {
            h = GCHandle.Alloc(payload, isPinned ? GCHandleType.Pinned : GCHandleType.WeakTrackResurrection);
        }

        Debug.Assert(TotalSize == size);
    }

    public ulong TotalSize => ArrayOverhead + (ulong)Util.NonNull(payload).Length;

    public void Free()
    {
#if DEBUG
        Interlocked.Increment(ref NumFreed);

        switch (state)
        {
            case ItemState.NoHandle:
                Util.AlwaysAssert(!h.IsAllocated);
                break;
            case ItemState.Pinned:
            case ItemState.WeakLong:
                Util.AlwaysAssert(h.IsAllocated);
                break;
            case ItemState.Strong:
            case ItemState.WeakShort:
            default:
                Util.AlwaysAssert(false); // these are unused
                break;
        }
#endif

        Debug.Assert((h.IsAllocated) == (state != ItemState.NoHandle)); // Note: this means the enum isn't useful..
        if (state != ItemState.NoHandle)
        {
            Debug.Assert(h.IsAllocated);
            h.Free();
        }

        Util.AlwaysAssert(!h.IsAllocated);

        payload = null;
    }

    public byte[] GetPayload() =>
        Util.NonNull(payload);

    sealed class ItemFinalizable : Item
    {
        public ItemFinalizable(uint size, bool isPinned, bool isWeakLong, bool isPoh)
            : base(size, isPinned, isWeakLong, isPoh)
        {
            Interlocked.Increment(ref NumCreatedWithFinalizers);
        }

        ~ItemFinalizable()
        {
            Interlocked.Increment(ref NumFinalized);
        }
    }

    sealed class ItemNonFinalizable : Item
    {
        public ItemNonFinalizable(uint size, bool isPinned, bool isWeakLong, bool isPoh)
            : base(size, isPinned, isWeakLong, isPoh) { }
    }
};

// This just contains a byte array to take up space
// and since it contains a ref it will exercise mark stack.
class SimpleRefPayLoad
{
#if DEBUG
    public static long NumPinned = 0;
    public static long NumUnpinned = 0;
#endif

    public byte[] payload;
    GCHandle handle;

    public static readonly uint FieldSize = 2 * Util.POINTER_SIZE;
    public static readonly uint SimpleRefPayLoadSize = Util.OBJECT_HEADER_SIZE + FieldSize;

    public static uint ArrayOverhead = SimpleRefPayLoadSize + Util.ARRAY_HEADER_SIZE;

    public SimpleRefPayLoad(uint size, bool isPinned, bool isPoh)
    {
#if STATISTICS
        Statistics statistics = Statistics.GetStatistics();
        statistics.sohAllocatedBytes += SimpleRefPayLoadSize;
#endif
        uint sizePayload = size - ArrayOverhead;
        uint remainingSize = size - SimpleRefPayLoadSize;
        if (isPoh)
        {
#if NET5_0_OR_GREATER
#if STATISTICS
            statistics.pohAllocatedBytes += remainingSize;
#endif
            payload = GC.AllocateArray<byte>((int)sizePayload, pinned: true);
#else
            throw new Exception("UNREACHABLE: POH allocations require netcoreapp5.0 or higher");
#endif
        }
        else
        {
#if STATISTICS
            if (size >= MemoryAlloc.LohThreshold)
            {
                statistics.lohAllocatedBytes += remainingSize;
            }
            else
            {
                statistics.sohAllocatedBytes += remainingSize;
            }
#endif
            payload = new byte[sizePayload];
        }

        Debug.Assert(OwnSize == size);

        if (isPinned)
        {
            handle = GCHandle.Alloc(payload, GCHandleType.Pinned);
#if DEBUG
            Interlocked.Increment(ref NumPinned);
#endif
        }
    }

    public void Free()
    {
        if (handle.IsAllocated)
        {
            handle.Free();
#if DEBUG
            Interlocked.Increment(ref NumUnpinned);
#endif
        }
    }

    public ulong OwnSize => ((ulong)payload.Length) + ArrayOverhead;
}

enum ReferenceItemOperation
{
    NewWithExistingList = 0,
    NewWithNewList = 1,
    MultipleNew = 2,
    MaxOperation = 3
};

// ReferenceItem is structured this way so we can point to other
// ReferenceItemWithSize objects on demand and record how much 
// memory it's holding alive.
abstract class ReferenceItemWithSize : ITypeWithPayload
{
#if DEBUG
    public static long NumConstructed = 0;
    public static long NumFreed = 0;
#endif
    public static long NumCreatedWithFinalizers = 0;
    public static long NumFinalized = 0;

    // The size includes indirect children too.

    // The way we link these together is a linked list.
    // level indicates how many nodes are on the list pointed
    // to by next.
    // We could traverse the list to figure out sizeTotal and level,
    // but I'm keeping them so it's readily available. The list could
    // get very long.
    private SimpleRefPayLoad? payload; // null only if this has been freed
    private ReferenceItemWithSize? next; // Note: ReferenceItemWithSize owns its 'next' -- should be the only reference to that
    public ulong TotalSize { get; private set; }

    public static readonly uint FieldSize = 2 * Util.POINTER_SIZE + sizeof(ulong);
    public static readonly uint ReferenceItemWithSizeSize = Util.OBJECT_HEADER_SIZE + FieldSize;

    public static readonly uint ArrayHeaderSize = 3 * Util.POINTER_SIZE;

    public static uint SohOverhead = ReferenceItemWithSizeSize + SimpleRefPayLoad.SimpleRefPayLoadSize;
    public static uint SimpleOverhead = ReferenceItemWithSizeSize;

    public static ReferenceItemWithSize New(uint size, bool isPinned, bool isFinalizable, bool isPoh)
        => isFinalizable
            ? (ReferenceItemWithSize) new ReferenceItemWithSizeFinalizable(size, isPinned, isPoh)
            : new ReferenceItemWithSizeNonFinalizable(size, isPinned, isPoh);

    protected ReferenceItemWithSize(uint size, bool isPinned, bool isPoh)
    {
        Debug.Assert(size >= SimpleOverhead + SimpleRefPayLoad.ArrayOverhead);
#if DEBUG
        Interlocked.Increment(ref NumConstructed);
#endif
#if STATISTICS
        Statistics statistics = Statistics.GetStatistics();
        statistics.sohAllocatedBytes += ReferenceItemWithSizeSize;
#endif
        uint sizePayload = size - SimpleOverhead;
        payload = new SimpleRefPayLoad(sizePayload, isPinned: isPinned, isPoh: isPoh);
        Debug.Assert(OwnSize == size);
        TotalSize = size;
    }

    public ReferenceItemWithSize? FreeHead()
    {
#if DEBUG
        Interlocked.Increment(ref NumFreed);
#endif
        ReferenceItemWithSize? tail = this.next;
        Util.NonNull(payload).Free();
        payload = null;
        return tail;
    }

    public void Free()
    {
#if DEBUG
        Interlocked.Increment(ref NumFreed);
#endif
        // Should not have already been freed, so payload should be non-null
        Util.NonNull(payload).Free();
        payload = null;
        next?.Free();
        next = null;
    }

    public byte[] GetPayload() => Util.NonNull(payload).payload;

    private ulong OwnSize => Util.NonNull(payload).OwnSize + SimpleOverhead;

    // NOTE: This adds the item on to the end of anything currently referenced.
    public void AddToEndOfList(ReferenceItemWithSize refItem)
    {
        if (next == null)
        {
            next = refItem;
        }
        else
        {
            next.AddToEndOfList(Util.NonNull(refItem));
        }
        TotalSize += refItem.TotalSize;

#if DEBUG
        ulong check = 0;
        for (ReferenceItemWithSize? r = this; r != null; r = r.next)
            check += r.OwnSize;
        Debug.Assert(check == TotalSize);
#endif
    }

    sealed class ReferenceItemWithSizeFinalizable : ReferenceItemWithSize
    {
        public ReferenceItemWithSizeFinalizable(uint size, bool isPinned, bool isPoh)
            : base(size, isPinned, isPoh)
        {
            Interlocked.Increment(ref NumCreatedWithFinalizers);
        }

        ~ReferenceItemWithSizeFinalizable()
        {
            Interlocked.Increment(ref NumFinalized);
        }
    }

    sealed class ReferenceItemWithSizeNonFinalizable : ReferenceItemWithSize
    {
        public ReferenceItemWithSizeNonFinalizable(uint size, bool isPinned, bool isPoh)
            : base(size, isPinned, isPoh) { }
    }
}


readonly struct SizeRange
{
    public readonly uint low;
    public readonly uint high;

    public SizeRange(uint low, uint high)
    {
        this.low = low;
        this.high = high;
    }

    public ulong Mean => Util.Mean(low, high);

    public override string ToString() =>
        $"{low}-{high}";
}

readonly struct BucketSpec
{
    public readonly SizeRange sizeRange;
    public readonly uint survInterval;
    public readonly uint reqSurvInterval;
    // Note: pinInterval and finalizableInterval only affect surviving objects
    public readonly uint pinInterval;
    public readonly uint finalizableInterval;
    // If we have buckets with weights of 2 and 1, we'll allocate, on average, 2 objects from the first bucket per 1 from the next.
    public readonly double weight;

    public readonly bool isPoh;

    public BucketSpec(SizeRange sizeRange, uint survInterval, uint reqSurvInterval, uint pinInterval, uint finalizableInterval, double weight, bool isPoh = false)
    {
        Debug.Assert(weight != 0);
        this.sizeRange = sizeRange;
        this.survInterval = survInterval;
        this.reqSurvInterval = reqSurvInterval;
        this.pinInterval = pinInterval;
        this.finalizableInterval = finalizableInterval;
        this.weight = weight;
        this.isPoh = isPoh;

        // Should avoid creating the bucket in this case, as our algorithm assumes it should use the bucket at least once
        Util.AlwaysAssert(weight != 0);

        if (this.pinInterval != 0 || this.finalizableInterval != 0)
        {
            Util.AlwaysAssert(
                this.survInterval != 0,
                $"pinInterval and finalizableInterval only affect surviving objects, but nothing survives (in bucket with size range {sizeRange})");
        }
    }

    public override string ToString()
    {
        string result = $"{sizeRange}; surv every {survInterval}; pin every {pinInterval}; finalize every {finalizableInterval}; weight {weight}";

#if NET5_0_OR_GREATER
        result += $"; isPoh {isPoh}";
#endif

        return result;
    }
}

struct SizeSlot
{
    public uint size;
    public uint count;
    SizeSlot(uint size, uint count)
    {
        this.size = size;
        this.count = count;
    }

    // an object size distribution for SOH/LOH derived from a real server scenario
    public static SizeSlot[] sohLohSizeSlots = new SizeSlot[]
    {
//        new SizeSlot(        24,  16229766), We are creating some small wrapper objects of 32 and 40 bytes around the
//        new SizeSlot(        32,  48790439), payload objects - somewhat compensate for this by distorting the distribution
//        new SizeSlot(        40,  21496250), at the low end
//        new SizeSlot(        48,  15020039),
//
        new SizeSlot(        56,  12134890),
        new SizeSlot(        64,   7554729),
        new SizeSlot(        72,   4745645),
        new SizeSlot(        80,   8807161),
        new SizeSlot(        88,   4490991),
        new SizeSlot(        96,   3713033),
        new SizeSlot(       104,   3201425),
        new SizeSlot(       112,    854877),
        new SizeSlot(       120,    730058),
        new SizeSlot(       128,    592202),
        new SizeSlot(       136,   2656705),
        new SizeSlot(       152,    820536),
        new SizeSlot(       168,    658168),
        new SizeSlot(       176,    724457),
        new SizeSlot(       192,    633177),
        new SizeSlot(       216,    571147),
        new SizeSlot(       240,    566779),
        new SizeSlot(       264,    413803),
        new SizeSlot(       280,    314899),
        new SizeSlot(       304,    357706),
        new SizeSlot(       328,    314889),
        new SizeSlot(       368,    292992),
        new SizeSlot(       408,    242612),
        new SizeSlot(       432,    620812),
        new SizeSlot(       496,    207587),
        new SizeSlot(       536,    188057),
        new SizeSlot(       568,    440902),
        new SizeSlot(       656,    165109),
        new SizeSlot(       752,    145145),
        new SizeSlot(       896,    121960),
        new SizeSlot(      1088,    104732),
        new SizeSlot(      1360,     85235),
        new SizeSlot(      1600,     69567),
        new SizeSlot(      2032,    167972),
        new SizeSlot(      2392,     51544),
        new SizeSlot(      2488,     44129),
        new SizeSlot(      3936,     35277),
        new SizeSlot(      4120,     30876),
        new SizeSlot(      8216,     21207),
        new SizeSlot(     15048,     10583),
        new SizeSlot(     20032,      7304),
        new SizeSlot(     37496,      4076),
        new SizeSlot(     65560,      2540),
        new SizeSlot(     87168,      1430),
        new SizeSlot(     96096,      1292),
        new SizeSlot(     97200,      4388),
        new SizeSlot(    106040,      1199),
        new SizeSlot(    108776,      1151),
        new SizeSlot(    110440,      1143),
        new SizeSlot(    113752,      1135),
        new SizeSlot(    117680,      1109),
        new SizeSlot(    130152,      1053),
        new SizeSlot(    131096,     46426),
        new SizeSlot(    140160,      1576),
        new SizeSlot(    175640,      1249),
        new SizeSlot(    202080,      2309),
        new SizeSlot(    270016,       813),
        new SizeSlot(    318832,       683),
        new SizeSlot(    454584,       568),
        new SizeSlot(    524312,       487),
        new SizeSlot(    674704,       370),
        new SizeSlot(   1453768,       216),
        new SizeSlot(   5639144,        85),
        new SizeSlot(  32000176,        26),
        new SizeSlot(  33554456,         1),
    };

    public static void BuildBucketSpecsFromSizeDistribution(List<BucketSpec> bucketSpecs, SizeSlot[] sizeSlots, SizeRange limitSizeRange, uint survInterval, uint reqSurvInterval, uint pinInterval, uint finalizableInterval, bool isPoh)
    {
        uint overhead = ReferenceItemWithSize.SohOverhead;
        uint lowSize = limitSizeRange.low;
        for (int i = 0; i < sizeSlots.Length; i++)
        {
            if (sizeSlots[i].size < limitSizeRange.low || limitSizeRange.high < sizeSlots[i].size)
                continue;
            SizeRange sizeRange = new SizeRange(lowSize + overhead + 1, sizeSlots[i].size + overhead + 1);
            lowSize = sizeSlots[i].size;
            double weight = (double)sizeSlots[i].count * Math.Max(survInterval, 1);
            BucketSpec bucketSpec = new BucketSpec(sizeRange, survInterval, reqSurvInterval, pinInterval, finalizableInterval, weight, isPoh);
            bucketSpecs.Add(bucketSpec);
        }
    }
    public static void BuildSOHBucketSpecsFromSizeDistribution(List<BucketSpec> bucketSpecs, uint survInterval, uint reqSurvInterval, uint pinInterval, uint finalizableInterval)
    {
        BuildBucketSpecsFromSizeDistribution(bucketSpecs, sohLohSizeSlots, new SizeRange(48, 84_999), survInterval, reqSurvInterval, pinInterval, finalizableInterval, false);
    }
    public static void BuildLOHBucketSpecsFromSizeDistribution(List<BucketSpec> bucketSpecs, uint survInterval, uint reqSurvInterval, uint pinInterval, uint finalizableInterval)
    {
        BuildBucketSpecsFromSizeDistribution(bucketSpecs, sohLohSizeSlots, new SizeRange(85_000, uint.MaxValue), survInterval, reqSurvInterval, pinInterval, finalizableInterval, false);
    }

    // an object size distribution for OH derived from a real server scenario
    public static SizeSlot[] pohSizeSlots = new SizeSlot[]
    {
        new SizeSlot(        56,        16),
        new SizeSlot(      1048,         1),
        new SizeSlot(      2072,         1),
        new SizeSlot(      4120,         1),
        new SizeSlot(      8184,         2),
        new SizeSlot(      8216,         1),
        new SizeSlot(     16344,         2),
        new SizeSlot(     16408,         1),
        new SizeSlot(     32664,         2),
        new SizeSlot(     32792,         1),
        new SizeSlot(     65304,         2),
        new SizeSlot(     65560,         1),
        new SizeSlot(    130584,         1),
        new SizeSlot(    131064,       160),
        new SizeSlot(   2259408,         1),
    };

    public static void BuildPOHBucketSpecsFromSizeDistribution(List<BucketSpec> bucketSpecs, SizeSlot[] sizeSlots, SizeRange sizeRange, uint survInterval, uint reqSurvInterval, uint pinInterval, uint finalizableInterval)
    {
        BuildBucketSpecsFromSizeDistribution(bucketSpecs, sizeSlots, sizeRange, survInterval, reqSurvInterval, pinInterval, finalizableInterval, true);
    }
    public static void BuildPOHBucketSpecsFromSizeDistribution(List<BucketSpec> bucketSpecs, uint survInterval, uint reqSurvInterval, uint pinInterval, uint finalizableInterval)
    {
        BuildBucketSpecsFromSizeDistribution(bucketSpecs, pohSizeSlots, new SizeRange(24, 10_000_000), survInterval, reqSurvInterval, pinInterval, finalizableInterval, true);
    }
}


readonly struct Phase
{
    public readonly TestKind testKind;
    public readonly ItemType allocType;
    public readonly ulong totalLiveBytes;
    public readonly ulong totalAllocBytes;
    public readonly ulong requestLiveBytes;
    public readonly ulong requestAllocBytes;
    public readonly double totalMinutesToRun;
    public readonly BucketSpec[] buckets;
    public readonly uint threadCount;
    public readonly uint compute;

    public Phase(
        TestKind testKind,
        ulong totalLiveBytes, ulong totalAllocBytes, double totalMinutesToRun,
        ulong requestLiveBytes, ulong requestAllocBytes,
        BucketSpec[] buckets,
        ItemType allocType,
        uint threadCount,
        uint compute)
    {
        Util.AlwaysAssert(totalAllocBytes != 0); // Must be set
        Util.AlwaysAssert(buckets.Length != 0);

        this.testKind = testKind;
        this.totalLiveBytes = totalLiveBytes;
        this.totalAllocBytes = totalAllocBytes;
        this.totalMinutesToRun = totalMinutesToRun;
        this.requestLiveBytes = requestLiveBytes;
        this.requestAllocBytes = requestAllocBytes;
        this.buckets = buckets;
        this.allocType = allocType;
        this.threadCount = threadCount;
        this.compute = compute;
    }

    public void Validate()
    {
        Util.AlwaysAssert(totalAllocBytes != 0); // Must be set
    }

    public bool print => false;
    public bool lohPauseMeasure => false;

    public void Describe()
    {
        Console.WriteLine($"{testKind}, {allocType}, tlgb {Util.BytesToGB(totalLiveBytes)}, tagb {Util.BytesToGB(totalAllocBytes)}, totalMins {totalMinutesToRun}, buckets:");
        for (uint i = 0; i < buckets.Length; i++)
        {
            Console.WriteLine("    {0}", buckets[i]);
        }
    }
}

readonly struct PerThreadArgs
{
    public readonly bool verifyLiveSize;
    public readonly uint printEveryNthIter;
    public readonly Phase[] phases;
    public PerThreadArgs(bool verifyLiveSize, uint printEveryNthIter, Phase[] phases)
    {
        this.verifyLiveSize = verifyLiveSize;
        this.printEveryNthIter = printEveryNthIter;
        this.phases = phases;
        for (uint i = 0; i < phases.Length; i++)
        {
            phases[i].Validate();
        }
    }
}

// .NET 4.7 does not have ReadOnlySpan<char>, so use this instead.
ref struct CharSpan
{
    private readonly string text;
    private readonly uint begin;
    public readonly uint Length;

    private CharSpan(string text, uint begin, uint length)
    {
        Debug.Assert(begin + length <= text.Length);
        this.text = text;
        this.begin = begin;
        Length = length;
    }

    public static implicit operator CharSpan(string s) =>
        new CharSpan(s, 0, (uint)s.Length);

    public static CharSpan OfString(string text) =>
        new CharSpan(text, 0, (uint)text.Length);

    public CharSpan Slice(uint begin, uint length)
    {
        Debug.Assert(begin + length <= this.Length);
        return new CharSpan(text: this.text, begin: this.begin + begin, length: length);
    }

    public CharSpan Slice(uint begin) =>
        Slice(begin, this.Length - begin);

    public char this[uint index]
    {
        get
        {
            Debug.Assert(index < Length);
            return text[(int)(begin + index)];
        }
    }

    public static bool operator ==(CharSpan a, CharSpan b)
    {
        // The '==' operator seems to test whether the spans refer to the same range of memory.
        // I can't find any builtin function for comparing actual equality, which seems weird.
        if (a.Length != b.Length)
            return false;
        for (uint i = 0; i < a.Length; i++)
            if (a[i] != b[i])
                return false;
        return true;
    }

    public static bool operator !=(CharSpan a, CharSpan b) =>
        !(a == b);

    public override bool Equals(object? obj) =>
        throw new NotImplementedException();

    public override int GetHashCode() =>
        throw new NotImplementedException();

    public override string ToString() =>
        text.Substring((int)this.begin, (int)this.Length);
}

ref struct TextReader
{
    CharSpan text;
    uint lineNumber;
    uint columnNumber;

    public TextReader(string fileName)
    {
        // TODO: dotnet was hanging on all accesses to invalid UNC paths, even just testing if it exists. So forbid those for now.
        if (fileName.StartsWith("//") || fileName.StartsWith("\\\\"))
            throw new Exception("TODO");
        this.text = CharSpan.OfString(File.ReadAllText(fileName));
        this.lineNumber = 1;
        this.columnNumber = 1;
    }

    public char Peek => text[0];

    public char Take()
    {
        char c = Peek;
        if (c == '\n')
        {
            lineNumber++;
            columnNumber = 1;
        }
        else
        {
            columnNumber++;
        }
        text = text.Slice(1);
        return c;
    }

    private CharSpan TakeN(uint n)
    {
        CharSpan span = text.Slice(0, n);
        for (uint i = 0; i < span.Length; i++)
            Debug.Assert(span[i] != '\n');
        text = text.Slice(n);
        columnNumber += n;
        return span;
    }

    public void Assert(bool condition, string message = "Parsing failed")
    {
        if (!condition)
            throw Fail(message);
    }

    public Exception Fail(string message = "Parsing failed") =>
        new Exception($"Parse error at {lineNumber}:{columnNumber} -- {message}");

    public void SkipBlankLines()
    {
        while (!Eof)
        {
            switch (Peek)
            {
                case '\r':
                case '\n':
                    Take();
                    break;
                case ';':
                case '#':
                    while (Take() != '\n') { }
                    break;
                default:
                    return;
            }
        }
    }

    public bool TryTake(char c)
    {
        if (Peek == c)
        {
            Take();
            return true;
        }
        else
        {
            return false;
        }
    }

    public void SkipWhite()
    {
        while (!Eof && IsWhite(Peek))
        {
            Take();
        }
    }
    public bool Eof =>
        text.Length == 0;

    public void ShouldTake(char c)
    {
        if (Take() != c)
            Fail($"Expected to take '{c}'");
    }

    public void TakeSpace()
    {
        ShouldTake(' ');
    }

    public uint TakeUInt() =>
        (uint)TakeUlong();

    public ulong TakeUlong()
    {
        Assert(IsDigit(Peek), "Expected to parse a ulong");
        uint i = 1;
        for (; i < text.Length && IsDigit(text[i]); i++) { }
        return ulong.Parse(TakeN(i).ToString());
    }

    public double TakeDouble()
    {
        Assert(IsDigitOrDecimalSeparator(Peek), "Expected to parse a double");
        uint i = 1;
        for (; i < text.Length && IsDigitOrDecimalSeparator(text[i]); i++) { }
        return double.Parse(TakeN(i).ToString());
    }

    public CharSpan TakeWord()
    {
        SkipWhite();
        Assert(IsLetter(Peek), "Expected to parse a word");
        uint i = 1;
        for (; i < text.Length && IsLetter(text[i]); i++) { }
        return TakeN(i);
    }

    private static bool IsLetter(char c) =>
        ('a' <= c && c <= 'z') || ('A' <= c && c <= 'Z');
    private static bool IsDigit(char c) =>
        '0' <= c && c <= '9';
    private static bool IsDigitOrDecimalSeparator(char c) =>
        IsDigit(c) || c == '.' || c == ',';
    private static bool IsWhite(char c) =>
        c == ' ' || c == '\t';
}

class Args
{
    public readonly uint threadCount;
    public readonly PerThreadArgs perThreadArgs;
    public readonly bool finishWithFullCollect;
    public readonly bool endException;

    public Args(uint threadCount, in PerThreadArgs perThreadArgs, bool finishWithFullCollect, bool endException)
    {
        this.threadCount = threadCount;
        this.perThreadArgs = perThreadArgs;
        this.finishWithFullCollect = finishWithFullCollect;
        this.endException = endException;
    }

    public void Describe()
    {
        Console.WriteLine($"Running {threadCount} threads.");
        for (uint i = 0; i < perThreadArgs.phases.Length; i++)
            perThreadArgs.phases[i].Describe();
    }
}

class ArgsParser
{
    public static Args Parse(string[] args)
    {
        if (args[0] == "-file")
        {
            Util.AlwaysAssert(args.Length == 2, "`-file path` must be the only arguments if present");
            return ParseFromFile(args[1]);
        }
        else
        {
            return ParseFromCommandLine(args);
        }
    }

    private static uint EnumFromNames(string[] names, CharSpan name)
    {
        for (uint i = 0; i < names.Length; i++)
        {
            if (names[i] == name)
            {
                return i;
            }
        }
        throw new Exception($"Invalid enum member {name.ToString()}, accepted: {string.Join(", ", names)}");
    }

    private static TestKind ParseTestKind(CharSpan str) =>
        (TestKind)EnumFromNames(testKindNames, str);

    private static ItemType ParseItemType(CharSpan str) =>
        (ItemType)EnumFromNames(itemTypeNames, str);

    private static void ParseRange(string str, out uint lo, out uint hi)
    {
        string[] parts = str.Split('-');
        Util.AlwaysAssert(parts.Length == 2);
        lo = ParseUInt32(parts[0]);
        hi = ParseUInt32(parts[1]);
    }

    private static readonly string[] testKindNames = new string[] { "time", "highSurvival" };
    private static readonly string[] itemTypeNames = new string[] { "simple", "reference" };

    private static uint ParseUInt32(string s)
    {
        bool success = s.StartsWith("0x")
            ? uint.TryParse(s.Substring("0x".Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)
            : uint.TryParse(s, out v);
        return success ? v : throw new Exception($"Failed to parse {s}");
    }

    private static double ParseDouble(string strDouble) => double.TryParse(strDouble, out double v) ? v : throw new Exception($"Failed to parse {strDouble}");

    private enum State { ParsePhase, ParseBucket, Eof };

    private static Args ParseFromFile(string fileName)
    {
        TextReader text = new TextReader(fileName);
        bool verifyLiveSize = false;
        uint printEveryNthIter = 0;
        uint threadCount = 1;
        uint compute = 0;
        bool finishWithFullCollect = false;
        text.SkipBlankLines();
        while (true)
        {
            State? s = TryReadTag(ref text);
            BucketSpec[] bucketSpecs = new BucketSpec[0];
            if (s == State.ParseBucket)
            {
                (s, bucketSpecs) = ParseBuckets(ref text);
            }
            Phase[] phases = new Phase[0];
            if (s == State.ParsePhase)
            {
                (s, phases) = ParsePhases(ref text, threadCount, compute);
            }
            if (s == State.Eof)
            {
                return new Args(
                    threadCount: threadCount,
                    perThreadArgs: new PerThreadArgs(
                        verifyLiveSize: verifyLiveSize,
                        printEveryNthIter: printEveryNthIter,
                        phases: phases),
                    finishWithFullCollect: finishWithFullCollect,
                    endException: false);
            }
            CharSpan word = text.TakeWord();
            text.TakeSpace();
            if (word == "printEveryNthIter")
            {
                printEveryNthIter = text.TakeUInt();
            }
            else if (word == "threadCount")
            {
                threadCount = text.TakeUInt();
            }
            else if (word == "verifyLiveSize")
            {
                verifyLiveSize = true;
            }
            else if (word == "compute")
            {
                compute = text.TakeUInt();
            }
            else if (word == "finishWithFullCollect")
            {
                finishWithFullCollect = true;
            }
            else
            {
                throw text.Fail($"Unexpected argument '{word.ToString()}'");
            }
            text.SkipBlankLines();
        }
    }

    // Called after we see the first [phase]; ends at EOF
    static (State, Phase[]) ParsePhases(ref TextReader text, uint threadCount, uint compute)
    {
        List<Phase> res = new List<Phase>();
        while (true)
        {
            (State s, Phase p) = ParsePhase(ref text, threadCount, compute);
            res.Add(p);
            if (s != State.ParsePhase)
            {
                Util.AlwaysAssert(s == State.Eof);
                return (s, res.ToArray());
            }
        }
    }

    static (State, Phase) ParsePhase(ref TextReader text, uint threadCount, uint compute)
    {
        TestKind testKind = TestKind.time;
        ulong? totalLiveBytes = null;
        ulong? totalAllocBytes = null;
        ulong requestLiveBytes = 0;
        ulong requestAllocBytes = 0;
        double totalMinutesToRun = 0;
        ItemType allocType = ItemType.ReferenceItem;

        while (true)
        {
            State? s = TryReadTag(ref text);
            if (s != null)
            {
                text.Assert(totalLiveBytes.HasValue && totalAllocBytes.HasValue, "Must set 'totalLiveMB' and 'totalAllocMB'");
                Debug.Assert(totalLiveBytes.HasValue && totalAllocBytes.HasValue);
                text.Assert(s == State.ParseBucket, "phase must end with buckets");
                (State s2, BucketSpec[] buckets) = ParseBuckets(ref text);

                ulong livePerThread = Util.NonNull(totalLiveBytes) / threadCount;
                ulong allocPerThread = Util.NonNull(totalAllocBytes) / threadCount;
                Phase phase = new Phase(
                    testKind: testKind,
                    totalLiveBytes: livePerThread,
                    totalAllocBytes: allocPerThread,
                    requestLiveBytes: requestLiveBytes,
                    requestAllocBytes: requestAllocBytes,
                    totalMinutesToRun: totalMinutesToRun,
                    buckets: buckets,
                    allocType: allocType,
                    threadCount: threadCount,
                    compute: compute);
                return (s2, phase);
            }

            CharSpan word = text.TakeWord();
            text.TakeSpace();
            switch (word.ToString())
            {
                case "allocType":
                    allocType = ParseItemType(text.TakeWord());
                    break;
                case "testKind":
                    testKind = ParseTestKind(text.TakeWord());
                    break;
                case "threadCount":
                    threadCount = text.TakeUInt();
                    text.Assert(threadCount != 0, "Cannot have 0 threads");
                    break;
                case "totalLiveGB":
                    totalLiveBytes = Util.GBToBytes(text.TakeDouble());
                    break;
                case "totalAllocGB":
                    totalAllocBytes = Util.GBToBytes(text.TakeDouble());
                    break;
                case "totalLiveMB":
                    totalLiveBytes = Util.MBToBytes(text.TakeDouble());
                    break;
                case "totalAllocMB":
                    totalAllocBytes = Util.MBToBytes(text.TakeDouble());
                    break;
                case "requestAllocMB":
                    requestAllocBytes = Util.MBToBytes(text.TakeDouble());
                    break;
                case "requestLiveMB":
                    requestLiveBytes = Util.MBToBytes(text.TakeDouble());
                    break;
                case "compute":
                    compute = text.TakeUInt();
                    break;
                default:
                    throw text.Fail($"Unexpected argument '{word.ToString()}'");
            }
            text.SkipBlankLines();
        }
    }

    // Called after we see the first [bucket] in a phase; ends at the next [phase] or at EOF
    static (State, BucketSpec[]) ParseBuckets(ref TextReader text)
    {
        List<BucketSpec> res = new List<BucketSpec>();
        while (true)
        {
            State s = ParseBucket(ref text, res);
            if (s != State.ParseBucket)
            {
                return (s, res.ToArray());
            }
        }
    }

    static State ParseBucket(ref TextReader text, List<BucketSpec> res)
    {
        uint lowSize = DEFAULT_SOH_ALLOC_LOW;
        uint highSize = DEFAULT_SOH_ALLOC_HIGH;
        uint survInterval = DEFAULT_SOH_SURV_INTERVAL;
        uint reqSurvInterval = DEFAULT_REQ_SOH_SURV_INTERVAL;
        uint pinInterval = DEFAULT_PINNING_INTERVAL;
        uint finalizableInterval = DEFAULT_FINALIZABLE_INTERVAL;
        uint weight = 1;
        uint sizeDistribution = 0;
        bool isPoh = false;
        while (true)
        {

            State? s = TryReadTag(ref text);
            if (s != null)
            {
                SizeRange sizeRange = new SizeRange(lowSize, highSize);
                if (sizeDistribution == 0)
                {

                    res.Add(new BucketSpec(
                        sizeRange: sizeRange,
                        survInterval: survInterval,
                        reqSurvInterval: reqSurvInterval,
                        pinInterval: pinInterval,
                        finalizableInterval: finalizableInterval,
                        weight: weight,
                        isPoh: isPoh));
                }
                else
                {
                    if (isPoh)
                        SizeSlot.BuildPOHBucketSpecsFromSizeDistribution(res, SizeSlot.pohSizeSlots, sizeRange, survInterval, reqSurvInterval, pinInterval, finalizableInterval);
                    else
                        SizeSlot.BuildBucketSpecsFromSizeDistribution(res, SizeSlot.sohLohSizeSlots, sizeRange, survInterval, reqSurvInterval, pinInterval, finalizableInterval, isPoh);
                }
                return s.Value;
            }

            CharSpan word = text.TakeWord();
            text.TakeSpace();
            switch (word.ToString())
            {
                case "isPoh":
                    isPoh = text.TakeUInt() != 0;
                    break;
                case "lowSize":
                    lowSize = text.TakeUInt();
                    break;
                case "highSize":
                    highSize = text.TakeUInt();
                    break;
                case "reqSurvInterval":
                    reqSurvInterval = text.TakeUInt();
                    break;
                case "survInterval":
                    survInterval = text.TakeUInt();
                    break;
                case "pinInterval":
                    pinInterval = text.TakeUInt();
                    break;
                case "finalizableInterval":
                    finalizableInterval = text.TakeUInt();
                    break;
                case "weight":
                    weight = text.TakeUInt();
                    break;
                case "sizeDistribution":
                    sizeDistribution = text.TakeUInt();
                    break;
                default:
                    throw text.Fail($"Unexpected argument '{word.ToString()}'");
            }
            text.SkipBlankLines();
        }
    }

    private static State? TryReadTag(ref TextReader text)
    {
        text.SkipWhite();
        if (text.Eof)
        {
            return State.Eof;
        }
        else if (text.TryTake('['))
        {
            CharSpan word = text.TakeWord();
            // https://github.com/dotnet/csharplang/issues/1881 -- can't switch on a span
            State res = word == CharSpan.OfString("phase") ? State.ParsePhase
                : word == CharSpan.OfString("bucket") ? State.ParseBucket
                : throw text.Fail($"Bad tag '{word.ToString()}'");
            text.ShouldTake(']');
            text.SkipBlankLines();
            return res;
        }
        else
        {
            return null;
        }
    }

    private const uint DEFAULT_SOH_ALLOC_LOW = 100;
    private const uint DEFAULT_SOH_ALLOC_HIGH = 4000;
    private const uint DEFAULT_SOH_SURV_INTERVAL = 30;

    private const uint DEFAULT_LOH_ALLOC_LOW = 100 * 1024;
    private const uint DEFAULT_LOH_ALLOC_HIGH = 200 * 1024;
    private const uint DEFAULT_LOH_SURV_INTERVAL = 5;

    private const uint DEFAULT_POH_ALLOC_LOW = 100;
    private const uint DEFAULT_POH_ALLOC_HIGH = 200 * 1024;

#if NET5_0_OR_GREATER
    private const uint DEFAULT_POH_PINNING_INTERVAL = 0;
    private const uint DEFAULT_POH_FINALIZABLE_INTERVAL = 0;
    private const uint DEFAULT_POH_SURV_INTERVAL = 0;
    private const uint DEFAULT_REQ_POH_SURV_INTERVAL = 0;
#endif

    private const uint DEFAULT_PINNING_INTERVAL = 100;
    private const uint DEFAULT_FINALIZABLE_INTERVAL = 0;

    private const uint DEFAULT_REQ_SOH_SURV_INTERVAL = 3;
    private const uint DEFAULT_REQ_LOH_SURV_INTERVAL = 2;

    private static Args ParseFromCommandLine(string[] args)
    {
        TestKind testKind = TestKind.time;
        uint threadCount = 4;
        uint lohAllocRatioArg = 0;
        ulong? totalLiveBytes = null;
        ulong? totalAllocBytes = null;
        double totalMinutesToRun = 0.0;
        uint sohAllocLow = DEFAULT_SOH_ALLOC_LOW;
        uint sohAllocHigh = DEFAULT_SOH_ALLOC_HIGH;
        uint lohAllocLow = DEFAULT_LOH_ALLOC_LOW;
        uint lohAllocHigh = DEFAULT_LOH_ALLOC_HIGH;
        // default is we survive every 30th element for SOH...this is about 3%.
        uint sohSurvInterval = DEFAULT_SOH_SURV_INTERVAL;
        uint lohSurvInterval = DEFAULT_LOH_SURV_INTERVAL;
        uint sohPinInterval = DEFAULT_PINNING_INTERVAL;
        uint lohPinInterval = DEFAULT_PINNING_INTERVAL;
        uint sohFinalizableInterval = DEFAULT_FINALIZABLE_INTERVAL;
        uint lohFinalizableInterval = DEFAULT_FINALIZABLE_INTERVAL;

        uint pohAllocLow = DEFAULT_POH_ALLOC_LOW;
        uint pohAllocHigh = DEFAULT_POH_ALLOC_HIGH;

        uint reqSohSurvInterval = DEFAULT_REQ_SOH_SURV_INTERVAL;
        uint reqLohSurvInterval = DEFAULT_REQ_LOH_SURV_INTERVAL;
#if NET5_0_OR_GREATER
        uint pohFinalizableInterval = DEFAULT_POH_FINALIZABLE_INTERVAL;
        uint pohSurvInterval = DEFAULT_POH_SURV_INTERVAL;
        uint reqPohSurvInterval = DEFAULT_REQ_POH_SURV_INTERVAL;
#endif
        uint pohAllocRatioArg = 0;

        ItemType allocType = ItemType.ReferenceItem;
        bool verifyLiveSize = false;
        uint printEveryNthIter = 0;
        bool finishWithFullCollect = false;
        uint compute = 0;
        bool endException = false;

        ulong requestAllocBytes = 0;
        ulong requestLiveBytes = 0;
        uint sizeDist = 0;

        for (uint i = 0; i < args.Length; ++i)
        {
            switch (args[i])
            {
                case "-compute":
                case "-c":
                    compute = ParseUInt32(args[++i]);
                    break;
                case "-finishWithFullCollect":
                    finishWithFullCollect = true;
                    break;
                case "-endException":
                case "-ee":
                    endException = true;
                    break;
                case "-testKind":
                case "-tk":
                    testKind = ParseTestKind(args[++i]);
                    break;
                case "-threadCount":
                case "-tc":
                    threadCount = ParseUInt32(args[++i]);
                    break;
                case "-lohAllocRatio":
                case "-lohar":
                    lohAllocRatioArg = ParseUInt32(args[++i]);
                    break;
                case "-pohAllocRatio":
                case "-pohar":
#if NET5_0_OR_GREATER
                    pohAllocRatioArg = ParseUInt32(args[++i]);
#else
                    Console.WriteLine("The flag {0} is only supported on .NET Core 5+. Skipping in this run.",
                                      args[i++]);
#endif
                    break;
                case "-totalLiveGB":
                case "-tlgb":
                    totalLiveBytes = Util.GBToBytes(ParseDouble(args[++i]));
                    break;
                case "-totalAllocGB":
                case "-tagb":
                    totalAllocBytes = Util.GBToBytes(ParseDouble(args[++i]));
                    break;
                case "-requestAllocMB":
                case "-ramb":
                    requestAllocBytes = Util.MBToBytes(ParseDouble(args[++i]));
                    break;
                case "-requestLiveMB":
                case "-rlmb":
                    requestLiveBytes = Util.MBToBytes(ParseDouble(args[++i]));
                    break;
                case "-totalMins":
                case "-tm":
                    totalMinutesToRun = ParseDouble(args[++i]);
                    break;
                case "-sohSizeRange":
                case "-sohsr":
                    ParseRange(args[++i], out sohAllocLow, out sohAllocHigh);
                    break;
                case "-lohSizeRange":
                case "-lohsr":
                    ParseRange(args[++i], out lohAllocLow, out lohAllocHigh);
                    Util.AlwaysAssert(lohAllocLow >= MemoryAlloc.LohThreshold, "lohAllocLow is below the minimum large object size");
                    break;
                case "-pohSizeRange":
                case "-pohsr":
#if NET5_0_OR_GREATER
                    ParseRange(args[++i], out pohAllocLow, out pohAllocHigh);
#else
                    Console.WriteLine("The flag {0} is only supported on .NET Core 5+. Skipping in this run.",
                                      args[i++]);
#endif
                    break;
                case "-sizeDistribution":
                case "-sizeDist":
                    sizeDist = ParseUInt32(args[++i]);
                    break;
                case "-sohSurvInterval":
                case "-sohsi":
                    sohSurvInterval = ParseUInt32(args[++i]);
                    break;
                case "-reqSohSurvInterval":
                case "-rsohsi":
                    reqSohSurvInterval = ParseUInt32(args[++i]);
                    break;
                case "-lohSurvInterval":
                case "-lohsi":
                    lohSurvInterval = ParseUInt32(args[++i]);
                    break;
                case "-reqLohSurvInterval":
                case "-rlohsi":
                    reqLohSurvInterval = ParseUInt32(args[++i]);
                    break;
                case "-pohSurvInterval":
                case "-pohsi":
#if NET5_0_OR_GREATER
                    pohSurvInterval = ParseUInt32(args[++i]);
#else
                    Console.WriteLine("The flag {0} is only supported on .NET Core 5+. Skipping in this run.",
                                      args[i++]);
#endif
                    break;
                case "-reqPohSurvInterval":
                case "-rpohsi":
#if NET5_0_OR_GREATER
                    reqPohSurvInterval = ParseUInt32(args[++i]);
#else
                    Console.WriteLine("The flag {0} is only supported on .NET Core 5+. Skipping in this run.",
                                      args[i++]);
#endif
                    break;
                case "-sohPinningInterval":
                case "-sohpi":
                    sohPinInterval = ParseUInt32(args[++i]);
                    break;
                case "-sohFinalizableInterval":
                case "-sohfi":
                    sohFinalizableInterval = ParseUInt32(args[++i]);
                    break;
                case "-lohPinningInterval":
                case "-lohpi":
                    lohPinInterval = ParseUInt32(args[++i]);
                    break;
                case "-lohFinalizableInterval":
                case "-lohfi":
                    lohFinalizableInterval = ParseUInt32(args[++i]);
                    break;

                case "-pohFinalizableInterval":
                case "-pohfi":
#if NET5_0_OR_GREATER
                    pohFinalizableInterval = ParseUInt32(args[++i]);
#else
                    Console.WriteLine("The flag {0} is only supported on .NET Core 5+. Skipping in this run.",
                                      args[i++]);
#endif
                    break;

                case "-allocType":
                case "-at":
                    allocType = ParseItemType(args[++i]);
                    break;
                case "-verifyLiveSize":
                    verifyLiveSize = true;
                    break;
                case "-printEveryNthIter":
                    printEveryNthIter = ParseUInt32(args[++i]);
                    break;
                default:
                    throw new Exception($"Unrecognized argument: {args[i]}");
            }
        }

        if (totalLiveBytes == 0 && (sohSurvInterval != 0 || lohSurvInterval != 0
#if NET5_0_OR_GREATER
            || pohSurvInterval != 0
#endif
            ))
        {
            throw new Exception("Can't set -sohsi or -lohsi or -pohsi if -tlgb is 0");
        }

        if ((totalAllocBytes == null) && (totalMinutesToRun == 0))
        {
            totalMinutesToRun = 1;
        }

        Console.WriteLine(Process.GetCurrentProcess().ProcessName + " " + Environment.CommandLine);

        ulong livePerThread = (totalLiveBytes ?? 0) / threadCount;
        ulong allocPerThread = (totalAllocBytes ?? 0) / threadCount;
        if (allocPerThread != 0) Console.WriteLine("allocating {0:n0} per thread", allocPerThread);

        List<BucketSpec> bucketList = new List<BucketSpec>();

        if (sizeDist == 1)
        {
            SizeSlot.BuildSOHBucketSpecsFromSizeDistribution(bucketList, sohSurvInterval, reqSohSurvInterval, sohPinInterval, sohFinalizableInterval);
            SizeSlot.BuildLOHBucketSpecsFromSizeDistribution(bucketList, lohSurvInterval, reqLohSurvInterval, lohPinInterval, lohFinalizableInterval);
#if NET5_0_OR_GREATER
            SizeSlot.BuildPOHBucketSpecsFromSizeDistribution(bucketList, pohSurvInterval, reqPohSurvInterval, 0,              pohFinalizableInterval);
#endif // NET5_0_OR_GREATER
        }
        else
        {
            long meanSohObjSize = (long)Util.Mean(sohAllocLow, sohAllocHigh);
            long meanLohObjSize = (long)Util.Mean(lohAllocLow, lohAllocHigh);
            long meanPohObjSize = (long)Util.Mean(pohAllocLow, pohAllocHigh);
            int sohAllocRatioArgInt = (int)(1000 - lohAllocRatioArg - pohAllocRatioArg);

            // If sohAllocRationArg is zero, then the determinant of the matrix below will be zero,
            // and the solution will fail.  Set it very small to get close.  A low value of
            // sohAllocRatioArg may lead to a negative sohWeight (essentially trying to counteract
            // the overhead if it already uses too much soh space), in which case the soh bucket
            // will be dropped below.
            double sohAllocRatioArg = (sohAllocRatioArgInt == 0) ? 0.0000001 : sohAllocRatioArgInt;

            /*
             * Solving for the weights by 3 linear equations using the Cramer's rule.
             * See http://cshung.github.io/posts/poh-tuning-2 for a full derivation of the coefficients.
             */
            double overhead = allocType == ItemType.ReferenceItem ? ReferenceItemWithSize.SohOverhead : Item.SohOverhead;
            double a11 = -lohAllocRatioArg * (meanSohObjSize - overhead);
            double a12 = sohAllocRatioArg * (meanLohObjSize - overhead);
            double a13 = 0;
            double a21 = -pohAllocRatioArg * (meanSohObjSize - overhead);
            double a22 = 0;
            double a23 = sohAllocRatioArg * (meanPohObjSize - overhead);
            double a31 = 1;
            double a32 = 1;
            double a33 = 1;
            double b1 = 1000 * lohAllocRatioArg * overhead;
            double b2 = 1000 * pohAllocRatioArg * overhead;
            double b3 = 1000;
            double det = a11 * a22 * a33 + a12 * a23 * a31 + a13 * a21 * a32 - a13 * a22 * a31 - a12 * a21 * a33 - a11 * a23 * a32;
            double sohWeight = ((b1 * a22 * a33 + a12 * a23 * b3 + a13 * b2 * a32 - a13 * a22 * b3 - a12 * b2 * a33 - b1 * a23 * a32) / det);
            double lohWeight = ((a11 * b2 * a33 + b1 * a23 * a31 + a13 * a21 * b3 - a13 * b2 * a31 - b1 * a21 * a33 - a11 * a23 * b3) / det);
            double pohWeight = ((a11 * a22 * b3 + a12 * b2 * a31 + b1 * a21 * a32 - b1 * a22 * a31 - a12 * a21 * b3 - a11 * b2 * a32) / det);

            if (lohWeight > 0)
            {
                BucketSpec lohBucket = new BucketSpec(
                    sizeRange: new SizeRange(lohAllocLow, lohAllocHigh),
                    survInterval: lohSurvInterval,
                    reqSurvInterval: reqLohSurvInterval,
                    pinInterval: lohPinInterval,
                    finalizableInterval: lohFinalizableInterval,
                    weight: lohWeight);

                bucketList.Add(lohBucket);
            }

#if NET5_0_OR_GREATER
            if (pohWeight > 0)
            {
                BucketSpec pohBucket = new BucketSpec(
                    sizeRange: new SizeRange(pohAllocLow, pohAllocHigh),
                    survInterval: pohSurvInterval,
                    reqSurvInterval: reqPohSurvInterval,
                    pinInterval: 0,
                    finalizableInterval: pohFinalizableInterval,
                    weight: pohWeight,
                    isPoh: true);

                bucketList.Add(pohBucket);
            }
#endif

            if (sohWeight > 0)
            {
                BucketSpec sohBucket = new BucketSpec(
                    sizeRange: new SizeRange(sohAllocLow, sohAllocHigh),
                    survInterval: sohSurvInterval,
                    reqSurvInterval: reqSohSurvInterval,
                    pinInterval: sohPinInterval,
                    finalizableInterval: sohFinalizableInterval,
                    weight: sohWeight);

                bucketList.Add(sohBucket);
            }
        }

        BucketSpec[] buckets = bucketList.ToArray();

        Phase onlyPhase = new Phase(
            testKind: testKind,
            totalLiveBytes: livePerThread,
            totalAllocBytes: allocPerThread,
            requestAllocBytes: requestAllocBytes,
            requestLiveBytes: requestLiveBytes,
            totalMinutesToRun: totalMinutesToRun,
            buckets: buckets,
            allocType: allocType,
            threadCount: threadCount,
            compute: compute);
        return new Args(
            threadCount: threadCount,
            perThreadArgs: new PerThreadArgs(verifyLiveSize: verifyLiveSize, printEveryNthIter: printEveryNthIter, phases: new Phase[] { onlyPhase }),
            finishWithFullCollect: finishWithFullCollect,
            endException: endException);
    }
}

readonly struct ObjectSpec
{
    public readonly uint Size;
    public readonly bool ShouldBePinned;
    public readonly bool ShouldBeFinalizable;
    public readonly bool ShouldSurvive;
    public readonly bool ShouldSurviveReq;
    public readonly bool IsPoh;

    public ObjectSpec(uint size, bool shouldBePinned, bool shouldBeFinalizable, bool shouldSurvive, bool shouldSurviveReq, bool isPoh)
    {
        Size = size;
        ShouldBeFinalizable = shouldBeFinalizable;
        ShouldBePinned = shouldBePinned;
        ShouldSurvive = shouldSurvive;
        ShouldSurviveReq = shouldSurviveReq;
        IsPoh = isPoh;
    }
}

class Bucket
{
    public readonly BucketSpec spec;
    public ulong count; // Used for pinInterval and survInterval
    public ulong allocatedBytesTotalSum;
    public ulong sohAllocatedBytes;
    public ulong lohAllocatedBytes;
    public ulong pohAllocatedBytes;
    public ulong allocatedBytesAsOfLastPrint;
    public ulong survivedBytesSinceLastPrint;
    public ulong pinnedBytesSinceLastPrint;
    public ulong allocatedCountSinceLastPrint;
    public ulong survivedCountSinceLastPrint;

    public Bucket(BucketSpec spec)
    {
        this.spec = spec;
        count = 0;
        allocatedBytesTotalSum = 0;
        sohAllocatedBytes = 0;
        lohAllocatedBytes = 0;
        pohAllocatedBytes = 0;
        allocatedBytesAsOfLastPrint = 0;
        survivedBytesSinceLastPrint = 0;
        pinnedBytesSinceLastPrint = 0;
        allocatedCountSinceLastPrint = 0;
        survivedCountSinceLastPrint = 0;
    }

    public SizeRange sizeRange => spec.sizeRange;
    public uint survInterval => spec.survInterval;
    public uint reqSurvInterval => spec.reqSurvInterval;
    public uint pinInterval => spec.pinInterval;
    public uint finalizableInterval => spec.finalizableInterval;
    // If we have buckets with weights of 2 and 1, we'll allocate 2 objects from the first bucket, then 1 from the next.
    public double weight => spec.weight;
    public bool isPoh => spec.isPoh;

    public ObjectSpec GetObjectSpec(Rand rand, uint overhead)
    {
        count++;

        uint size = rand.GetRand(sizeRange);
        bool shouldSurvive = Util.isNth(survInterval, count);
        bool shouldSurviveReq = Util.isNth(reqSurvInterval, count);
        bool shouldBePinned = shouldSurvive && Util.isNth(pinInterval, count / survInterval);
        bool shouldBeFinalizable = shouldSurvive && Util.isNth(finalizableInterval, count / survInterval);

        if (isPoh)
        {
            sohAllocatedBytes += overhead;
            pohAllocatedBytes += (size - overhead);
        }
        else if (size >= 85000)
        {
            sohAllocatedBytes += overhead;
            lohAllocatedBytes += (size - overhead);
        }
        else
        {
            sohAllocatedBytes += size;
        }
        allocatedBytesTotalSum += size;
        allocatedCountSinceLastPrint++;
        if (shouldBePinned)
        {
            pinnedBytesSinceLastPrint += size;
        }
        if (shouldSurvive)
        {
            survivedBytesSinceLastPrint += size;
            survivedCountSinceLastPrint++;
        }

        return new ObjectSpec(size, shouldBePinned: shouldBePinned, shouldBeFinalizable: shouldBeFinalizable, shouldSurvive: shouldSurvive, shouldSurviveReq: shouldSurviveReq, isPoh: isPoh);
    }
}

struct BucketChooser
{
    public readonly Bucket[] buckets;
    private readonly double combinedWeight;

    public BucketChooser(BucketSpec[] bucketSpecs)
    {
        Util.AlwaysAssert(bucketSpecs.Length != 0);
        this.buckets = new Bucket[bucketSpecs.Length];
        double weightSum = 0;
        for (uint i = 0; i < bucketSpecs.Length; i++)
        {
            var bucket = new Bucket(bucketSpecs[i]);
            this.buckets[i] = bucket;
            weightSum += bucket.weight;
        }
        combinedWeight = weightSum;
    }

    private Bucket GetNextBucket(Rand rand)
    {
        var nextRand = rand.GetFloat() * combinedWeight;

        for (int i = 0; i < buckets.Length; i++)
        {
            var curBucket = buckets[i];
            if (nextRand < curBucket.weight)
            {
                return curBucket;
            }

            nextRand -= curBucket.weight;
        }

        throw new Exception("UNREACHABLE");
    }

    public ObjectSpec GetNextObjectSpec(Rand rand, uint overhead) =>
        GetNextBucket(rand).GetObjectSpec(rand, overhead);

    public ulong AverageObjectSize()
    {
        // Average object size in each bucket, weighed by the bucket
        double totalAverage = 0;
        double totalWeight = 0;
        // https://github.com/dotnet/csharplang/issues/461
        for (uint i = 0; i < buckets.Length; i++)
        {
            Bucket bucket = buckets[i];
            totalAverage += bucket.sizeRange.Mean * bucket.weight;
            totalWeight += bucket.weight;
        }

        return (ulong)(totalAverage / totalWeight);
    }
}

class ThreadLauncher
{
    readonly uint threadIndex;
    readonly PerThreadArgs perThreadArgs;
    public MemoryAlloc? alloc; // To be created by the thread

    public MemoryAlloc Alloc => Util.NonNull(alloc);

    public ThreadLauncher(uint threadIndex, in PerThreadArgs perThreadArgs)
    {
        this.threadIndex = threadIndex;
        this.perThreadArgs = perThreadArgs;
    }

    public void Run()
    {
        alloc = new MemoryAlloc(threadIndex, perThreadArgs);
        alloc.RunTest();
    }
}

// Encapsulating this to ensure items are freed
struct OldArr
{
    public readonly ITypeWithPayload?[] items;
    public ulong TotalLiveBytes { get; private set; }
    public uint NonEmptyLength;

    public OldArr(ulong numElements)
    {
        items = new ITypeWithPayload?[numElements];
        TotalLiveBytes = Util.ArraySize(items);
        NonEmptyLength = (uint)numElements;
    }

    public ulong OwnSize => Util.ArraySize(items);

    public void Initialize(uint index, ITypeWithPayload item)
    {
        Debug.Assert(items[index] == null);
        items[index] = item;
        TotalLiveBytes += item.TotalSize;
    }

    public ITypeWithPayload? Peek(uint index) => items[index];

    public void Free(uint index)
    {
        ITypeWithPayload? item = items[index];
        items[index] = null;
        ulong size = item?.TotalSize ?? 0;
        item?.Free();
        TotalLiveBytes -= size;
    }

    public void Replace(uint index, ITypeWithPayload newItem)
    {
        Free(index);
        TotalLiveBytes += newItem.TotalSize;
        items[index] = newItem;
    }

    public ITypeWithPayload? TakeAndReduceTotalSizeButDoNotFree(uint index)
    {
        ITypeWithPayload? item = items[index];
        if (item != null)
        {
            items[index] = null;
            TotalLiveBytes -= item.TotalSize;
        }
        return item;
    }

    public uint Length => (uint)items.Length;

    public void FreeAll()
    {
        for (uint i = 0; i < items.Length; i++)
        {
            Free(i);
        }

        Debug.Assert(TotalLiveBytes == OwnSize);
    }

    public void VerifyLiveSize()
    {
        ulong liveSizeCalculated = OwnSize;
        for (uint i = 0; i < items.Length; i++)
            liveSizeCalculated += items[i]?.TotalSize ?? 0;
        Debug.Assert(liveSizeCalculated == TotalLiveBytes);
    }
}

struct TestResult
{
    public double secondsTaken;
    public ulong sohAllocatedBytes;
    public ulong lohAllocatedBytes;
    public ulong pohAllocatedBytes;
}

class MemoryAlloc
{
    public static int LohThreshold = 85000;

    private readonly Rand rand;
    OldArr oldArr;
    OldArr reqArr;
    // TODO We should consider adding another array for medium lifetime.
    private readonly uint threadIndex;
    private readonly PerThreadArgs args;
    private long totalAllocBytesLeft;
    private long requestAllocBytesLeft;
    // private readonly bool printIterInfo = false;
    private BucketChooser bucketChooser;

    // TODO: replace this with an array that records the 10 longest pauses 
    // and pause buckets.
    public List<double> lohAllocPauses = new List<double>(10);

    // changes when we switch phases
    Phase curPhase;

    int curPhaseIndex;

    public MemoryAlloc(uint _threadIndex, in PerThreadArgs args)
    {
        rand = new Rand();
        threadIndex = _threadIndex;

        this.args = args;
        this.curPhaseIndex = -1;
        this.GoToNextPhase();
    }

    void TouchPage(byte[] b)
    {
        uint size = (uint)b.Length;
        const uint pageSize = 4096;
        uint numPages = size / pageSize;

        for (uint i = 0; i < numPages; i++)
        {
            b[i * pageSize] = (byte)(i % 256);
        }
    }

    void TouchPage(ITypeWithPayload item)
    {
        byte[] b = item.GetPayload();
        TouchPage(b);
    }

    public void RunTest()
    {
        switch (curPhase.testKind)
        {
            case TestKind.time:
                TimeTest();
                break;
            case TestKind.highsurvival:
                HighSurvivalTest();
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    public void HighSurvivalTest()
    {
        if (curPhase.totalMinutesToRun == 0.0) throw new Exception("totalMinutesToRun must be set");

        // Note: threads already launched, so this is just the code for a single thread.
        // Initial memory for tlgb has already been allocated.

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        while (stopwatch.Elapsed.TotalMinutes < curPhase.totalMinutesToRun)
        {
            GC.Collect();
        }
        stopwatch.Stop();
    }

    private static void PrintIterInfoHeader(uint nBuckets)
    {
        Console.Write("{0,3} | {1,10} | {2,5} | {3,5} | {4,4} | {5,6} | {6,8}",
            "T",                     // 0
            "iter",                  // 1

            "gen0",                  // 2
            "gen1",                  // 3
            "gen2",                  // 4

            "ms",                    // 5
                                     // This size is what we get from GC.GetTotalMemory(false) so 
                                     // it's not the live data size. Change it to GC.GetTotalMemory(true)
                                     // if you want to make sure the live size is expected.
            "size(mb)"               // 6
        );
        for (uint b = 0; b < nBuckets; b++)
        {
            Console.Write(" | {0,14} | {1,14} | {2,14} | {3,5} | {4,10} | {5,10}",
                $"b{b} total",           // 0
                $"b{b} alloc",           // 1
                $"b{b} surv",            // 2
                "ratio",                 // 3
                $"b{b} pinned",          // 4
                "ratio"                  // 5
            );
        }
        Console.WriteLine();
    }

    // Keep this in sync with PrintIterInfoHeader
    private static void PrintIterInfoForBuckets(uint threadIndex, ulong n, long elapsedDiffMS, Bucket[] buckets)
    {
        Console.Write("{0,3} | {1,10:n0} | {2,5:d} | {3,5:d} | {4,4:d} | {5,6:n0} | {6,8:n0}",
            threadIndex,                                                                      // 0
            n,                                                                                // 1
            GC.CollectionCount(0),                                                            // 2
            GC.CollectionCount(1),                                                            // 3
            GC.CollectionCount(2),                                                            // 4
            elapsedDiffMS,                                                                    // 5
            Util.BytesToMB((ulong)GC.GetTotalMemory(false))                                   // 6
        );

        for (uint i = 0; i < buckets.Length; i++)
        {
            Bucket bucket = buckets[i];
            ulong allocatedBytesSinceLast = bucket.allocatedBytesTotalSum - bucket.allocatedBytesAsOfLastPrint;

            Console.Write(" | {0,14:n0} | {1,14:n0} | {2,14:n0} | {3,5:n0} | {4,10:n0} | {5,10:f2}",
                bucket.allocatedBytesTotalSum,
                allocatedBytesSinceLast,                                                         // 0
                bucket.survivedBytesSinceLastPrint,                                              // 1
                GetPercent(bucket.survivedBytesSinceLastPrint, allocatedBytesSinceLast),         // 2
                bucket.pinnedBytesSinceLastPrint,                                                // 3
                                                                                                 // TODO: might be more pinned than survived ...
                GetPercent(bucket.pinnedBytesSinceLastPrint, bucket.survivedBytesSinceLastPrint) // 4
            );

            // Reset the numbers.
            bucket.allocatedBytesAsOfLastPrint = bucket.allocatedBytesTotalSum;
            bucket.survivedBytesSinceLastPrint = 0;
            bucket.pinnedBytesSinceLastPrint = 0;
            bucket.allocatedCountSinceLastPrint = 0;
            bucket.survivedCountSinceLastPrint = 0;
        }
        Console.WriteLine();
    }

    private static int GetPercent(ulong a, ulong b) => b == 0 ? 0 : (int)(a * 100.0 / b);

    public void TimeTest()
    {
        ulong n = 0;

        Stopwatch stopwatchGlobal = new Stopwatch();

        long elapsedLastMS = 0;

        if (threadIndex == 0 && args.printEveryNthIter != 0)
        {
            PrintIterInfoHeader((uint)bucketChooser.buckets.Length);
        }

        stopwatchGlobal.Reset();
        stopwatchGlobal.Start();

        while (true)
        {
            if (threadIndex == 0 && Util.isNth(args.printEveryNthIter, n))
            {
                long elapsedCurrentMS = stopwatchGlobal.ElapsedMilliseconds;
                long elapsedDiffMS = elapsedCurrentMS - elapsedLastMS;
                elapsedLastMS = elapsedCurrentMS;
                PrintIterInfoForBuckets(threadIndex, n, elapsedDiffMS, bucketChooser.buckets);
            }

            if (n % ((ulong)1 * 1024 * 1024) == 0)
            {
                if (curPhase.totalMinutesToRun != 0 && stopwatchGlobal.Elapsed.TotalMinutes >= curPhase.totalMinutesToRun)
                {
                    if (!GoToNextPhase()) break;
                }
            }

            if (totalAllocBytesLeft <= 0)
            {
                if (!GoToNextPhase()) break;
            }

            if (requestAllocBytesLeft <= 0)
            {
                reqArr.FreeAll();
                ulong numReqElements = curPhase.requestLiveBytes / bucketChooser.AverageObjectSize();
                reqArr = new OldArr(numReqElements);
                reqArr.NonEmptyLength = 0;
                requestAllocBytesLeft = (long)curPhase.requestAllocBytes;
            }

            MakeObjectAndMaybeSurvive(); // modifies totalAllocBytesLeft && requestAllocBytesLeft

            if (curPhase.compute != 0)
            {
                // Generating some random numbers
                uint count = rand.GetRand(curPhase.compute);
                for (uint i = 0; i < count; i++)
                {
                    rand.GetRand(1000000);
                }
            }
            n++;
        }

        Finish();
    }

    void Finish()
    {
        oldArr.FreeAll();
        reqArr.FreeAll();
    }

    // Returns false if no next phase
    bool GoToNextPhase()
    {
        curPhaseIndex++;
        if (curPhaseIndex < args.phases.Length)
        {
            curPhase = args.phases[curPhaseIndex];
            totalAllocBytesLeft = (long)curPhase.totalAllocBytes;

            bucketChooser = new BucketChooser(curPhase.buckets);

            ulong numElements = curPhase.totalLiveBytes / bucketChooser.AverageObjectSize();
            oldArr = new OldArr(numElements);

            for (uint i = 0; i < numElements; i++)
            {
                (ITypeWithPayload item, ObjectSpec _) = MakeObjectAndTouchPage();
                oldArr.Initialize(i, item);
            }

            ulong numReqElements = curPhase.requestLiveBytes / bucketChooser.AverageObjectSize();
            reqArr = new OldArr(numReqElements);
            // we don't want to fill the request right away
            reqArr.NonEmptyLength = 0;
            requestAllocBytesLeft = (long)curPhase.requestAllocBytes;

            if (curPhase.totalLiveBytes == 0)
                Util.AlwaysAssert(oldArr.TotalLiveBytes < 100);
            else
                Util.AssertAboutEqual(oldArr.TotalLiveBytes, curPhase.totalLiveBytes);

            if (args.verifyLiveSize)
            {
                oldArr.VerifyLiveSize();
                if (!Util.AboutEquals(oldArr.TotalLiveBytes, curPhase.totalLiveBytes))
                {
                    Console.WriteLine($"totalLiveBytes: {oldArr.TotalLiveBytes}, args.totalLiveBytes: {curPhase.totalLiveBytes}");
                    throw new Exception("TODO");
                }
            }

            if (curPhase.totalAllocBytes != 0)
            {
                Console.WriteLine("Thread {0} stopping phase after {1}MB", threadIndex, Util.BytesToMB(curPhase.totalAllocBytes));
            }
            else
            {
                Console.WriteLine("Stopping phase after {0} mins", curPhase.totalMinutesToRun);
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    (ITypeWithPayload, ObjectSpec spec) JustMakeObject()
    {
        uint SohOverhead;
        switch (curPhase.allocType)
        {
            case ItemType.SimpleItem:
                SohOverhead = Item.SohOverhead;
                break;
            case ItemType.ReferenceItem:
                SohOverhead = ReferenceItemWithSize.SohOverhead;
                break;
            default:
                throw new NotImplementedException();
        }

        ObjectSpec spec = bucketChooser.GetNextObjectSpec(rand, SohOverhead);
        totalAllocBytesLeft -= spec.Size;
        requestAllocBytesLeft -= spec.Size;
        switch (curPhase.allocType)
        {
            case ItemType.SimpleItem:
                return (Item.New(spec.Size, isPinned: spec.ShouldBePinned, isFinalizable: spec.ShouldBeFinalizable, isPoh: spec.IsPoh), spec);
            case ItemType.ReferenceItem:
                return (ReferenceItemWithSize.New(spec.Size, isPinned: spec.ShouldBePinned, isFinalizable: spec.ShouldBeFinalizable, isPoh: spec.IsPoh), spec);
            default:
                throw new NotImplementedException();
        }
    }

    (ITypeWithPayload, ObjectSpec) MakeObjectAndTouchPage()
    {
        (ITypeWithPayload item, ObjectSpec spec) = JustMakeObject();
        TouchPage(item);
        return (item, spec);
    }

    // Allocates object and survives it. Returns the object size.
    void MakeObjectAndMaybeSurvive()
    {
        // TODO: Convert this into a config - refer to the comment above.
        // if (isLarge && args.lohPauseMeasure)
        // {
        //     stopwatch.Reset();
        //     stopwatch.Start();
        // }

        // TODO: We should have a sequence number that just grows (since we only allocate sequentially on 
        // the same thread anyway). This way we can use this number to indicate the ages of items. 
        // If an item with a very current seq number points to an item with small seq number we can conclude
        // that we have young gen object pointing to a very old object. This can help us recognize things
        // like object locality, eg if demotion has demoted very old objects next to young objects.
        (ITypeWithPayload item, ObjectSpec spec) = MakeObjectAndTouchPage();

        // TODO: As above, convert this into a config.
        // if (isLarge && args.lohPauseMeasure)
        // {
        //     stopwatch.Stop();
        //     lohAllocPauses.Add(stopwatch.Elapsed.TotalMilliseconds);
        // }

        // Thread.Sleep(1);

        if (spec.ShouldSurvive)
        {
            DoSurvive(ref oldArr, item, false);
        }
        else if (reqArr.Length > 0 && spec.ShouldSurviveReq)
        {
            DoSurvive(ref reqArr, item, reqArr.TotalLiveBytes < curPhase.requestLiveBytes);
        }
        else
        {
            item.Free();
        }
    }

    private void DoSurvive(ref OldArr arr, ITypeWithPayload item, bool rampUp)
    {
        // TODO: How to survive shouldn't belong here; it should
        // belong to the building block that churns the array.
        switch (curPhase.allocType)
        {
            case ItemType.SimpleItem:
                arr.Replace(rand.GetRand(arr.Length), item);
                break;
            case ItemType.ReferenceItem:
                if (!rampUp)
                {
                    // Step 1: Decide which list to pick a victim to free
                    uint victimIndex = rand.GetRand(arr.NonEmptyLength);
                    // Step 2: Free the head
                    ITypeWithPayload? victimList = arr.TakeAndReduceTotalSizeButDoNotFree(victimIndex);
                    // Within the NonEmpty range, the list cannot be null
                    ReferenceItemWithSize victimHead = (ReferenceItemWithSize)victimList!;
                    ReferenceItemWithSize? victimTail = victimHead.FreeHead();
                    if (victimTail == null)
                    {
                        // If the list had just one head, and we removed it
                        if (victimIndex != arr.NonEmptyLength - 1)
                        {
                            // and when it is not the last one, move the last one to fill the hole
                            ITypeWithPayload borrow = arr.TakeAndReduceTotalSizeButDoNotFree(arr.NonEmptyLength - 1)!;
                            arr.Replace(victimIndex, borrow);
                        }
                        arr.NonEmptyLength = arr.NonEmptyLength - 1;
                    }
                    else
                    {
                        arr.Replace(victimIndex, victimTail);
                    }
                }
                // Step 3: Do we want to create a new list?
                const uint createProbability = 50;
                bool create;
                if (arr.NonEmptyLength == 0)
                    create = true;
                else if (arr.NonEmptyLength == arr.Length)
                    create = false;
                else
                    create = rand.GetRand(100) < createProbability;
                if (create)
                {
                    // Create a new list
                    arr.Replace(arr.NonEmptyLength, item);
                    arr.NonEmptyLength = arr.NonEmptyLength + 1;
                }
                else
                {
                    // Or extend an existing one
                    uint extendIndex = rand.GetRand(arr.NonEmptyLength);
                    ReferenceItemWithSize extendList = (ReferenceItemWithSize)arr.TakeAndReduceTotalSizeButDoNotFree(extendIndex)!;
                    extendList.AddToEndOfList((ReferenceItemWithSize)item);
                    arr.Replace(extendIndex, extendList);
                }
                break;
            default:
                throw new InvalidOperationException();
        }

        if (args.verifyLiveSize)
        {
            arr.VerifyLiveSize();
        }
    }

    void PrintPauses()
    {
        if (curPhase.lohPauseMeasure)
        {
            Console.WriteLine("T{0} {1:n0} entries in pause, top entries(ms)", threadIndex, lohAllocPauses.Count);

            int numLOHAllocPauses = lohAllocPauses.Count;
            if (numLOHAllocPauses >= 0)
            {
                lohAllocPauses.Sort();
                // lohAllocPauses.OrderByDescending(a => a);

                Console.WriteLine("===============STATS for thread {0}=================", threadIndex);

                int startIndex = ((numLOHAllocPauses < 10) ? 0 : (numLOHAllocPauses - 10));
                for (int i = startIndex; i < numLOHAllocPauses; i++)
                {
                    Console.WriteLine(lohAllocPauses[i]);
                }

                Console.WriteLine("===============END STATS for thread {0}=================", threadIndex);
            }
        }
    }

#if TODO
    [DllImport("psapi.dll")]
    public static extern bool EmptyWorkingSet(IntPtr hProcess);
#endif

    static TestResult DoTest(in Args args, int currentPid)
    {
        TestResult testResult = new TestResult();
        long tStart = Environment.TickCount;
        Phase[] phases = args.perThreadArgs.phases;
        for (int phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
        {
            // launch threads separately for each phase, so we can vary the number of threads in each phase
            Phase phase = phases[phaseIndex];
            PerThreadArgs perThreadArgs = new PerThreadArgs(args.perThreadArgs.verifyLiveSize, args.perThreadArgs.printEveryNthIter, new Phase[] { phase });

            if (phase.threadCount > 1)
            {
                ThreadLauncher[] threadLaunchers = new ThreadLauncher[phase.threadCount];
                Thread[] threads = new Thread[phase.threadCount];

                for (uint i = 0; i < phase.threadCount; i++)
                {
                    threadLaunchers[i] = new ThreadLauncher(i, perThreadArgs);
                    ThreadStart ts = new ThreadStart(threadLaunchers[i].Run);
                    threads[i] = new Thread(ts);
                }

                for (uint i = 0; i < threads.Length; i++)
                    threads[i].Start();
                for (uint i = 0; i < threads.Length; i++)
                    threads[i].Join();
                for (uint i = 0; i < threadLaunchers.Length; i++)
                    threadLaunchers[i].Alloc.PrintPauses();

                for (int i = 0; i < threadLaunchers.Length; i++)
                {
                    Bucket[] buckets = threadLaunchers[i].Alloc.bucketChooser.buckets;
                    for (int j = 0; j < buckets.Length; j++)
                    {
                        testResult.sohAllocatedBytes += buckets[j].sohAllocatedBytes;
                        testResult.lohAllocatedBytes += buckets[j].lohAllocatedBytes;
                        testResult.pohAllocatedBytes += buckets[j].pohAllocatedBytes;
                    }
                }
            }
            else
            {
                // Easier to debug without launching a separate thread
                ThreadLauncher t = new ThreadLauncher(0, perThreadArgs);
                t.Run();
                t.Alloc.PrintPauses();

                Bucket[] buckets = t.Alloc.bucketChooser.buckets;
                for (int j = 0; j < buckets.Length; j++)
                {
                    testResult.sohAllocatedBytes += buckets[j].sohAllocatedBytes;
                    testResult.lohAllocatedBytes += buckets[j].lohAllocatedBytes;
                    testResult.pohAllocatedBytes += buckets[j].pohAllocatedBytes;
                }
            }
        }
        long tEnd = Environment.TickCount;

#if DEBUG
        Debug.Assert(Item.NumConstructed == Item.NumFreed);
        Debug.Assert(ReferenceItemWithSize.NumConstructed == ReferenceItemWithSize.NumFreed);
        Debug.Assert(SimpleRefPayLoad.NumPinned == SimpleRefPayLoad.NumUnpinned);
#endif
        return testResult;
    }

    public static int Test(string[] argsStrs)
    {
        try
        {
            Args args;
            args = ArgsParser.Parse(argsStrs);

            TestResult testResult = MainInner(args);

            if (args.endException)
            {
                GC.Collect(2, GCCollectionMode.Forced, true);
#if TODO
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
#endif
                // Debugger.Break();
                throw new System.ArgumentException("Just an opportunity for debugging", "test");
            }

            if (args.finishWithFullCollect)
            {
                while (ITypeWithPayload.Totals.NumFinalized < ITypeWithPayload.Totals.NumCreatedWithFinalizers)
                {
                    Console.WriteLine($"{ITypeWithPayload.Totals.NumFinalized} out of {ITypeWithPayload.Totals.NumCreatedWithFinalizers} finalizers have run, doing a full collect");
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                    GC.WaitForPendingFinalizers();
                }
                Util.AlwaysAssert(ITypeWithPayload.Totals.NumFinalized == ITypeWithPayload.Totals.NumCreatedWithFinalizers);
            }

            PrintResult(testResult);

            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(e.StackTrace);
            return 1;
        }
    }

    public static TestResult MainInner(Args args)
    {
        Console.WriteLine($"Running 64-bit? {Environment.Is64BitProcess}");
        Console.WriteLine($"Running SVR GC? {System.Runtime.GCSettings.IsServerGC}");

        int currentPid = Process.GetCurrentProcess().Id;
        Console.WriteLine("PID: {0}", currentPid);

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        args.Describe();
        TestResult result = DoTest(args, currentPid);

        stopwatch.Stop();

        result.secondsTaken = stopwatch.Elapsed.TotalSeconds;
        return result;
    }

    private static void PrintResult(TestResult testResult)
    {
        // GCPerfSim may print additional info before this.
        // dotnet-gc-infra will slice the stdout to after "=== STATS ===" and parse as yaml.
        // See `class GCPerfSimResult` in `bench_file.py`, and `_parse_gcperfsim_result` in `run_single_test.py`.

        Console.WriteLine("=== STATS ===");
        Console.WriteLine($"sohAllocatedBytes: {testResult.sohAllocatedBytes}");
        Console.WriteLine($"lohAllocatedBytes: {testResult.lohAllocatedBytes}");
        Console.WriteLine($"pohAllocatedBytes: {testResult.pohAllocatedBytes}");
#if STATISTICS
        TestResult statistics = Statistics.Aggregate();
        Debug.Assert(testResult.sohAllocatedBytes == statistics.sohAllocatedBytes);
        Debug.Assert(testResult.lohAllocatedBytes == statistics.lohAllocatedBytes);
        Debug.Assert(testResult.pohAllocatedBytes == statistics.pohAllocatedBytes);
#endif
        Console.WriteLine($"seconds_taken: {testResult.secondsTaken}");

        Console.Write($"collection_counts: [");
        for (int gen = 0; gen <= 2; gen++)
        {
            if (gen != 0) Console.Write(", ");
            Console.Write($"{GC.CollectionCount(gen)}");
        }
        Console.WriteLine("]");

        Console.WriteLine($"num_created_with_finalizers: {ITypeWithPayload.Totals.NumCreatedWithFinalizers}");
        Console.WriteLine($"num_finalized: {ITypeWithPayload.Totals.NumFinalized}");
        Console.WriteLine($"final_total_memory_bytes: {GC.GetTotalMemory(forceFullCollection: false)}");

#if NET5_0_OR_GREATER
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        long heapSizeBytes = info.HeapSizeBytes;
        long fragmentedBytes = info.FragmentedBytes;
        Console.WriteLine($"final_heap_size_bytes: {heapSizeBytes}");
        Console.WriteLine($"final_fragmentation_bytes: {fragmentedBytes}");
#else
        var getGCMemoryInfo = typeof(GC).GetMethod("GetGCMemoryInfo", new Type[] { });
        if (getGCMemoryInfo != null)
        {
            object info = Util.NonNull(getGCMemoryInfo.Invoke(null, parameters: null));
            long heapSizeBytes = GetProperty<long>(info, "HeapSizeBytes");
            long fragmentedBytes = GetProperty<long>(info, "FragmentedBytes");
            Console.WriteLine($"final_heap_size_bytes: {heapSizeBytes}");
            Console.WriteLine($"final_fragmentation_bytes: {fragmentedBytes}");
        }
#endif
    }

#if !NET5_0_OR_GREATER
    private static T GetProperty<T>(object instance, string name)
    {
        PropertyInfo property = Util.NonNull(instance.GetType().GetProperty(name));
        return (T)Util.NonNull(Util.NonNull(property.GetGetMethod()).Invoke(instance, parameters: null));
    }
#endif
}