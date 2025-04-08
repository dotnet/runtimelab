// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Swift.Runtime.InteropServices;

#nullable enable

/// <summary>
/// Represents a class for marshaling data to and from Swift
/// </summary>
public static class SwiftMarshal
{
    /// <summary>
    /// Marshals a value to a Swift destination
    /// </summary>
    /// <typeparam name="T">The type of the value being marshaled</typeparam>
    /// <param name="value">The value to marshal</param>
    /// <param name="swiftDestSpan">the destination for marshaling</param>
    /// <returns>the number of bytes written to the destination</returns>
    public static int MarshalToSwift<T>(T value, ref Span<byte> swiftDestSpan)
    {
        if (value is ISwiftObject swiftValue)
        {
            return swiftValue.MarshalToSwift(ref swiftDestSpan);
        }

        var type = typeof(T);
        if ((type.IsPrimitive || typeof(nint).IsAssignableFrom(type) || typeof(nuint).IsAssignableFrom(type)) && !typeof(char).IsAssignableFrom(type))
        {
            unsafe
            {
                int size = Unsafe.SizeOf<T>();
                if (size > swiftDestSpan.Length)
                {
                    throw new ArgumentException($"Span size does not match type size, Expected: {size}, Actual: {swiftDestSpan.Length}");
                }
                fixed (void* swiftDest = swiftDestSpan)
                {
                    MarshalPrimitiveToSwift(value, swiftDest);
                    return size;
                }
            }
        }

        // TODO: Implement for tuples

        // TODO: Implement for closures

        // TODO: Implement for existential containers

        throw new NotSupportedException($"Cannot marshal type {type} to Swift");
    }

    /// <summary>
    /// Marshals a primitive value to a Swift destination
    /// </summary>
    /// <typeparam name="T">the type of the primitive</typeparam>
    /// <param name="value">The value to marshal</param>
    /// <param name="swiftDest">where in memory to marshal it</param>
    /// <returns>the resulting pointer for passing to a Swift method.</returns>
    /// <exception cref="NotSupportedException"></exception>
    static unsafe void MarshalPrimitiveToSwift<T>(T value, void* swiftDest)
    {
        if (value is bool boolValue)
        {
            *((byte*)swiftDest) = (byte)(boolValue ? 1 : 0);
        }
        else if (value is byte byteValue)
        {
            *((byte*)swiftDest) = byteValue;
        }
        else if (value is sbyte sbyteValue)
        {
            *((sbyte*)swiftDest) = sbyteValue;
        }
        else if (value is short shortValue)
        {
            *((short*)swiftDest) = shortValue;
        }
        else if (value is ushort ushortValue)
        {
            *((ushort*)swiftDest) = ushortValue;
        }
        else if (value is int intValue)
        {
            *((int*)swiftDest) = intValue;
        }
        else if (value is uint uintValue)
        {
            *((uint*)swiftDest) = uintValue;
        }
        else if (value is long longValue)
        {
            *((long*)swiftDest) = longValue;
        }
        else if (value is ulong ulongValue)
        {
            *((ulong*)swiftDest) = ulongValue;
        }
        else if (value is float floatValue)
        {
            *((float*)swiftDest) = floatValue;
        }
        else if (value is double doubleValue)
        {
            *((double*)swiftDest) = doubleValue;
        }
        else if (value is nint nintValue)
        {
            *((nint*)swiftDest) = nintValue;
        }
        else if (value is nuint nuintValue)
        {
            *((nuint*)swiftDest) = nuintValue;
        }
        else
        {
            throw new NotSupportedException($"Cannot marshal type {typeof(T)} to Swift");
        }
    }

    /// <summary>
    /// Marshals a value from a Swift source.
    /// </summary>
    /// <typeparam name="T">The type of the expected value</typeparam>
    /// <param name="swiftSource">Memory to read from</param>
    /// <returns>The C# type created by marshaling</returns>
    /// <exception cref="NotSupportedException"></exception>
    public static T MarshalFromSwift<T>(IntPtr swiftSource)
    {
        if (typeof(ISwiftObject).IsAssignableFrom(typeof(T)))
        {
            var helper = typeof(SwiftObjectHelper<>).MakeGenericType(typeof(T));
            return (T)helper.GetMethod("NewFromPayload")!.Invoke(null, new object[] { swiftSource })!;
        }
        var type = typeof(T);
        if (type.IsPrimitive)
        {
            unsafe
            {
                return MarshalPrimitiveFromSwift<T>(swiftSource);
            }
        }

        // TODO: Implement for tuples

        // TODO: Implement for closures

        // TODO: Implement for existential containers
        throw new NotSupportedException($"Cannot marshal type {type} from Swift");
    }

    /// <summary>
    /// Marshals a primitive value from a Swift source
    /// </summary>
    /// <typeparam name="T">The type of the value to marshal</typeparam>
    /// <param name="swiftSource">Memory to read from</param>
    /// <returns>The marshaled type</returns>
    /// <exception cref="NotSupportedException"></exception>
    public static unsafe T MarshalPrimitiveFromSwift<T>(IntPtr swiftSource)
    {
        if (typeof(T) == typeof(bool))
        {
            return (T)(object)(((*(byte*)swiftSource) & 1) != 0);
        }
        else if (typeof(T) == typeof(byte))
        {
            return (T)(object)(*(byte*)swiftSource);
        }
        else if (typeof(T) == typeof(sbyte))
        {
            return (T)(object)(*(sbyte*)swiftSource);
        }
        else if (typeof(T) == typeof(short))
        {
            return (T)(object)(*(short*)swiftSource);
        }
        else if (typeof(T) == typeof(ushort))
        {
            return (T)(object)(*(ushort*)swiftSource);
        }
        else if (typeof(T) == typeof(int))
        {
            return (T)(object)(*(int*)swiftSource);
        }
        else if (typeof(T) == typeof(uint))
        {
            return (T)(object)(*(uint*)swiftSource);
        }
        else if (typeof(T) == typeof(long))
        {
            return (T)(object)(*(long*)swiftSource);
        }
        else if (typeof(T) == typeof(ulong))
        {
            return (T)(object)(*(ulong*)swiftSource);
        }
        else if (typeof(T) == typeof(float))
        {
            return (T)(object)(*(float*)swiftSource);
        }
        else if (typeof(T) == typeof(double))
        {
            return (T)(object)(*(double*)swiftSource);
        }
        else if (typeof(T) == typeof(nint))
        {
            return (T)(object)(*(nint*)swiftSource);
        }
        else if (typeof(T) == typeof(nuint))
        {
            return (T)(object)(*(nuint*)swiftSource);
        }
        else
        {
            throw new NotSupportedException($"Cannot marshal type {typeof(T)} from Swift");
        }
    }
}
