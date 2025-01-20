// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Swift.GenericTests;
using Swift.Runtime;

namespace BindingsGeneration.FunctionalTests
{
    public class GenericsTests : IClassFixture<GenericsTests.TestFixture>
    {
        private readonly TestFixture _fixture;

        public GenericsTests(TestFixture fixture)
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
        public void TestFunctionTakesPrimitiveGenericParamsThrows()
        {
            nint a = 1;
            double b = 2.3;
            Assert.Throws<SwiftRuntimeException>(() => GenericTests.AcceptsGenericParametersAndThrows(a, b));
        }

        [Fact]
        public void TestFunctionTakesPrimitiveGenericParams()
        {
            nint a = 1;
            double b = 2.3;
            var result = GenericTests.AcceptsGenericParameters(a, b);
            Assert.Equal(0, result);
        }

        [Fact]
        public void TestFunctionTakesStructGenericParams()
        {
            var a = new FrozenStruct(1, 2);
            var b = new NonFrozenStruct(3, 4);
            var result = GenericTests.AcceptsGenericParameters(a, b);
            Assert.Equal(0, result);
        }

        [Fact]
        public void TestFunctionTakesGenericPrimitiveAndReturnsOne()
        {
            nint a = 1;
            var result = GenericTests.AcceptsGenericParameterAndReturnsGeneric(a);
            Assert.Equal(a, result);

            double b = 2.3;
            var result2 = GenericTests.AcceptsGenericParameterAndReturnsGeneric(b);
            Assert.Equal(b, result2);

            float c = 3.4f;
            var result3 = GenericTests.AcceptsGenericParameterAndReturnsGeneric(c);
            Assert.Equal(c, result3);
        }

        [Fact]
        public void TestFunctionTakesGenericStructAndReturnsOne()
        {
            var a = new FrozenStruct(1, 2);
            var result = GenericTests.AcceptsGenericParameterAndReturnsGeneric(a);
            Assert.Equal(a, result);

            var b = new NonFrozenStruct(3, 4);
            var result2 = GenericTests.AcceptsGenericParameterAndReturnsGeneric(b);
            // No deep comparison for non-frozen structs yet
        }
    }
}
