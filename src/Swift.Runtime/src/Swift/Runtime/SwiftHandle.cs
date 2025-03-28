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
/// Represents an opaque handle to a Swift object
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
    public SwiftHandle(IntPtr handle, bool allocatedHandle = true)
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
        metadata.ValueWitnessTable->Destroy((void*)handle, metadata);
        if (_allocatedHandle)
            NativeMemory.Free((void*)handle);
        handle = IntPtr.Zero;
        return true;
    }
}
