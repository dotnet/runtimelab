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
            BindingsTool.GenerateBindings("libSmokeTests.dylib", "SmokeTests.swiftinterface", "");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using SmokeTests;

                namespace Hello {
                    class MainClass {
                        public static int Main(string[] args)
                        {
                            TopLevelEntities.SimplePinvokeVoidVoid();
                            return 42;
                        }
                    }
                }
                """;

            int result = TestsHelper.CompileAndExecuteFromFileAndString("TopLevelEntitiesSmokeTests.cs", sourceCode);
            Assert.Equal(42, result);
        }
    }
}
