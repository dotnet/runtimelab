// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Swift;
using Swift.Runtime;
using Swift.Runtime.InteropServices;

namespace Swift;

/// <summary>
/// Represents a Swift array
/// </summary>
/// <typeparam name="T">the element type</typeparam>
public sealed class SwiftArray<T> : ISwiftObject, IList<T>
{

    SwiftHandle handle;

    /// <summary>
    /// Constructs a new SwiftArray from the given handle to a Swift array
    /// </summary>
    public SwiftArray(SwiftHandle handle)
    {
        this.handle = handle;
    }


    /// <summary>
    /// Constructs a new empty SwiftArray
    /// </summary>
    public SwiftArray()
        : this(PInvokesForSwiftArray.Init(ElementTypeMetadata))
    {
    }

    /// <summary>
    /// Constructs a new SwiftArray and populates it with the given list
    /// </summary>
    /// <param name="list">a list of items to populate the array</param>
    public SwiftArray(IList<T> list)
        : this()
    {
        AddRange(list);
    }

    /// <summary>
    /// Constructs a new SwiftArray and populates it with the enumeration
    /// </summary>
    /// <param name="collection">an enumeration of items to populate the array</param>
    public SwiftArray(IEnumerable<T> collection)
        : this()
    {
        AddRange(collection);
    }

    /// <summary>
    /// Constructs a new SwiftArray and populates it with the given array
    /// </summary>
    /// <param name="items">an array of items to populate the array</param>
    public SwiftArray(params T[] items)
        : this()
    {
        AddRange(items);
    }

    /// <summary>
    /// Returns the type metadata for the element
    /// </summary>
    static TypeMetadata ElementTypeMetadata
    {
        get => TypeMetadata.GetTypeMetadataOrThrow<T>();
    }

    /// <summary>
    /// Returns the size of the element in bytes
    /// </summary>
    static unsafe nuint ElementSize
    {
        get => ElementTypeMetadata.ValueWitnessTable->Size;
    }

    /// <inheritdoc/>
    static TypeMetadata ISwiftObject.GetTypeMetadata()
    {
        return TypeMetadata.Cache.GetOrAdd(typeof(SwiftArray<T>), _ =>
                PInvokesForSwiftArray._MetadataAccessor(TypeMetadataRequest.Complete, ElementTypeMetadata));
    }

    /// <inheritdoc/>
    static ISwiftObject ISwiftObject.NewFromPayload(SwiftHandle payload)
    {
        return new SwiftArray<T>(payload);
    }

    /// <inheritdoc/>
    IntPtr ISwiftObject.MarshalToSwift(IntPtr swiftDest)
    {
        var metadata = SwiftObjectHelper<SwiftArray<T>>.GetTypeMetadata();
        unsafe
        {
            metadata.ValueWitnessTable->InitializeWithCopy((void*)swiftDest, (void*)handle, metadata);
        }
        return swiftDest;
    }

    /// <inheritdoc/>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();

