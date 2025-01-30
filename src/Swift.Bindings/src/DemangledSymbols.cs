// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BindingsGeneration;
using Xamarin;

public readonly record struct DemangledSymbols
{
    public Dictionary<(NamedTypeSpec, NamedTypeSpec), string> ProtocolConformanceDescriptors { get; init; }

    public DemangledSymbols(Dictionary<(NamedTypeSpec, NamedTypeSpec), string> descriptors)
    {
        ProtocolConformanceDescriptors = descriptors;
    }
}

public sealed class DemangledSymbolsRegister
{
    private static readonly Lazy<DemangledSymbolsRegister> _instance = new(() => new DemangledSymbolsRegister());

    private DemangledSymbols _symbols = new(new Dictionary<(NamedTypeSpec, NamedTypeSpec), string>());
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
            var dictionary = new Dictionary<(NamedTypeSpec, NamedTypeSpec), string>();

            foreach (var descriptor in descriptors)
            {
                dictionary[(descriptor.ImplementingType, descriptor.ProtocolType)] = descriptor.Symbol[1..];
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
