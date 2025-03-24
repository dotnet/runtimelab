// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Microsoft.Win32.SafeHandles;

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
    /// Returns an SwiftHandle with a zero value
    /// </summary>
    public readonly static SwiftHandle Zero = new SwiftHandle(IntPtr.Zero);

    /// <summary>
    /// The handle to the Swift native object
    /// </summary>
    public IntPtr Handle
    {
        get => handle;
        set => SetHandle(value);
    }

    /// <summary>
    /// Constructs a SwiftHandle from the given IntPtr
    /// </summary>
    public SwiftHandle(IntPtr handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
        _metadata = TypeMetadata.Zero;
    }

    /// <summary>
    /// Sets the metadata for the Swift object
    /// </summary>
    public void SetMetadata(TypeMetadata metadata)
    {
        _metadata = metadata;
    }

    /// <summary>
    /// Releases the handle to the Swift object
    /// </summary>
    protected override unsafe bool ReleaseHandle()
    {
        if (_metadata.Handle == IntPtr.Zero)
            return false;

        _metadata.ValueWitnessTable->Destroy((void*)handle, _metadata);
        handle = IntPtr.Zero;
        return true;
    }
}
