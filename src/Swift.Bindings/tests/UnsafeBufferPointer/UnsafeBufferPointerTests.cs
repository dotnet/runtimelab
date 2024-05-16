// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class UnsafeBufferPointerTests : IClassFixture<UnsafeBufferPointerTests.TestFixture>
    {
        private readonly TestFixture _fixture;
        private static string _assemblyPath;

        public UnsafeBufferPointerTests(TestFixture fixture)
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
                BindingsGenerator.GenerateBindings("UnsafeBufferPointer/UnsafeBufferPointerTests.abi.json", "UnsafeBufferPointer/");
                _assemblyPath = TestsHelper.Compile(
                new string [] { "UnsafeBufferPointer/*.cs" }, 
                new string [] { },
                new string [] { "System.Security.Cryptography" });
            }
        }

        [Fact]
        public static void TestUnsafeBufferPointerCryptoKit()
        {
            int result = (int)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "Main", new object [] { new string[0] });
            Assert.Equal(1, result);
        }
    }
}
