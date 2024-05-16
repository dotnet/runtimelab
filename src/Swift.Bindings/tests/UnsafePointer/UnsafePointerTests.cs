// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class UnsafePointerTests: IClassFixture<UnsafePointerTests.TestFixture>
    {
        private readonly TestFixture _fixture;
        private static string _assemblyPath;

        public UnsafePointerTests(TestFixture fixture)
        {
            _fixture = fixture;
        }

        public class TestFixture
        {
            static TestFixture()
            {
                InitializeResources();
            }

            private static void InitializeResources()
            {
                BindingsGenerator.GenerateBindings("UnsafePointer/UnsafePointerTests.abi.json", "UnsafePointer/");
                _assemblyPath = TestsHelper.Compile(
                new string [] { "UnsafePointer/*.cs" }, 
                new string [] { },
                new string [] { "System.Security.Cryptography" });
            }
        }

        [Fact]
        public static void TestUnsafePointerCryptoKit()
        {
            int result = (int)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "Main", new object [] { new string[0] });
            Assert.Equal(1, result);
        }
    }
}
