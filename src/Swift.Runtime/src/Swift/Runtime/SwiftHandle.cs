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
    private TypeMetadata _metadata = default;
    public TypeMetadata Metadata => _metadata;

    /// <summary>
    /// Returns an SwiftHandle with a zero value
    /// </summary>
    public readonly static SwiftHandle Zero = new SwiftHandle(IntPtr.Zero);

// get set
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
    }

    protected override unsafe bool ReleaseHandle()
    {
        // _metadata.ValueWitnessTable->Destroy((void*)handle, _metadata);
        return true;
    }

    // /// <summary>
    // /// Implicit conversion from SwiftHandle to IntPtr
    // /// </summary>
    // public static implicit operator IntPtr(SwiftHandle value)
    // {
    //     return value.handle;
    // }

    // /// <summary>
    // /// Explicit conversion from SwiftHandle to void*
    // /// </summary>
    // public unsafe static explicit operator void*(SwiftHandle value)
    // {
    //     return (void*)value.handle;
    // }

}
