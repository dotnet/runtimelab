// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BindingsGeneration;
using Xamarin;

public readonly record struct DemangledSymbols
{
    public Dictionary<(SwiftTypeName, SwiftTypeName), string> ProtocolConformanceDescriptors { get; init; }

    public DemangledSymbols(Dictionary<(SwiftTypeName, SwiftTypeName), string> descriptors)
    {
        ProtocolConformanceDescriptors = descriptors;
    }
}

public sealed class DemangledSymbolsRegister
{
    private static readonly Lazy<DemangledSymbolsRegister> _instance = new(() => new DemangledSymbolsRegister());

    private DemangledSymbols _symbols = new(new Dictionary<(SwiftTypeName, SwiftTypeName), string>());
    private bool _isLoaded = false;

    public static DemangledSymbolsRegister Instance => _instance.Value;

    private DemangledSymbolsRegister() { }

    public DemangledSymbols GetData(string dylibPath = "")
    {
        if (!_isLoaded)
        {
            if (string.IsNullOrEmpty(dylibPath))
            {
                throw new ArgumentException("dylibPath cannot be null or empty.");
            }

            Load(dylibPath);
        }
        return _symbols;
    }

    private void Load(string dylibPath)
    {
        try
        {
            var abis = MachO.GetArchitectures(dylibPath);
            var descriptors = DemanglingResults.FromFile(dylibPath, abis[0]).ProtocolConformanceDescriptors;
            var dictionary = new Dictionary<(SwiftTypeName, SwiftTypeName), string>();

            foreach (var descriptor in descriptors)
            {
                var implementingType = SwiftTypeName.FromTypeSpec(descriptor.ImplementingType);
                var protocolType = SwiftTypeName.FromTypeSpec(descriptor.ProtocolType);
                dictionary[(implementingType, protocolType)] = descriptor.Symbol[1..];
            }

            _symbols = new DemangledSymbols(dictionary);
            _isLoaded = true;
        }
        catch (Exception e)
        {
            // This happens if the dylib cannot be located on disk, which will be the case for Apple shipped dylibs. (e.g. Foundation, StoreKit, etc.)
            Console.Error.WriteLine($"Error loading demangled symbols: {e.Message}");
        }
    }
}
