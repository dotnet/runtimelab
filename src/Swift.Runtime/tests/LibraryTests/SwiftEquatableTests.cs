using BindingsGeneration.Tests;
using Swift.Runtime;
using Xunit;

namespace LibraryTests
{
    public class SwiftEquatableTests
    {
        [Fact]
        public void SwiftIntMock_Equals_ShouldWork()
        {
            var intMock1 = new SwiftIntMock(42);
            var intMock2 = new SwiftIntMock(42);
            var intMock3 = new SwiftIntMock(100);

            bool equalsResult1 = SwiftEquatable.Equals(intMock1, intMock2);
            bool equalsResult2 = SwiftEquatable.Equals(intMock1, intMock3);


            Assert.True(equalsResult1, "Two SwiftIntMock instances with the same value should be equal");
            Assert.False(equalsResult2, "Two SwiftIntMock instances with different values should not be equal");
        }
    }
}
