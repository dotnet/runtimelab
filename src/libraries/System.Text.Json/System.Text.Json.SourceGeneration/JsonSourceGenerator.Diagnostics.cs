// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;

namespace System.Text.Json.SourceGeneration
{
    internal sealed partial class JsonSourceGeneratorHelper
    {
        // Diagnostic descriptors for user.
        private DiagnosticDescriptor _generatedTypeClass;
        private DiagnosticDescriptor _typeNameClash;
        private DiagnosticDescriptor _failedToGenerateTypeClass;

        private void InitializeDiagnosticDescriptors()
        {
            _generatedTypeClass =
                new DiagnosticDescriptor(
                    "JsonSourceGeneration",
                    "Generated type serialization metadata",
                    "Generated serialization metadata for type {0}",
                    "category",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true);

            _failedToGenerateTypeClass =
                new DiagnosticDescriptor(
                    "JsonSourceGeneration",
                    "Did not generate serialization metadata for type",
                    "Did not generate serialization metadata for type {0}",
                    "category",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true,
                    description: "Error message: {2}");

            _typeNameClash =
                new DiagnosticDescriptor(
                    "JsonSourceGeneration",
                    "Two types with the same name",
                    "Duplicate type name detected. Setting the JsonTypeInfo<T> property for type {0} in assembly {1} to {2}. To use please call JsonContext.Instance.{2}",
                    "category",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true);
        }
    }
}
