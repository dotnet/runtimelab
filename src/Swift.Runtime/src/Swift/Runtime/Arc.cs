// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Swift.Runtime;

/// <summary>
/// Arc is a class containing p/invokes for Swift Automatic Reference Counting and memory management.
/// </summary>
public static class Arc {
    /// <summary>
    /// Retain a heap-allocated Swift object
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    [DllImport(KnownLibraries.SwiftCore, CallingConvention = CallingConvention.Cdecl)]
    static extern void swift_retain(IntPtr p);

    /// <summary>
    /// Retain a heap-allocated Swift object
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    /// <returns>The pointer passed in.</returns>
    /// <exception cref="ArgumentNullException">Throws if p is null</exception>
    public static IntPtr Retain(IntPtr p)
    {
        if (p == IntPtr.Zero)
            throw new ArgumentNullException(nameof(p));
        swift_retain(p);
        return p;
    }

    /// <summary>
    /// Check to see if a pointer is in the process of being deallocated.
    /// </summary>
    /// <param name="p">A non-null pointer to an unmanaged Swift object</param>
    /// <returns>True if and only if the pointer in the process of being deallocated.</returns>
    [DllImport(KnownLibraries.SwiftCore, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    static extern bool swift_isDeallocating(IntPtr p);

    /// <summary>
    /// Check to see if a pointer is in the process of being deallocated.
    /// </summary>
    /// <param name="p">A non-null pointer to an unmanaged Swift object</param>
    /// <returns>True if and only if the pointer in the process of being deallocated.</returns>
    /// <exception cref="ArgumentNullException">Throws if p is null</exception>
    public static bool IsDeallocating(IntPtr p)
    {
        if (p == IntPtr.Zero)
            throw new ArgumentNullException(nameof(p));
        return swift_isDeallocating(p);
    }

    /// <summary>
    /// Releases a heap-allocated Swift object
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    [DllImport(KnownLibraries.SwiftCore, CallingConvention = CallingConvention.Cdecl)]
    static extern void swift_release(IntPtr p);

    /// <summary>
    /// Releases a heap-allocated Swift object
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    /// <returns>The pointer passed in</returns>
    /// <exception cref="ArgumentNullException">Throws if p is null</exception>
    /// <exception cref="Exception">Throws if p points to an object that has been deinitialized</exception>
    public static IntPtr Release(IntPtr p)
    {
        if (p == IntPtr.Zero)
            throw new ArgumentNullException(nameof(p));
        if (swift_isDeallocating(p))
        {
            throw new Exception(string.Format ("Attempt to release a Swift object that has been deinitialized {0}", p.ToString($"X{IntPtr.Size * 2}")));
        }
        swift_release(p);
        return p;
    }

    /// <summary>
    /// Retains an 'unowned' heap-allocated Swift object.
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    [DllImport(KnownLibraries.SwiftCore, CallingConvention = CallingConvention.Cdecl)]
    static extern void swift_unownedRetain(IntPtr p);

    /// <summary>
    /// Retains an 'unowned' heap-allocated Swift object.
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    /// <returns>The pointer passed in</returns>
    /// <exception cref="ArgumentNullException">Throws if p is null</exception>
    public static IntPtr UnownedRetain(IntPtr p)
    {
        if (p == IntPtr.Zero)
            throw new ArgumentNullException(nameof(p));
        swift_unownedRetain(p);
        return p;
    }

    /// <summary>
    /// Releases an 'unowned' heap-allocated Swift object.
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    [DllImport(KnownLibraries.SwiftCore, CallingConvention = CallingConvention.Cdecl)]
    static extern void swift_unownedRelease(IntPtr p);

    /// <summary>
    /// Releases an 'unowned' heap-allocated Swift object.
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    /// <returns>The pointer passed in</returns>
    /// <exception cref="ArgumentNullException">Throws if p is null</exception>
    public static IntPtr UnownedRelease(IntPtr p)
    {
        if (p == IntPtr.Zero)
            throw new ArgumentNullException(nameof(p));
        swift_unownedRelease(p);
        return p;
    }

    /// <summary>
    /// Returns the retain count for the heap-allocated Swift object.
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    /// <returns>The retain count</returns>
    [DllImport(KnownLibraries.SwiftCore, CallingConvention = CallingConvention.Cdecl)]
    static extern nint swift_retainCount(IntPtr p);

    /// <summary>
    /// Returns the retain count for the heap-allocated Swift object
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    /// <returns>The retain count</returns>
    /// <exception cref="ArgumentNullException">Throws if p is null</exception>
    public static nint RetainCount(IntPtr p)
    {
        if (p == IntPtr.Zero)
            throw new ArgumentNullException(nameof(p));
        return swift_retainCount(p);
    }

    /// <summary>
    /// Returns the 'unowned' retain count for the heap-allocated Swift object.
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    /// <returns>The unowned retain count</returns>
    [DllImport(KnownLibraries.SwiftCore, CallingConvention = CallingConvention.Cdecl)]
    static extern nint swift_unownedRetainCount(IntPtr p);

    /// <summary>
    /// Returns the 'unowned' retain count for the heap-allocated Swift object.
    /// </summary>
    /// <param name="p">Pointer to an unmanaged Swift object, must be non-null.</param>
    /// <returns>The unowned retain count</returns>
    /// <exception cref="ArgumentNullException">Throws if p is null</exception>
    public static nint UnownedRetainCount(IntPtr p)
    {
        if (p == IntPtr.Zero)
            throw new ArgumentNullException(nameof(p));
        return swift_unownedRetainCount(p);
    }
}
