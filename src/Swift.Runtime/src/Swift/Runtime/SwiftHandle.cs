// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

#nullable enable

namespace Swift.Runtime;

/// <summary>
/// Represents an opaque handle to a Swift object
/// </summary>
public readonly struct SwiftHandle : IEquatable<SwiftHandle> {
    readonly IntPtr handle;

    public IntPtr Handle => handle;

    /// <summary>
    /// Returns an SwiftHandle with a zero value
    /// </summary>
    public readonly static SwiftHandle Zero = default (SwiftHandle);

    /// <summary>
    /// Constructs a SwiftHandle from the given IntPtr
    /// </summary>
    public SwiftHandle (IntPtr handle)
    {
        this.handle = handle;
    }

    /// <summary>
    /// Returns true if the SwiftHandle and the IntPtr are the same
    /// </summary>
   public static bool operator == (SwiftHandle left, IntPtr right)
    {
        return left.handle == right;
    }

    /// <summary>
    /// Returns true if both SwiftHandles are the same
    /// </summary>
    public static bool operator == (SwiftHandle left, SwiftHandle right)
    {
        return left.handle == right.handle;
    }

    /// <summary>
    /// Returns true if the IntPtr and the SwiftHandle are the same
    /// </summary>
    public static bool operator == (IntPtr left, SwiftHandle right)
    {
        return left == right.Handle;
    }

    /// <summary>
    /// Returns true if the SwiftHandle and the IntPtr are different
    /// </summary>
    public static bool operator != (SwiftHandle left, IntPtr right)
    {
        return left.handle != right;
    }

    /// <summary>
    /// Returns true if the IntPtr and the SwiftHandle are different
    /// </summary>
    public static bool operator != (IntPtr left, SwiftHandle right)
    {
        return left != right.Handle;
    }

    /// <summary>
    /// Returns true if the SwiftHandles are different
    /// </summary>
    public static bool operator != (SwiftHandle left, SwiftHandle right)
    {
        return left.handle != right.Handle;
    }


    /// <summary>
    /// Implicit conversion from SwiftHandle to IntPtr
    /// </summary>
    public static implicit operator IntPtr (SwiftHandle value)
    {
        return value.Handle;
    }

    /// <summary>
    /// Implicit conversion from IntPtr to SwiftHandle
    /// </summary>
    public static implicit operator SwiftHandle (IntPtr value)
    {
        return new SwiftHandle (value);
    }

    /// <summary>
    /// Explicit conversion from SwiftHandle to void*
    /// </summary>
    public unsafe static explicit operator void* (SwiftHandle value)
    {
        return (void *) (IntPtr) value;
    }

    /// <summary>
    /// Explicit conversion from void* to SwiftHandle
    /// </summary>
    public unsafe static explicit operator SwiftHandle (void* value)
    {
        return new SwiftHandle ((IntPtr) value);
    }

    /// <summary>
    /// Compare this SwiftHandle to the given object, returns true if they're the same.
    /// </summary>
    public override bool Equals (object? o)
    {
        if (o is SwiftHandle nh)
            return nh.handle == this.handle;
        return false;
    }

    /// <summary>
    /// Generate a hashcode for the SwiftHandle
    /// </summary>
    public override int GetHashCode ()
    {
        return handle.GetHashCode ();
    }

    /// <summary>
    /// Returns true if this matches the give SwiftHandle
    /// </summary>
    public bool Equals (SwiftHandle other)
    {
        return other.handle == handle;
    }

    /// <summary>
    /// Generates a string representation of this SwiftHandle
    /// </summary>
    public override string ToString ()
    {
        return "0x" + handle.ToString ("x");
    }
}
