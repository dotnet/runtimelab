// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Internal.Mint.Abstraction;

internal unsafe struct ThreadContextInstanceAbstractionVTable
{
    public delegate* unmanaged<ThreadContextInstanceAbstractionNativeAot*, byte*, void> set_stack_pointer;
    public delegate* unmanaged<ThreadContextInstanceAbstractionNativeAot*, UIntPtr, int> check_sufficient_stack;
}

internal unsafe struct ThreadContextInstanceAbstractionNativeAot
{
    public ThreadContextInstanceAbstractionVTable* vtable;
    public byte* stack_pointer;
    public byte* stack_start;
    public byte* stack_end;
    public byte* stack_real_end;

    public FrameDataAllocatorNativeAot data_stack;
}

internal unsafe struct FrameDataAllocatorNativeAot
{
    public FrameDataFragmentNativeAot* first;
    public FrameDataFragmentNativeAot* current;
    public FrameDataInfoNativeAot* infos;
    public int infos_len;
    public int infos_capacity;
    /* For GC sync */
    public int inited;
}

internal unsafe struct FrameDataFragmentNativeAot
{
    public const int FRAME_DATA_FRAGMENT_SIZE = 8192;
    public byte* pos;
    public byte* end;
    public FrameDataFragmentNativeAot* next;
    public fixed double data[FRAME_DATA_FRAGMENT_SIZE / sizeof(double)];
};

internal unsafe struct FrameDataInfoNativeAot
{
    public IntPtr /*InterpFrame* */ frame;
    /*
     * frag and pos hold the current allocation position when the stored frame
     * starts allocating memory. This is used for restoring the localloc stack
     * when frame returns.
     */
    public FrameDataFragmentNativeAot* frag;
    public byte* pos;
}

// Unlike Mono, we will not overlap the reference and non-reference parts of the stackval.
// So our stack will actually use twice as much space as Mono's.

// we will tell the GC this is what we allocated...
[StructLayout(LayoutKind.Sequential)]
internal struct EEstackvalVisible
{
    public object obj;
    public EEstackvalNonRef val;
}

// ... but this is what we will pass to native
[StructLayout(LayoutKind.Sequential)]
internal struct EEstackval
{
    public IntPtr objRefDangerous;
    public EEstackvalNonRef val;
}

[StructLayout(LayoutKind.Explicit)]
internal struct EEstackvalNonRef
{
    [FieldOffset(0)]
    public int i4;
    [FieldOffset(0)]
    public long i8;
    [FieldOffset(0)]
    public float r4;
    [FieldOffset(0)]
    public double r8;
    [FieldOffset(0)]
    public IntPtr ptr;
}
