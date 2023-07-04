﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Interop
{
    /// <summary>
    /// A provider for diagnostic descriptors for common error scenarios and for generator resolution.
    /// </summary>
    public interface IDiagnosticDescriptorProvider
    {
        /// <summary>
        /// The diagnostic descriptor to use when the provided marshalling attribute is invalid.
        /// </summary>
        DiagnosticDescriptor InvalidMarshallingAttributeInfo { get; }
        /// <summary>
        /// The diagnostic descriptor to use when the provided configuration is not supported.
        /// </summary>
        DiagnosticDescriptor ConfigurationNotSupported { get; }
        /// <summary>
        /// The diagnostic descriptor to use when the provided value for a given configuration isn't supported.
        /// </summary>
        DiagnosticDescriptor ConfigurationValueNotSupported { get; }
        /// <summary>
        /// Gets the diagnostic to use for the given generator diagnostic, or <c>null</c> if no diagnostic should be reported to the user.
        /// </summary>
        /// <param name="diagnostic">The diagnostic generated by the generator factory.</param>
        /// <returns>The descriptor to use to create the diagnostic, if any</returns>
        /// <remarks>
        /// A descriptor must be returned if the diagnostic is fatal.
        /// </remarks>
        DiagnosticDescriptor? GetDescriptor(GeneratorDiagnostic diagnostic);
    }

    public interface ISignatureDiagnosticLocations
    {
        DiagnosticInfo CreateDiagnosticInfo(DiagnosticDescriptor descriptor, GeneratorDiagnostic diagnostic);
    }

    public sealed record MethodSignatureDiagnosticLocations(string MethodIdentifier, ImmutableArray<Location> ManagedParameterLocations, Location FallbackLocation) : ISignatureDiagnosticLocations
    {
        public MethodSignatureDiagnosticLocations(MethodDeclarationSyntax syntax)
            : this(syntax.Identifier.Text, syntax.ParameterList.Parameters.Select(p => p.Identifier.GetLocation()).ToImmutableArray(), syntax.Identifier.GetLocation())
        {
        }

        public bool Equals(MethodSignatureDiagnosticLocations other)
        {
            return MethodIdentifier == other.MethodIdentifier
                && ManagedParameterLocations.SequenceEqual(other.ManagedParameterLocations)
                && FallbackLocation.Equals(other.FallbackLocation);
        }

        public override int GetHashCode() => throw new UnreachableException();

        public DiagnosticInfo CreateDiagnosticInfo(DiagnosticDescriptor descriptor, GeneratorDiagnostic diagnostic)
        {
            var (location, elementName) = diagnostic.TypePositionInfo switch
            {
                { ManagedIndex: >= 0 and int index, InstanceIdentifier: string identifier } => (ManagedParameterLocations[index], identifier),
                _ => (FallbackLocation, MethodIdentifier),
            };
            return diagnostic.ToDiagnosticInfo(descriptor, location, elementName);
        }
    }
}
