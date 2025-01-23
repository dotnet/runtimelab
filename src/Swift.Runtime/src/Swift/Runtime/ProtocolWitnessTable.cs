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

    internal static bool TryGetProtocolConformanceDescriptor<T, U>([NotNullWhen(true)] out ProtocolConformanceDescriptor? result)
    {
        var type = typeof(T);

        if (typeof(ISwiftObject).IsAssignableFrom(type))
        {
            var helperType = typeof(ProtocolConformanceDescriptorHelper<,>).MakeGenericType(typeof(T), typeof(U));
            result = (ProtocolConformanceDescriptor)helperType.GetMethod("GetProtocolConformanceDescriptor")!.Invoke(null, null)!;
            return true;
        }

        result = null;
        return false;
    }

    public static ProtocolConformanceDescriptor LoadProtocolConformanceDescriptor(string libraryPath, string symbolName)
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

public readonly struct ProtocolWitnessTable : IEquatable<ProtocolWitnessTable>
{
    private readonly IntPtr _handle;

    private ProtocolWitnessTable(IntPtr handle)
    {
        _handle = handle;
    }

    public readonly static ProtocolWitnessTable Zero = default;

    public bool IsValid => _handle != IntPtr.Zero;

    public bool Equals(ProtocolWitnessTable other)
    {
        return _handle == other._handle;
    }

    public ProtocolWitnessTable GetProtocolWitnessTable<T, U>()
    {
        var metadata = TypeMetadata.GetTypeMetadataOrThrow<T>();
        if (!ProtocolConformanceDescriptor.TryGetProtocolConformanceDescriptor<T, U>(out var conformanceDescriptor))
        {
            throw new SwiftRuntimeException($"Unable to get protocol conformance descriptor for {typeof(T)} and {typeof(U)}");
        }

        return GetProtocolWitnessTable(conformanceDescriptor.Value, metadata);
    }

    ProtocolWitnessTable GetProtocolWitnessTable(ProtocolConformanceDescriptor conformanceDescriptor, TypeMetadata typeMetadata)
    {

        var witnessTable = swift_getWitnessTable(conformanceDescriptor, typeMetadata, IntPtr.Zero);
        return witnessTable;
    }

    /// <summary>
    /// Gets the protocol witness table for a given type and protocol.
    /// </summary>
    /// <param name="conformanceDescriptor">The protocol conformance descriptor.</param>
    /// <param name="typeMetadata">The type metadata.</param>
    /// <param name="instantiationArgs">The instantiation arguments used for conditional conformance.</param>
    [DllImport(KnownLibraries.SwiftCore, CallingConvention = CallingConvention.Cdecl)]
    static extern ProtocolWitnessTable swift_getWitnessTable(ProtocolConformanceDescriptor conformanceDescriptor, TypeMetadata typeMetadata, IntPtr instantiationArgs);
}
