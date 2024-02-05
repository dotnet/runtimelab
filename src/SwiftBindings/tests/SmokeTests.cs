using System;
using Xunit;

namespace SwiftBindings.Tests
{
    public class MyClassTests
    {
        [Fact]
        public void Test1()
        {
            Assert.True(MyClass.ReturnTrue);
        }
    }
}
