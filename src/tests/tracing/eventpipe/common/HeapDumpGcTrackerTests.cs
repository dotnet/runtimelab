// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Tracing.Tests.Common;

public class HeapDumpGcTrackerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnrelatedCollectionDoesNotEndSnapshot(bool stoppedBeforeSnapshot)
    {
        HeapDumpGcTracker tracker = new HeapDumpGcTracker();
        tracker.GCStarted(10);
        if (stoppedBeforeSnapshot)
        {
            tracker.GCStopped(10);
        }
        tracker.GCStarted(11);
        tracker.HeapEvent();
        Assert.Equal(11, tracker.SnapshotGCCount);
        Assert.False(tracker.HasInvalidHeapEvent);
        Assert.False(tracker.IsComplete);

        tracker.GCStopped(10);
        tracker.HeapEvent();
        Assert.False(tracker.HasInvalidHeapEvent);
        Assert.False(tracker.IsComplete);

        tracker.GCStopped(11);
        Assert.True(tracker.IsComplete);
        tracker.GCStarted(12);
        tracker.GCStopped(12);
        tracker.HeapEvent();
        Assert.True(tracker.HasInvalidHeapEvent);
        Console.WriteLine($"GC correlation with earlier collection stopped={stoppedBeforeSnapshot}: passed.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void HeapEventAfterSnapshotIsRejected(bool laterCollectionStopped)
    {
        HeapDumpGcTracker tracker = new HeapDumpGcTracker();
        tracker.GCStarted(10);
        tracker.HeapEvent();
        tracker.GCStopped(10);
        if (laterCollectionStopped)
        {
            tracker.GCStarted(11);
            tracker.GCStopped(11);
        }
        tracker.HeapEvent();
        Assert.True(tracker.HasInvalidHeapEvent);
        Console.WriteLine($"Post-snapshot heap event, laterCollectionStopped={laterCollectionStopped}: rejected.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void HeapEventOutsideCollectionIsRejected(bool collectionStarted)
    {
        HeapDumpGcTracker tracker = new HeapDumpGcTracker();
        if (collectionStarted)
        {
            tracker.GCStarted(10);
            tracker.GCStopped(10);
        }
        tracker.HeapEvent();
        Assert.True(tracker.HasInvalidHeapEvent);
        Console.WriteLine($"Out-of-collection heap event, collectionStarted={collectionStarted}: rejected.");
    }
}
