// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class UnsafePointerTests
    {
        [Fact]
        public static void TestUnsafePointerCryptoKit()
        {
            BindingsGenerator.GenerateBindings("UnsafePointer/UnsafePointerTests.abi.json", "UnsafePointer/");

            int result = (int)TestsHelper.CompileAndExecute(
                new string [] { "UnsafePointer/*.cs" }, 
                new string [] { },
                new string [] { "System.Security.Cryptography" },
                "Test.MainClass", "Main", new object [] { new string[0] });
            Assert.Equal(1, result);
        }
    }
}
