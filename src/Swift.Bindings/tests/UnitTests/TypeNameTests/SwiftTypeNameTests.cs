// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class SwiftTypeNameTests
    {
        [Fact]
        public void FromModuleQualifiedName_ValidName_SetsPropertiesCorrectly()
        {
            var typeName = SwiftTypeName.FromModuleQualifiedName("Swift.String");
            Assert.Equal("Swift", typeName.Module);
            Assert.Equal("String", typeName.Name);
            Assert.Equal("Swift.String", typeName.ModuleQualifiedName);
        }

        [Fact]
        public void FromModuleQualifiedName_NestedTypeName_SetsPropertiesCorrectly()
        {
            var typeName = SwiftTypeName.FromModuleQualifiedName("Foundation.NSString.SubType");
            Assert.Equal("Foundation", typeName.Module);
            Assert.Equal("SubType", typeName.Name);
            Assert.Equal("Foundation.NSString.SubType", typeName.ModuleQualifiedName);
        }

        [Fact]
        public void FromModuleQualifiedName_GenericTypeName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => SwiftTypeName.FromModuleQualifiedName("Swift.Array<Swift.String>"));
        }

        [Fact]
        public void FromModuleQualifiedName_NullName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SwiftTypeName.FromModuleQualifiedName(null));
        }

        [Fact]
        public void FromModuleQualifiedName_EmptyName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => SwiftTypeName.FromModuleQualifiedName(""));
        }

        [Theory]
        [InlineData("String")]
        [InlineData(".String")]
        [InlineData("Swift.")]
        public void FromModuleQualifiedName_InvalidFormat_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => SwiftTypeName.FromModuleQualifiedName(name));
        }

        [Fact]
        public void VoidType_HasCorrectProperties()
        {
            Assert.Equal("()", SwiftTypeName.VoidType.Name);
            Assert.Equal("", SwiftTypeName.VoidType.Module);
            Assert.Equal("()", SwiftTypeName.VoidType.ModuleQualifiedName);
        }

        [Fact]
        public void AnyType_HasCorrectProperties()
        {
            Assert.Equal("Any", SwiftTypeName.AnyType.Name);
            Assert.Equal("", SwiftTypeName.AnyType.Module);
            Assert.Equal("Any", SwiftTypeName.AnyType.ModuleQualifiedName);
        }
    }
}
