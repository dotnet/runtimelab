// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace Swift.Runtime;

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

    public static ProtocolWitnessTable GetProtocolWitnessTable<TType, TProtocol>()
        where TProtocol : class
    {
        var metadata = TypeMetadata.GetTypeMetadataOrThrow<TType>();
        if (!ProtocolConformanceDescriptor.TryGetProtocolConformanceDescriptor<TType, TProtocol>(out var conformanceDescriptor))
        {
            throw new SwiftRuntimeException($"Unable to get protocol conformance descriptor for {typeof(TType)} and {typeof(TProtocol)}");
        }

        return GetProtocolWitnessTable(conformanceDescriptor.Value, metadata);
    }

    static ProtocolWitnessTable GetProtocolWitnessTable(ProtocolConformanceDescriptor conformanceDescriptor, TypeMetadata typeMetadata)
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
