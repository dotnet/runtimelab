// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Swift.Runtime.InteropServices;

#nullable enable

/// <summary>
/// Represents a class for marshaling data to and from Swift
/// </summary>
public static class SwiftMarshal {
    /// <summary>
    /// Marshals a value to a Swift destination
    /// </summary>
    /// <typeparam name="T">The type of the value being marshaled</typeparam>
    /// <param name="value">The value to marshal</param>
    /// <param name="swiftDest">the destination for marshaling</param>
    /// <returns>A pointer to memory to pass in to Swift for marshaling. Note: this value may be different from the value passed in.</returns>
    /// <exception cref="NotSupportedException"></exception>
    public static IntPtr MarshalToSwift<T>(T value, IntPtr swiftDest) {
        if (value is ISwiftObject swiftValue) {
            return swiftValue.MarshalToSwift(new IntPtr(swiftDest));
        }

        var type = typeof(T);
        if (type.IsPrimitive || typeof(nint).IsAssignableFrom(type) || typeof(nuint).IsAssignableFrom(type)) {
            unsafe {
                return new IntPtr(MarshalPrimitiveToSwift(value, (void *)swiftDest));
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
    static unsafe void* MarshalPrimitiveToSwift<T>(T value, void *swiftDest) {
        if (value is bool boolValue) {
            *((byte*)swiftDest) = (byte)(boolValue ? 1 : 0);
            return swiftDest;
        } else if (value is byte byteValue) {
            *((byte*)swiftDest) = byteValue;
            return swiftDest;
        } else if (value is sbyte sbyteValue) {
            *((sbyte*)swiftDest) = sbyteValue;
            return swiftDest;
        } else if (value is short shortValue) {
            *((short*)swiftDest) = shortValue;
            return swiftDest;
        } else if (value is ushort ushortValue) {
            *((ushort*)swiftDest) = ushortValue;
            return swiftDest;
        } else if (value is int intValue) {
            *((int*)swiftDest) = intValue;
            return swiftDest;
        } else if (value is uint uintValue) {
            *((uint*)swiftDest) = uintValue;
            return swiftDest;
        } else if (value is long longValue) {
            *((long*)swiftDest) = longValue;
            return swiftDest;
        } else if (value is ulong ulongValue) {
            *((ulong*)swiftDest) = ulongValue;
            return swiftDest;
        } else if (value is float floatValue) {
            *((float*)swiftDest) = floatValue;
            return swiftDest;
        } else if (value is double doubleValue) {
            *((double*)swiftDest) = doubleValue;
            return swiftDest;
        } else if (value is nint nintValue) {
            *((nint*)swiftDest) = nintValue;
            return swiftDest;
        } else if (value is nuint nuintValue) {
            *((nuint*)swiftDest) = nuintValue;
            return swiftDest;
        } else {
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
    public static T MarshalFromSwift<T>(SwiftHandle swiftSource) {
        if (typeof(ISwiftObject).IsAssignableFrom(typeof(T))) {
            var helper = typeof(SwiftObjectHelper<>).MakeGenericType(typeof(T));
            return (T)helper.GetMethod("NewFromPayload")!.Invoke(null, new object[] { new SwiftHandle(swiftSource) })!;
        }
        var type = typeof(T);
        if (type.IsPrimitive) {
            unsafe {
                return MarshalPrimitiveFromSwift<T>((void*)swiftSource);
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
    public static unsafe T MarshalPrimitiveFromSwift<T>(void *swiftSource) {
        if (typeof(T) == typeof(bool)) {
            return (T)(object)(((*(byte *)swiftSource) & 1) != 0);
        } else if (typeof(T) == typeof(byte)) {
            return (T)(object)(*(byte*)swiftSource);
        } else if (typeof(T) == typeof(sbyte)) {
            return (T)(object)(*(sbyte*)swiftSource);
        } else if (typeof(T) == typeof(short)) {
            return (T)(object)(*(short*)swiftSource);
        } else if (typeof(T) == typeof(ushort)) {
            return (T)(object)(*(ushort*)swiftSource);
        } else if (typeof(T) == typeof(int)) {
            return (T)(object)(*(int*)swiftSource);
        } else if (typeof(T) == typeof(uint)) {
            return (T)(object)(*(uint*)swiftSource);
        } else if (typeof(T) == typeof(long)) {
            return (T)(object)(*(long*)swiftSource);
        } else if (typeof(T) == typeof(ulong)) {
            return (T)(object)(*(ulong*)swiftSource);
        } else if (typeof(T) == typeof(float)) {
            return (T)(object)(*(float*)swiftSource);
        } else if (typeof(T) == typeof(double)) {
            return (T)(object)(*(double*)swiftSource);
        } else if (typeof(T) == typeof(nint)) {
            return (T)(object)(*(nint*)swiftSource);
        } else if (typeof(T) == typeof(nuint)) {
            return (T)(object)(*(nuint*)swiftSource);
        } else {
            throw new NotSupportedException($"Cannot marshal type {typeof(T)} from Swift");
        }
    }
}
