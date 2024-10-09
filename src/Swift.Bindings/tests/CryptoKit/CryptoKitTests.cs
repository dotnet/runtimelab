// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class CryptoKitTests: IClassFixture<CryptoKitTests.TestFixture>
    {
        private readonly TestFixture _fixture;
        private static string _assemblyPath;

        public CryptoKitTests(TestFixture fixture)
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
                _assemblyPath = TestsHelper.Compile(
                new string [] { "CryptoKit/*.cs" }, 
                new string [] { },
                new string [] { "System.Security.Cryptography" });
            }
        }

        [Fact]
        public static void TestUnsafePointerCryptoKit()
        {
            int result = (int)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "Main", new object [] { new string[0] });
            Assert.Equal(0, result);
        }
    }
}
