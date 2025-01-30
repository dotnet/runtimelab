// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Swift.GenericTests;
using Swift.Runtime;
using Xunit;

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

        [Fact]
        public void TestFunctionTakesMultipleGenericParametersOfSameType()
        {
            var a = new FrozenStruct(1, 2);
            var b = new FrozenStruct(3, 4);
            var result = GenericTests.AcceptsTwoValuesOfTheSameGenericType(a, b);
            Assert.Equal(a, result);
        }

        [Fact]
        public void TestFunctionTakesGenericParameterConstrainedToProtocol()
        {
            var a = new SummableStruct(2, 40);
            var result = GenericTests.AcceptsSummable(a);
            Assert.Equal(42, result);
        }

        [Fact]
        public void TestFunctionTakesMultipleGenericParametersOfSameTypeConstrainedToProtocol()
        {
            var a = new SummableStruct(2, 40);
            var b = new SummableStruct(3, 39);
            var result = GenericTests.AcceptsMultipleGenericParamsOfTheSameTypeConstrainedByProtocol(a, b);
            Assert.Equal(42 + 42, result);
        }

        [Fact]
        public void TestFunctionTakesGenericParameterConstrainedToMultipleProtocols()
        {
            var a = new StructWithMultipleProtocols(43, 177);
            var result = GenericTests.AcceptsMultipleProtocols(a);
            Assert.Equal(43 + 177 + 43 - 177 + 43 * 177, result);
        }

        [Fact]
        public void TestFunctionTakesMultipleGenericParametersConstrainedToMultipleProtocols()
        {
            var a = new StructWithMultipleProtocols(43, 177);
            var b = new StructWithMultipleProtocols(531, 133);
            var result = GenericTests.AcceptsMultipleGenericParamsWithProtocols(a, b);
            Assert.Equal((43 + 177) + (43 * 177) + (531 - 133) + (531 / 133), result);
        }

        [Fact]
        public void TestFunctionTakesMultipleGenericParametersOfDifferentTypesConstrainedByTheSameProtocol()
        {
            var a = new SummableStruct(2, 40);
            var b = new AnotherSummableStruct(3, 39);
            var result = GenericTests.AcceptsMultipleGenericParamsOfDifferentTypesConstrainedByTheSameProtocol(a, b);
            Assert.Equal(42 + 42, result);
        }

        [Fact]
        public void TestFunctionWithGenericParamConstrainedToPAT()
        {
            var a = new IntContainer1(42);
            var result = GenericTests.AcceptsIntContainer(a);
            Assert.Equal(42 * 2, result);

            var b = new IntContainer2(42);
            var result2 = GenericTests.AcceptsIntContainer(b);
            Assert.Equal(42 * 4, result2);
        }
    }
}
