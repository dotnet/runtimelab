using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

#nullable enable

namespace Microsoft.Interop
{
    public static class GeneratorDiagnostics
    {
        private class Ids
        {
            public const string Prefix = "DLLIMPORTGEN";
            public const string TypeNotSupported = Prefix + "001";
            public const string ConfigurationNotSupported = Prefix + "002";
        }

        private const string Category = "DllImportGenerator";

        public readonly static DiagnosticDescriptor ParameterTypeNotSupported =
            new DiagnosticDescriptor(
                Ids.TypeNotSupported,
                GetResourceString(nameof(Resources.TypeNotSupportedTitle)),
                GetResourceString(nameof(Resources.TypeNotSupportedMessageParameter)),
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: GetResourceString(Resources.TypeNotSupportedDescription));

        public readonly static DiagnosticDescriptor ReturnTypeNotSupported =
            new DiagnosticDescriptor(
                Ids.TypeNotSupported,
                GetResourceString(nameof(Resources.TypeNotSupportedTitle)),
                GetResourceString(nameof(Resources.TypeNotSupportedMessageReturn)),
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: GetResourceString(Resources.TypeNotSupportedDescription));

        public readonly static DiagnosticDescriptor ConfigurationNotSupported =
            new DiagnosticDescriptor(
                Ids.ConfigurationNotSupported,
                GetResourceString(nameof(Resources.ConfigurationNotSupportedTitle)),
                GetResourceString(nameof(Resources.ConfigurationNotSupportedMessage)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: GetResourceString(Resources.ConfigurationNotSupportedDescription));

        public static Diagnostic CreateDiagnostic(
            this ISymbol symbol,
            DiagnosticDescriptor descriptor,
            params object[] args)
        {
            IEnumerable<Location> locationsInSource = symbol.Locations.Where(l => l.IsInSource);
            if (!locationsInSource.Any())
                return Diagnostic.Create(descriptor, Location.None, args);

            return Diagnostic.Create(
                descriptor,
                location: locationsInSource.First(),
                additionalLocations: locationsInSource.Skip(1),
                messageArgs: args);
        }

        private static LocalizableResourceString GetResourceString(string resourceName)
        {
            return new LocalizableResourceString(resourceName, Resources.ResourceManager, typeof(Resources));
        }
    }
}
