// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BindingsGeneration;
using Xunit;

#nullable enable

namespace BindingsGeneration.Tests;

public class GenericSignatureParserTests
{
    [Fact]
    public void ParseGenericSignature_ReturnsEmpty_WhenEitherSignatureIsNullOrEmpty()
    {
        string? genericSig = null;
        string? sugaredSig = null;

        var result = GenericSignatureParser.ParseGenericSignature(genericSig, sugaredSig);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseGenericSignature_ParsesSingleParamNoConstraints()
    {
        var genericSig = "<τ_0_0>";
        var sugaredSig = "<T>";

        var result = GenericSignatureParser.ParseGenericSignature(genericSig, sugaredSig);

        Assert.Single(result);
        var decl = result[0];
        Assert.Equal("τ_0_0", decl.TypeName);
        Assert.Equal("T", decl.SugaredTypeName);
        Assert.Empty(decl.GenericConformances);
    }

    [Fact]
    public void ParseGenericSignature_ParsesMultipleParamsNoConstraints()
    {
        var genericSig = "<τ_0_0, τ_0_1>";
        var sugaredSig = "<T, U>";

        var result = GenericSignatureParser.ParseGenericSignature(genericSig, sugaredSig);

        Assert.Equal(2, result.Count);

        var first = result[0];
        Assert.Equal("τ_0_0", first.TypeName);
        Assert.Equal("T", first.SugaredTypeName);
        Assert.Empty(first.GenericConformances);

        var second = result[1];
        Assert.Equal("τ_0_1", second.TypeName);
        Assert.Equal("U", second.SugaredTypeName);
        Assert.Empty(second.GenericConformances);
    }

    [Fact]
    public void ParseGenericSignature_ParsesSingleParamWithConstraints()
    {
        var genericSig = "<τ_0_0 where τ_0_0 : Swift.Equatable>";
        var sugaredSig = "<T where T : Swift.Equatable>";

        var result = GenericSignatureParser.ParseGenericSignature(genericSig, sugaredSig);

        Assert.Single(result);
        var decl = result[0];
        Assert.Equal("τ_0_0", decl.TypeName);
        Assert.Equal("T", decl.SugaredTypeName);
        Assert.Single(decl.GenericConformances);
        var conformance = Assert.IsType<GenericParameterConformance>(decl.GenericConformances[0]);
        Assert.Equal("τ_0_0", conformance.Path[0]);
        Assert.Equal("Swift.Equatable", conformance.ConformanceTarget.ModuleQualifiedName);
    }

    [Fact]
    public void ParseGenericSignature_ParsesMultipleParamsWithConstraints()
    {
        var genericSig = "<τ_0_0, τ_0_1 where τ_0_0 : Swift.Equatable, τ_0_1 : Swift.Hashable>";
        var sugaredSig = "<T, U where T : Swift.Equatable, U : Swift.Hashable>";

        var result = GenericSignatureParser.ParseGenericSignature(genericSig, sugaredSig);

        Assert.Equal(2, result.Count);

        var first = result[0];
        Assert.Equal("τ_0_0", first.TypeName);
        Assert.Equal("T", first.SugaredTypeName);
        Assert.Single(first.GenericConformances);
        var firstConformance = Assert.IsType<GenericParameterConformance>(first.GenericConformances[0]);
        Assert.Equal("τ_0_0", firstConformance.Path[0]);
        Assert.Equal("Swift.Equatable", firstConformance.ConformanceTarget.ModuleQualifiedName);

        var second = result[1];
        Assert.Equal("τ_0_1", second.TypeName);
        Assert.Equal("U", second.SugaredTypeName);
        Assert.Single(second.GenericConformances);
        var secondConformance = Assert.IsType<GenericParameterConformance>(second.GenericConformances[0]);
        Assert.Equal("τ_0_1", secondConformance.Path[0]);
        Assert.Equal("Swift.Hashable", secondConformance.ConformanceTarget.ModuleQualifiedName);
    }

    [Fact]
    public void ParseGenericSignature_ParsesAssociatedTypeConstraints()
    {
        var genericSig = "<τ_0_0 where τ_0_0 : SomeModule.SomeProtocol, τ_0_0.ID == System.Guid, τ_0_0.ID : SomeModule.SomeProtocol>";
        var sugaredSig = "<T where T : SomeModule.SomeProtocol, T.ID == System.Guid, T.ID : SomeModule.SomeProtocol>";

        var result = GenericSignatureParser.ParseGenericSignature(genericSig, sugaredSig);

        Assert.Single(result);
        var decl = result[0];
        Assert.Equal("τ_0_0", decl.TypeName);
        Assert.Equal("T", decl.SugaredTypeName);
        Assert.Single(decl.GenericConformances);
        Assert.Equal(2, decl.AssosiatedTypeConformances.Count);

        var proto = Assert.IsType<GenericParameterConformance>(decl.GenericConformances[0]);
        Assert.Equal("τ_0_0", proto.Path[0]);
        Assert.Equal("SomeModule.SomeProtocol", proto.ConformanceTarget.ModuleQualifiedName);
        Assert.Equal(ConformanceKind.Protocol, proto.Kind);

        proto = Assert.IsType<GenericParameterConformance>(decl.AssosiatedTypeConformances[0]);
        Assert.Equal("τ_0_0", proto.Path[0]);
        Assert.Equal("ID", proto.Path[1]);
        Assert.Equal("System.Guid", proto.ConformanceTarget.ModuleQualifiedName);
        Assert.Equal(ConformanceKind.ConcreteType, proto.Kind);

        proto = Assert.IsType<GenericParameterConformance>(decl.AssosiatedTypeConformances[1]);
        Assert.Equal("τ_0_0", proto.Path[0]);
        Assert.Equal("ID", proto.Path[1]);
        Assert.Equal("SomeModule.SomeProtocol", proto.ConformanceTarget.ModuleQualifiedName);
        Assert.Equal(ConformanceKind.Protocol, proto.Kind);
    }
}
