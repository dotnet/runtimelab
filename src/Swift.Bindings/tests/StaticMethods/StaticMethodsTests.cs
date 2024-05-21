// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class StaticMethodsTests : IClassFixture<StaticMethodsTests.TestFixture>
    {
        private readonly TestFixture _fixture;
        private static string _assemblyPath;

        public StaticMethodsTests(TestFixture fixture)
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
                BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
                _assemblyPath = TestsHelper.Compile(
                new string [] { "StaticMethods/*.cs" }, 
                new string [] { },
                new string [] { });
            }
        }

        [Fact]
        public static void TestSwiftType0()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType0", new object[] { });
            Assert.Equal(-1302454221810123473, result);
        }
        
        [Fact]
        public static void TestSwiftType1()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType1", new object[] { });
            Assert.Equal(914300229919721579, result);
        }
        
        [Fact]
        public static void TestSwiftType2()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType2", new object[] { });
            Assert.Equal(3606159555131430051, result);
        }
        
        [Fact]
        public static void TestSwiftType3()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType3", new object[] { });
            Assert.Equal(9085678888513549564, result);
        }
        
        [Fact]
        public static void TestSwiftType4()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType4", new object[] { });
            Assert.Equal(-9013520609104109583, result);
        }
        
        [Fact]
        public static void TestSwiftType5()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType5", new object[] { });
            Assert.Equal(27416593309743651, result);
        }
        
        [Fact]
        public static void TestSwiftType6()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType6", new object[] { });
            Assert.Equal(3661604645194525580, result);
        }
        
        [Fact]
        public static void TestSwiftType7()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType7", new object[] { });
            Assert.Equal(-3025493081346654563, result);
        }
        
        [Fact]
        public static void TestSwiftType8()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType8", new object[] { });
            Assert.Equal(-7677466411347177033, result);
        }
        
        [Fact]
        public static void TestSwiftType9()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType9", new object[] { });
            Assert.Equal(-2253623701143287732, result);
        }
        
        [Fact]
        public static void TestSwiftType10()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType10", new object[] { });
            Assert.Equal(8019726010431750353, result);
        }
        
        [Fact]
        public static void TestSwiftType11()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType11", new object[] { });
            Assert.Equal(3146418414537113518, result);
        }
        
        [Fact]
        public static void TestSwiftType12()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType12", new object[] { });
            Assert.Equal(1803324178910069028, result);
        }
        
        [Fact]
        public static void TestSwiftType13()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType13", new object[] { });
            Assert.Equal(4689617795014579452, result);
        }
        
        [Fact]
        public static void TestSwiftType14()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType14", new object[] { });
            Assert.Equal(6289251196731842658, result);
        }
        
        [Fact]
        public static void TestSwiftType15()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType15", new object[] { });
            Assert.Equal(391791165974922649, result);
        }
        
        [Fact]
        public static void TestSwiftType16()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType16", new object[] { });
            Assert.Equal(621294471543772429, result);
        }
        
        [Fact]
        public static void TestSwiftType17()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType17", new object[] { });
            Assert.Equal(607854041403170315, result);
        }
        
        [Fact]
        public static void TestSwiftType18()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType18", new object[] { });
            Assert.Equal(-3483333800069613251, result);
        }
        
        [Fact]
        public static void TestSwiftType19()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType19", new object[] { });
            Assert.Equal(-5674147374399610801, result);
        }
        
        [Fact]
        public static void TestSwiftType20()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType20", new object[] { });
            Assert.Equal(-5106234148504633478, result);
        }
        
        [Fact]
        public static void TestSwiftType21()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType21", new object[] { });
            Assert.Equal(246133781151241632, result);
        }
        
        [Fact]
        public static void TestSwiftType22()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType22", new object[] { });
            Assert.Equal(9123574604720661329, result);
        }
        
        [Fact]
        public static void TestSwiftType23()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType23", new object[] { });
            Assert.Equal(-3515963896618663036, result);
        }
        
        [Fact]
        public static void TestSwiftType24()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType24", new object[] { });
            Assert.Equal(-9105318085603802964, result);
        }
        
        [Fact]
        public static void TestSwiftType25()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType25", new object[] { });
            Assert.Equal(8436092602422195875, result);
        }
        
        [Fact]
        public static void TestSwiftType26()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType26", new object[] { });
            Assert.Equal(4414091805691157982, result);
        }
        
        [Fact]
        public static void TestSwiftType27()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType27", new object[] { });
            Assert.Equal(5482643438640306451, result);
        }
        
        [Fact]
        public static void TestSwiftType28()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType28", new object[] { });
            Assert.Equal(-7531537036341229865, result);
        }
        
        [Fact]
        public static void TestSwiftType29()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType29", new object[] { });
            Assert.Equal(-110785621188401307, result);
        }
        
        [Fact]
        public static void TestSwiftType30()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType30", new object[] { });
            Assert.Equal(4661143371282223404, result);
        }
        
        [Fact]
        public static void TestSwiftType31()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType31", new object[] { });
            Assert.Equal(-5437539143661417520, result);
        }
        
        [Fact]
        public static void TestSwiftType32()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType32", new object[] { });
            Assert.Equal(-8564140570796838325, result);
        }
        
        [Fact]
        public static void TestSwiftType33()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType33", new object[] { });
            Assert.Equal(127893875284315364, result);
        }
        
        [Fact]
        public static void TestSwiftType34()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType34", new object[] { });
            Assert.Equal(1670198715257327092, result);
        }
        
        [Fact]
        public static void TestSwiftType35()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType35", new object[] { });
            Assert.Equal(-580800586705400168, result);
        }
        
        [Fact]
        public static void TestSwiftType36()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType36", new object[] { });
            Assert.Equal(-6671430655764225195, result);
        }
        
        [Fact]
        public static void TestSwiftType37()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType37", new object[] { });
            Assert.Equal(-2879189024178887556, result);
        }
        
        [Fact]
        public static void TestSwiftType38()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType38", new object[] { });
            Assert.Equal(-57788390475521057, result);
        }
        
        [Fact]
        public static void TestSwiftType39()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType39", new object[] { });
            Assert.Equal(8161332097230727509, result);
        }
        
        [Fact]
        public static void TestSwiftType40()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType40", new object[] { });
            Assert.Equal(7939635022138285726, result);
        }
        
        [Fact]
        public static void TestSwiftType41()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType41", new object[] { });
            Assert.Equal(-5638617594007136487, result);
        }
        
        [Fact]
        public static void TestSwiftType42()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType42", new object[] { });
            Assert.Equal(-6488902911358175373, result);
        }
        
        [Fact]
        public static void TestSwiftType43()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType43", new object[] { });
            Assert.Equal(-8040904851637460412, result);
        }
        
        [Fact]
        public static void TestSwiftType44()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType44", new object[] { });
            Assert.Equal(-7336859208496404469, result);
        }
        
        [Fact]
        public static void TestSwiftType45()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType45", new object[] { });
            Assert.Equal(6345937011278591838, result);
        }
        
        [Fact]
        public static void TestSwiftType46()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType46", new object[] { });
            Assert.Equal(-5325095042452577111, result);
        }
        
        [Fact]
        public static void TestSwiftType47()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType47", new object[] { });
            Assert.Equal(-861931995212022056, result);
        }
        
        [Fact]
        public static void TestSwiftType48()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType48", new object[] { });
            Assert.Equal(7332186240727164655, result);
        }
        
        [Fact]
        public static void TestSwiftType49()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType49", new object[] { });
            Assert.Equal(5015549653232840682, result);
        }
        
        [Fact]
        public static void TestSwiftType50()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType50", new object[] { });
            Assert.Equal(-1759411520443392441, result);
        }
        
        [Fact]
        public static void TestSwiftType51()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType51", new object[] { });
            Assert.Equal(4361279221094392059, result);
        }
        
        [Fact]
        public static void TestSwiftType52()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType52", new object[] { });
            Assert.Equal(-1699583181824442426, result);
        }
        
        [Fact]
        public static void TestSwiftType53()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType53", new object[] { });
            Assert.Equal(-8575366906149444658, result);
        }
        
        [Fact]
        public static void TestSwiftType54()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType54", new object[] { });
            Assert.Equal(5229487626593371452, result);
        }
        
        [Fact]
        public static void TestSwiftType55()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType55", new object[] { });
            Assert.Equal(-3892962395563826067, result);
        }
        
        [Fact]
        public static void TestSwiftType56()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType56", new object[] { });
            Assert.Equal(-2208890062033214998, result);
        }
        
        [Fact]
        public static void TestSwiftType57()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType57", new object[] { });
            Assert.Equal(-2497407779594054558, result);
        }
        
        [Fact]
        public static void TestSwiftType58()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType58", new object[] { });
            Assert.Equal(324174718095320818, result);
        }
        
        [Fact]
        public static void TestSwiftType59()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType59", new object[] { });
            Assert.Equal(-1332509595342217973, result);
        }
        
        [Fact]
        public static void TestSwiftType60()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType60", new object[] { });
            Assert.Equal(4081230995526660101, result);
        }
        
        [Fact]
        public static void TestSwiftType61()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType61", new object[] { });
            Assert.Equal(-3737054516232390576, result);
        }
        
        [Fact]
        public static void TestSwiftType62()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType62", new object[] { });
            Assert.Equal(-2635903538728387146, result);
        }
        
        [Fact]
        public static void TestSwiftType63()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType63", new object[] { });
            Assert.Equal(-6625241565325616919, result);
        }
        
        [Fact]
        public static void TestSwiftType64()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType64", new object[] { });
            Assert.Equal(-2829834324768398881, result);
        }
        
        [Fact]
        public static void TestSwiftType65()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType65", new object[] { });
            Assert.Equal(-4954353219915555130, result);
        }
        
        [Fact]
        public static void TestSwiftType66()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType66", new object[] { });
            Assert.Equal(7241753510039977648, result);
        }
        
        [Fact]
        public static void TestSwiftType67()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType67", new object[] { });
            Assert.Equal(-1832824433293148886, result);
        }
        
        [Fact]
        public static void TestSwiftType68()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType68", new object[] { });
            Assert.Equal(901199712025494241, result);
        }
        
        [Fact]
        public static void TestSwiftType69()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType69", new object[] { });
            Assert.Equal(7394107227683775943, result);
        }
        
        [Fact]
        public static void TestSwiftType70()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType70", new object[] { });
            Assert.Equal(1357280204861671662, result);
        }
        
        [Fact]
        public static void TestSwiftType71()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType71", new object[] { });
            Assert.Equal(6209167671701393392, result);
        }
        
        [Fact]
        public static void TestSwiftType72()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType72", new object[] { });
            Assert.Equal(-2016241838007125046, result);
        }
        
        [Fact]
        public static void TestSwiftType73()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType73", new object[] { });
            Assert.Equal(-2590768908127775473, result);
        }
        
        [Fact]
        public static void TestSwiftType74()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType74", new object[] { });
            Assert.Equal(-3415693461695316082, result);
        }
        
        [Fact]
        public static void TestSwiftType75()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType75", new object[] { });
            Assert.Equal(1052970797754173925, result);
        }
        
        [Fact]
        public static void TestSwiftType76()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType76", new object[] { });
            Assert.Equal(1284170383645516782, result);
        }
        
        [Fact]
        public static void TestSwiftType77()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType77", new object[] { });
            Assert.Equal(-3361603965782237971, result);
        }
        
        [Fact]
        public static void TestSwiftType78()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType78", new object[] { });
            Assert.Equal(-7618842638074074063, result);
        }
        
        [Fact]
        public static void TestSwiftType79()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType79", new object[] { });
            Assert.Equal(2001881504172846833, result);
        }
        
        [Fact]
        public static void TestSwiftType80()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType80", new object[] { });
            Assert.Equal(8780417345600432609, result);
        }
        
        [Fact]
        public static void TestSwiftType81()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType81", new object[] { });
            Assert.Equal(-8146835476385947337, result);
        }
        
        [Fact]
        public static void TestSwiftType82()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType82", new object[] { });
            Assert.Equal(8856268358096150912, result);
        }
        
        [Fact]
        public static void TestSwiftType83()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType83", new object[] { });
            Assert.Equal(1319882132930383678, result);
        }
        
        [Fact]
        public static void TestSwiftType84()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType84", new object[] { });
            Assert.Equal(-508146693585361758, result);
        }
        
        [Fact]
        public static void TestSwiftType85()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType85", new object[] { });
            Assert.Equal(-2649507594516546671, result);
        }
        
        [Fact]
        public static void TestSwiftType86()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType86", new object[] { });
            Assert.Equal(8088375796594482986, result);
        }
        
        [Fact]
        public static void TestSwiftType87()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType87", new object[] { });
            Assert.Equal(1442750423798503241, result);
        }
        
        [Fact]
        public static void TestSwiftType88()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType88", new object[] { });
            Assert.Equal(7158341730093042416, result);
        }
        
        [Fact]
        public static void TestSwiftType89()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType89", new object[] { });
            Assert.Equal(553531604612512814, result);
        }
        
        [Fact]
        public static void TestSwiftType90()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType90", new object[] { });
            Assert.Equal(669600114590149160, result);
        }
        
        [Fact]
        public static void TestSwiftType91()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType91", new object[] { });
            Assert.Equal(577292016191472559, result);
        }
        
        [Fact]
        public static void TestSwiftType92()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType92", new object[] { });
            Assert.Equal(8168985653034001556, result);
        }
        
        [Fact]
        public static void TestSwiftType93()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType93", new object[] { });
            Assert.Equal(-4511150573256535639, result);
        }
        
        [Fact]
        public static void TestSwiftType94()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType94", new object[] { });
            Assert.Equal(762831339959854365, result);
        }
        
        [Fact]
        public static void TestSwiftType95()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType95", new object[] { });
            Assert.Equal(1749138547312475471, result);
        }
        
        [Fact]
        public static void TestSwiftType96()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType96", new object[] { });
            Assert.Equal(603119544333039874, result);
        }
        
        [Fact]
        public static void TestSwiftType97()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType97", new object[] { });
            Assert.Equal(3825727416281544568, result);
        }
        
        [Fact]
        public static void TestSwiftType98()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType98", new object[] { });
            Assert.Equal(-2876779109167562933, result);
        }
        
        [Fact]
        public static void TestSwiftType99()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType99", new object[] { });
            Assert.Equal(8604643581634903352, result);
        }
        
        [Fact]
        public static void TestSwiftType100()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType100", new object[] { });
            Assert.Equal(-5183461739495323880, result);
        }
        
        [Fact]
        public static void TestSwiftType101()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType101", new object[] { });
            Assert.Equal(3700353538232897146, result);
        }
        
        [Fact]
        public static void TestSwiftType102()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType102", new object[] { });
            Assert.Equal(-1815728824758141834, result);
        }
        
        [Fact]
        public static void TestSwiftType103()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType103", new object[] { });
            Assert.Equal(-6044068917807042715, result);
        }
        
        [Fact]
        public static void TestSwiftType104()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType104", new object[] { });
            Assert.Equal(-3658393639098834511, result);
        }
        
        [Fact]
        public static void TestSwiftType105()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType105", new object[] { });
            Assert.Equal(3377392297168521549, result);
        }
        
        [Fact]
        public static void TestSwiftType106()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType106", new object[] { });
            Assert.Equal(-7100411163736428415, result);
        }
        
        [Fact]
        public static void TestSwiftType107()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType107", new object[] { });
            Assert.Equal(-3587794744717933961, result);
        }
        
        [Fact]
        public static void TestSwiftType108()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType108", new object[] { });
            Assert.Equal(6240892100988090868, result);
        }
        
        [Fact]
        public static void TestSwiftType109()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType109", new object[] { });
            Assert.Equal(-5642305488203428102, result);
        }
        
        [Fact]
        public static void TestSwiftType110()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType110", new object[] { });
            Assert.Equal(-3231178403858758072, result);
        }
        
        [Fact]
        public static void TestSwiftType111()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType111", new object[] { });
            Assert.Equal(-2276280640263091297, result);
        }
        
        [Fact]
        public static void TestSwiftType112()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType112", new object[] { });
            Assert.Equal(6280951790717052089, result);
        }
        
        [Fact]
        public static void TestSwiftType113()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType113", new object[] { });
            Assert.Equal(7596813894285301944, result);
        }
        
        [Fact]
        public static void TestSwiftType114()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType114", new object[] { });
            Assert.Equal(684334554920709908, result);
        }
        
        [Fact]
        public static void TestSwiftType115()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType115", new object[] { });
            Assert.Equal(3833906899286760162, result);
        }
        
        [Fact]
        public static void TestSwiftType116()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType116", new object[] { });
            Assert.Equal(660514051314300574, result);
        }
        
        [Fact]
        public static void TestSwiftType117()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType117", new object[] { });
            Assert.Equal(586766441905616701, result);
        }
        
        [Fact]
        public static void TestSwiftType118()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType118", new object[] { });
            Assert.Equal(1437306852202736988, result);
        }
        
        [Fact]
        public static void TestSwiftType119()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType119", new object[] { });
            Assert.Equal(7840710793526771328, result);
        }
        
        [Fact]
        public static void TestSwiftType120()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType120", new object[] { });
            Assert.Equal(-1966007234001659582, result);
        }
        
        [Fact]
        public static void TestSwiftType121()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType121", new object[] { });
            Assert.Equal(-121329981534870825, result);
        }
        
        [Fact]
        public static void TestSwiftType122()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType122", new object[] { });
            Assert.Equal(1415111888184892374, result);
        }
        
        [Fact]
        public static void TestSwiftType123()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType123", new object[] { });
            Assert.Equal(-6001975047106517528, result);
        }
        
        [Fact]
        public static void TestSwiftType124()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType124", new object[] { });
            Assert.Equal(7933515710817159940, result);
        }
        
        [Fact]
        public static void TestSwiftType125()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType125", new object[] { });
            Assert.Equal(-8015621203482153651, result);
        }
        
        [Fact]
        public static void TestSwiftType126()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType126", new object[] { });
            Assert.Equal(4395505961823925134, result);
        }
        
        [Fact]
        public static void TestSwiftType127()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType127", new object[] { });
            Assert.Equal(-2207693396529891830, result);
        }
        
        [Fact]
        public static void TestSwiftType128()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType128", new object[] { });
            Assert.Equal(-1970446111156807291, result);
        }
        
        [Fact]
        public static void TestSwiftType129()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType129", new object[] { });
            Assert.Equal(-788990220686720046, result);
        }
        
        [Fact]
        public static void TestSwiftType130()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType130", new object[] { });
            Assert.Equal(-898048570311494372, result);
        }
        
        [Fact]
        public static void TestSwiftType131()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType131", new object[] { });
            Assert.Equal(8899105243716276813, result);
        }
        
        [Fact]
        public static void TestSwiftType132()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType132", new object[] { });
            Assert.Equal(8488353822900473986, result);
        }
        
        [Fact]
        public static void TestSwiftType133()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType133", new object[] { });
            Assert.Equal(3435889942386083384, result);
        }
        
        [Fact]
        public static void TestSwiftType134()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType134", new object[] { });
            Assert.Equal(-8815603871699215989, result);
        }
        
        [Fact]
        public static void TestSwiftType135()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType135", new object[] { });
            Assert.Equal(-3729506638839213907, result);
        }
        
        [Fact]
        public static void TestSwiftType136()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType136", new object[] { });
            Assert.Equal(1154091763175769141, result);
        }
        
        [Fact]
        public static void TestSwiftType137()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType137", new object[] { });
            Assert.Equal(5716785126267550184, result);
        }
        
        [Fact]
        public static void TestSwiftType138()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType138", new object[] { });
            Assert.Equal(-4989739197202666758, result);
        }
        
        [Fact]
        public static void TestSwiftType139()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType139", new object[] { });
            Assert.Equal(5976199894150725410, result);
        }
        
        [Fact]
        public static void TestSwiftType140()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType140", new object[] { });
            Assert.Equal(6889201745455097965, result);
        }
        
        [Fact]
        public static void TestSwiftType141()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType141", new object[] { });
            Assert.Equal(2313199209249006652, result);
        }
        
        [Fact]
        public static void TestSwiftType142()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType142", new object[] { });
            Assert.Equal(3367964750273980708, result);
        }
        
        [Fact]
        public static void TestSwiftType143()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType143", new object[] { });
            Assert.Equal(3487868267203425370, result);
        }
        
        [Fact]
        public static void TestSwiftType144()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType144", new object[] { });
            Assert.Equal(2096831882286791291, result);
        }
        
        [Fact]
        public static void TestSwiftType145()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType145", new object[] { });
            Assert.Equal(-5226086631818805671, result);
        }
        
        [Fact]
        public static void TestSwiftType146()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType146", new object[] { });
            Assert.Equal(-5648887699739103852, result);
        }
        
        [Fact]
        public static void TestSwiftType147()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType147", new object[] { });
            Assert.Equal(-7005217645564374753, result);
        }
        
        [Fact]
        public static void TestSwiftType148()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType148", new object[] { });
            Assert.Equal(-5808618445805089436, result);
        }
        
        [Fact]
        public static void TestSwiftType149()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType149", new object[] { });
            Assert.Equal(-9158574050224105016, result);
        }
        
        [Fact]
        public static void TestSwiftType150()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType150", new object[] { });
            Assert.Equal(-5916600624380327077, result);
        }
        
        [Fact]
        public static void TestSwiftType151()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType151", new object[] { });
            Assert.Equal(-2648923089381979377, result);
        }
        
        [Fact]
        public static void TestSwiftType152()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType152", new object[] { });
            Assert.Equal(-452105228537114039, result);
        }
        
        [Fact]
        public static void TestSwiftType153()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType153", new object[] { });
            Assert.Equal(-6797445698059191175, result);
        }
        
        [Fact]
        public static void TestSwiftType154()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType154", new object[] { });
            Assert.Equal(608794204138285486, result);
        }
        
        [Fact]
        public static void TestSwiftType155()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType155", new object[] { });
            Assert.Equal(-4082797397364962438, result);
        }
        
        [Fact]
        public static void TestSwiftType156()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType156", new object[] { });
            Assert.Equal(-7906006689784259384, result);
        }
        
        [Fact]
        public static void TestSwiftType157()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType157", new object[] { });
            Assert.Equal(8659889919274962482, result);
        }
        
        [Fact]
        public static void TestSwiftType158()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType158", new object[] { });
            Assert.Equal(1468038131265307737, result);
        }
        
        [Fact]
        public static void TestSwiftType159()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType159", new object[] { });
            Assert.Equal(-4715570166168922954, result);
        }
        
        [Fact]
        public static void TestSwiftType160()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType160", new object[] { });
            Assert.Equal(665296926896072299, result);
        }
        
        [Fact]
        public static void TestSwiftType161()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType161", new object[] { });
            Assert.Equal(-5413008670567133046, result);
        }
        
        [Fact]
        public static void TestSwiftType162()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType162", new object[] { });
            Assert.Equal(-6739692845823709352, result);
        }
        
        [Fact]
        public static void TestSwiftType163()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType163", new object[] { });
            Assert.Equal(7925510834824462180, result);
        }
        
        [Fact]
        public static void TestSwiftType164()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType164", new object[] { });
            Assert.Equal(2675248728736638871, result);
        }
        
        [Fact]
        public static void TestSwiftType165()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType165", new object[] { });
            Assert.Equal(2056258949234144081, result);
        }
        
        [Fact]
        public static void TestSwiftType166()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType166", new object[] { });
            Assert.Equal(6400995704552108755, result);
        }
        
        [Fact]
        public static void TestSwiftType167()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType167", new object[] { });
            Assert.Equal(-29956367469261099, result);
        }
        
        [Fact]
        public static void TestSwiftType168()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType168", new object[] { });
            Assert.Equal(-3643954138502892509, result);
        }
        
        [Fact]
        public static void TestSwiftType169()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType169", new object[] { });
            Assert.Equal(1918912945413304390, result);
        }
        
        [Fact]
        public static void TestSwiftType170()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType170", new object[] { });
            Assert.Equal(-4387239261931382494, result);
        }
        
        [Fact]
        public static void TestSwiftType171()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType171", new object[] { });
            Assert.Equal(415493127137062773, result);
        }
        
        [Fact]
        public static void TestSwiftType172()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType172", new object[] { });
            Assert.Equal(4112515667169269651, result);
        }
        
        [Fact]
        public static void TestSwiftType173()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType173", new object[] { });
            Assert.Equal(662427201547009264, result);
        }
        
        [Fact]
        public static void TestSwiftType174()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType174", new object[] { });
            Assert.Equal(9195655655750290121, result);
        }
        
        [Fact]
        public static void TestSwiftType175()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType175", new object[] { });
            Assert.Equal(-8866160771291525329, result);
        }
        
        [Fact]
        public static void TestSwiftType176()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType176", new object[] { });
            Assert.Equal(-7919806487363423519, result);
        }
        
        [Fact]
        public static void TestSwiftType177()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType177", new object[] { });
            Assert.Equal(-3938287191714746229, result);
        }
        
        [Fact]
        public static void TestSwiftType178()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType178", new object[] { });
            Assert.Equal(8766743431031461175, result);
        }
        
        [Fact]
        public static void TestSwiftType179()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType179", new object[] { });
            Assert.Equal(-8986509835952913005, result);
        }
        
        [Fact]
        public static void TestSwiftType180()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType180", new object[] { });
            Assert.Equal(3590691304079810474, result);
        }
        
        [Fact]
        public static void TestSwiftType181()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType181", new object[] { });
            Assert.Equal(7724744106161839777, result);
        }
        
        [Fact]
        public static void TestSwiftType182()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType182", new object[] { });
            Assert.Equal(2229816267186466030, result);
        }
        
        [Fact]
        public static void TestSwiftType183()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType183", new object[] { });
            Assert.Equal(4118228357113608828, result);
        }
        
        [Fact]
        public static void TestSwiftType184()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType184", new object[] { });
            Assert.Equal(1804145342247858854, result);
        }
        
        [Fact]
        public static void TestSwiftType185()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType185", new object[] { });
            Assert.Equal(7704759995325073605, result);
        }
        
        [Fact]
        public static void TestSwiftType186()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType186", new object[] { });
            Assert.Equal(1372448214341753544, result);
        }
        
        [Fact]
        public static void TestSwiftType187()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType187", new object[] { });
            Assert.Equal(1971274345882451651, result);
        }
        
        [Fact]
        public static void TestSwiftType188()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType188", new object[] { });
            Assert.Equal(-4574023028842821245, result);
        }
        
        [Fact]
        public static void TestSwiftType189()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType189", new object[] { });
            Assert.Equal(4929146646809221225, result);
        }
        
        [Fact]
        public static void TestSwiftType190()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType190", new object[] { });
            Assert.Equal(-8349326794474127128, result);
        }
        
        [Fact]
        public static void TestSwiftType191()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType191", new object[] { });
            Assert.Equal(-8460397198155335392, result);
        }
        
        [Fact]
        public static void TestSwiftType192()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType192", new object[] { });
            Assert.Equal(-1356384574701834659, result);
        }
        
        [Fact]
        public static void TestSwiftType193()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType193", new object[] { });
            Assert.Equal(3156535411293896611, result);
        }
        
        [Fact]
        public static void TestSwiftType194()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType194", new object[] { });
            Assert.Equal(6472602285402761050, result);
        }
        
        [Fact]
        public static void TestSwiftType195()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType195", new object[] { });
            Assert.Equal(7737348280147095578, result);
        }
        
        [Fact]
        public static void TestSwiftType196()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType196", new object[] { });
            Assert.Equal(3183280857369603368, result);
        }
        
        [Fact]
        public static void TestSwiftType197()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType197", new object[] { });
            Assert.Equal(-1357428895534966919, result);
        }
        
        [Fact]
        public static void TestSwiftType198()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType198", new object[] { });
            Assert.Equal(1224872778054606228, result);
        }
        
        [Fact]
        public static void TestSwiftType199()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType199", new object[] { });
            Assert.Equal(731988194140412712, result);
        }
        
        [Fact]
        public static void TestSwiftType200()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType200", new object[] { });
            Assert.Equal(8751287127369919344, result);
        }
        
        [Fact]
        public static void TestSwiftType201()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType201", new object[] { });
            Assert.Equal(-1578096571707272808, result);
        }
        
        [Fact]
        public static void TestSwiftType202()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType202", new object[] { });
            Assert.Equal(-1564978127064141375, result);
        }
        
        [Fact]
        public static void TestSwiftType203()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType203", new object[] { });
            Assert.Equal(-8806294851017938129, result);
        }
        
        [Fact]
        public static void TestSwiftType204()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType204", new object[] { });
            Assert.Equal(829863142919886848, result);
        }
        
        [Fact]
        public static void TestSwiftType205()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType205", new object[] { });
            Assert.Equal(6909175346769209936, result);
        }
        
        [Fact]
        public static void TestSwiftType206()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType206", new object[] { });
            Assert.Equal(-7275121935272093171, result);
        }
        
        [Fact]
        public static void TestSwiftType207()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType207", new object[] { });
            Assert.Equal(-7930005622093663545, result);
        }
        
        [Fact]
        public static void TestSwiftType208()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType208", new object[] { });
            Assert.Equal(4327506764072823738, result);
        }
        
        [Fact]
        public static void TestSwiftType209()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType209", new object[] { });
            Assert.Equal(-5871068301339653861, result);
        }
        
        [Fact]
        public static void TestSwiftType210()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType210", new object[] { });
            Assert.Equal(-3617576866883394702, result);
        }
        
        [Fact]
        public static void TestSwiftType211()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType211", new object[] { });
            Assert.Equal(8797535796924509829, result);
        }
        
        [Fact]
        public static void TestSwiftType212()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType212", new object[] { });
            Assert.Equal(-9093135547841380823, result);
        }
        
        [Fact]
        public static void TestSwiftType213()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType213", new object[] { });
            Assert.Equal(-7370059868804831648, result);
        }
        
        [Fact]
        public static void TestSwiftType214()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType214", new object[] { });
            Assert.Equal(-1571987122778653432, result);
        }
        
        [Fact]
        public static void TestSwiftType215()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType215", new object[] { });
            Assert.Equal(3759391477887331378, result);
        }
        
        [Fact]
        public static void TestSwiftType216()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType216", new object[] { });
            Assert.Equal(5546215590190126240, result);
        }
        
        [Fact]
        public static void TestSwiftType217()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType217", new object[] { });
            Assert.Equal(5515496509618210499, result);
        }
        
        [Fact]
        public static void TestSwiftType218()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType218", new object[] { });
            Assert.Equal(3389738121929217758, result);
        }
        
        [Fact]
        public static void TestSwiftType219()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType219", new object[] { });
            Assert.Equal(204164697602924849, result);
        }
        
        [Fact]
        public static void TestSwiftType220()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType220", new object[] { });
            Assert.Equal(4843295259536981670, result);
        }
        
        [Fact]
        public static void TestSwiftType221()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType221", new object[] { });
            Assert.Equal(1203075211124201782, result);
        }
        
        [Fact]
        public static void TestSwiftType222()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType222", new object[] { });
            Assert.Equal(987652878952837373, result);
        }
        
        [Fact]
        public static void TestSwiftType223()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType223", new object[] { });
            Assert.Equal(-8597106858903601342, result);
        }
        
        [Fact]
        public static void TestSwiftType224()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType224", new object[] { });
            Assert.Equal(-6448247700166283672, result);
        }
        
        [Fact]
        public static void TestSwiftType225()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType225", new object[] { });
            Assert.Equal(6150748742965086152, result);
        }
        
        [Fact]
        public static void TestSwiftType226()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType226", new object[] { });
            Assert.Equal(-7248006303634400941, result);
        }
        
        [Fact]
        public static void TestSwiftType227()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType227", new object[] { });
            Assert.Equal(1410935449793688018, result);
        }
        
        [Fact]
        public static void TestSwiftType228()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType228", new object[] { });
            Assert.Equal(-8849936543451192470, result);
        }
        
        [Fact]
        public static void TestSwiftType229()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType229", new object[] { });
            Assert.Equal(-5808564569735307097, result);
        }
        
        [Fact]
        public static void TestSwiftType230()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType230", new object[] { });
            Assert.Equal(5915561197349509280, result);
        }
        
        [Fact]
        public static void TestSwiftType231()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType231", new object[] { });
            Assert.Equal(5213807528371859026, result);
        }
        
        [Fact]
        public static void TestSwiftType232()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType232", new object[] { });
            Assert.Equal(-5650716389767694265, result);
        }
        
        [Fact]
        public static void TestSwiftType233()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType233", new object[] { });
            Assert.Equal(-1943851521608495833, result);
        }
        
        [Fact]
        public static void TestSwiftType234()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType234", new object[] { });
            Assert.Equal(8696900450471275766, result);
        }
        
        [Fact]
        public static void TestSwiftType235()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType235", new object[] { });
            Assert.Equal(208110160139832832, result);
        }
        
        [Fact]
        public static void TestSwiftType236()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType236", new object[] { });
            Assert.Equal(6610413077972723642, result);
        }
        
        [Fact]
        public static void TestSwiftType237()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType237", new object[] { });
            Assert.Equal(-2876024817762115114, result);
        }
        
        [Fact]
        public static void TestSwiftType238()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType238", new object[] { });
            Assert.Equal(5694387538268430038, result);
        }
        
        [Fact]
        public static void TestSwiftType239()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType239", new object[] { });
            Assert.Equal(3353268450079572736, result);
        }
        
        [Fact]
        public static void TestSwiftType240()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType240", new object[] { });
            Assert.Equal(609815570147520289, result);
        }
        
        [Fact]
        public static void TestSwiftType241()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType241", new object[] { });
            Assert.Equal(7778548124421051982, result);
        }
        
        [Fact]
        public static void TestSwiftType242()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType242", new object[] { });
            Assert.Equal(8994000051989817089, result);
        }
        
        [Fact]
        public static void TestSwiftType243()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType243", new object[] { });
            Assert.Equal(-8637665132542722587, result);
        }
        
        [Fact]
        public static void TestSwiftType244()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType244", new object[] { });
            Assert.Equal(-4511397732638447968, result);
        }
        
        [Fact]
        public static void TestSwiftType245()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType245", new object[] { });
            Assert.Equal(750836114795011244, result);
        }
        
        [Fact]
        public static void TestSwiftType246()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType246", new object[] { });
            Assert.Equal(8978418421514884105, result);
        }
        
        [Fact]
        public static void TestSwiftType247()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType247", new object[] { });
            Assert.Equal(-4788927712479296194, result);
        }
        
        [Fact]
        public static void TestSwiftType248()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType248", new object[] { });
            Assert.Equal(-5053614839553447791, result);
        }
        
        [Fact]
        public static void TestSwiftType249()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType249", new object[] { });
            Assert.Equal(6394764035969715939, result);
        }
        
        [Fact]
        public static void TestSwiftType250()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType250", new object[] { });
            Assert.Equal(1538523573391109700, result);
        }
        
        [Fact]
        public static void TestSwiftType251()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType251", new object[] { });
            Assert.Equal(-6038112166435015472, result);
        }
        
        [Fact]
        public static void TestSwiftType252()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType252", new object[] { });
            Assert.Equal(-2194148373478066058, result);
        }
        
        [Fact]
        public static void TestSwiftType253()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType253", new object[] { });
            Assert.Equal(-5696561042698540867, result);
        }
        
        [Fact]
        public static void TestSwiftType254()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType254", new object[] { });
            Assert.Equal(-5808614047758576592, result);
        }
        
        [Fact]
        public static void TestSwiftType255()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType255", new object[] { });
            Assert.Equal(5879231729607274961, result);
        }
        
        [Fact]
        public static void TestSwiftType256()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType256", new object[] { });
            Assert.Equal(-7448087530822462958, result);
        }
        
        [Fact]
        public static void TestSwiftType257()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType257", new object[] { });
            Assert.Equal(-1383735803969955367, result);
        }
        
        [Fact]
        public static void TestSwiftType258()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType258", new object[] { });
            Assert.Equal(1391155564802257753, result);
        }
        
        [Fact]
        public static void TestSwiftType259()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType259", new object[] { });
            Assert.Equal(7701306734874843396, result);
        }
        
        [Fact]
        public static void TestSwiftType260()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType260", new object[] { });
            Assert.Equal(210589735848477449, result);
        }
        
        [Fact]
        public static void TestSwiftType261()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType261", new object[] { });
            Assert.Equal(-3584437386805933162, result);
        }
        
        [Fact]
        public static void TestSwiftType262()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType262", new object[] { });
            Assert.Equal(-1324056366855609618, result);
        }
        
        [Fact]
        public static void TestSwiftType263()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType263", new object[] { });
            Assert.Equal(-4841518573318706584, result);
        }
        
        [Fact]
        public static void TestSwiftType264()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType264", new object[] { });
            Assert.Equal(-1968512687697882966, result);
        }
        
        [Fact]
        public static void TestSwiftType265()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType265", new object[] { });
            Assert.Equal(-5399180687717383068, result);
        }
        
        [Fact]
        public static void TestSwiftType266()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType266", new object[] { });
            Assert.Equal(2991564855356304835, result);
        }
        
        [Fact]
        public static void TestSwiftType267()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType267", new object[] { });
            Assert.Equal(-6016726080209032828, result);
        }
        
        [Fact]
        public static void TestSwiftType268()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType268", new object[] { });
            Assert.Equal(4659456914887193846, result);
        }
        
        [Fact]
        public static void TestSwiftType269()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType269", new object[] { });
            Assert.Equal(3979182145248251593, result);
        }
        
        [Fact]
        public static void TestSwiftType270()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType270", new object[] { });
            Assert.Equal(591640642936787734, result);
        }
        
        [Fact]
        public static void TestSwiftType271()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType271", new object[] { });
            Assert.Equal(5044478833016954148, result);
        }
        
        [Fact]
        public static void TestSwiftType272()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType272", new object[] { });
            Assert.Equal(-8121508732296114185, result);
        }
        
        [Fact]
        public static void TestSwiftType273()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType273", new object[] { });
            Assert.Equal(5264772612232238808, result);
        }
        
        [Fact]
        public static void TestSwiftType274()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType274", new object[] { });
            Assert.Equal(-2231327004938290454, result);
        }
        
        [Fact]
        public static void TestSwiftType275()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType275", new object[] { });
            Assert.Equal(-3773240513285678005, result);
        }
        
        [Fact]
        public static void TestSwiftType276()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType276", new object[] { });
            Assert.Equal(4935582694013642489, result);
        }
        
        [Fact]
        public static void TestSwiftType277()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType277", new object[] { });
            Assert.Equal(-1326027808488383381, result);
        }
        
        [Fact]
        public static void TestSwiftType278()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType278", new object[] { });
            Assert.Equal(-8938060229869367453, result);
        }
        
        [Fact]
        public static void TestSwiftType279()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType279", new object[] { });
            Assert.Equal(593553793169496424, result);
        }
        
        [Fact]
        public static void TestSwiftType280()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType280", new object[] { });
            Assert.Equal(1691025257347504405, result);
        }
        
        [Fact]
        public static void TestSwiftType281()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType281", new object[] { });
            Assert.Equal(-6310027403672872531, result);
        }
        
        [Fact]
        public static void TestSwiftType282()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType282", new object[] { });
            Assert.Equal(-2912831909921751162, result);
        }
        
        [Fact]
        public static void TestSwiftType283()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType283", new object[] { });
            Assert.Equal(7930192212614880088, result);
        }
        
        [Fact]
        public static void TestSwiftType284()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType284", new object[] { });
            Assert.Equal(-2189305905996890980, result);
        }
        
        [Fact]
        public static void TestSwiftType285()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType285", new object[] { });
            Assert.Equal(-1160194257063564797, result);
        }
        
        [Fact]
        public static void TestSwiftType286()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType286", new object[] { });
            Assert.Equal(-3025651207962572234, result);
        }
        
        [Fact]
        public static void TestSwiftType287()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType287", new object[] { });
            Assert.Equal(-6755609106063950752, result);
        }
        
        [Fact]
        public static void TestSwiftType288()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType288", new object[] { });
            Assert.Equal(7656311073493574779, result);
        }
        
        [Fact]
        public static void TestSwiftType289()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType289", new object[] { });
            Assert.Equal(2867324145537522437, result);
        }
        
        [Fact]
        public static void TestSwiftType290()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType290", new object[] { });
            Assert.Equal(-5289008638091810662, result);
        }
        
        [Fact]
        public static void TestSwiftType291()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType291", new object[] { });
            Assert.Equal(8072638257956225766, result);
        }
        
        [Fact]
        public static void TestSwiftType292()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType292", new object[] { });
            Assert.Equal(6876973284445166072, result);
        }
        
        [Fact]
        public static void TestSwiftType293()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType293", new object[] { });
            Assert.Equal(-6693787192806373363, result);
        }
        
        [Fact]
        public static void TestSwiftType294()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType294", new object[] { });
            Assert.Equal(-3926953705818647021, result);
        }
        
        [Fact]
        public static void TestSwiftType295()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType295", new object[] { });
            Assert.Equal(-5961805883595131218, result);
        }
        
        [Fact]
        public static void TestSwiftType296()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType296", new object[] { });
            Assert.Equal(3074085374913202105, result);
        }
        
        [Fact]
        public static void TestSwiftType297()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType297", new object[] { });
            Assert.Equal(1071541657854385635, result);
        }
        
        [Fact]
        public static void TestSwiftType298()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType298", new object[] { });
            Assert.Equal(-5722720525946162898, result);
        }
        
        [Fact]
        public static void TestSwiftType299()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType299", new object[] { });
            Assert.Equal(3855690635967189595, result);
        }
        
        [Fact]
        public static void TestSwiftType300()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType300", new object[] { });
            Assert.Equal(-4906832853580219999, result);
        }
        
        [Fact]
        public static void TestSwiftType301()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType301", new object[] { });
            Assert.Equal(-8957339717014574618, result);
        }
        
        [Fact]
        public static void TestSwiftType302()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType302", new object[] { });
            Assert.Equal(-6873002678636213555, result);
        }
        
        [Fact]
        public static void TestSwiftType303()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType303", new object[] { });
            Assert.Equal(7700397584519272242, result);
        }
        
        [Fact]
        public static void TestSwiftType304()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType304", new object[] { });
            Assert.Equal(6935852189365092809, result);
        }
        
        [Fact]
        public static void TestSwiftType305()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType305", new object[] { });
            Assert.Equal(-1189941486758588483, result);
        }
        
        [Fact]
        public static void TestSwiftType306()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType306", new object[] { });
            Assert.Equal(6798776298972722462, result);
        }
        
        [Fact]
        public static void TestSwiftType307()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType307", new object[] { });
            Assert.Equal(7576763534199239620, result);
        }
        
        [Fact]
        public static void TestSwiftType308()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType308", new object[] { });
            Assert.Equal(3092344266969342041, result);
        }
        
        [Fact]
        public static void TestSwiftType309()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType309", new object[] { });
            Assert.Equal(-5296476420954873497, result);
        }
        
        [Fact]
        public static void TestSwiftType310()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType310", new object[] { });
            Assert.Equal(3879585883908093839, result);
        }
        
        [Fact]
        public static void TestSwiftType311()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType311", new object[] { });
            Assert.Equal(-4420149166872491203, result);
        }
        
        [Fact]
        public static void TestSwiftType312()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType312", new object[] { });
            Assert.Equal(7127809210547205315, result);
        }
        
        [Fact]
        public static void TestSwiftType313()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType313", new object[] { });
            Assert.Equal(1718548579748602589, result);
        }
        
        [Fact]
        public static void TestSwiftType314()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType314", new object[] { });
            Assert.Equal(6752650262500334937, result);
        }
        
        [Fact]
        public static void TestSwiftType315()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType315", new object[] { });
            Assert.Equal(1669901293133599845, result);
        }
        
        [Fact]
        public static void TestSwiftType316()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType316", new object[] { });
            Assert.Equal(5780391959566276186, result);
        }
        
        [Fact]
        public static void TestSwiftType317()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType317", new object[] { });
            Assert.Equal(-2649507594516546671, result);
        }
        
        [Fact]
        public static void TestSwiftType318()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType318", new object[] { });
            Assert.Equal(-3843114812536738695, result);
        }
        
        [Fact]
        public static void TestSwiftType319()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType319", new object[] { });
            Assert.Equal(564856539678866074, result);
        }
        
        [Fact]
        public static void TestSwiftType320()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType320", new object[] { });
            Assert.Equal(-4930850372708223179, result);
        }
        
        [Fact]
        public static void TestSwiftType321()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType321", new object[] { });
            Assert.Equal(4846025311995925664, result);
        }
        
        [Fact]
        public static void TestSwiftType322()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType322", new object[] { });
            Assert.Equal(-4834024210082438503, result);
        }
        
        [Fact]
        public static void TestSwiftType323()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType323", new object[] { });
            Assert.Equal(-4047060341357433926, result);
        }
        
        [Fact]
        public static void TestSwiftType324()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType324", new object[] { });
            Assert.Equal(-6818112558983887280, result);
        }
        
        [Fact]
        public static void TestSwiftType325()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType325", new object[] { });
            Assert.Equal(649991725034402779, result);
        }
        
        [Fact]
        public static void TestSwiftType326()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType326", new object[] { });
            Assert.Equal(-5970781214029323595, result);
        }
        
        [Fact]
        public static void TestSwiftType327()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType327", new object[] { });
            Assert.Equal(2941980381236318257, result);
        }
        
        [Fact]
        public static void TestSwiftType328()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType328", new object[] { });
            Assert.Equal(8914815933085291204, result);
        }
        
        [Fact]
        public static void TestSwiftType329()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType329", new object[] { });
            Assert.Equal(-4858187451163638360, result);
        }
        
        [Fact]
        public static void TestSwiftType330()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType330", new object[] { });
            Assert.Equal(-3277769636860925079, result);
        }
        
        [Fact]
        public static void TestSwiftType331()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType331", new object[] { });
            Assert.Equal(-3902262935105108267, result);
        }
        
        [Fact]
        public static void TestSwiftType332()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType332", new object[] { });
            Assert.Equal(1290948625416397098, result);
        }
        
        [Fact]
        public static void TestSwiftType333()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType333", new object[] { });
            Assert.Equal(-3524064562037935227, result);
        }
        
        [Fact]
        public static void TestSwiftType334()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType334", new object[] { });
            Assert.Equal(-1858690360864303906, result);
        }
        
        [Fact]
        public static void TestSwiftType335()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType335", new object[] { });
            Assert.Equal(-334487821673350542, result);
        }
        
        [Fact]
        public static void TestSwiftType336()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType336", new object[] { });
            Assert.Equal(-7266192344694219418, result);
        }
        
        [Fact]
        public static void TestSwiftType337()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType337", new object[] { });
            Assert.Equal(8973148690506940174, result);
        }
        
        [Fact]
        public static void TestSwiftType338()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType338", new object[] { });
            Assert.Equal(5224909108301209218, result);
        }
        
        [Fact]
        public static void TestSwiftType339()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType339", new object[] { });
            Assert.Equal(2602199087732930739, result);
        }
        
        [Fact]
        public static void TestSwiftType340()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType340", new object[] { });
            Assert.Equal(-5092613311314256162, result);
        }
        
        [Fact]
        public static void TestSwiftType341()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType341", new object[] { });
            Assert.Equal(4592629004123936242, result);
        }
        
        [Fact]
        public static void TestSwiftType342()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType342", new object[] { });
            Assert.Equal(2961133842124433026, result);
        }
        
        [Fact]
        public static void TestSwiftType343()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType343", new object[] { });
            Assert.Equal(-1678085087134853366, result);
        }
        
        [Fact]
        public static void TestSwiftType344()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType344", new object[] { });
            Assert.Equal(-1105421971532693998, result);
        }
        
        [Fact]
        public static void TestSwiftType345()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType345", new object[] { });
            Assert.Equal(-2832997306306529749, result);
        }
        
        [Fact]
        public static void TestSwiftType346()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType346", new object[] { });
            Assert.Equal(-6969258921184097051, result);
        }
        
        [Fact]
        public static void TestSwiftType347()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType347", new object[] { });
            Assert.Equal(279596736138991104, result);
        }
        
        [Fact]
        public static void TestSwiftType348()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType348", new object[] { });
            Assert.Equal(5602327263456262086, result);
        }
        
        [Fact]
        public static void TestSwiftType349()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType349", new object[] { });
            Assert.Equal(-8597986116169910690, result);
        }
        
        [Fact]
        public static void TestSwiftType350()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType350", new object[] { });
            Assert.Equal(8923789696140165270, result);
        }
        
        [Fact]
        public static void TestSwiftType351()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType351", new object[] { });
            Assert.Equal(-1336053951289096330, result);
        }
        
        [Fact]
        public static void TestSwiftType352()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType352", new object[] { });
            Assert.Equal(-131802630901555874, result);
        }
        
        [Fact]
        public static void TestSwiftType353()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType353", new object[] { });
            Assert.Equal(-6625810836787257284, result);
        }
        
        [Fact]
        public static void TestSwiftType354()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType354", new object[] { });
            Assert.Equal(5270619647366752580, result);
        }
        
        [Fact]
        public static void TestSwiftType355()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType355", new object[] { });
            Assert.Equal(-1701163299766814043, result);
        }
        
        [Fact]
        public static void TestSwiftType356()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType356", new object[] { });
            Assert.Equal(-1550705282006029491, result);
        }
        
        [Fact]
        public static void TestSwiftType357()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType357", new object[] { });
            Assert.Equal(6681987026482264083, result);
        }
        
        [Fact]
        public static void TestSwiftType358()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType358", new object[] { });
            Assert.Equal(-2033315680351275623, result);
        }
        
        [Fact]
        public static void TestSwiftType359()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType359", new object[] { });
            Assert.Equal(6973787733211351142, result);
        }
        
        [Fact]
        public static void TestSwiftType360()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType360", new object[] { });
            Assert.Equal(3664947251085737281, result);
        }
        
        [Fact]
        public static void TestSwiftType361()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType361", new object[] { });
            Assert.Equal(415565234096897685, result);
        }
        
        [Fact]
        public static void TestSwiftType362()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType362", new object[] { });
            Assert.Equal(295801482863592169, result);
        }
        
        [Fact]
        public static void TestSwiftType363()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType363", new object[] { });
            Assert.Equal(-4847418080359704006, result);
        }
        
        [Fact]
        public static void TestSwiftType364()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType364", new object[] { });
            Assert.Equal(-6034347456074523955, result);
        }
        
        [Fact]
        public static void TestSwiftType365()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType365", new object[] { });
            Assert.Equal(5611645301987577034, result);
        }
        
        [Fact]
        public static void TestSwiftType366()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType366", new object[] { });
            Assert.Equal(-1960305953532885815, result);
        }
        
        [Fact]
        public static void TestSwiftType367()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType367", new object[] { });
            Assert.Equal(-3785633265034196563, result);
        }
        
        [Fact]
        public static void TestSwiftType368()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType368", new object[] { });
            Assert.Equal(-6551340915042681677, result);
        }
        
        [Fact]
        public static void TestSwiftType369()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType369", new object[] { });
            Assert.Equal(-6949495942111874244, result);
        }
        
        [Fact]
        public static void TestSwiftType370()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType370", new object[] { });
            Assert.Equal(-2439014749661513945, result);
        }
        
        [Fact]
        public static void TestSwiftType371()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType371", new object[] { });
            Assert.Equal(-8319675326568653235, result);
        }
        
        [Fact]
        public static void TestSwiftType372()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType372", new object[] { });
            Assert.Equal(841649437616840460, result);
        }
        
        [Fact]
        public static void TestSwiftType373()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType373", new object[] { });
            Assert.Equal(-1740739174720849383, result);
        }
        
        [Fact]
        public static void TestSwiftType374()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType374", new object[] { });
            Assert.Equal(-8488642455115438639, result);
        }
        
        [Fact]
        public static void TestSwiftType375()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType375", new object[] { });
            Assert.Equal(7678767092711717444, result);
        }
        
        [Fact]
        public static void TestSwiftType376()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType376", new object[] { });
            Assert.Equal(-2534172109392825488, result);
        }
        
        [Fact]
        public static void TestSwiftType377()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType377", new object[] { });
            Assert.Equal(-5726993050876658753, result);
        }
        
        [Fact]
        public static void TestSwiftType378()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType378", new object[] { });
            Assert.Equal(-5273716212365502184, result);
        }
        
        [Fact]
        public static void TestSwiftType379()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType379", new object[] { });
            Assert.Equal(3147555241696120016, result);
        }
        
        [Fact]
        public static void TestSwiftType380()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType380", new object[] { });
            Assert.Equal(4054482155005731604, result);
        }
        
        [Fact]
        public static void TestSwiftType381()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType381", new object[] { });
            Assert.Equal(641368654376073872, result);
        }
        
        [Fact]
        public static void TestSwiftType382()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType382", new object[] { });
            Assert.Equal(-2432570045665555742, result);
        }
        
        [Fact]
        public static void TestSwiftType383()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType383", new object[] { });
            Assert.Equal(6569272560158614758, result);
        }
        
        [Fact]
        public static void TestSwiftType384()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType384", new object[] { });
            Assert.Equal(7068325176092208113, result);
        }
        
        [Fact]
        public static void TestSwiftType385()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType385", new object[] { });
            Assert.Equal(4635659444355057900, result);
        }
        
        [Fact]
        public static void TestSwiftType386()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType386", new object[] { });
            Assert.Equal(6276434994091822532, result);
        }
        
        [Fact]
        public static void TestSwiftType387()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType387", new object[] { });
            Assert.Equal(-7178683098914978415, result);
        }
        
        [Fact]
        public static void TestSwiftType388()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType388", new object[] { });
            Assert.Equal(605032694565748564, result);
        }
        
        [Fact]
        public static void TestSwiftType389()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType389", new object[] { });
            Assert.Equal(2766821019775738384, result);
        }
        
        [Fact]
        public static void TestSwiftType390()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType390", new object[] { });
            Assert.Equal(815233715740760203, result);
        }
        
        [Fact]
        public static void TestSwiftType391()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType391", new object[] { });
            Assert.Equal(-8969823465370983724, result);
        }
        
        [Fact]
        public static void TestSwiftType392()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType392", new object[] { });
            Assert.Equal(-9169579336628190189, result);
        }
        
        [Fact]
        public static void TestSwiftType393()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType393", new object[] { });
            Assert.Equal(7250428304757445709, result);
        }
        
        [Fact]
        public static void TestSwiftType394()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType394", new object[] { });
            Assert.Equal(5586363182792468972, result);
        }
        
        [Fact]
        public static void TestSwiftType395()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType395", new object[] { });
            Assert.Equal(-8193209147041370451, result);
        }
        
        [Fact]
        public static void TestSwiftType396()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType396", new object[] { });
            Assert.Equal(2937495561540670839, result);
        }
        
        [Fact]
        public static void TestSwiftType397()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType397", new object[] { });
            Assert.Equal(-6860674082066421230, result);
        }
        
        [Fact]
        public static void TestSwiftType398()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType398", new object[] { });
            Assert.Equal(5260672631800259017, result);
        }
        
        [Fact]
        public static void TestSwiftType399()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType399", new object[] { });
            Assert.Equal(-8810323782677945282, result);
        }
        
        [Fact]
        public static void TestSwiftType400()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType400", new object[] { });
            Assert.Equal(7727777822775495554, result);
        }
        
        [Fact]
        public static void TestSwiftType401()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType401", new object[] { });
            Assert.Equal(-6915587708849107502, result);
        }
        
        [Fact]
        public static void TestSwiftType402()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType402", new object[] { });
            Assert.Equal(-4966739196006501207, result);
        }
        
        [Fact]
        public static void TestSwiftType403()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType403", new object[] { });
            Assert.Equal(8654043253704010916, result);
        }
        
        [Fact]
        public static void TestSwiftType404()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType404", new object[] { });
            Assert.Equal(6519717980839005112, result);
        }
        
        [Fact]
        public static void TestSwiftType405()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType405", new object[] { });
            Assert.Equal(3459217808417385212, result);
        }
        
        [Fact]
        public static void TestSwiftType406()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType406", new object[] { });
            Assert.Equal(-4575982977251622046, result);
        }
        
        [Fact]
        public static void TestSwiftType407()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType407", new object[] { });
            Assert.Equal(5578769012424498626, result);
        }
        
        [Fact]
        public static void TestSwiftType408()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType408", new object[] { });
            Assert.Equal(6966325052094053556, result);
        }
        
        [Fact]
        public static void TestSwiftType409()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType409", new object[] { });
            Assert.Equal(-509761177789724739, result);
        }
        
        [Fact]
        public static void TestSwiftType410()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType410", new object[] { });
            Assert.Equal(-5018197425982911939, result);
        }
        
        [Fact]
        public static void TestSwiftType411()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType411", new object[] { });
            Assert.Equal(-6260927431643092310, result);
        }
        
        [Fact]
        public static void TestSwiftType412()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType412", new object[] { });
            Assert.Equal(-4247982083591395051, result);
        }
        
        [Fact]
        public static void TestSwiftType413()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType413", new object[] { });
            Assert.Equal(6527952547738705482, result);
        }
        
        [Fact]
        public static void TestSwiftType414()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType414", new object[] { });
            Assert.Equal(7985770896057025413, result);
        }
        
        [Fact]
        public static void TestSwiftType415()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType415", new object[] { });
            Assert.Equal(3820921403140653113, result);
        }
        
        [Fact]
        public static void TestSwiftType416()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType416", new object[] { });
            Assert.Equal(-3284314349373785262, result);
        }
        
        [Fact]
        public static void TestSwiftType417()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType417", new object[] { });
            Assert.Equal(8769838448971532803, result);
        }
        
        [Fact]
        public static void TestSwiftType418()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType418", new object[] { });
            Assert.Equal(180381667452574854, result);
        }
        
        [Fact]
        public static void TestSwiftType419()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType419", new object[] { });
            Assert.Equal(3860903177905724486, result);
        }
        
        [Fact]
        public static void TestSwiftType420()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType420", new object[] { });
            Assert.Equal(4680556141452481621, result);
        }
        
        [Fact]
        public static void TestSwiftType421()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType421", new object[] { });
            Assert.Equal(8918745350502716139, result);
        }
        
        [Fact]
        public static void TestSwiftType422()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType422", new object[] { });
            Assert.Equal(7440258612429021759, result);
        }
        
        [Fact]
        public static void TestSwiftType423()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType423", new object[] { });
            Assert.Equal(-7783235671149717910, result);
        }
        
        [Fact]
        public static void TestSwiftType424()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType424", new object[] { });
            Assert.Equal(-1228974309994701654, result);
        }
        
        [Fact]
        public static void TestSwiftType425()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType425", new object[] { });
            Assert.Equal(648102651988741497, result);
        }
        
        [Fact]
        public static void TestSwiftType426()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType426", new object[] { });
            Assert.Equal(-5808625042874858702, result);
        }
        
        [Fact]
        public static void TestSwiftType427()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType427", new object[] { });
            Assert.Equal(535508779590666133, result);
        }
        
        [Fact]
        public static void TestSwiftType428()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType428", new object[] { });
            Assert.Equal(6840370006887115940, result);
        }
        
        [Fact]
        public static void TestSwiftType429()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType429", new object[] { });
            Assert.Equal(818319554451827883, result);
        }
        
        [Fact]
        public static void TestSwiftType430()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType430", new object[] { });
            Assert.Equal(-3016789913300536077, result);
        }
        
        [Fact]
        public static void TestSwiftType431()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType431", new object[] { });
            Assert.Equal(-9210708301318953955, result);
        }
        
        [Fact]
        public static void TestSwiftType432()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType432", new object[] { });
            Assert.Equal(-3232107605133056497, result);
        }
        
        [Fact]
        public static void TestSwiftType433()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType433", new object[] { });
            Assert.Equal(-5500828122832595486, result);
        }
        
        [Fact]
        public static void TestSwiftType434()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType434", new object[] { });
            Assert.Equal(-403789833093848052, result);
        }
        
        [Fact]
        public static void TestSwiftType435()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType435", new object[] { });
            Assert.Equal(-7442437356528311699, result);
        }
        
        [Fact]
        public static void TestSwiftType436()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType436", new object[] { });
            Assert.Equal(1691835474197242426, result);
        }
        
        [Fact]
        public static void TestSwiftType437()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType437", new object[] { });
            Assert.Equal(-4901368629152969742, result);
        }
        
        [Fact]
        public static void TestSwiftType438()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType438", new object[] { });
            Assert.Equal(-3636991592063641174, result);
        }
        
        [Fact]
        public static void TestSwiftType439()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType439", new object[] { });
            Assert.Equal(5397481060424965145, result);
        }
        
        [Fact]
        public static void TestSwiftType440()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType440", new object[] { });
            Assert.Equal(-605185230668150482, result);
        }
        
        [Fact]
        public static void TestSwiftType441()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType441", new object[] { });
            Assert.Equal(2860442675980140193, result);
        }
        
        [Fact]
        public static void TestSwiftType442()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType442", new object[] { });
            Assert.Equal(-2409143221206391557, result);
        }
        
        [Fact]
        public static void TestSwiftType443()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType443", new object[] { });
            Assert.Equal(598478454465173457, result);
        }
        
        [Fact]
        public static void TestSwiftType444()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType444", new object[] { });
            Assert.Equal(-750802910854975063, result);
        }
        
        [Fact]
        public static void TestSwiftType445()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType445", new object[] { });
            Assert.Equal(-7779067967876068949, result);
        }
        
        [Fact]
        public static void TestSwiftType446()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType446", new object[] { });
            Assert.Equal(6740833353374069276, result);
        }
        
        [Fact]
        public static void TestSwiftType447()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType447", new object[] { });
            Assert.Equal(-4277300291582903233, result);
        }
        
        [Fact]
        public static void TestSwiftType448()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType448", new object[] { });
            Assert.Equal(4635659444355057900, result);
        }
        
        [Fact]
        public static void TestSwiftType449()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType449", new object[] { });
            Assert.Equal(-7711992276714047289, result);
        }
        
        [Fact]
        public static void TestSwiftType450()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType450", new object[] { });
            Assert.Equal(7255994502266463888, result);
        }
        
        [Fact]
        public static void TestSwiftType451()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType451", new object[] { });
            Assert.Equal(3471684984972755719, result);
        }
        
        [Fact]
        public static void TestSwiftType452()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType452", new object[] { });
            Assert.Equal(-7518657982614324457, result);
        }
        
        [Fact]
        public static void TestSwiftType453()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType453", new object[] { });
            Assert.Equal(-4745752112673179602, result);
        }
        
        [Fact]
        public static void TestSwiftType454()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType454", new object[] { });
            Assert.Equal(590684067820433389, result);
        }
        
        [Fact]
        public static void TestSwiftType455()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType455", new object[] { });
            Assert.Equal(-5657265487867349989, result);
        }
        
        [Fact]
        public static void TestSwiftType456()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType456", new object[] { });
            Assert.Equal(8722873399830605424, result);
        }
        
        [Fact]
        public static void TestSwiftType457()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType457", new object[] { });
            Assert.Equal(-4371179691031244903, result);
        }
        
        [Fact]
        public static void TestSwiftType458()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType458", new object[] { });
            Assert.Equal(-3794336876723658367, result);
        }
        
        [Fact]
        public static void TestSwiftType459()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType459", new object[] { });
            Assert.Equal(322182842193855259, result);
        }
        
        [Fact]
        public static void TestSwiftType460()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType460", new object[] { });
            Assert.Equal(6237885446262797985, result);
        }
        
        [Fact]
        public static void TestSwiftType461()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType461", new object[] { });
            Assert.Equal(-622298591155045021, result);
        }
        
        [Fact]
        public static void TestSwiftType462()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType462", new object[] { });
            Assert.Equal(-6521523735227058600, result);
        }
        
        [Fact]
        public static void TestSwiftType463()
        {
            long result = (long)TestsHelper.Execute(_assemblyPath, "Test.MainClass", "SwiftType463", new object[] { });
            Assert.Equal(-7180632988021255058, result);
        }
        
    }
}
