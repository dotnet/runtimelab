// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Swift.ProtocolsTests;
using Xunit;

namespace BindingsGeneration.FunctionalTests
{
    public class ProtocolsTests : IClassFixture<ProtocolsTests.TestFixture>
    {
        private readonly TestFixture _fixture;

        public ProtocolsTests(TestFixture fixture)
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
                // Initialize
            }
        }

        [Fact]
        public void ProtocolIsProjected()
        {
            // This test is to ensure that the protocol is projected, it just needs to compile
            Assert.True(typeof(ISwiftPrintable).IsInterface);
        }
    }
}
