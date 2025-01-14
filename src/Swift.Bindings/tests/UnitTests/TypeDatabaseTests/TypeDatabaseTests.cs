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
            var myType = new TypeRecord
            {
                CSTypeIdentifier = "MyType",
                SwiftTypeIdentifier = "MyType",
                MetadataAccessor = "mangledAccessor",
                Namespace = "BindingsGeneration.Tests",
                ModuleName = "TestModule",
                IsBlittable = false,
                IsFrozen = false
            };
            module.RegisterType("MyType", myType);
            typeDatabase.AddModuleDatabase(module);

            var found = typeDatabase.TryGetTypeRecord("TestModule", "MyType", out var record);

            Assert.True(found);
            Assert.NotNull(record);
            Assert.Equal("MyType", record!.CSTypeIdentifier);
        }

        [Fact]
        public void TryGetTypeRecord_ReturnsFalse_WhenTypeDoesNotExist()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            typeDatabase.AddModuleDatabase(module);

            var found = typeDatabase.TryGetTypeRecord("TestModule", "NonExistentType", out var record);

            Assert.False(found);
            Assert.Null(record);
        }

        [Fact]
        public void AddOutOfModuleTypes_TryGetTypeRecord_ReturnsTrue_WhenOutOfModuleTypeExists()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            typeDatabase.AddModuleDatabase(module);

            var outOfModuleIdentifier = "TestModule.MyOutOfModuleType";
            var outOfModuleRecord = new TypeRecord
            {
                CSTypeIdentifier = "MyOutOfModuleType",
                SwiftTypeIdentifier = "MyOutOfModuleType",
                MetadataAccessor = "mangledOutOfModule",
                Namespace = "BindingsGeneration.Tests",
                ModuleName = "AnotherModule",
                IsBlittable = false,
                IsFrozen = false
            };

            typeDatabase.AddOutOfModuleTypes(new[]
            {
                (outOfModuleIdentifier, outOfModuleRecord)
            });

            var found = typeDatabase.TryGetTypeRecord("TestModule", "MyOutOfModuleType", out var record);

            Assert.True(found);
            Assert.NotNull(record);
            Assert.Equal("MyOutOfModuleType", record!.CSTypeIdentifier);
            Assert.Equal("AnotherModule", record.ModuleName);
        }

        [Fact]
        public void IsTypeProcessed_ReturnsTrue_WhenTypeProcessed()
        {
            // Arrange
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            module.RegisterType("ProcessedType", new TypeRecord
            {
                CSTypeIdentifier = "ProcessedType",
                SwiftTypeIdentifier = "ProcessedType",
                MetadataAccessor = string.Empty,
                Namespace = string.Empty,
                ModuleName = "TestModule",
                IsBlittable = false,
                IsFrozen = false
            });
            typeDatabase.AddModuleDatabase(module);

            var result = typeDatabase.IsTypeProcessed("TestModule", "ProcessedType");

            Assert.True(result);
        }

        [Fact]
        public void IsTypeProcessed_ReturnsFalse_WhenTypeNotProcessed()
        {
            var typeDatabase = new TypeDatabase();
            var module = new ModuleTypeDatabase("TestModule", "/fake/path");
            typeDatabase.AddModuleDatabase(module);

            var result = typeDatabase.IsTypeProcessed("TestModule", "UnprocessedType");

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
