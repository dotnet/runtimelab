// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Swift.Runtime;

public readonly struct ProtocolConformanceDescriptor : IEquatable<ProtocolConformanceDescriptor>
{
    private readonly IntPtr _handle;

    private ProtocolConformanceDescriptor(IntPtr handle)
    {
        _handle = handle;
    }

    public readonly static ProtocolConformanceDescriptor Zero = default;

    public bool IsValid => _handle != IntPtr.Zero;

    public bool Equals(ProtocolConformanceDescriptor other)
    {
        return _handle == other._handle;
    }

    internal static bool TryGetProtocolConformanceDescriptor<TType, TProtocol>([NotNullWhen(true)] out ProtocolConformanceDescriptor? result)
        where TProtocol : class
    {
        var type = typeof(TType);

        if (typeof(ISwiftObject).IsAssignableFrom(type))
        {
            var helperType = typeof(ProtocolConformanceDescriptorHelper<,>).MakeGenericType(typeof(TType), typeof(TProtocol));
            result = (ProtocolConformanceDescriptor)helperType.GetMethod("GetProtocolConformanceDescriptor")!.Invoke(null, null)!;
            return true;
        }

        result = null;
        return false;
    }

    public static ProtocolConformanceDescriptor LoadFromSymbol(string libraryPath, string symbolName)
    {
        var libraryHandle = NativeLibrary.Load(libraryPath);

        if (libraryHandle == IntPtr.Zero)
        {
            throw new SwiftRuntimeException($"Unable to load library: {libraryPath}");
        }

        if (NativeLibrary.TryGetExport(libraryHandle, symbolName, out var handle))
        {
            return new ProtocolConformanceDescriptor(handle);
        }

        throw new SwiftRuntimeException($"Unable to find symbol: {symbolName} in library: {libraryPath}");
    }
}
