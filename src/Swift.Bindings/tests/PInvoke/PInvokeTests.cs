// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class PInvokeTests : IClassFixture<PInvokeTests.TestFixture>
    {
        private readonly TestFixture _fixture;
        private static string _assemblyPath;

        public PInvokeTests(TestFixture fixture)
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
                BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
                _assemblyPath = TestsHelper.Compile(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { },
                new string [] { });
            }
        }

        [Fact]
        public static void TestSwiftSmoke()
        {
            int result = (int)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftSmoke", new object[] { });
            Assert.Equal(42, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftBool()
        {
            bool result = (bool)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftBool", new object[] { });
            Assert.True(result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc0()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc0", new object[] { });
            Assert.Equal(-7706330218351441791, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc1()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc1", new object[] { });
            Assert.Equal(-3202601456867082324, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc2()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc2", new object[] { });
            Assert.Equal(911474180935535301, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc3()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc3", new object[] { });
            Assert.Equal(-6350065034291914241, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc4()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc4", new object[] { });
            Assert.Equal(-9091922861563963282, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc5()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc5", new object[] { });
            Assert.Equal(-3357359150345247842, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc6()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc6", new object[] { });
            Assert.Equal(-581969692498632062, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc7()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc7", new object[] { });
            Assert.Equal(4054341816496194551, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc8()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc8", new object[] { });
            Assert.Equal(-2147505143518021575, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc9()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc9", new object[] { });
            Assert.Equal(3533238385513656508, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc10()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc10", new object[] { });
            Assert.Equal(8515181823957334780, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc11()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc11", new object[] { });
            Assert.Equal(-5125817077505710853, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc12()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc12", new object[] { });
            Assert.Equal(4774074602111830179, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc13()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc13", new object[] { });
            Assert.Equal(8686515529117439727, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc14()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc14", new object[] { });
            Assert.Equal(1430703777921650146, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc15()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc15", new object[] { });
            Assert.Equal(7324810059718518437, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc16()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc16", new object[] { });
            Assert.Equal(-2322427926688559587, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc17()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc17", new object[] { });
            Assert.Equal(-5704419938581148490, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc18()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc18", new object[] { });
            Assert.Equal(-7333181440701096551, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc19()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc19", new object[] { });
            Assert.Equal(-7514368921355633465, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc20()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc20", new object[] { });
            Assert.Equal(4347999520285809529, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc21()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc21", new object[] { });
            Assert.Equal(9056719667499044372, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc22()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc22", new object[] { });
            Assert.Equal(2450837469650376012, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc23()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc23", new object[] { });
            Assert.Equal(-6077835106866375633, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc24()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc24", new object[] { });
            Assert.Equal(-7246961535839287248, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc25()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc25", new object[] { });
            Assert.Equal(4681650148273269479, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc26()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc26", new object[] { });
            Assert.Equal(-7896710633380101536, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc27()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc27", new object[] { });
            Assert.Equal(-2413801917489038945, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc28()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc28", new object[] { });
            Assert.Equal(-5115695744450024635, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc29()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc29", new object[] { });
            Assert.Equal(7218188220935660367, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc30()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc30", new object[] { });
            Assert.Equal(3303407505715961682, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc31()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc31", new object[] { });
            Assert.Equal(6926745355509484660, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc32()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc32", new object[] { });
            Assert.Equal(-8134759728697468421, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc33()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc33", new object[] { });
            Assert.Equal(-8926062754575499112, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc34()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc34", new object[] { });
            Assert.Equal(3916199453627741495, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc35()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc35", new object[] { });
            Assert.Equal(4225631615746848021, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc36()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc36", new object[] { });
            Assert.Equal(9029057458451328084, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc37()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc37", new object[] { });
            Assert.Equal(9091326884382848930, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc38()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc38", new object[] { });
            Assert.Equal(2966780901945169708, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc39()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc39", new object[] { });
            Assert.Equal(-7464446680392812994, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc40()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc40", new object[] { });
            Assert.Equal(-3563617050423332895, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc41()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc41", new object[] { });
            Assert.Equal(-2569382956498289470, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc42()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc42", new object[] { });
            Assert.Equal(-1108582741386924293, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc43()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc43", new object[] { });
            Assert.Equal(-5808479907339934850, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc44()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc44", new object[] { });
            Assert.Equal(-234686925954875908, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc45()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc45", new object[] { });
            Assert.Equal(-9083497234002976264, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc46()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc46", new object[] { });
            Assert.Equal(-7467754277704703568, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc47()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc47", new object[] { });
            Assert.Equal(7149358155385248658, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc48()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc48", new object[] { });
            Assert.Equal(-8590814201057560160, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc49()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc49", new object[] { });
            Assert.Equal(739011484971652047, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc50()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc50", new object[] { });
            Assert.Equal(3055246540243887734, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc51()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc51", new object[] { });
            Assert.Equal(7917142179400080853, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc52()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc52", new object[] { });
            Assert.Equal(-8118257769004209257, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc53()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc53", new object[] { });
            Assert.Equal(9075957082830800153, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc54()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc54", new object[] { });
            Assert.Equal(715458900514912094, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc55()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc55", new object[] { });
            Assert.Equal(-7812796314477300904, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc56()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc56", new object[] { });
            Assert.Equal(-3660123537755587162, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc57()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc57", new object[] { });
            Assert.Equal(-8830493546874923270, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc58()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc58", new object[] { });
            Assert.Equal(6514055640091085387, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc59()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc59", new object[] { });
            Assert.Equal(5046324847209516867, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc60()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc60", new object[] { });
            Assert.Equal(-8176066941526010601, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc61()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc61", new object[] { });
            Assert.Equal(-8047185703659702100, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc62()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc62", new object[] { });
            Assert.Equal(6758416630263865563, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc63()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc63", new object[] { });
            Assert.Equal(-24765264996518815, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc64()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc64", new object[] { });
            Assert.Equal(4496411701938139124, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc65()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc65", new object[] { });
            Assert.Equal(7620356050748244213, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc66()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc66", new object[] { });
            Assert.Equal(-6837183037573462724, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc67()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc67", new object[] { });
            Assert.Equal(3965211134150981679, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc68()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc68", new object[] { });
            Assert.Equal(8645187640386338150, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc69()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc69", new object[] { });
            Assert.Equal(-2766546132850174765, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc70()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc70", new object[] { });
            Assert.Equal(-6730251310408327023, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc71()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc71", new object[] { });
            Assert.Equal(-4761426221194945322, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc72()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc72", new object[] { });
            Assert.Equal(8722701469163367659, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc73()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc73", new object[] { });
            Assert.Equal(9091436234605144348, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc74()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc74", new object[] { });
            Assert.Equal(-4564195959279673945, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc75()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc75", new object[] { });
            Assert.Equal(-3369734987080453648, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc76()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc76", new object[] { });
            Assert.Equal(-8920640767423704440, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc77()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc77", new object[] { });
            Assert.Equal(6960169366615671879, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc78()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc78", new object[] { });
            Assert.Equal(4812301631028745377, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc79()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc79", new object[] { });
            Assert.Equal(693619259694162127, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc80()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc80", new object[] { });
            Assert.Equal(-4631030647197364647, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc81()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc81", new object[] { });
            Assert.Equal(-8908581242517107527, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc82()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc82", new object[] { });
            Assert.Equal(-1543576629977717704, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc83()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc83", new object[] { });
            Assert.Equal(-4161389211393419243, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc84()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc84", new object[] { });
            Assert.Equal(8984640578940854556, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc85()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc85", new object[] { });
            Assert.Equal(-5603269280984392717, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc86()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc86", new object[] { });
            Assert.Equal(-756030944410084256, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc87()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc87", new object[] { });
            Assert.Equal(3151224756940080953, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc88()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc88", new object[] { });
            Assert.Equal(3274371447309987255, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc89()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc89", new object[] { });
            Assert.Equal(-737269134554333880, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc90()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc90", new object[] { });
            Assert.Equal(3441802633846719073, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc91()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc91", new object[] { });
            Assert.Equal(711186144202003795, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc92()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc92", new object[] { });
            Assert.Equal(9206890599465525240, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc93()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc93", new object[] { });
            Assert.Equal(7367909694938381646, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc94()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc94", new object[] { });
            Assert.Equal(7957085466204676840, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc95()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc95", new object[] { });
            Assert.Equal(-8941275780625427292, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc96()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc96", new object[] { });
            Assert.Equal(2083246537822351760, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc97()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc97", new object[] { });
            Assert.Equal(8647824177212049859, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc98()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc98", new object[] { });
            Assert.Equal(7040925530630314472, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc99()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftFunc99", new object[] { });
            Assert.Equal(-7883825139759684683, result);
            Console.WriteLine("OK");
        }
    }
}
