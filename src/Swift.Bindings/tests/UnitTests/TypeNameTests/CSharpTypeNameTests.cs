// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests;

public class CSharpTypeNameTests
{
    [Theory]
    [InlineData("System", "Int32", "System.Int32")]
    [InlineData("My.Namespace", "MyType", "My.Namespace.MyType")]
    public void FromNamespaceAndName_CreatesCorrectName(string @namespace, string name, string expectedFullName)
    {
        var typeName = CSharpTypeName.FromNamespaceAndName(@namespace, name);

        Assert.Equal(@namespace, typeName.Namespace);
        Assert.Equal(name, typeName.Name);
        Assert.Equal(expectedFullName, typeName.FullyQualifiedName);
        Assert.Equal(expectedFullName, typeName.ToString());
    }

    [Fact]
    public void VoidType_HasCorrectProperties()
    {
        Assert.Equal("", CSharpTypeName.VoidType.Namespace);
        Assert.Equal("", CSharpTypeName.VoidType.Name);
        Assert.Equal("void", CSharpTypeName.VoidType.FullyQualifiedName);
    }

    [Fact]
    public void AnyType_HasCorrectProperties()
    {
        Assert.Equal("Swift", CSharpTypeName.AnyType.Namespace);
        Assert.Equal("AnyType", CSharpTypeName.AnyType.Name);
        Assert.Equal("Swift.AnyType", CSharpTypeName.AnyType.FullyQualifiedName);
    }

    [Theory]
    [InlineData("Swift", "Array<Swift.String>")]
    [InlineData("Swift", "Dictionary<Swift.String, Swift.Int>")]
    public void ThrowsOnGenericTypes(string ns, string name)
    {
        Assert.Throws<ArgumentException>(() => CSharpTypeName.FromNamespaceAndName(ns, name));
    }

    [Theory]
    [InlineData(null, "Test")]
    [InlineData("Test", null)]
    public void ThrowsOnInvalidNullInput(string ns, string name)
    {
        Assert.Throws<ArgumentNullException>(() => CSharpTypeName.FromNamespaceAndName(ns, name));
    }

    [Theory]
    [InlineData("Test", "")]
    [InlineData("", "Test")]
    public void ThrowsOnInvalidEmptyInput(string ns, string name)
    {
        Assert.Throws<ArgumentException>(() => CSharpTypeName.FromNamespaceAndName(ns, name));
    }
}
