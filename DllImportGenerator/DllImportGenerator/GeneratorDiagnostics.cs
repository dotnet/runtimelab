using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.CodeAnalysis;

#nullable enable

namespace Microsoft.Interop
{
    internal static class DiagnosticExtensions
    {
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

        public static Diagnostic CreateDiagnostic(
            this AttributeData attributeData,
            DiagnosticDescriptor descriptor,
            params object[] args)
        {
            SyntaxReference? syntaxReference = attributeData.ApplicationSyntaxReference;
            Location location = syntaxReference is not null
                ? syntaxReference.GetSyntax().GetLocation()
                : Location.None;

            return Diagnostic.Create(
                descriptor,
                location: location.IsInSource ? location : Location.None,
                messageArgs: args);
        }
    }

    public class GeneratorDiagnostics
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

        public readonly static DiagnosticDescriptor ParameterConfigurationNotSupported =
            new DiagnosticDescriptor(
                Ids.ConfigurationNotSupported,
                GetResourceString(nameof(Resources.ConfigurationNotSupportedTitle)),
                GetResourceString(nameof(Resources.ConfigurationNotSupportedMessageParameter)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: GetResourceString(Resources.ConfigurationNotSupportedDescription));

        public readonly static DiagnosticDescriptor ReturnConfigurationNotSupported =
            new DiagnosticDescriptor(
                Ids.ConfigurationNotSupported,
                GetResourceString(nameof(Resources.ConfigurationNotSupportedTitle)),
                GetResourceString(nameof(Resources.ConfigurationNotSupportedMessageReturn)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: GetResourceString(Resources.ConfigurationNotSupportedDescription));

        public readonly static DiagnosticDescriptor ConfigurationNotSupported =
            new DiagnosticDescriptor(
                Ids.ConfigurationNotSupported,
                GetResourceString(nameof(Resources.ConfigurationNotSupportedTitle)),
                GetResourceString(nameof(Resources.ConfigurationNotSupportedMessage)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: GetResourceString(Resources.ConfigurationNotSupportedDescription));

        public readonly static DiagnosticDescriptor ConfigurationValueNotSupported =
            new DiagnosticDescriptor(
                Ids.ConfigurationNotSupported,
                GetResourceString(nameof(Resources.ConfigurationNotSupportedTitle)),
                GetResourceString(nameof(Resources.ConfigurationNotSupportedMessageValue)),
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: GetResourceString(Resources.ConfigurationNotSupportedDescription));

        private readonly GeneratorExecutionContext context;

        public GeneratorDiagnostics(GeneratorExecutionContext context)
        {
            this.context = context;
        }

        public void ReportConfigurationNotSupported(
            AttributeData attributeData,
            string configurationName,
            string? unsupportedValue = null)
        {
            if (unsupportedValue == null)
            {
                this.context.ReportDiagnostic(
                    attributeData.CreateDiagnostic(
                        GeneratorDiagnostics.ConfigurationNotSupported,
                        configurationName));
            }
            else
            {
                this.context.ReportDiagnostic(
                    attributeData.CreateDiagnostic(
                        GeneratorDiagnostics.ConfigurationValueNotSupported,
                        unsupportedValue,
                        configurationName));
            }
        }

        internal void ReportMarshallingNotSupported(
            IMethodSymbol method,
            TypePositionInfo info)
        {
            if (info.MarshallingAttributeInfo != null && info.MarshallingAttributeInfo is MarshalAsInfo)
            {
                // Report that the specified marshalling configuration is not supported.
                // We don't forward marshalling attributes, so this is reported differently
                // than when there is no attribute and the type itself is not supported.
                if (info.IsManagedReturnPosition)
                {
                    this.context.ReportDiagnostic(
                        method.CreateDiagnostic(
                            GeneratorDiagnostics.ReturnConfigurationNotSupported,
                            nameof(System.Runtime.InteropServices.MarshalAsAttribute),
                            method.Name));
                }
                else
                {
                    Debug.Assert(info.ManagedIndex <= method.Parameters.Length);
                    IParameterSymbol paramSymbol = method.Parameters[info.ManagedIndex];
                    this.context.ReportDiagnostic(
                        paramSymbol.CreateDiagnostic(
                            GeneratorDiagnostics.ParameterConfigurationNotSupported,
                            nameof(System.Runtime.InteropServices.MarshalAsAttribute),
                            paramSymbol.Name));
                }
            }
            else
            {
                // Report that the type is not supported
                if (info.IsManagedReturnPosition)
                {
                    this.context.ReportDiagnostic(
                        method.CreateDiagnostic(
                            GeneratorDiagnostics.ReturnTypeNotSupported,
                            method.ReturnType.ToDisplayString(),
                            method.Name));
                }
                else
                {
                    Debug.Assert(info.ManagedIndex <= method.Parameters.Length);
                    IParameterSymbol paramSymbol = method.Parameters[info.ManagedIndex];
                    this.context.ReportDiagnostic(
                        paramSymbol.CreateDiagnostic(
                            GeneratorDiagnostics.ParameterTypeNotSupported,
                            paramSymbol.Type.ToDisplayString(),
                            paramSymbol.Name));
                }
            }
        }

        private static LocalizableResourceString GetResourceString(string resourceName)
        {
            return new LocalizableResourceString(resourceName, Resources.ResourceManager, typeof(Resources));
        }
    }
}