            unsafe
            {
                var payload = stackalloc byte[(int)ElementSize];
                PInvokesForSwiftArray.Get(new SwiftIndirectResult(payload), (nint)index, handle, ElementTypeMetadata);
                return SwiftMarshal.MarshalFromSwift<T>(new SwiftHandle((IntPtr)payload));
            }
        }
        set
        {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();

            var metadata = SwiftObjectHelper<SwiftArray<T>>.GetTypeMetadata();
            unsafe
            {
                var payload = stackalloc byte[(int)ElementSize];
                SwiftMarshal.MarshalToSwift(value, (IntPtr)payload);

                var localHandle = handle;
                var localHandlePtr = &localHandle;

                PInvokesForSwiftArray.Set(new SwiftSelf(localHandlePtr), new SwiftHandle((IntPtr)payload), (nint)index, metadata);
                handle = localHandle;
            }
        }
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            unsafe
            {
                return (int)PInvokesForSwiftArray.Count(handle, ElementTypeMetadata);
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        int count = Count;
        for (int i = 0; i < count; i++)
        {
            yield return this[i];
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc/>
    public void Add(T item)
    {
        unsafe
        {
            var elementMetadata = ElementTypeMetadata;
            var payload = stackalloc byte[(int)ElementSize];
            SwiftMarshal.MarshalToSwift(item, (IntPtr)payload);

            var localHandle = handle;
            void* localHandlePtr = &localHandle;
            var metadata = SwiftObjectHelper<SwiftArray<T>>.GetTypeMetadata();
            PInvokesForSwiftArray.Append(new SwiftSelf(localHandlePtr), new SwiftHandle((IntPtr)payload), metadata);
            handle = localHandle;
        }
    }

    /// <inheritdoc/>
    public void AddRange(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
            Add(list[i]);
    }

    /// <inheritdoc/>
    public void AddRange(IEnumerable<T> collection)
    {
        foreach (T elem in collection)
        {
            Add(elem);
        }
    }


    /// <inheritdoc/>
    public void Clear()
    {
        unsafe
        {
            var localHandle = handle;
            var localHandlePtr = &localHandle;
            var metadata = SwiftObjectHelper<SwiftArray<T>>.GetTypeMetadata();
            PInvokesForSwiftArray.RemoveAll(new SwiftSelf(localHandlePtr), 1, metadata);
            handle = localHandle;
        }
    }

    /// <inheritdoc/>
    public bool Contains(T item)
    {
        foreach (T thing in this)
        {
            if (Equals(thing, item))
                return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));

        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        if (Count > array.Length - arrayIndex)
            throw new ArgumentException("Destination array was not long enough.");

        foreach (T thing in this)
        {
            array[arrayIndex++] = thing;
        }
    }

    /// <inheritdoc/>
    public bool Remove(T item)
    {
        int i = 0;
        foreach (T thing in this)
        {
            if (Equals(thing, item))
            {
                RemoveAt(i);
                return true;
            }
            i++;
        }
        return false;
    }

    /// <inheritdoc/>
    public int IndexOf(T item)
    {
        int i = 0;
        foreach (T thing in this)
        {
            if (Equals(thing, item))
                return i;
            i++;
        }
        return -1;
    }

    /// <inheritdoc/>
    public void Insert(int index, T item)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        unsafe
        {
            var payload = stackalloc byte[(int)ElementSize];
            SwiftMarshal.MarshalToSwift(item, (IntPtr)payload);

            var localHandle = handle;
            var localHandlePtr = &localHandle;
            var metadata = SwiftObjectHelper<SwiftArray<T>>.GetTypeMetadata();
            PInvokesForSwiftArray.Insert(new SwiftSelf(localHandlePtr), new SwiftHandle((IntPtr)payload), (nint)index, metadata);
            handle = localHandle;
        }
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        unsafe
        {
            var payload = stackalloc byte[(int)ElementSize];
            var localHandle = handle;
            var localHandlePtr = &localHandle;
            var metadata = SwiftObjectHelper<SwiftArray<T>>.GetTypeMetadata();
            PInvokesForSwiftArray.RemoveAt(new SwiftIndirectResult(payload), new SwiftSelf(localHandlePtr), (nint)index, metadata);
            var witness = ElementTypeMetadata.ValueWitnessTable;
            witness->Destroy((void*)payload, witness);
            handle = localHandle;
        }
    }

    /// <inheritdoc/>
    public bool IsReadOnly
    {
        get
        {
            return false;
        }
    }
}

internal static class PInvokesForSwiftArray
{
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSaMa")]
    public static extern TypeMetadata _MetadataAccessor(TypeMetadataRequest request, TypeMetadata typeMetadata);

    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sS2ayxGycfC")]
    public static extern SwiftHandle Init(TypeMetadata typeMetadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSayxSicig")]
    public static unsafe extern void Get(SwiftIndirectResult result, nint index, SwiftHandle handle, TypeMetadata elementMetadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSayxSicis")]
    public static unsafe extern void Set(SwiftSelf self, SwiftHandle value, nint index, TypeMetadata elementMetadata);

    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa5countSivg")]
    public static extern nint Count(SwiftHandle handle, TypeMetadata elementMetadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa6appendyyxnF")]
    public static unsafe extern void Append(SwiftSelf self, SwiftHandle value, TypeMetadata metadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa9removeAll15keepingCapacityySb_tF")]
    public static unsafe extern void RemoveAll(SwiftSelf self, byte keepCapacity, TypeMetadata metadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa6remove2atxSi_tF")]
    public static unsafe extern void RemoveAt(SwiftIndirectResult result, SwiftSelf self, nint index, TypeMetadata metadata);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    [DllImport(KnownLibraries.SwiftCore, EntryPoint = "$sSa6insert_2atyxn_SitF")]
    public static unsafe extern void Insert(SwiftSelf self, SwiftHandle value, nint index, TypeMetadata metadata);
}
