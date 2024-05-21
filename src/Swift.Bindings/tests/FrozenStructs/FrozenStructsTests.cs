// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class FrozenStructsTests : IClassFixture<FrozenStructsTests.TestFixture>
    {
        private readonly TestFixture _fixture;
        private static string _assemblyPath;

        public FrozenStructsTests(TestFixture fixture)
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
                BindingsGenerator.GenerateBindings("FrozenStructs/FrozenStructsTests.abi.json", "FrozenStructs/");
                _assemblyPath = TestsHelper.Compile(
                new string [] { "FrozenStructs/*.cs" }, 
                new string [] { },
                new string [] { });
            }
        }

        [Fact]
        public static void TestSwiftStruct0()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct0", new object[] { });
            Assert.Equal(-3555910212621330623, result[0]);
            Assert.Equal(3232700585171816769, result[1]);
            Assert.Equal(5405857218178297237, result[2]);
            Assert.Equal(-5199645484972017144, result[3]);
            Console.WriteLine("OK");
        }
    
        [Fact]
        public static void TestSwiftStruct1()
        {

            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct1", new object[] { });
            Assert.Equal(-8001373452266497130, result[0]);
            Assert.Equal(-5808561271200422464, result[1]);
            Assert.Equal(619381321311063739, result[2]);
            Assert.Equal(-5789188411070459345, result[3]);
            Console.WriteLine("OK");
        }
    
        [Fact]
        public static void TestSwiftStruct2()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct2", new object[] { });
            Assert.Equal(-2644299808654375646, result[0]);
            Assert.Equal(-5946857472632060272, result[1]);
            Assert.Equal(-2996592682669871081, result[2]);
            Assert.Equal(-5808559072177166042, result[3]);
            Assert.Equal(8741931523650439060, result[4]);
            Assert.Equal(5451771724251677586, result[5]);
            Assert.Equal(-1831688667491861211, result[6]);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftStruct3()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct3", new object[] { });
            Assert.Equal(2183680504548519170, result[0]);
            Assert.Equal(6464181192521079681, result[1]);
            Assert.Equal(-3914014010231380795, result[2]);
            Assert.Equal(5367593114012495226, result[3]);
            Assert.Equal(-5127890789322606739, result[4]);
            Assert.Equal(-1801544295733561908, result[5]);
            Assert.Equal(9153003238137716468, result[6]);
            Assert.Equal(-4315142132599941972, result[7]);
            Assert.Equal(-1490133437166812017, result[8]);
            Assert.Equal(-1699583181824442426, result[9]);
            Console.WriteLine("OK");
        }
    
        [Fact]
        public static void TestSwiftStruct4()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct4", new object[] { });
            Assert.Equal(-5555791715238606502, result[0]);
            Assert.Equal(7917312046649396258, result[1]);
            Assert.Equal(7787216292523950588, result[2]);
            Assert.Equal(-6752434813728457588, result[3]);
            Assert.Equal(-664221457051710106, result[4]);
            Assert.Equal(-1729670953176434449, result[5]);
            Assert.Equal(5366279618472372586, result[6]);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftStruct5()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct5", new object[] { });
            Assert.Equal(-8984750220696046997, result[0]);
            Assert.Equal(5832440388901373477, result[1]);
            Console.WriteLine("OK");
        }
    
        [Fact]
        public static void TestSwiftStruct6()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct6", new object[] { });
            Assert.Equal(1692306587549742161, result[0]);
            Assert.Equal(-1484226257450236447, result[1]);
            Assert.Equal(-6229230135174619697, result[2]);
            Assert.Equal(-3202371936141214966, result[3]);
            Assert.Equal(631817796766569309, result[4]);
            Assert.Equal(-5808600853619038060, result[5]);
            Assert.Equal(584944617122307319, result[6]);
            Assert.Equal(-8871753131984133391, result[7]);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftStruct7()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct7", new object[] { });
            Assert.Equal(1770792034096671794, result[0]);
            Assert.Equal(-5808605251665550904, result[1]);
            Assert.Equal(5963731324167739917, result[2]);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftStruct8()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct8", new object[] { });
            Assert.Equal(962290567653668427, result[0]);
            Assert.Equal(1919194302322813426, result[1]);
            Console.WriteLine("OK");
        }
    
        [Fact]
        public static void TestSwiftStruct9()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct9", new object[] { });
            Assert.Equal(-127835592947727486, result[0]);
            Assert.Equal(5462998930071304245, result[1]);
            Assert.Equal(-5878079645235476214, result[2]);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestSwiftStruct10()
        {
            long[] result = (long[])TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFrozenStruct10", new object[] { });
            Assert.Equal(-1058509415045616378, result[0]);
            Assert.Equal(6725059428863802130, result[1]);
            Assert.Equal(716752888238966276, result[2]);
            Assert.Equal(5451770624740049375, result[3]);
            Assert.Equal(4635659444355057900, result[4]);
            Assert.Equal(-5714135075575530569, result[5]);
            Console.WriteLine("OK");
        }
    }
}
