// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

#nullable enable

namespace Swift.Runtime;

/// <summary>
/// Represents an opaque handle to a Swift object
/// </summary>
public sealed class SwiftHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// The metadata for the Swift object
    /// </summary>
    private TypeMetadata _metadata;

    /// <summary>
    /// Indicates whether the handle was allocated by C#
    /// </summary>
    private bool _allocatedHandle;

    /// <summary>
    /// Returns an SwiftHandle with a zero value
    /// </summary>
    public readonly static SwiftHandle Zero = new SwiftHandle(IntPtr.Zero, TypeMetadata.Zero);

    /// <summary>
    /// The handle to the Swift native object
    /// </summary>
    public IntPtr Handle => handle;

    /// <summary>
    /// Constructs a SwiftHandle from the given IntPtr
    /// </summary>
    public SwiftHandle(IntPtr handle, TypeMetadata metadata, bool allocatedHandle = true)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
        _metadata = metadata;
        _allocatedHandle = allocatedHandle;
    }

    /// <summary>
    /// Releases the handle to the Swift object
    /// </summary>
    protected override unsafe bool ReleaseHandle()
    {
        _metadata.ValueWitnessTable->Destroy((void*)handle, _metadata);
        if (_allocatedHandle)
            NativeMemory.Free((void*)handle);
        handle = IntPtr.Zero;
        return true;
    }
}
