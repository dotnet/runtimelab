// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class UnsafeRawPointerTests
    {
        [Fact]
        public static void TestUnsafeRawPointerCryptoKit()
        {
            BindingsGenerator.GenerateBindings("UnsafeRawPointerTests.abi.json", "");
    
            string filePath = "UnsafeRawPointerTests.cs.template";
            string sourceCode = File.ReadAllText(filePath);

            int result = (int)TestsHelper.CompileAndExecute("UnsafeRawPointerTestsBindings.cs", sourceCode, "Test.MainClass", "Main", new object [] { new string[0] });
            Assert.Equal(1, result);
        }
    }
}
