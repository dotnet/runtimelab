// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;

namespace SwiftBindings.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void DirectPInvokeVoidVoid()
        {
            BindingsTool.GenerateBindings("SmokeTests.abi.json", "");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using SmokeTestsBindings;

                namespace Hello {
                    class MainClass {
                        public static int Main(string[] args)
                        {
                            SmokeTests.SimplePinvokeVoidVoid();
                            return 42;
                        }
                    }
                }
                """;

            int result = TestsHelper.CompileAndExecuteFromFileAndString("SmokeTestsBindings.cs", sourceCode);
            Assert.Equal(42, result);
        }
    }
}
