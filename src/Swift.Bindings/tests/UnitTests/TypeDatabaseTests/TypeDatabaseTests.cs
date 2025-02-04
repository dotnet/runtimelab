// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class TypeDatabaseTests
    {
        [Fact]
        public void AddModuleDatabase_ModuleExists_Throws()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("MyModule", "/fake/path");
            typeDatabase.AddModuleDatabase(module);

            var ex = Assert.Throws<Exception>(() => typeDatabase.AddModuleDatabase(module));
            Assert.Contains("already exists in the database", ex.Message);
        }

        [Fact]
        public void IsModuleProcessed_ReturnsTrue_WhenModuleExists()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("MyModule", "/fake/path");
            typeDatabase.AddModuleDatabase(module);

            var result = typeDatabase.IsModuleProcessed("MyModule");

            Assert.True(result);
        }

        [Fact]
        public void IsModuleProcessed_ReturnsFalse_WhenModuleDoesNotExist()
        {
            var typeDatabase = new TypeDatabase();

            var result = typeDatabase.IsModuleProcessed("NonExistentModule");

            Assert.False(result);
        }

        [Fact]
        public void TryGetTypeRecord_ReturnsTrue_WhenTypeExists()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            var swiftTypeName = SwiftTypeName.FromModuleQualifiedName("TestModule.MyType");
            var myType = new TypeRecord
            {
                CSTypeIdentifier = "MyType",
                SwiftTypeName = swiftTypeName,
                MetadataAccessor = "mangledAccessor",
                Namespace = "BindingsGeneration.Tests",
                IsBlittable = false,
                IsFrozen = false
            };
            module.RegisterType(swiftTypeName, myType);
            typeDatabase.AddModuleDatabase(module);

            var found = typeDatabase.TryGetTypeRecord(swiftTypeName, out var record);

            Assert.True(found);
            Assert.NotNull(record);
            Assert.Equal("MyType", record!.CSTypeIdentifier);
        }

        [Fact]
        public void TryGetTypeRecord_ReturnsFalse_WhenTypeDoesNotExist()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            var swiftTypeName = SwiftTypeName.FromModuleQualifiedName("TestModule.MyType");
            typeDatabase.AddModuleDatabase(module);

            var found = typeDatabase.TryGetTypeRecord(swiftTypeName, out var record);

            Assert.False(found);
            Assert.Null(record);
        }

        [Fact]
        public void AddOutOfModuleTypes_TryGetTypeRecord_ReturnsTrue_WhenOutOfModuleTypeExists()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            var swiftTypeName = SwiftTypeName.FromModuleQualifiedName("AnotherModule.MyOutOfModuleType");
            typeDatabase.AddModuleDatabase(module);

            var outOfModuleRecord = new TypeRecord
            {
                CSTypeIdentifier = "MyOutOfModuleType",
                SwiftTypeName = swiftTypeName,
                MetadataAccessor = "mangledOutOfModule",
                Namespace = "BindingsGeneration.Tests",
                IsBlittable = false,
                IsFrozen = false
            };

            typeDatabase.AddOutOfModuleTypes(new[]
            {
                (swiftTypeName, outOfModuleRecord)
            });

            var found = typeDatabase.TryGetTypeRecord(swiftTypeName, out var record);

            Assert.True(found);
            Assert.NotNull(record);
            Assert.Equal("MyOutOfModuleType", record!.CSTypeIdentifier);
            Assert.Equal("AnotherModule", record.SwiftTypeName.Module);
        }

        [Fact]
        public void IsTypeProcessed_ReturnsTrue_WhenTypeProcessed()
        {
            // Arrange
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            var swiftTypeName = SwiftTypeName.FromModuleQualifiedName("TestModule.ProcessedType");
            module.RegisterType(swiftTypeName, new TypeRecord
            {
                CSTypeIdentifier = "ProcessedType",
                SwiftTypeName = swiftTypeName,
                MetadataAccessor = string.Empty,
                Namespace = string.Empty,
                IsBlittable = false,
                IsFrozen = false
            });
            typeDatabase.AddModuleDatabase(module);

            var result = typeDatabase.IsTypeProcessed(swiftTypeName);

            Assert.True(result);
        }

        [Fact]
        public void IsTypeProcessed_ReturnsFalse_WhenTypeNotProcessed()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            var swiftTypeName = SwiftTypeName.FromModuleQualifiedName("TestModule.UnprocessedType");

            typeDatabase.AddModuleDatabase(module);

            var result = typeDatabase.IsTypeProcessed(swiftTypeName);

            Assert.False(result);
        }

        [Fact]
        public void GetLibraryPath_ReturnsCorrectPath_WhenModuleExists()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            typeDatabase.AddModuleDatabase(module);

            var path = typeDatabase.GetLibraryPath("TestModule");

            Assert.Equal("/fake/path", path);
        }

        [Fact]
        public void GetLibraryPath_Throws_WhenModuleDoesNotExist()
        {
            var typeDatabase = new TypeDatabase();

            var ex = Assert.Throws<Exception>(() => typeDatabase.GetLibraryPath("NonExistentModule"));
            Assert.Contains("Module NonExistentModule does not exist in the database.", ex.Message);
        }
    }
}
