// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Swift.Runtime;

/// <summary>
/// Represents a Swift protocol conformance descriptor.
/// </summary>
public readonly struct ProtocolConformanceDescriptor : IEquatable<ProtocolConformanceDescriptor>
{
    private readonly IntPtr _handle;

    private ProtocolConformanceDescriptor(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// An empty / invalid protocol conformance descriptor.
    /// </summary>
    public readonly static ProtocolConformanceDescriptor Zero = default;

    /// <summary>
    /// Returns true if and only if the protocol conformance descriptor is valid.
    /// </summary>
    public bool IsValid => _handle != IntPtr.Zero;

    /// <inheritdoc/>
    public bool Equals(ProtocolConformanceDescriptor other)
    {
        return _handle == other._handle;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ProtocolConformanceDescriptor other && Equals(other);
    }

    /// <summary>
    /// Determines whether two <see cref="ProtocolConformanceDescriptor"/> instances are equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>
    /// <c>true</c> if the two instances are equal; otherwise, <c>false</c>.
    /// </returns>
    public static bool operator ==(ProtocolConformanceDescriptor left, ProtocolConformanceDescriptor right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="ProtocolConformanceDescriptor"/> instances are not equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>
    /// <c>true</c> if the two instances are not equal; otherwise, <c>false</c>.
    /// </returns>
    public static bool operator !=(ProtocolConformanceDescriptor left, ProtocolConformanceDescriptor right)
    {
        return !(left == right);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return _handle.GetHashCode();
    }

    /// <summary>
    /// Attempts to obtain a <see cref="ProtocolConformanceDescriptor"/> for the specified type and protocol.
    /// </summary>
    /// <typeparam name="TType">The type for which to get the protocol conformance descriptor.</typeparam>
    /// <typeparam name="TProtocol">The interface type representing the protocol.</typeparam>
    /// <param name="result">
    /// When this method returns, contains the <see cref="ProtocolConformanceDescriptor"/> if successful;
    /// otherwise, <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the <see cref="ProtocolConformanceDescriptor"/> was found; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryGet<TType, TProtocol>([NotNullWhen(true)] out ProtocolConformanceDescriptor? result)
        where TProtocol : class
    {
        var type = typeof(TType);

        if (typeof(ISwiftObject).IsAssignableFrom(type))
        {
            var helperType = typeof(ProtocolConformanceDescriptorHelper<,>).MakeGenericType(typeof(TType), typeof(TProtocol));
            var candidate = (ProtocolConformanceDescriptor)helperType.GetMethod("GetProtocolConformanceDescriptor")!.Invoke(null, null)!;

            // GetProtocolConformanceDescriptor can return an IntPtr.Zero
            if (candidate.IsValid)
            {
                result = candidate;
                return true;
            }
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Loads a <see cref="ProtocolConformanceDescriptor"/> from a symbol in the specified library.
    /// </summary>
    /// <param name="libraryName">The name of the library to load.</param>
    /// <param name="symbolName">The name of the symbol to retrieve.</param>
    /// <returns>
    /// A <see cref="ProtocolConformanceDescriptor"/> representing the loaded symbol.
    /// </returns>
    /// <exception cref="SwiftRuntimeException">
    /// Thrown when the specified library or symbol cannot be loaded.
    /// </exception>
    public static ProtocolConformanceDescriptor LoadFromSymbol(string libraryName, string symbolName)
    {
        IntPtr libraryHandle = IntPtr.Zero;

        try
        {
            if (!NativeLibrary.TryLoad(libraryName, typeof(ProtocolConformanceDescriptor).Assembly, null, out libraryHandle))
            {

                throw new SwiftRuntimeException($"Unable to load library: {libraryName}");
            }

            if (NativeLibrary.TryGetExport(libraryHandle, symbolName, out var handle))
            {
                return new ProtocolConformanceDescriptor(handle);
            }

            throw new SwiftRuntimeException($"Unable to find symbol: {symbolName} in library: {libraryName}");
        }
        finally
        {
            NativeLibrary.Free(libraryHandle);
        }
    }
}
