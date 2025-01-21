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
        Assert.Empty(decl.Constraints);
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
        Assert.Empty(first.Constraints);

        var second = result[1];
        Assert.Equal("τ_0_1", second.TypeName);
        Assert.Equal("U", second.SugaredTypeName);
        Assert.Empty(second.Constraints);
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
        Assert.Single(decl.Constraints);
        var conformance = Assert.IsType<ProtocolConformance>(decl.Constraints[0]);
        Assert.Equal("τ_0_0", conformance.TargetType);
        Assert.Equal("Swift.Equatable", conformance.ProtocolName);
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
        Assert.Single(first.Constraints);
        var firstConformance = Assert.IsType<ProtocolConformance>(first.Constraints[0]);
        Assert.Equal("τ_0_0", firstConformance.TargetType);
        Assert.Equal("Swift.Equatable", firstConformance.ProtocolName);

        var second = result[1];
        Assert.Equal("τ_0_1", second.TypeName);
        Assert.Equal("U", second.SugaredTypeName);
        Assert.Single(second.Constraints);
        var secondConformance = Assert.IsType<ProtocolConformance>(second.Constraints[0]);
        Assert.Equal("τ_0_1", secondConformance.TargetType);
        Assert.Equal("Swift.Hashable", secondConformance.ProtocolName);
    }

    [Fact]
    public void ParseGenericSignature_ParsesAssociatedTypeConstraints()
    {
        var genericSig = "<τ_0_0 where τ_0_0 : SomeProtocol, τ_0_0.ID == System.Guid>";
        var sugaredSig = "<T where T : SomeProtocol, T.ID == System.Guid>";

        var result = GenericSignatureParser.ParseGenericSignature(genericSig, sugaredSig);

        Assert.Single(result);
        var decl = result[0];
        Assert.Equal("τ_0_0", decl.TypeName);
        Assert.Equal("T", decl.SugaredTypeName);
        Assert.Equal(2, decl.Constraints.Count);

        var proto = Assert.IsType<ProtocolConformance>(decl.Constraints[0]);
        Assert.Equal("τ_0_0", proto.TargetType);
        Assert.Equal("SomeProtocol", proto.ProtocolName);

        var assoc = Assert.IsType<AssociatedTypeConformance>(decl.Constraints[1]);
        Assert.Equal("τ_0_0", assoc.TargetType);
        Assert.Equal("System.Guid", assoc.ProtocolName);
        Assert.Equal("ID", assoc.AssociatedTypeName);
    }
}
