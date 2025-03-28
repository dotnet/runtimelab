// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

#nullable enable

namespace Swift.Runtime;

/// <summary>
/// Represents an opaque raw handle to a Swift object.
/// Used internally in private constructors to prevent conflicts with public IntPtr constructors.
/// </summary>
public struct SwiftHandle
{
    /// <summary>
    /// The handle to the Swift native object
    /// </summary>
    public IntPtr Handle { get; }

    /// <summary>
    /// Constructs a SwiftHandle from the given IntPtr
    /// </summary>
    public SwiftHandle(IntPtr handle)
    {
        Handle = handle;
    }

    /// <summary>
    /// Implicit conversion from SwiftHandle to IntPtr
    /// </summary>
    public static implicit operator IntPtr(SwiftHandle value)
    {
        return value.Handle;
    }

    /// <summary>
    /// Explicit conversion from IntPtr to SwiftHandle
    /// </summary>
    public static implicit operator SwiftHandle(IntPtr value)
    {
        return new SwiftHandle(value);
    }
}

/// <summary>
/// Represents an opaque handle to a Swift object of type T.
/// Used to manage native memory associated with a Swift object of type T.
/// </summary>
public sealed class SwiftHandle<T> : SafeHandleZeroOrMinusOneIsInvalid where T : ISwiftObject
{
    /// <summary>
    /// Indicates whether the handle was allocated by C#
    /// </summary>
    private bool _allocatedHandle;

    /// <summary>
    /// Returns a SwiftHandle with a zero value
    /// </summary>
    public readonly static SwiftHandle<T> Zero = new SwiftHandle<T>(IntPtr.Zero);

    /// <summary>
    /// The handle to the Swift native object
    /// </summary>
    public IntPtr Handle => handle;

    /// <summary>
    /// Constructs a SwiftHandle from the given IntPtr
    /// </summary>
    public SwiftHandle(IntPtr handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
        _allocatedHandle = false;
    }

    /// <summary>
    /// Constructs a SwiftHandle from the given IntPtr
    /// </summary>
    public SwiftHandle(IntPtr handle, bool allocatedHandle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
        _allocatedHandle = allocatedHandle;
    }

    /// <summary>
    /// Releases the handle to the Swift object
    /// </summary>
    protected override unsafe bool ReleaseHandle()
    {
        var metadata = SwiftObjectHelper<T>.GetTypeMetadata();
        metadata.ValueWitnessTable->Destroy(this, metadata);
        if (_allocatedHandle)
            NativeMemory.Free(this);
        handle = IntPtr.Zero;
        return true;
    }

    /// <summary>
    /// Implicit conversion from SwiftHandle to void*
    /// </summary>
    public static unsafe implicit operator void*(SwiftHandle<T> value)
    {
        return (void*)value.Handle;
    }
}
