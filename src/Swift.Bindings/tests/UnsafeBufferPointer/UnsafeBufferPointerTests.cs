// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class UnsafeBufferPointerTests
    {
        [Fact]
        public static void TestUnsafeBufferPointerCryptoKit()
        {
            BindingsGenerator.GenerateBindings("UnsafeBufferPointer/UnsafeBufferPointerTests.abi.json", "UnsafeBufferPointer/");

            int result = (int)TestsHelper.CompileAndExecute(
                new string [] { "UnsafeBufferPointer/*.cs" }, 
                new string [] { },
                new string [] { "System.Security.Cryptography" },
                "Test.MainClass", "Main", new object [] { new string[0] });
            Assert.Equal(1, result);
        }
    }
}
