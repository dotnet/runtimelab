using Microsoft.Extensions.Logging;
using TbdParsing;

namespace BindingsGeneration.Demangling;

/// <summary>
/// A class to contain results from demangling the set of symbols in TBD file.
/// </summary>
public class DemanglingResults
{
    /// <summary>
    /// Constructs a DemanglingResults object with the desired reductions separated out
    /// </summary>
    /// <param name="reductions">An array of reductions</param>
    DemanglingResults(IReduction[] reductions)
    {
        Errors = ArrayOf<ReductionError>(reductions);
        MetadataAccessors = ArrayOf<MetadataAccessorReduction>(reductions);
        DispatchThunks = ArrayOf<DispatchThunkFunctionReduction>(reductions);
        ProtocolWitnessTables = ArrayOf<ProtocolWitnessTableReduction>(reductions);
        ProtocolConformanceDescriptors = ArrayOf<ProtocolConformanceDescriptorReduction>(reductions);
    }

    /// <summary>
    /// A utility routine to filter out a specific type of reduction from a set of general reductions
    /// </summary>
    /// <typeparam name="T">The type of the desired aggregation of reductions</typeparam>
    /// <param name="reductions">an array of general reductions to filter</param>
    /// <returns>An array of the requested filtered reductions</returns>
    static T[] ArrayOf<T>(IReduction[] reductions) where T : IReduction
    {
        return reductions.OfType<T>().ToArray();
    }

    /// <summary>
    /// All errors encountered while demangling symbols
    /// </summary>
    public ReductionError[] Errors { get; private set; }

    /// <summary>
    /// All type metadata accessor functions encountered while demangling symbols
    /// </summary>
    public MetadataAccessorReduction[] MetadataAccessors { get; private set; }

    /// <summary>
    /// All dispatch thunk functions encountered while demangling symbols
    /// </summary>
    public DispatchThunkFunctionReduction[] DispatchThunks { get; private set; }

    /// <summary>
    /// All protocol witness tables found while demangling symbols
    /// </summary>
    public ProtocolWitnessTableReduction[] ProtocolWitnessTables { get; private set; }

    /// <summary>
    /// All protocol conformance descriptors founc while demangling symbols
    /// </summary>
    public ProtocolConformanceDescriptorReduction[] ProtocolConformanceDescriptors { get; private set; }

    /// <summary>
    /// Factory method to generate a suite of demangling results from the given TBD file.
    /// </summary>
    /// <param name="path">Path to the TBD file.</param>
    /// <param name="loggerFactory">ILoggerFactory instance.</param>
    /// <returns>A set of demangling results.</returns>
    public static DemanglingResults FromTbd(string path, ILoggerFactory loggerFactory)
    {
        var tbdParser = new TbdParser(loggerFactory);
        var tbdFile = tbdParser.ParseFile(path);

        var demangler = new Swift5Demangler();

        // Run demangler for each export and aggregate results
        var allReductions = tbdFile.Exports.SelectMany(export => export.SwiftSymbols.Select(sym =>
            demangler.Run(sym.Name.StartsWith('_') ? sym.Name[1..] : sym.Name))).ToArray();
        return new DemanglingResults(allReductions);
    }

    /// <summary>
    /// Retrieve MetadataAccessor for a type.
    /// </summary>
    /// <param name="swiftTypeName">The Swift type name.</param>
    /// <returns>The mangled name of the metadata accessor.</returns>
    /// exception cref="Exception">
    /// Thrown if the metadata accessor is not found in demangled results.
    /// </exception>
    public string GetMetadataAccessor(SwiftTypeName swiftTypeName)
    {
        var metadataAccessor = MetadataAccessors.FirstOrDefault(x => x.TypeSpec.Name == swiftTypeName.ModuleQualifiedName);

        if (metadataAccessor == null)
        {
            throw new Exception($"Metadata accessor not found for type '{swiftTypeName}'.");
        }

        return metadataAccessor.Symbol;
    }

    /// <summary>
    /// Retrieve ProtocolConformanceDescriptor for a type.
    /// </summary>
    /// <param name="implementingType">The implementing Swift type.</param>
    /// <param name="protocol">The Swift protocol.</param>
    /// <returns>The mangled name of the protocol conformance descriptor.</returns>
    /// exception cref="Exception">
    /// Thrown if the protocol conformance descriptor is not found in demangled results.
    /// </exception>
    public string GetProtocolConformanceDescriptor(SwiftTypeName implementingType, SwiftTypeName protocol)
    {
        var protocolConformanceDescriptor = ProtocolConformanceDescriptors.FirstOrDefault(
            x => x.ImplementingType.Name == implementingType.ModuleQualifiedName && x.ProtocolType.Name == protocol.ModuleQualifiedName);

        if (protocolConformanceDescriptor == null)
        {
            throw new Exception($"Protocol conformance descriptor not found for type '{implementingType}' and protocol '{protocol}'.");
        }

        return protocolConformanceDescriptor.Symbol;
    }
}
