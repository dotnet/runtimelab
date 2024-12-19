// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.StressTests
{
    public class StaticMethodsTests : IClassFixture<StaticMethodsTests.TestFixture>
    {
        private readonly TestFixture _fixture;

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
                // Initialize
            }
        }

        [Fact]
        public static void TestSwiftType0()
        {
            long result = Swift.StaticMethodsTests.Type1.swiftFunc0(9, 57);
            Assert.Equal(-1302454221810123473, result);
        }

        [Fact]
        public static void TestSwiftType1()
        {
            long result = Swift.StaticMethodsTests.Type1.Type1Sub2.swiftFunc0(30);
            Assert.Equal(914300229919721579, result);
        }

        [Fact]
        public static void TestSwiftType2()
        {
            long result = Swift.StaticMethodsTests.Type1.Type1Sub2.Type1Sub2Sub3.swiftFunc0(47, 19, 43, 56, 17, 57, 45, 77, 7);
            Assert.Equal(3606159555131430051, result);
        }

        [Fact]
        public static void TestSwiftType3()
        {
            long result = Swift.StaticMethodsTests.Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.swiftFunc0(10, 65, 2, 68.10, 84, 17, 75, 89, 94, 28);
            Assert.Equal(9085678888513549564, result);
        }

        [Fact]
        public static void TestSwiftType4()
        {
            long result = Swift.StaticMethodsTests.Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.Type1Sub2Sub3Sub4Sub5.swiftFunc0(38, 55, 77, 5, 37);
            Assert.Equal(-9013520609104109583, result);
        }

        [Fact]
        public static void TestSwiftType5()
        {
            long result = Swift.StaticMethodsTests.Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.Type1Sub2Sub3Sub4Sub5.Type1Sub2Sub3Sub4Sub5Sub6.swiftFunc0(12, 99, 15, 55, 52, 25, 59, 22, 5, 73.35);
            Assert.Equal(27416593309743651, result);
        }

        [Fact]
        public static void TestSwiftType6()
        {
            long result = Swift.StaticMethodsTests.Type2.swiftFunc0(83, 32, 43, 15, 46, 4);
            Assert.Equal(3661604645194525580, result);
        }

        [Fact]
        public static void TestSwiftType7()
        {
            long result = Swift.StaticMethodsTests.Type2.Type2Sub2.swiftFunc0(97, 12, 53);
            Assert.Equal(-3025493081346654563, result);
        }

        [Fact]
        public static void TestSwiftType8()
        {
            long result = Swift.StaticMethodsTests.Type2.Type2Sub2.Type2Sub2Sub3.swiftFunc0(85, 19, 5, 30, 75.30, 53, 42, 50);
            Assert.Equal(-7677466411347177033, result);
        }

        [Fact]
        public static void TestSwiftType9()
        {
            long result = Swift.StaticMethodsTests.Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.swiftFunc0(22, 12, 62, 99, 10, 82, 19, 11, 36);
            Assert.Equal(-2253623701143287732, result);
        }

        [Fact]
        public static void TestSwiftType10()
        {
            long result = Swift.StaticMethodsTests.Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.swiftFunc0(71, 21, 76, 44.79, 13, 6.64, 85, 90, 88.02);
            Assert.Equal(8019726010431750353, result);
        }

        [Fact]
        public static void TestSwiftType11()
        {
            long result = Swift.StaticMethodsTests.Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.swiftFunc0(1, 66, 48, 11, 74.86, 29, 2);
            Assert.Equal(3146418414537113518, result);
        }

        [Fact]
        public static void TestSwiftType12()
        {
            long result = Swift.StaticMethodsTests.Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.Type2Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(54, 28.87, 1, 56, 41.63);
            Assert.Equal(1803324178910069028, result);
        }

        [Fact]
        public static void TestSwiftType13()
        {
            long result = Swift.StaticMethodsTests.Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.Type2Sub2Sub3Sub4Sub5Sub6Sub7.Type2Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(5, 48.78, 13.27, 28, 60.92);
            Assert.Equal(4689617795014579452, result);
        }

        [Fact]
        public static void TestSwiftType14()
        {
            long result = Swift.StaticMethodsTests.Type3.swiftFunc0(64, 48, 10, 20, 57, 18, 98);
            Assert.Equal(6289251196731842658, result);
        }

        [Fact]
        public static void TestSwiftType15()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.swiftFunc0(15.44, 57, 64.31, 35, 67, 12, 96, 52.06, 19);
            Assert.Equal(391791165974922649, result);
        }

        [Fact]
        public static void TestSwiftType16()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.swiftFunc0(96);
            Assert.Equal(621294471543772429, result);
        }

        [Fact]
        public static void TestSwiftType17()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.swiftFunc0(18, 60);
            Assert.Equal(607854041403170315, result);
        }

        [Fact]
        public static void TestSwiftType18()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.swiftFunc0(17, 4, 63.40);
            Assert.Equal(-3483333800069613251, result);
        }

        [Fact]
        public static void TestSwiftType19()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.swiftFunc0(48, 14, 10.64, 2, 43, 8.57);
            Assert.Equal(-5674147374399610801, result);
        }

        [Fact]
        public static void TestSwiftType20()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(0.69);
            Assert.Equal(-5106234148504633478, result);
        }

        [Fact]
        public static void TestSwiftType21()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(60.15, 75, 37, 70, 13, 44);
            Assert.Equal(246133781151241632, result);
        }

        [Fact]
        public static void TestSwiftType22()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(35, 74.04, 98, 8);
            Assert.Equal(9123574604720661329, result);
        }

        [Fact]
        public static void TestSwiftType23()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(45, 48, 20, 65, 98, 74, 81);
            Assert.Equal(-3515963896618663036, result);
        }

        [Fact]
        public static void TestSwiftType24()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0(9);
            Assert.Equal(-9105318085603802964, result);
        }

        [Fact]
        public static void TestSwiftType25()
        {
            long result = Swift.StaticMethodsTests.Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11Sub12.swiftFunc0(98, 74.69, 32.29, 13, 53);
            Assert.Equal(8436092602422195875, result);
        }

        [Fact]
        public static void TestSwiftType26()
        {
            long result = Swift.StaticMethodsTests.Type4.swiftFunc0(51, 53, 38.12, 86, 91.96, 20, 82, 73, 38, 56);
            Assert.Equal(4414091805691157982, result);
        }

        [Fact]
        public static void TestSwiftType27()
        {
            long result = Swift.StaticMethodsTests.Type5.swiftFunc0(0, 82.91, 50, 84, 85, 90, 87, 49);
            Assert.Equal(5482643438640306451, result);
        }

        [Fact]
        public static void TestSwiftType28()
        {
            long result = Swift.StaticMethodsTests.Type5.Type5Sub2.swiftFunc0(77, 64, 77, 57, 60, 68, 61.72, 38, 77, 4.87);
            Assert.Equal(-7531537036341229865, result);
        }

        [Fact]
        public static void TestSwiftType29()
        {
            long result = Swift.StaticMethodsTests.Type6.swiftFunc0(51, 81, 5, 12, 12, 19);
            Assert.Equal(-110785621188401307, result);
        }

        [Fact]
        public static void TestSwiftType30()
        {
            long result = Swift.StaticMethodsTests.Type6.Type6Sub2.swiftFunc0(68, 97, 7, 1, 63, 9, 43, 73);
            Assert.Equal(4661143371282223404, result);
        }

        [Fact]
        public static void TestSwiftType31()
        {
            long result = Swift.StaticMethodsTests.Type6.Type6Sub2.Type6Sub2Sub3.swiftFunc0(84, 74, 75, 56);
            Assert.Equal(-5437539143661417520, result);
        }

        [Fact]
        public static void TestSwiftType32()
        {
            long result = Swift.StaticMethodsTests.Type6.Type6Sub2.Type6Sub2Sub3.Type6Sub2Sub3Sub4.swiftFunc0(58, 96, 45, 73);
            Assert.Equal(-8564140570796838325, result);
        }

        [Fact]
        public static void TestSwiftType33()
        {
            long result = Swift.StaticMethodsTests.Type6.Type6Sub2.Type6Sub2Sub3.Type6Sub2Sub3Sub4.Type6Sub2Sub3Sub4Sub5.swiftFunc0(1, 81, 27, 91, 76, 66.80, 60, 42, 11, 76);
            Assert.Equal(127893875284315364, result);
        }

        [Fact]
        public static void TestSwiftType34()
        {
            long result = Swift.StaticMethodsTests.Type7.swiftFunc0(64, 87, 60, 64);
            Assert.Equal(1670198715257327092, result);
        }

        [Fact]
        public static void TestSwiftType35()
        {
            long result = Swift.StaticMethodsTests.Type7.Type7Sub2.swiftFunc0(28, 54, 53);
            Assert.Equal(-580800586705400168, result);
        }

        [Fact]
        public static void TestSwiftType36()
        {
            long result = Swift.StaticMethodsTests.Type7.Type7Sub2.Type7Sub2Sub3.swiftFunc0(43, 97, 52, 24, 30.99, 26);
            Assert.Equal(-6671430655764225195, result);
        }

        [Fact]
        public static void TestSwiftType37()
        {
            long result = Swift.StaticMethodsTests.Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.swiftFunc0(100, 26.83, 24, 81, 65, 75, 92.49, 100, 4);
            Assert.Equal(-2879189024178887556, result);
        }

        [Fact]
        public static void TestSwiftType38()
        {
            long result = Swift.StaticMethodsTests.Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.Type7Sub2Sub3Sub4Sub5.swiftFunc0(68, 100, 74.14, 61);
            Assert.Equal(-57788390475521057, result);
        }

        [Fact]
        public static void TestSwiftType39()
        {
            long result = Swift.StaticMethodsTests.Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.Type7Sub2Sub3Sub4Sub5.Type7Sub2Sub3Sub4Sub5Sub6.swiftFunc0(66, 37, 26, 66, 10, 34, 3.57, 94, 84);
            Assert.Equal(8161332097230727509, result);
        }

        [Fact]
        public static void TestSwiftType40()
        {
            long result = Swift.StaticMethodsTests.Type8.swiftFunc0(43, 99, 22.16);
            Assert.Equal(7939635022138285726, result);
        }

        [Fact]
        public static void TestSwiftType41()
        {
            long result = Swift.StaticMethodsTests.Type8.Type8Sub2.swiftFunc0(28, 62, 100);
            Assert.Equal(-5638617594007136487, result);
        }

        [Fact]
        public static void TestSwiftType42()
        {
            long result = Swift.StaticMethodsTests.Type8.Type8Sub2.Type8Sub2Sub3.swiftFunc0(67, 18, 9, 23, 64, 87, 35, 26, 49, 92);
            Assert.Equal(-6488902911358175373, result);
        }

        [Fact]
        public static void TestSwiftType43()
        {
            long result = Swift.StaticMethodsTests.Type8.Type8Sub2.Type8Sub2Sub3.Type8Sub2Sub3Sub4.swiftFunc0(13, 10, 40, 37.89, 71);
            Assert.Equal(-8040904851637460412, result);
        }

        [Fact]
        public static void TestSwiftType44()
        {
            long result = Swift.StaticMethodsTests.Type8.Type8Sub2.Type8Sub2Sub3.Type8Sub2Sub3Sub4.Type8Sub2Sub3Sub4Sub5.swiftFunc0(71, 38.64, 24, 43, 69, 60.10);
            Assert.Equal(-7336859208496404469, result);
        }

        [Fact]
        public static void TestSwiftType45()
        {
            long result = Swift.StaticMethodsTests.Type9.swiftFunc0(20, 74, 55, 56, 72, 12);
            Assert.Equal(6345937011278591838, result);
        }

        [Fact]
        public static void TestSwiftType46()
        {
            long result = Swift.StaticMethodsTests.Type9.Type9Sub2.swiftFunc0(29, 88, 15.50, 99, 55);
            Assert.Equal(-5325095042452577111, result);
        }

        [Fact]
        public static void TestSwiftType47()
        {
            long result = Swift.StaticMethodsTests.Type9.Type9Sub2.Type9Sub2Sub3.swiftFunc0(24, 77, 0, 19, 67, 12, 32.25, 84);
            Assert.Equal(-861931995212022056, result);
        }

        [Fact]
        public static void TestSwiftType48()
        {
            long result = Swift.StaticMethodsTests.Type10.swiftFunc0(46, 35, 43, 3, 1, 72, 3, 5);
            Assert.Equal(7332186240727164655, result);
        }

        [Fact]
        public static void TestSwiftType49()
        {
            long result = Swift.StaticMethodsTests.Type10.Type10Sub2.swiftFunc0(45, 3, 97, 48, 100);
            Assert.Equal(5015549653232840682, result);
        }

        [Fact]
        public static void TestSwiftType50()
        {
            long result = Swift.StaticMethodsTests.Type10.Type10Sub2.Type10Sub2Sub3.swiftFunc0(99, 64, 18.04, 48);
            Assert.Equal(-1759411520443392441, result);
        }

        [Fact]
        public static void TestSwiftType51()
        {
            long result = Swift.StaticMethodsTests.Type11.swiftFunc0(33, 7);
            Assert.Equal(4361279221094392059, result);
        }

        [Fact]
        public static void TestSwiftType52()
        {
            long result = Swift.StaticMethodsTests.Type11.Type11Sub2.swiftFunc0(35);
            Assert.Equal(-1699583181824442426, result);
        }

        [Fact]
        public static void TestSwiftType53()
        {
            long result = Swift.StaticMethodsTests.Type11.Type11Sub2.Type11Sub2Sub3.swiftFunc0(77, 76, 16.20, 96, 15.68, 67, 51, 76, 73);
            Assert.Equal(-8575366906149444658, result);
        }

        [Fact]
        public static void TestSwiftType54()
        {
            long result = Swift.StaticMethodsTests.Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.swiftFunc0(60, 68.52, 47, 63, 77, 14, 73.90, 69, 1, 81);
            Assert.Equal(5229487626593371452, result);
        }

        [Fact]
        public static void TestSwiftType55()
        {
            long result = Swift.StaticMethodsTests.Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.swiftFunc0(53, 33, 87.06);
            Assert.Equal(-3892962395563826067, result);
        }

        [Fact]
        public static void TestSwiftType56()
        {
            long result = Swift.StaticMethodsTests.Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.swiftFunc0(90, 75, 50, 38);
            Assert.Equal(-2208890062033214998, result);
        }

        [Fact]
        public static void TestSwiftType57()
        {
            long result = Swift.StaticMethodsTests.Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.Type11Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(58.29, 93.54, 8, 68);
            Assert.Equal(-2497407779594054558, result);
        }

        [Fact]
        public static void TestSwiftType58()
        {
            long result = Swift.StaticMethodsTests.Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.Type11Sub2Sub3Sub4Sub5Sub6Sub7.Type11Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(5.14, 6, 80, 77, 27, 23, 3, 19, 94, 77);
            Assert.Equal(324174718095320818, result);
        }

        [Fact]
        public static void TestSwiftType59()
        {
            long result = Swift.StaticMethodsTests.Type12.swiftFunc0(40.61, 36, 29, 91, 96, 86);
            Assert.Equal(-1332509595342217973, result);
        }

        [Fact]
        public static void TestSwiftType60()
        {
            long result = Swift.StaticMethodsTests.Type12.Type12Sub2.swiftFunc0(71, 39, 34.07);
            Assert.Equal(4081230995526660101, result);
        }

        [Fact]
        public static void TestSwiftType61()
        {
            long result = Swift.StaticMethodsTests.Type13.swiftFunc0(9, 9, 60.59, 96, 66, 61.51, 53, 82);
            Assert.Equal(-3737054516232390576, result);
        }

        [Fact]
        public static void TestSwiftType62()
        {
            long result = Swift.StaticMethodsTests.Type13.Type13Sub2.swiftFunc0(55, 100);
            Assert.Equal(-2635903538728387146, result);
        }

        [Fact]
        public static void TestSwiftType63()
        {
            long result = Swift.StaticMethodsTests.Type13.Type13Sub2.Type13Sub2Sub3.swiftFunc0(17, 21.05, 47, 13, 99);
            Assert.Equal(-6625241565325616919, result);
        }

        [Fact]
        public static void TestSwiftType64()
        {
            long result = Swift.StaticMethodsTests.Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.swiftFunc0(70, 0, 87, 57);
            Assert.Equal(-2829834324768398881, result);
        }

        [Fact]
        public static void TestSwiftType65()
        {
            long result = Swift.StaticMethodsTests.Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.swiftFunc0(28, 69, 10.54, 64, 13, 17.41, 81, 33.57);
            Assert.Equal(-4954353219915555130, result);
        }

        [Fact]
        public static void TestSwiftType66()
        {
            long result = Swift.StaticMethodsTests.Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.Type13Sub2Sub3Sub4Sub5Sub6.swiftFunc0(38, 3);
            Assert.Equal(7241753510039977648, result);
        }

        [Fact]
        public static void TestSwiftType67()
        {
            long result = Swift.StaticMethodsTests.Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.Type13Sub2Sub3Sub4Sub5Sub6.Type13Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(56, 68, 67.66);
            Assert.Equal(-1832824433293148886, result);
        }

        [Fact]
        public static void TestSwiftType68()
        {
            long result = Swift.StaticMethodsTests.Type14.swiftFunc0(39, 11.26, 72, 88, 24, 27, 48, 76, 31.63);
            Assert.Equal(901199712025494241, result);
        }

        [Fact]
        public static void TestSwiftType69()
        {
            long result = Swift.StaticMethodsTests.Type15.swiftFunc0(64, 20, 8, 36, 74, 77, 91, 24.38, 27);
            Assert.Equal(7394107227683775943, result);
        }

        [Fact]
        public static void TestSwiftType70()
        {
            long result = Swift.StaticMethodsTests.Type15.Type15Sub2.swiftFunc0(84.10, 66.46, 5, 11, 10, 95, 26, 26, 35);
            Assert.Equal(1357280204861671662, result);
        }

        [Fact]
        public static void TestSwiftType71()
        {
            long result = Swift.StaticMethodsTests.Type15.Type15Sub2.Type15Sub2Sub3.swiftFunc0(27, 16, 78.80, 78, 20, 25, 0.02, 78, 80);
            Assert.Equal(6209167671701393392, result);
        }

        [Fact]
        public static void TestSwiftType72()
        {
            long result = Swift.StaticMethodsTests.Type16.swiftFunc0(40, 39);
            Assert.Equal(-2016241838007125046, result);
        }

        [Fact]
        public static void TestSwiftType73()
        {
            long result = Swift.StaticMethodsTests.Type16.Type16Sub2.swiftFunc0(41.36, 29.18, 79, 61.72, 98.56, 29, 1, 31);
            Assert.Equal(-2590768908127775473, result);
        }

        [Fact]
        public static void TestSwiftType74()
        {
            long result = Swift.StaticMethodsTests.Type17.swiftFunc0(4, 16, 89);
            Assert.Equal(-3415693461695316082, result);
        }

        [Fact]
        public static void TestSwiftType75()
        {
            long result = Swift.StaticMethodsTests.Type17.Type17Sub2.swiftFunc0(22, 60, 28, 68);
            Assert.Equal(1052970797754173925, result);
        }

        [Fact]
        public static void TestSwiftType76()
        {
            long result = Swift.StaticMethodsTests.Type17.Type17Sub2.Type17Sub2Sub3.swiftFunc0(7, 91, 5, 68, 92, 13, 98, 37.01);
            Assert.Equal(1284170383645516782, result);
        }

        [Fact]
        public static void TestSwiftType77()
        {
            long result = Swift.StaticMethodsTests.Type17.Type17Sub2.Type17Sub2Sub3.Type17Sub2Sub3Sub4.swiftFunc0(50, 4, 24, 41.75);
            Assert.Equal(-3361603965782237971, result);
        }

        [Fact]
        public static void TestSwiftType78()
        {
            long result = Swift.StaticMethodsTests.Type18.swiftFunc0(60.67, 91, 20, 7.71, 51, 69, 15);
            Assert.Equal(-7618842638074074063, result);
        }

        [Fact]
        public static void TestSwiftType79()
        {
            long result = Swift.StaticMethodsTests.Type18.Type18Sub2.swiftFunc0(37, 78, 60, 35);
            Assert.Equal(2001881504172846833, result);
        }

        [Fact]
        public static void TestSwiftType80()
        {
            long result = Swift.StaticMethodsTests.Type19.swiftFunc0(42, 94, 99, 14, 61);
            Assert.Equal(8780417345600432609, result);
        }

        [Fact]
        public static void TestSwiftType81()
        {
            long result = Swift.StaticMethodsTests.Type19.Type19Sub2.swiftFunc0(60.53, 26.15, 83, 10.19, 58, 44, 51);
            Assert.Equal(-8146835476385947337, result);
        }

        [Fact]
        public static void TestSwiftType82()
        {
            long result = Swift.StaticMethodsTests.Type19.Type19Sub2.Type19Sub2Sub3.swiftFunc0(73.94, 46, 18, 4.69, 93, 59, 16, 58);
            Assert.Equal(8856268358096150912, result);
        }

        [Fact]
        public static void TestSwiftType83()
        {
            long result = Swift.StaticMethodsTests.Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.swiftFunc0(37.20, 5, 15.16, 42.91, 52.97, 51);
            Assert.Equal(1319882132930383678, result);
        }

        [Fact]
        public static void TestSwiftType84()
        {
            long result = Swift.StaticMethodsTests.Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.swiftFunc0(10, 88, 33.80, 22.70, 13);
            Assert.Equal(-508146693585361758, result);
        }

        [Fact]
        public static void TestSwiftType85()
        {
            long result = Swift.StaticMethodsTests.Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.Type19Sub2Sub3Sub4Sub5Sub6.swiftFunc0(84);
            Assert.Equal(-2649507594516546671, result);
        }

        [Fact]
        public static void TestSwiftType86()
        {
            long result = Swift.StaticMethodsTests.Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.Type19Sub2Sub3Sub4Sub5Sub6.Type19Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(27, 48.79, 63, 71.75, 39, 50, 29, 70.13);
            Assert.Equal(8088375796594482986, result);
        }

        [Fact]
        public static void TestSwiftType87()
        {
            long result = Swift.StaticMethodsTests.Type20.swiftFunc0(11, 54, 49);
            Assert.Equal(1442750423798503241, result);
        }

        [Fact]
        public static void TestSwiftType88()
        {
            long result = Swift.StaticMethodsTests.Type20.Type20Sub2.swiftFunc0(12, 51, 32, 98);
            Assert.Equal(7158341730093042416, result);
        }

        [Fact]
        public static void TestSwiftType89()
        {
            long result = Swift.StaticMethodsTests.Type20.Type20Sub2.Type20Sub2Sub3.swiftFunc0(36, 65, 84, 75, 51, 50, 56, 47.91, 7);
            Assert.Equal(553531604612512814, result);
        }

        [Fact]
        public static void TestSwiftType90()
        {
            long result = Swift.StaticMethodsTests.Type20.Type20Sub2.Type20Sub2Sub3.Type20Sub2Sub3Sub4.swiftFunc0(37, 91, 82, 69, 52, 76.67, 17, 13);
            Assert.Equal(669600114590149160, result);
        }

        [Fact]
        public static void TestSwiftType91()
        {
            long result = Swift.StaticMethodsTests.Type20.Type20Sub2.Type20Sub2Sub3.Type20Sub2Sub3Sub4.Type20Sub2Sub3Sub4Sub5.swiftFunc0(50);
            Assert.Equal(577292016191472559, result);
        }

        [Fact]
        public static void TestSwiftType92()
        {
            long result = Swift.StaticMethodsTests.Type21.swiftFunc0(65, 90, 100, 91, 36, 62, 36, 41);
            Assert.Equal(8168985653034001556, result);
        }

        [Fact]
        public static void TestSwiftType93()
        {
            long result = Swift.StaticMethodsTests.Type21.Type21Sub2.swiftFunc0(88, 24, 51, 11, 70.97, 37, 34, 2);
            Assert.Equal(-4511150573256535639, result);
        }

        [Fact]
        public static void TestSwiftType94()
        {
            long result = Swift.StaticMethodsTests.Type21.Type21Sub2.Type21Sub2Sub3.swiftFunc0(33, 85, 69, 8.53, 99);
            Assert.Equal(762831339959854365, result);
        }

        [Fact]
        public static void TestSwiftType95()
        {
            long result = Swift.StaticMethodsTests.Type22.swiftFunc0(65.66, 93, 44, 53.51, 78, 39, 69, 32.52, 34);
            Assert.Equal(1749138547312475471, result);
        }

        [Fact]
        public static void TestSwiftType96()
        {
            long result = Swift.StaticMethodsTests.Type22.Type22Sub2.swiftFunc0(23);
            Assert.Equal(603119544333039874, result);
        }

        [Fact]
        public static void TestSwiftType97()
        {
            long result = Swift.StaticMethodsTests.Type22.Type22Sub2.Type22Sub2Sub3.swiftFunc0(78, 99);
            Assert.Equal(3825727416281544568, result);
        }

        [Fact]
        public static void TestSwiftType98()
        {
            long result = Swift.StaticMethodsTests.Type23.swiftFunc0(52, 2.14, 5.51, 27, 73.58, 26, 59, 64, 19, 81);
            Assert.Equal(-2876779109167562933, result);
        }

        [Fact]
        public static void TestSwiftType99()
        {
            long result = Swift.StaticMethodsTests.Type23.Type23Sub2.swiftFunc0(52, 14, 89, 47, 29, 100, 6, 58, 2);
            Assert.Equal(8604643581634903352, result);
        }

        [Fact]
        public static void TestSwiftType100()
        {
            long result = Swift.StaticMethodsTests.Type23.Type23Sub2.Type23Sub2Sub3.swiftFunc0(17, 13, 66, 35.35, 75);
            Assert.Equal(-5183461739495323880, result);
        }

        [Fact]
        public static void TestSwiftType101()
        {
            long result = Swift.StaticMethodsTests.Type24.swiftFunc0(31);
            Assert.Equal(3700353538232897146, result);
        }

        [Fact]
        public static void TestSwiftType102()
        {
            long result = Swift.StaticMethodsTests.Type24.Type24Sub2.swiftFunc0(53, 18.02, 65, 18, 65);
            Assert.Equal(-1815728824758141834, result);
        }

        [Fact]
        public static void TestSwiftType103()
        {
            long result = Swift.StaticMethodsTests.Type24.Type24Sub2.Type24Sub2Sub3.swiftFunc0(62, 79, 41, 59, 66, 17, 42);
            Assert.Equal(-6044068917807042715, result);
        }

        [Fact]
        public static void TestSwiftType104()
        {
            long result = Swift.StaticMethodsTests.Type24.Type24Sub2.Type24Sub2Sub3.Type24Sub2Sub3Sub4.swiftFunc0(4);
            Assert.Equal(-3658393639098834511, result);
        }

        [Fact]
        public static void TestSwiftType105()
        {
            long result = Swift.StaticMethodsTests.Type24.Type24Sub2.Type24Sub2Sub3.Type24Sub2Sub3Sub4.Type24Sub2Sub3Sub4Sub5.swiftFunc0(69, 34, 6, 85, 100);
            Assert.Equal(3377392297168521549, result);
        }

        [Fact]
        public static void TestSwiftType106()
        {
            long result = Swift.StaticMethodsTests.Type25.swiftFunc0(67, 33.68, 78, 47.50, 51, 93.80, 62, 46);
            Assert.Equal(-7100411163736428415, result);
        }

        [Fact]
        public static void TestSwiftType107()
        {
            long result = Swift.StaticMethodsTests.Type25.Type25Sub2.swiftFunc0(74, 38, 88, 46, 37, 45, 99, 40, 55, 12);
            Assert.Equal(-3587794744717933961, result);
        }

        [Fact]
        public static void TestSwiftType108()
        {
            long result = Swift.StaticMethodsTests.Type25.Type25Sub2.Type25Sub2Sub3.swiftFunc0(86, 16, 98, 29);
            Assert.Equal(6240892100988090868, result);
        }

        [Fact]
        public static void TestSwiftType109()
        {
            long result = Swift.StaticMethodsTests.Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.swiftFunc0(73, 16, 22);
            Assert.Equal(-5642305488203428102, result);
        }

        [Fact]
        public static void TestSwiftType110()
        {
            long result = Swift.StaticMethodsTests.Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.swiftFunc0(55, 71, 97, 47, 11, 34);
            Assert.Equal(-3231178403858758072, result);
        }

        [Fact]
        public static void TestSwiftType111()
        {
            long result = Swift.StaticMethodsTests.Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.swiftFunc0(5, 73, 48, 8, 82.30);
            Assert.Equal(-2276280640263091297, result);
        }

        [Fact]
        public static void TestSwiftType112()
        {
            long result = Swift.StaticMethodsTests.Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.Type25Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(13, 41, 14, 20, 87.13, 100);
            Assert.Equal(6280951790717052089, result);
        }

        [Fact]
        public static void TestSwiftType113()
        {
            long result = Swift.StaticMethodsTests.Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.Type25Sub2Sub3Sub4Sub5Sub6Sub7.Type25Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(18.14, 78, 48);
            Assert.Equal(7596813894285301944, result);
        }

        [Fact]
        public static void TestSwiftType114()
        {
            long result = Swift.StaticMethodsTests.Type26.swiftFunc0(68, 90, 39, 90);
            Assert.Equal(684334554920709908, result);
        }

        [Fact]
        public static void TestSwiftType115()
        {
            long result = Swift.StaticMethodsTests.Type27.swiftFunc0(61, 88, 44, 32, 82);
            Assert.Equal(3833906899286760162, result);
        }

        [Fact]
        public static void TestSwiftType116()
        {
            long result = Swift.StaticMethodsTests.Type27.Type27Sub2.swiftFunc0(75);
            Assert.Equal(660514051314300574, result);
        }

        [Fact]
        public static void TestSwiftType117()
        {
            long result = Swift.StaticMethodsTests.Type27.Type27Sub2.Type27Sub2Sub3.swiftFunc0(53, 82, 31);
            Assert.Equal(586766441905616701, result);
        }

        [Fact]
        public static void TestSwiftType118()
        {
            long result = Swift.StaticMethodsTests.Type27.Type27Sub2.Type27Sub2Sub3.Type27Sub2Sub3Sub4.swiftFunc0(36, 55, 14, 28, 67.94, 12, 9, 79);
            Assert.Equal(1437306852202736988, result);
        }

        [Fact]
        public static void TestSwiftType119()
        {
            long result = Swift.StaticMethodsTests.Type27.Type27Sub2.Type27Sub2Sub3.Type27Sub2Sub3Sub4.Type27Sub2Sub3Sub4Sub5.swiftFunc0(38.25, 34.89, 45, 79, 61, 95, 30.73, 9, 87, 21);
            Assert.Equal(7840710793526771328, result);
        }

        [Fact]
        public static void TestSwiftType120()
        {
            long result = Swift.StaticMethodsTests.Type28.swiftFunc0(55, 41, 78, 3.10, 28);
            Assert.Equal(-1966007234001659582, result);
        }

        [Fact]
        public static void TestSwiftType121()
        {
            long result = Swift.StaticMethodsTests.Type28.Type28Sub2.swiftFunc0(37, 43, 71, 67);
            Assert.Equal(-121329981534870825, result);
        }

        [Fact]
        public static void TestSwiftType122()
        {
            long result = Swift.StaticMethodsTests.Type28.Type28Sub2.Type28Sub2Sub3.swiftFunc0(62, 40, 99, 87, 71, 6, 90, 15, 45);
            Assert.Equal(1415111888184892374, result);
        }

        [Fact]
        public static void TestSwiftType123()
        {
            long result = Swift.StaticMethodsTests.Type28.Type28Sub2.Type28Sub2Sub3.Type28Sub2Sub3Sub4.swiftFunc0(5, 0, 8);
            Assert.Equal(-6001975047106517528, result);
        }

        [Fact]
        public static void TestSwiftType124()
        {
            long result = Swift.StaticMethodsTests.Type28.Type28Sub2.Type28Sub2Sub3.Type28Sub2Sub3Sub4.Type28Sub2Sub3Sub4Sub5.swiftFunc0(77.98, 64, 76, 30, 28, 72, 33, 22, 64, 83);
            Assert.Equal(7933515710817159940, result);
        }

        [Fact]
        public static void TestSwiftType125()
        {
            long result = Swift.StaticMethodsTests.Type29.swiftFunc0(61.61, 89, 1, 9, 19, 23, 35, 97.07, 19, 97);
            Assert.Equal(-8015621203482153651, result);
        }

        [Fact]
        public static void TestSwiftType126()
        {
            long result = Swift.StaticMethodsTests.Type29.Type29Sub2.swiftFunc0(68.09, 4, 79.03);
            Assert.Equal(4395505961823925134, result);
        }

        [Fact]
        public static void TestSwiftType127()
        {
            long result = Swift.StaticMethodsTests.Type29.Type29Sub2.Type29Sub2Sub3.swiftFunc0(40, 68, 25, 54, 29.74, 43, 11, 53);
            Assert.Equal(-2207693396529891830, result);
        }

        [Fact]
        public static void TestSwiftType128()
        {
            long result = Swift.StaticMethodsTests.Type29.Type29Sub2.Type29Sub2Sub3.Type29Sub2Sub3Sub4.swiftFunc0(31, 50, 5.28, 30, 31, 26.71, 90, 28, 15);
            Assert.Equal(-1970446111156807291, result);
        }

        [Fact]
        public static void TestSwiftType129()
        {
            long result = Swift.StaticMethodsTests.Type30.swiftFunc0(24.73, 31.60, 31, 95.56, 26.29, 94, 56, 85, 37);
            Assert.Equal(-788990220686720046, result);
        }

        [Fact]
        public static void TestSwiftType130()
        {
            long result = Swift.StaticMethodsTests.Type30.Type30Sub2.swiftFunc0(53, 35, 77.32, 32, 24);
            Assert.Equal(-898048570311494372, result);
        }

        [Fact]
        public static void TestSwiftType131()
        {
            long result = Swift.StaticMethodsTests.Type30.Type30Sub2.Type30Sub2Sub3.swiftFunc0(40, 26, 6, 79, 97, 23, 94, 41);
            Assert.Equal(8899105243716276813, result);
        }

        [Fact]
        public static void TestSwiftType132()
        {
            long result = Swift.StaticMethodsTests.Type31.swiftFunc0(22, 22.07, 46, 19, 63.41, 80, 32, 72);
            Assert.Equal(8488353822900473986, result);
        }

        [Fact]
        public static void TestSwiftType133()
        {
            long result = Swift.StaticMethodsTests.Type31.Type31Sub2.swiftFunc0(25, 73, 18, 45, 85, 57, 86);
            Assert.Equal(3435889942386083384, result);
        }

        [Fact]
        public static void TestSwiftType134()
        {
            long result = Swift.StaticMethodsTests.Type31.Type31Sub2.Type31Sub2Sub3.swiftFunc0(69, 64, 6, 59.17, 6, 42, 28);
            Assert.Equal(-8815603871699215989, result);
        }

        [Fact]
        public static void TestSwiftType135()
        {
            long result = Swift.StaticMethodsTests.Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.swiftFunc0(67, 80, 91, 94, 13, 44, 38, 27, 38);
            Assert.Equal(-3729506638839213907, result);
        }

        [Fact]
        public static void TestSwiftType136()
        {
            long result = Swift.StaticMethodsTests.Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.swiftFunc0(48.86, 94);
            Assert.Equal(1154091763175769141, result);
        }

        [Fact]
        public static void TestSwiftType137()
        {
            long result = Swift.StaticMethodsTests.Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.swiftFunc0(37, 100, 63, 70, 92, 79, 6);
            Assert.Equal(5716785126267550184, result);
        }

        [Fact]
        public static void TestSwiftType138()
        {
            long result = Swift.StaticMethodsTests.Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.Type31Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(28.04);
            Assert.Equal(-4989739197202666758, result);
        }

        [Fact]
        public static void TestSwiftType139()
        {
            long result = Swift.StaticMethodsTests.Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.Type31Sub2Sub3Sub4Sub5Sub6Sub7.Type31Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(60, 9.42, 66, 88.47, 53, 68, 23.71, 41, 98);
            Assert.Equal(5976199894150725410, result);
        }

        [Fact]
        public static void TestSwiftType140()
        {
            long result = Swift.StaticMethodsTests.Type32.swiftFunc0(87, 92, 98, 50, 10.13, 68, 59, 10, 59);
            Assert.Equal(6889201745455097965, result);
        }

        [Fact]
        public static void TestSwiftType141()
        {
            long result = Swift.StaticMethodsTests.Type32.Type32Sub2.swiftFunc0(61.68, 15, 82, 33, 95.38, 11.56, 50, 69.24);
            Assert.Equal(2313199209249006652, result);
        }

        [Fact]
        public static void TestSwiftType142()
        {
            long result = Swift.StaticMethodsTests.Type33.swiftFunc0(10, 89);
            Assert.Equal(3367964750273980708, result);
        }

        [Fact]
        public static void TestSwiftType143()
        {
            long result = Swift.StaticMethodsTests.Type33.Type33Sub2.swiftFunc0(63, 100, 11, 96, 54, 81, 98);
            Assert.Equal(3487868267203425370, result);
        }

        [Fact]
        public static void TestSwiftType144()
        {
            long result = Swift.StaticMethodsTests.Type33.Type33Sub2.Type33Sub2Sub3.swiftFunc0(62, 55.58, 45, 7.82, 32, 96);
            Assert.Equal(2096831882286791291, result);
        }

        [Fact]
        public static void TestSwiftType145()
        {
            long result = Swift.StaticMethodsTests.Type33.Type33Sub2.Type33Sub2Sub3.Type33Sub2Sub3Sub4.swiftFunc0(75, 22, 33, 24, 81, 58, 43.18, 20);
            Assert.Equal(-5226086631818805671, result);
        }

        [Fact]
        public static void TestSwiftType146()
        {
            long result = Swift.StaticMethodsTests.Type34.swiftFunc0(89, 4, 46, 56, 38);
            Assert.Equal(-5648887699739103852, result);
        }

        [Fact]
        public static void TestSwiftType147()
        {
            long result = Swift.StaticMethodsTests.Type34.Type34Sub2.swiftFunc0(17.48, 96, 11, 72, 35);
            Assert.Equal(-7005217645564374753, result);
        }

        [Fact]
        public static void TestSwiftType148()
        {
            long result = Swift.StaticMethodsTests.Type34.Type34Sub2.Type34Sub2Sub3.swiftFunc0(41);
            Assert.Equal(-5808618445805089436, result);
        }

        [Fact]
        public static void TestSwiftType149()
        {
            long result = Swift.StaticMethodsTests.Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.swiftFunc0(3.97, 47, 5.29, 1, 70, 17);
            Assert.Equal(-9158574050224105016, result);
        }

        [Fact]
        public static void TestSwiftType150()
        {
            long result = Swift.StaticMethodsTests.Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.swiftFunc0(97, 34.50, 100, 15, 70, 64, 43.93);
            Assert.Equal(-5916600624380327077, result);
        }

        [Fact]
        public static void TestSwiftType151()
        {
            long result = Swift.StaticMethodsTests.Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.swiftFunc0(64, 56, 14, 89.68, 58, 2, 83, 63, 67);
            Assert.Equal(-2648923089381979377, result);
        }

        [Fact]
        public static void TestSwiftType152()
        {
            long result = Swift.StaticMethodsTests.Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(71, 69, 26);
            Assert.Equal(-452105228537114039, result);
        }

        [Fact]
        public static void TestSwiftType153()
        {
            long result = Swift.StaticMethodsTests.Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(37, 66.74);
            Assert.Equal(-6797445698059191175, result);
        }

        [Fact]
        public static void TestSwiftType154()
        {
            long result = Swift.StaticMethodsTests.Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(83, 16, 98, 9.87, 66, 62, 81);
            Assert.Equal(608794204138285486, result);
        }

        [Fact]
        public static void TestSwiftType155()
        {
            long result = Swift.StaticMethodsTests.Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(61, 16, 77, 40, 86, 1);
            Assert.Equal(-4082797397364962438, result);
        }

        [Fact]
        public static void TestSwiftType156()
        {
            long result = Swift.StaticMethodsTests.Type35.swiftFunc0(40, 42, 83, 71, 66, 26, 67);
            Assert.Equal(-7906006689784259384, result);
        }

        [Fact]
        public static void TestSwiftType157()
        {
            long result = Swift.StaticMethodsTests.Type35.Type35Sub2.swiftFunc0(99, 87, 9, 11.35, 96.05);
            Assert.Equal(8659889919274962482, result);
        }

        [Fact]
        public static void TestSwiftType158()
        {
            long result = Swift.StaticMethodsTests.Type35.Type35Sub2.Type35Sub2Sub3.swiftFunc0(28);
            Assert.Equal(1468038131265307737, result);
        }

        [Fact]
        public static void TestSwiftType159()
        {
            long result = Swift.StaticMethodsTests.Type35.Type35Sub2.Type35Sub2Sub3.Type35Sub2Sub3Sub4.swiftFunc0(46.79, 41.75, 93, 48, 25, 44, 60, 34, 4);
            Assert.Equal(-4715570166168922954, result);
        }

        [Fact]
        public static void TestSwiftType160()
        {
            long result = Swift.StaticMethodsTests.Type35.Type35Sub2.Type35Sub2Sub3.Type35Sub2Sub3Sub4.Type35Sub2Sub3Sub4Sub5.swiftFunc0(86);
            Assert.Equal(665296926896072299, result);
        }

        [Fact]
        public static void TestSwiftType161()
        {
            long result = Swift.StaticMethodsTests.Type36.swiftFunc0(74, 35, 82.42, 68.23, 4, 3, 23, 46, 100);
            Assert.Equal(-5413008670567133046, result);
        }

        [Fact]
        public static void TestSwiftType162()
        {
            long result = Swift.StaticMethodsTests.Type36.Type36Sub2.swiftFunc0(99, 31, 71, 50, 8, 73.31, 95, 20.19, 18);
            Assert.Equal(-6739692845823709352, result);
        }

        [Fact]
        public static void TestSwiftType163()
        {
            long result = Swift.StaticMethodsTests.Type36.Type36Sub2.Type36Sub2Sub3.swiftFunc0(91, 95, 19.35, 0, 75, 55.48, 11, 58);
            Assert.Equal(7925510834824462180, result);
        }

        [Fact]
        public static void TestSwiftType164()
        {
            long result = Swift.StaticMethodsTests.Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.swiftFunc0(76.20);
            Assert.Equal(2675248728736638871, result);
        }

        [Fact]
        public static void TestSwiftType165()
        {
            long result = Swift.StaticMethodsTests.Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.Type36Sub2Sub3Sub4Sub5.swiftFunc0(20);
            Assert.Equal(2056258949234144081, result);
        }

        [Fact]
        public static void TestSwiftType166()
        {
            long result = Swift.StaticMethodsTests.Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.Type36Sub2Sub3Sub4Sub5.Type36Sub2Sub3Sub4Sub5Sub6.swiftFunc0(70, 13.23, 18, 20);
            Assert.Equal(6400995704552108755, result);
        }

        [Fact]
        public static void TestSwiftType167()
        {
            long result = Swift.StaticMethodsTests.Type37.swiftFunc0(100, 10.01);
            Assert.Equal(-29956367469261099, result);
        }

        [Fact]
        public static void TestSwiftType168()
        {
            long result = Swift.StaticMethodsTests.Type37.Type37Sub2.swiftFunc0(20, 89, 79, 41, 20, 3);
            Assert.Equal(-3643954138502892509, result);
        }

        [Fact]
        public static void TestSwiftType169()
        {
            long result = Swift.StaticMethodsTests.Type37.Type37Sub2.Type37Sub2Sub3.swiftFunc0(50, 41);
            Assert.Equal(1918912945413304390, result);
        }

        [Fact]
        public static void TestSwiftType170()
        {
            long result = Swift.StaticMethodsTests.Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.swiftFunc0(96.38);
            Assert.Equal(-4387239261931382494, result);
        }

        [Fact]
        public static void TestSwiftType171()
        {
            long result = Swift.StaticMethodsTests.Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.swiftFunc0(55, 19, 58, 73, 41.32);
            Assert.Equal(415493127137062773, result);
        }

        [Fact]
        public static void TestSwiftType172()
        {
            long result = Swift.StaticMethodsTests.Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.Type37Sub2Sub3Sub4Sub5Sub6.swiftFunc0(81, 17.14, 34.21, 69, 48, 99, 95, 18, 49.15, 5.13);
            Assert.Equal(4112515667169269651, result);
        }

        [Fact]
        public static void TestSwiftType173()
        {
            long result = Swift.StaticMethodsTests.Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.Type37Sub2Sub3Sub4Sub5Sub6.Type37Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(85);
            Assert.Equal(662427201547009264, result);
        }

        [Fact]
        public static void TestSwiftType174()
        {
            long result = Swift.StaticMethodsTests.Type38.swiftFunc0(24, 5, 86, 34, 31, 12, 27, 57.54, 63);
            Assert.Equal(9195655655750290121, result);
        }

        [Fact]
        public static void TestSwiftType175()
        {
            long result = Swift.StaticMethodsTests.Type38.Type38Sub2.swiftFunc0(30, 79.43, 70, 81, 18.21, 53);
            Assert.Equal(-8866160771291525329, result);
        }

        [Fact]
        public static void TestSwiftType176()
        {
            long result = Swift.StaticMethodsTests.Type38.Type38Sub2.Type38Sub2Sub3.swiftFunc0(61, 55.43, 37, 32.95, 47, 7);
            Assert.Equal(-7919806487363423519, result);
        }

        [Fact]
        public static void TestSwiftType177()
        {
            long result = Swift.StaticMethodsTests.Type38.Type38Sub2.Type38Sub2Sub3.Type38Sub2Sub3Sub4.swiftFunc0(17, 88, 61, 12, 71, 1, 88);
            Assert.Equal(-3938287191714746229, result);
        }

        [Fact]
        public static void TestSwiftType178()
        {
            long result = Swift.StaticMethodsTests.Type38.Type38Sub2.Type38Sub2Sub3.Type38Sub2Sub3Sub4.Type38Sub2Sub3Sub4Sub5.swiftFunc0(7, 72, 61);
            Assert.Equal(8766743431031461175, result);
        }

        [Fact]
        public static void TestSwiftType179()
        {
            long result = Swift.StaticMethodsTests.Type39.swiftFunc0(71, 29, 18, 74, 19, 1, 39, 57);
            Assert.Equal(-8986509835952913005, result);
        }

        [Fact]
        public static void TestSwiftType180()
        {
            long result = Swift.StaticMethodsTests.Type39.Type39Sub2.swiftFunc0(21.10, 39, 24, 9, 89);
            Assert.Equal(3590691304079810474, result);
        }

        [Fact]
        public static void TestSwiftType181()
        {
            long result = Swift.StaticMethodsTests.Type39.Type39Sub2.Type39Sub2Sub3.swiftFunc0(6, 17, 13, 88, 58, 50, 35.88, 11.76, 25, 79);
            Assert.Equal(7724744106161839777, result);
        }

        [Fact]
        public static void TestSwiftType182()
        {
            long result = Swift.StaticMethodsTests.Type40.swiftFunc0(11, 22, 81.72, 92, 11, 94, 87, 40);
            Assert.Equal(2229816267186466030, result);
        }

        [Fact]
        public static void TestSwiftType183()
        {
            long result = Swift.StaticMethodsTests.Type40.Type40Sub2.swiftFunc0(62, 6, 74, 41, 45, 50, 36, 39, 76);
            Assert.Equal(4118228357113608828, result);
        }

        [Fact]
        public static void TestSwiftType184()
        {
            long result = Swift.StaticMethodsTests.Type40.Type40Sub2.Type40Sub2Sub3.swiftFunc0(86, 76, 47, 68, 18, 2.53, 83, 62.25);
            Assert.Equal(1804145342247858854, result);
        }

        [Fact]
        public static void TestSwiftType185()
        {
            long result = Swift.StaticMethodsTests.Type40.Type40Sub2.Type40Sub2Sub3.Type40Sub2Sub3Sub4.swiftFunc0(26.49, 9, 20, 93);
            Assert.Equal(7704759995325073605, result);
        }

        [Fact]
        public static void TestSwiftType186()
        {
            long result = Swift.StaticMethodsTests.Type41.swiftFunc0(20, 37, 96, 53, 19, 58.48, 5, 87, 72, 37);
            Assert.Equal(1372448214341753544, result);
        }

        [Fact]
        public static void TestSwiftType187()
        {
            long result = Swift.StaticMethodsTests.Type42.swiftFunc0(57, 13, 75.72, 75, 74, 79, 43.53);
            Assert.Equal(1971274345882451651, result);
        }

        [Fact]
        public static void TestSwiftType188()
        {
            long result = Swift.StaticMethodsTests.Type42.Type42Sub2.swiftFunc0(22, 25, 98, 30, 88, 98, 43, 23.85, 45, 23);
            Assert.Equal(-4574023028842821245, result);
        }

        [Fact]
        public static void TestSwiftType189()
        {
            long result = Swift.StaticMethodsTests.Type42.Type42Sub2.Type42Sub2Sub3.swiftFunc0(100, 40);
            Assert.Equal(4929146646809221225, result);
        }

        [Fact]
        public static void TestSwiftType190()
        {
            long result = Swift.StaticMethodsTests.Type42.Type42Sub2.Type42Sub2Sub3.Type42Sub2Sub3Sub4.swiftFunc0(54, 22, 29, 75, 76, 33, 63, 33, 14);
            Assert.Equal(-8349326794474127128, result);
        }

        [Fact]
        public static void TestSwiftType191()
        {
            long result = Swift.StaticMethodsTests.Type43.swiftFunc0(59, 99, 7);
            Assert.Equal(-8460397198155335392, result);
        }

        [Fact]
        public static void TestSwiftType192()
        {
            long result = Swift.StaticMethodsTests.Type43.Type43Sub2.swiftFunc0(12, 11, 55, 16, 76, 86);
            Assert.Equal(-1356384574701834659, result);
        }

        [Fact]
        public static void TestSwiftType193()
        {
            long result = Swift.StaticMethodsTests.Type43.Type43Sub2.Type43Sub2Sub3.swiftFunc0(25, 79, 72.86, 38, 46, 24);
            Assert.Equal(3156535411293896611, result);
        }

        [Fact]
        public static void TestSwiftType194()
        {
            long result = Swift.StaticMethodsTests.Type44.swiftFunc0(64, 46, 32, 81, 24, 8, 100);
            Assert.Equal(6472602285402761050, result);
        }

        [Fact]
        public static void TestSwiftType195()
        {
            long result = Swift.StaticMethodsTests.Type44.Type44Sub2.swiftFunc0(95);
            Assert.Equal(7737348280147095578, result);
        }

        [Fact]
        public static void TestSwiftType196()
        {
            long result = Swift.StaticMethodsTests.Type44.Type44Sub2.Type44Sub2Sub3.swiftFunc0(78, 72, 10, 5, 34, 32, 65.87, 88, 36);
            Assert.Equal(3183280857369603368, result);
        }

        [Fact]
        public static void TestSwiftType197()
        {
            long result = Swift.StaticMethodsTests.Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.swiftFunc0(60.98, 15, 72, 20, 0.43, 65, 21, 71, 12.86, 32);
            Assert.Equal(-1357428895534966919, result);
        }

        [Fact]
        public static void TestSwiftType198()
        {
            long result = Swift.StaticMethodsTests.Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.swiftFunc0(29.61);
            Assert.Equal(1224872778054606228, result);
        }

        [Fact]
        public static void TestSwiftType199()
        {
            long result = Swift.StaticMethodsTests.Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.swiftFunc0(5, 52, 40, 64, 84, 64, 19, 68.39, 71);
            Assert.Equal(731988194140412712, result);
        }

        [Fact]
        public static void TestSwiftType200()
        {
            long result = Swift.StaticMethodsTests.Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(59, 37, 37, 1, 86, 52, 97, 23.50, 41);
            Assert.Equal(8751287127369919344, result);
        }

        [Fact]
        public static void TestSwiftType201()
        {
            long result = Swift.StaticMethodsTests.Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(56, 23, 75, 33, 90, 38, 17.65, 38, 13);
            Assert.Equal(-1578096571707272808, result);
        }

        [Fact]
        public static void TestSwiftType202()
        {
            long result = Swift.StaticMethodsTests.Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(95.21, 34, 32, 15, 20, 69, 95);
            Assert.Equal(-1564978127064141375, result);
        }

        [Fact]
        public static void TestSwiftType203()
        {
            long result = Swift.StaticMethodsTests.Type45.swiftFunc0(3, 73, 19.95, 70, 35, 47, 1, 35.40, 98.95, 92);
            Assert.Equal(-8806294851017938129, result);
        }

        [Fact]
        public static void TestSwiftType204()
        {
            long result = Swift.StaticMethodsTests.Type45.Type45Sub2.swiftFunc0(22, 39.68, 50, 39, 49, 81.21);
            Assert.Equal(829863142919886848, result);
        }

        [Fact]
        public static void TestSwiftType205()
        {
            long result = Swift.StaticMethodsTests.Type45.Type45Sub2.Type45Sub2Sub3.swiftFunc0(98, 57, 43.08, 89, 51, 44.56, 30);
            Assert.Equal(6909175346769209936, result);
        }

        [Fact]
        public static void TestSwiftType206()
        {
            long result = Swift.StaticMethodsTests.Type46.swiftFunc0(78, 48, 90, 12, 80.62);
            Assert.Equal(-7275121935272093171, result);
        }

        [Fact]
        public static void TestSwiftType207()
        {
            long result = Swift.StaticMethodsTests.Type46.Type46Sub2.swiftFunc0(99, 21);
            Assert.Equal(-7930005622093663545, result);
        }

        [Fact]
        public static void TestSwiftType208()
        {
            long result = Swift.StaticMethodsTests.Type46.Type46Sub2.Type46Sub2Sub3.swiftFunc0(100, 32, 10, 47, 60, 32, 1, 99.92);
            Assert.Equal(4327506764072823738, result);
        }

        [Fact]
        public static void TestSwiftType209()
        {
            long result = Swift.StaticMethodsTests.Type46.Type46Sub2.Type46Sub2Sub3.Type46Sub2Sub3Sub4.swiftFunc0(69, 85.30, 26, 44, 41, 35.15);
            Assert.Equal(-5871068301339653861, result);
        }

        [Fact]
        public static void TestSwiftType210()
        {
            long result = Swift.StaticMethodsTests.Type47.swiftFunc0(77.59, 16.71);
            Assert.Equal(-3617576866883394702, result);
        }

        [Fact]
        public static void TestSwiftType211()
        {
            long result = Swift.StaticMethodsTests.Type47.Type47Sub2.swiftFunc0(88, 85, 21, 37, 75, 87, 67.88, 11.38, 43);
            Assert.Equal(8797535796924509829, result);
        }

        [Fact]
        public static void TestSwiftType212()
        {
            long result = Swift.StaticMethodsTests.Type47.Type47Sub2.Type47Sub2Sub3.swiftFunc0(62, 98);
            Assert.Equal(-9093135547841380823, result);
        }

        [Fact]
        public static void TestSwiftType213()
        {
            long result = Swift.StaticMethodsTests.Type48.swiftFunc0(33, 16, 99, 15, 32, 24);
            Assert.Equal(-7370059868804831648, result);
        }

        [Fact]
        public static void TestSwiftType214()
        {
            long result = Swift.StaticMethodsTests.Type48.Type48Sub2.swiftFunc0(77, 28, 68, 12, 14, 15, 43, 80);
            Assert.Equal(-1571987122778653432, result);
        }

        [Fact]
        public static void TestSwiftType215()
        {
            long result = Swift.StaticMethodsTests.Type48.Type48Sub2.Type48Sub2Sub3.swiftFunc0(73, 89.03, 46, 76, 89.07, 83);
            Assert.Equal(3759391477887331378, result);
        }

        [Fact]
        public static void TestSwiftType216()
        {
            long result = Swift.StaticMethodsTests.Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.swiftFunc0(77, 50, 49, 90.87, 83);
            Assert.Equal(5546215590190126240, result);
        }

        [Fact]
        public static void TestSwiftType217()
        {
            long result = Swift.StaticMethodsTests.Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.Type48Sub2Sub3Sub4Sub5.swiftFunc0(80, 36, 36, 34, 61, 18, 16, 31, 65, 45);
            Assert.Equal(5515496509618210499, result);
        }

        [Fact]
        public static void TestSwiftType218()
        {
            long result = Swift.StaticMethodsTests.Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.Type48Sub2Sub3Sub4Sub5.Type48Sub2Sub3Sub4Sub5Sub6.swiftFunc0(12, 63.75);
            Assert.Equal(3389738121929217758, result);
        }

        [Fact]
        public static void TestSwiftType219()
        {
            long result = Swift.StaticMethodsTests.Type49.swiftFunc0(66, 61, 26, 14.45, 9, 2, 34);
            Assert.Equal(204164697602924849, result);
        }

        [Fact]
        public static void TestSwiftType220()
        {
            long result = Swift.StaticMethodsTests.Type49.Type49Sub2.swiftFunc0(38, 58, 31, 88, 40);
            Assert.Equal(4843295259536981670, result);
        }

        [Fact]
        public static void TestSwiftType221()
        {
            long result = Swift.StaticMethodsTests.Type49.Type49Sub2.Type49Sub2Sub3.swiftFunc0(91, 22, 23, 86.07);
            Assert.Equal(1203075211124201782, result);
        }

        [Fact]
        public static void TestSwiftType222()
        {
            long result = Swift.StaticMethodsTests.Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.swiftFunc0(79.64, 64, 100, 10, 86, 34, 57.79);
            Assert.Equal(987652878952837373, result);
        }

        [Fact]
        public static void TestSwiftType223()
        {
            long result = Swift.StaticMethodsTests.Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.swiftFunc0(94, 48.44, 61, 6.13, 43.63, 23, 60, 19.43);
            Assert.Equal(-8597106858903601342, result);
        }

        [Fact]
        public static void TestSwiftType224()
        {
            long result = Swift.StaticMethodsTests.Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.Type49Sub2Sub3Sub4Sub5Sub6.swiftFunc0(98, 89, 61, 82, 57, 72);
            Assert.Equal(-6448247700166283672, result);
        }

        [Fact]
        public static void TestSwiftType225()
        {
            long result = Swift.StaticMethodsTests.Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.Type49Sub2Sub3Sub4Sub5Sub6.Type49Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(76, 4, 45);
            Assert.Equal(6150748742965086152, result);
        }

        [Fact]
        public static void TestSwiftType226()
        {
            long result = Swift.StaticMethodsTests.Type50.swiftFunc0(32, 21, 6, 75, 7.43, 56, 8, 36);
            Assert.Equal(-7248006303634400941, result);
        }

        [Fact]
        public static void TestSwiftType227()
        {
            long result = Swift.StaticMethodsTests.Type50.Type50Sub2.swiftFunc0(16, 98, 8, 9.02);
            Assert.Equal(1410935449793688018, result);
        }

        [Fact]
        public static void TestSwiftType228()
        {
            long result = Swift.StaticMethodsTests.Type51.swiftFunc0(74, 78, 77, 12, 24, 90);
            Assert.Equal(-8849936543451192470, result);
        }

        [Fact]
        public static void TestSwiftType229()
        {
            long result = Swift.StaticMethodsTests.Type51.Type51Sub2.swiftFunc0(24);
            Assert.Equal(-5808564569735307097, result);
        }

        [Fact]
        public static void TestSwiftType230()
        {
            long result = Swift.StaticMethodsTests.Type51.Type51Sub2.Type51Sub2Sub3.swiftFunc0(48, 1, 24, 76);
            Assert.Equal(5915561197349509280, result);
        }

        [Fact]
        public static void TestSwiftType231()
        {
            long result = Swift.StaticMethodsTests.Type51.Type51Sub2.Type51Sub2Sub3.Type51Sub2Sub3Sub4.swiftFunc0(74, 19, 34, 37, 8, 18, 91, 72);
            Assert.Equal(5213807528371859026, result);
        }

        [Fact]
        public static void TestSwiftType232()
        {
            long result = Swift.StaticMethodsTests.Type51.Type51Sub2.Type51Sub2Sub3.Type51Sub2Sub3Sub4.Type51Sub2Sub3Sub4Sub5.swiftFunc0(54, 65, 47, 6, 39, 29, 3, 77, 34);
            Assert.Equal(-5650716389767694265, result);
        }

        [Fact]
        public static void TestSwiftType233()
        {
            long result = Swift.StaticMethodsTests.Type52.swiftFunc0(99, 84, 83.32, 44, 15);
            Assert.Equal(-1943851521608495833, result);
        }

        [Fact]
        public static void TestSwiftType234()
        {
            long result = Swift.StaticMethodsTests.Type52.Type52Sub2.swiftFunc0(20, 82.67, 82, 71, 36, 53, 39.05, 73, 100);
            Assert.Equal(8696900450471275766, result);
        }

        [Fact]
        public static void TestSwiftType235()
        {
            long result = Swift.StaticMethodsTests.Type52.Type52Sub2.Type52Sub2Sub3.swiftFunc0(81, 59, 87, 71, 88, 15, 59, 38, 69);
            Assert.Equal(208110160139832832, result);
        }

        [Fact]
        public static void TestSwiftType236()
        {
            long result = Swift.StaticMethodsTests.Type52.Type52Sub2.Type52Sub2Sub3.Type52Sub2Sub3Sub4.swiftFunc0(35, 91, 13.36, 35, 26.72);
            Assert.Equal(6610413077972723642, result);
        }

        [Fact]
        public static void TestSwiftType237()
        {
            long result = Swift.StaticMethodsTests.Type53.swiftFunc0(51);
            Assert.Equal(-2876024817762115114, result);
        }

        [Fact]
        public static void TestSwiftType238()
        {
            long result = Swift.StaticMethodsTests.Type53.Type53Sub2.swiftFunc0(63, 9.78, 11, 0, 44, 28, 93, 89, 74, 55);
            Assert.Equal(5694387538268430038, result);
        }

        [Fact]
        public static void TestSwiftType239()
        {
            long result = Swift.StaticMethodsTests.Type54.swiftFunc0(37);
            Assert.Equal(3353268450079572736, result);
        }

        [Fact]
        public static void TestSwiftType240()
        {
            long result = Swift.StaticMethodsTests.Type54.Type54Sub2.swiftFunc0(28);
            Assert.Equal(609815570147520289, result);
        }

        [Fact]
        public static void TestSwiftType241()
        {
            long result = Swift.StaticMethodsTests.Type54.Type54Sub2.Type54Sub2Sub3.swiftFunc0(97, 48.70, 83);
            Assert.Equal(7778548124421051982, result);
        }

        [Fact]
        public static void TestSwiftType242()
        {
            long result = Swift.StaticMethodsTests.Type54.Type54Sub2.Type54Sub2Sub3.Type54Sub2Sub3Sub4.swiftFunc0(29, 26, 25);
            Assert.Equal(8994000051989817089, result);
        }

        [Fact]
        public static void TestSwiftType243()
        {
            long result = Swift.StaticMethodsTests.Type55.swiftFunc0(96);
            Assert.Equal(-8637665132542722587, result);
        }

        [Fact]
        public static void TestSwiftType244()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.swiftFunc0(43, 76, 76, 43, 13, 40);
            Assert.Equal(-4511397732638447968, result);
        }

        [Fact]
        public static void TestSwiftType245()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.Type55Sub2Sub3.swiftFunc0(23, 84, 17, 22, 85.99, 67, 49, 62, 49);
            Assert.Equal(750836114795011244, result);
        }

        [Fact]
        public static void TestSwiftType246()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.swiftFunc0(24, 88.62, 100);
            Assert.Equal(8978418421514884105, result);
        }

        [Fact]
        public static void TestSwiftType247()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.swiftFunc0(42, 18, 58, 98, 53, 19.39, 88, 20, 34);
            Assert.Equal(-4788927712479296194, result);
        }

        [Fact]
        public static void TestSwiftType248()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.swiftFunc0(73, 61);
            Assert.Equal(-5053614839553447791, result);
        }

        [Fact]
        public static void TestSwiftType249()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(97, 72, 36, 23, 92);
            Assert.Equal(6394764035969715939, result);
        }

        [Fact]
        public static void TestSwiftType250()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(89, 88, 33.17, 91, 70, 12, 99, 67, 40, 97);
            Assert.Equal(1538523573391109700, result);
        }

        [Fact]
        public static void TestSwiftType251()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(36, 65);
            Assert.Equal(-6038112166435015472, result);
        }

        [Fact]
        public static void TestSwiftType252()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(53.32, 95.33, 90, 14, 13, 30, 35, 16.69, 13, 44);
            Assert.Equal(-2194148373478066058, result);
        }

        [Fact]
        public static void TestSwiftType253()
        {
            long result = Swift.StaticMethodsTests.Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0(56);
            Assert.Equal(-5696561042698540867, result);
        }

        [Fact]
        public static void TestSwiftType254()
        {
            long result = Swift.StaticMethodsTests.Type56.swiftFunc0(53);
            Assert.Equal(-5808614047758576592, result);
        }

        [Fact]
        public static void TestSwiftType255()
        {
            long result = Swift.StaticMethodsTests.Type56.Type56Sub2.swiftFunc0(40, 13, 41);
            Assert.Equal(5879231729607274961, result);
        }

        [Fact]
        public static void TestSwiftType256()
        {
            long result = Swift.StaticMethodsTests.Type56.Type56Sub2.Type56Sub2Sub3.swiftFunc0(58, 41);
            Assert.Equal(-7448087530822462958, result);
        }

        [Fact]
        public static void TestSwiftType257()
        {
            long result = Swift.StaticMethodsTests.Type57.swiftFunc0(77, 19, 72, 42, 63, 5, 62.77, 25, 27);
            Assert.Equal(-1383735803969955367, result);
        }

        [Fact]
        public static void TestSwiftType258()
        {
            long result = Swift.StaticMethodsTests.Type57.Type57Sub2.swiftFunc0(28, 46, 2, 85.90, 45, 59, 19, 75, 56);
            Assert.Equal(1391155564802257753, result);
        }

        [Fact]
        public static void TestSwiftType259()
        {
            long result = Swift.StaticMethodsTests.Type57.Type57Sub2.Type57Sub2Sub3.swiftFunc0(0, 35.78, 8, 54, 81, 61, 28, 91.93, 63.64);
            Assert.Equal(7701306734874843396, result);
        }

        [Fact]
        public static void TestSwiftType260()
        {
            long result = Swift.StaticMethodsTests.Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.swiftFunc0(21, 56.03);
            Assert.Equal(210589735848477449, result);
        }

        [Fact]
        public static void TestSwiftType261()
        {
            long result = Swift.StaticMethodsTests.Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.Type57Sub2Sub3Sub4Sub5.swiftFunc0(21, 63.14, 67, 75, 63, 98.20, 4);
            Assert.Equal(-3584437386805933162, result);
        }

        [Fact]
        public static void TestSwiftType262()
        {
            long result = Swift.StaticMethodsTests.Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.Type57Sub2Sub3Sub4Sub5.Type57Sub2Sub3Sub4Sub5Sub6.swiftFunc0(43);
            Assert.Equal(-1324056366855609618, result);
        }

        [Fact]
        public static void TestSwiftType263()
        {
            long result = Swift.StaticMethodsTests.Type58.swiftFunc0(9, 79, 96, 21.65, 48, 67, 31);
            Assert.Equal(-4841518573318706584, result);
        }

        [Fact]
        public static void TestSwiftType264()
        {
            long result = Swift.StaticMethodsTests.Type58.Type58Sub2.swiftFunc0(14, 49);
            Assert.Equal(-1968512687697882966, result);
        }

        [Fact]
        public static void TestSwiftType265()
        {
            long result = Swift.StaticMethodsTests.Type59.swiftFunc0(70, 89, 13, 88, 3, 78.19, 90.91, 17, 20.88, 48);
            Assert.Equal(-5399180687717383068, result);
        }

        [Fact]
        public static void TestSwiftType266()
        {
            long result = Swift.StaticMethodsTests.Type59.Type59Sub2.swiftFunc0(70);
            Assert.Equal(2991564855356304835, result);
        }

        [Fact]
        public static void TestSwiftType267()
        {
            long result = Swift.StaticMethodsTests.Type59.Type59Sub2.Type59Sub2Sub3.swiftFunc0(97);
            Assert.Equal(-6016726080209032828, result);
        }

        [Fact]
        public static void TestSwiftType268()
        {
            long result = Swift.StaticMethodsTests.Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.swiftFunc0(53, 60, 90, 65, 63, 28, 98);
            Assert.Equal(4659456914887193846, result);
        }

        [Fact]
        public static void TestSwiftType269()
        {
            long result = Swift.StaticMethodsTests.Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.swiftFunc0(94, 18, 48, 2.84, 35, 26);
            Assert.Equal(3979182145248251593, result);
        }

        [Fact]
        public static void TestSwiftType270()
        {
            long result = Swift.StaticMethodsTests.Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.swiftFunc0(3);
            Assert.Equal(591640642936787734, result);
        }

        [Fact]
        public static void TestSwiftType271()
        {
            long result = Swift.StaticMethodsTests.Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(13.31, 73, 3, 75, 84);
            Assert.Equal(5044478833016954148, result);
        }

        [Fact]
        public static void TestSwiftType272()
        {
            long result = Swift.StaticMethodsTests.Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(50, 56.68, 62);
            Assert.Equal(-8121508732296114185, result);
        }

        [Fact]
        public static void TestSwiftType273()
        {
            long result = Swift.StaticMethodsTests.Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(79, 72, 69, 84.00, 43.48, 29);
            Assert.Equal(5264772612232238808, result);
        }

        [Fact]
        public static void TestSwiftType274()
        {
            long result = Swift.StaticMethodsTests.Type60.swiftFunc0(54, 18.20, 28, 87, 37, 95, 28);
            Assert.Equal(-2231327004938290454, result);
        }

        [Fact]
        public static void TestSwiftType275()
        {
            long result = Swift.StaticMethodsTests.Type60.Type60Sub2.swiftFunc0(92, 68, 30.60, 0, 68, 1);
            Assert.Equal(-3773240513285678005, result);
        }

        [Fact]
        public static void TestSwiftType276()
        {
            long result = Swift.StaticMethodsTests.Type60.Type60Sub2.Type60Sub2Sub3.swiftFunc0(4, 3, 46, 96, 69, 64, 84, 98.80);
            Assert.Equal(4935582694013642489, result);
        }

        [Fact]
        public static void TestSwiftType277()
        {
            long result = Swift.StaticMethodsTests.Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.swiftFunc0(15, 23.46, 35, 12, 39);
            Assert.Equal(-1326027808488383381, result);
        }

        [Fact]
        public static void TestSwiftType278()
        {
            long result = Swift.StaticMethodsTests.Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.swiftFunc0(78.84, 75, 32);
            Assert.Equal(-8938060229869367453, result);
        }

        [Fact]
        public static void TestSwiftType279()
        {
            long result = Swift.StaticMethodsTests.Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.swiftFunc0(13);
            Assert.Equal(593553793169496424, result);
        }

        [Fact]
        public static void TestSwiftType280()
        {
            long result = Swift.StaticMethodsTests.Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(12, 46);
            Assert.Equal(1691025257347504405, result);
        }

        [Fact]
        public static void TestSwiftType281()
        {
            long result = Swift.StaticMethodsTests.Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(93.44, 2, 73, 58, 20, 45, 25);
            Assert.Equal(-6310027403672872531, result);
        }

        [Fact]
        public static void TestSwiftType282()
        {
            long result = Swift.StaticMethodsTests.Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(89, 58);
            Assert.Equal(-2912831909921751162, result);
        }

        [Fact]
        public static void TestSwiftType283()
        {
            long result = Swift.StaticMethodsTests.Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(60, 56, 27, 84, 83, 23);
            Assert.Equal(7930192212614880088, result);
        }

        [Fact]
        public static void TestSwiftType284()
        {
            long result = Swift.StaticMethodsTests.Type61.swiftFunc0(71, 17, 1, 97, 76, 4.01);
            Assert.Equal(-2189305905996890980, result);
        }

        [Fact]
        public static void TestSwiftType285()
        {
            long result = Swift.StaticMethodsTests.Type61.Type61Sub2.swiftFunc0(32, 38, 37, 31, 48);
            Assert.Equal(-1160194257063564797, result);
        }

        [Fact]
        public static void TestSwiftType286()
        {
            long result = Swift.StaticMethodsTests.Type62.swiftFunc0(84, 26, 86, 32, 87);
            Assert.Equal(-3025651207962572234, result);
        }

        [Fact]
        public static void TestSwiftType287()
        {
            long result = Swift.StaticMethodsTests.Type62.Type62Sub2.swiftFunc0(81.64, 92);
            Assert.Equal(-6755609106063950752, result);
        }

        [Fact]
        public static void TestSwiftType288()
        {
            long result = Swift.StaticMethodsTests.Type62.Type62Sub2.Type62Sub2Sub3.swiftFunc0(39, 46, 50, 7);
            Assert.Equal(7656311073493574779, result);
        }

        [Fact]
        public static void TestSwiftType289()
        {
            long result = Swift.StaticMethodsTests.Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.swiftFunc0(50, 36, 63, 41, 49, 82, 71, 40.84, 59);
            Assert.Equal(2867324145537522437, result);
        }

        [Fact]
        public static void TestSwiftType290()
        {
            long result = Swift.StaticMethodsTests.Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.swiftFunc0(58, 30, 99, 57, 31, 20, 18, 27.06);
            Assert.Equal(-5289008638091810662, result);
        }

        [Fact]
        public static void TestSwiftType291()
        {
            long result = Swift.StaticMethodsTests.Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.swiftFunc0(64, 5, 70);
            Assert.Equal(8072638257956225766, result);
        }

        [Fact]
        public static void TestSwiftType292()
        {
            long result = Swift.StaticMethodsTests.Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.Type62Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(85, 62, 99, 62, 40, 98, 73);
            Assert.Equal(6876973284445166072, result);
        }

        [Fact]
        public static void TestSwiftType293()
        {
            long result = Swift.StaticMethodsTests.Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.Type62Sub2Sub3Sub4Sub5Sub6Sub7.Type62Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(35, 53, 16, 98.23);
            Assert.Equal(-6693787192806373363, result);
        }

        [Fact]
        public static void TestSwiftType294()
        {
            long result = Swift.StaticMethodsTests.Type63.swiftFunc0(63, 55, 51, 93, 13, 0, 85, 15, 47);
            Assert.Equal(-3926953705818647021, result);
        }

        [Fact]
        public static void TestSwiftType295()
        {
            long result = Swift.StaticMethodsTests.Type63.Type63Sub2.swiftFunc0(10, 5, 93, 25);
            Assert.Equal(-5961805883595131218, result);
        }

        [Fact]
        public static void TestSwiftType296()
        {
            long result = Swift.StaticMethodsTests.Type63.Type63Sub2.Type63Sub2Sub3.swiftFunc0(9, 47, 9, 64, 31, 32, 2, 46);
            Assert.Equal(3074085374913202105, result);
        }

        [Fact]
        public static void TestSwiftType297()
        {
            long result = Swift.StaticMethodsTests.Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.swiftFunc0(26, 53, 69, 66, 62);
            Assert.Equal(1071541657854385635, result);
        }

        [Fact]
        public static void TestSwiftType298()
        {
            long result = Swift.StaticMethodsTests.Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.Type63Sub2Sub3Sub4Sub5.swiftFunc0(19, 3.92, 20, 10, 95, 6);
            Assert.Equal(-5722720525946162898, result);
        }

        [Fact]
        public static void TestSwiftType299()
        {
            long result = Swift.StaticMethodsTests.Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.Type63Sub2Sub3Sub4Sub5.Type63Sub2Sub3Sub4Sub5Sub6.swiftFunc0(38, 10);
            Assert.Equal(3855690635967189595, result);
        }

        [Fact]
        public static void TestSwiftType300()
        {
            long result = Swift.StaticMethodsTests.Type64.swiftFunc0(99, 17, 74);
            Assert.Equal(-4906832853580219999, result);
        }

        [Fact]
        public static void TestSwiftType301()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.swiftFunc0(94, 85.81, 54.20, 79, 53, 63.54);
            Assert.Equal(-8957339717014574618, result);
        }

        [Fact]
        public static void TestSwiftType302()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.Type64Sub2Sub3.swiftFunc0(8);
            Assert.Equal(-6873002678636213555, result);
        }

        [Fact]
        public static void TestSwiftType303()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.swiftFunc0(8, 39, 42, 48, 62);
            Assert.Equal(7700397584519272242, result);
        }

        [Fact]
        public static void TestSwiftType304()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.swiftFunc0(37.04, 93, 19, 25, 0, 81);
            Assert.Equal(6935852189365092809, result);
        }

        [Fact]
        public static void TestSwiftType305()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.swiftFunc0(89, 50.15, 58);
            Assert.Equal(-1189941486758588483, result);
        }

        [Fact]
        public static void TestSwiftType306()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(46, 18, 49, 46);
            Assert.Equal(6798776298972722462, result);
        }

        [Fact]
        public static void TestSwiftType307()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(97);
            Assert.Equal(7576763534199239620, result);
        }

        [Fact]
        public static void TestSwiftType308()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(63, 67, 67, 41, 22, 82, 73, 3, 22);
            Assert.Equal(3092344266969342041, result);
        }

        [Fact]
        public static void TestSwiftType309()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(26, 67, 98.92, 29, 9, 40);
            Assert.Equal(-5296476420954873497, result);
        }

        [Fact]
        public static void TestSwiftType310()
        {
            long result = Swift.StaticMethodsTests.Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0(85, 22, 50.88, 61, 79.13, 51);
            Assert.Equal(3879585883908093839, result);
        }

        [Fact]
        public static void TestSwiftType311()
        {
            long result = Swift.StaticMethodsTests.Type65.swiftFunc0(14.48, 16);
            Assert.Equal(-4420149166872491203, result);
        }

        [Fact]
        public static void TestSwiftType312()
        {
            long result = Swift.StaticMethodsTests.Type65.Type65Sub2.swiftFunc0(62, 60, 70, 32);
            Assert.Equal(7127809210547205315, result);
        }

        [Fact]
        public static void TestSwiftType313()
        {
            long result = Swift.StaticMethodsTests.Type65.Type65Sub2.Type65Sub2Sub3.swiftFunc0(32, 9, 83, 38.46, 94, 60, 24);
            Assert.Equal(1718548579748602589, result);
        }

        [Fact]
        public static void TestSwiftType314()
        {
            long result = Swift.StaticMethodsTests.Type65.Type65Sub2.Type65Sub2Sub3.Type65Sub2Sub3Sub4.swiftFunc0(69, 26, 96, 50.91, 51, 52, 77, 46, 84, 54);
            Assert.Equal(6752650262500334937, result);
        }

        [Fact]
        public static void TestSwiftType315()
        {
            long result = Swift.StaticMethodsTests.Type65.Type65Sub2.Type65Sub2Sub3.Type65Sub2Sub3Sub4.Type65Sub2Sub3Sub4Sub5.swiftFunc0(41.85, 13);
            Assert.Equal(1669901293133599845, result);
        }

        [Fact]
        public static void TestSwiftType316()
        {
            long result = Swift.StaticMethodsTests.Type66.swiftFunc0(35.67);
            Assert.Equal(5780391959566276186, result);
        }

        [Fact]
        public static void TestSwiftType317()
        {
            long result = Swift.StaticMethodsTests.Type66.Type66Sub2.swiftFunc0(84);
            Assert.Equal(-2649507594516546671, result);
        }

        [Fact]
        public static void TestSwiftType318()
        {
            long result = Swift.StaticMethodsTests.Type67.swiftFunc0(17, 37, 94, 30, 56);
            Assert.Equal(-3843114812536738695, result);
        }

        [Fact]
        public static void TestSwiftType319()
        {
            long result = Swift.StaticMethodsTests.Type67.Type67Sub2.swiftFunc0(47);
            Assert.Equal(564856539678866074, result);
        }

        [Fact]
        public static void TestSwiftType320()
        {
            long result = Swift.StaticMethodsTests.Type67.Type67Sub2.Type67Sub2Sub3.swiftFunc0(82, 60, 21, 72, 43, 58, 65, 5, 31.82);
            Assert.Equal(-4930850372708223179, result);
        }

        [Fact]
        public static void TestSwiftType321()
        {
            long result = Swift.StaticMethodsTests.Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.swiftFunc0(54, 84, 27, 33.07, 33, 81, 37);
            Assert.Equal(4846025311995925664, result);
        }

        [Fact]
        public static void TestSwiftType322()
        {
            long result = Swift.StaticMethodsTests.Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.swiftFunc0(16, 98, 6, 82.49, 91);
            Assert.Equal(-4834024210082438503, result);
        }

        [Fact]
        public static void TestSwiftType323()
        {
            long result = Swift.StaticMethodsTests.Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.swiftFunc0(82, 29, 23, 37, 7.37, 95.44, 54, 63.89, 75);
            Assert.Equal(-4047060341357433926, result);
        }

        [Fact]
        public static void TestSwiftType324()
        {
            long result = Swift.StaticMethodsTests.Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.Type67Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(92.17, 5, 55.78, 62, 35, 22, 59);
            Assert.Equal(-6818112558983887280, result);
        }

        [Fact]
        public static void TestSwiftType325()
        {
            long result = Swift.StaticMethodsTests.Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.Type67Sub2Sub3Sub4Sub5Sub6Sub7.Type67Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(70);
            Assert.Equal(649991725034402779, result);
        }

        [Fact]
        public static void TestSwiftType326()
        {
            long result = Swift.StaticMethodsTests.Type68.swiftFunc0(83, 26, 27, 31, 45);
            Assert.Equal(-5970781214029323595, result);
        }

        [Fact]
        public static void TestSwiftType327()
        {
            long result = Swift.StaticMethodsTests.Type68.Type68Sub2.swiftFunc0(35, 27, 16, 9, 81, 84);
            Assert.Equal(2941980381236318257, result);
        }

        [Fact]
        public static void TestSwiftType328()
        {
            long result = Swift.StaticMethodsTests.Type68.Type68Sub2.Type68Sub2Sub3.swiftFunc0(15, 47, 77, 51, 98.93);
            Assert.Equal(8914815933085291204, result);
        }

        [Fact]
        public static void TestSwiftType329()
        {
            long result = Swift.StaticMethodsTests.Type68.Type68Sub2.Type68Sub2Sub3.Type68Sub2Sub3Sub4.swiftFunc0(15, 97, 28, 78, 22, 74, 52, 72, 27.99, 96);
            Assert.Equal(-4858187451163638360, result);
        }

        [Fact]
        public static void TestSwiftType330()
        {
            long result = Swift.StaticMethodsTests.Type69.swiftFunc0(99, 79);
            Assert.Equal(-3277769636860925079, result);
        }

        [Fact]
        public static void TestSwiftType331()
        {
            long result = Swift.StaticMethodsTests.Type70.swiftFunc0(43.27, 51, 14, 56, 37);
            Assert.Equal(-3902262935105108267, result);
        }

        [Fact]
        public static void TestSwiftType332()
        {
            long result = Swift.StaticMethodsTests.Type70.Type70Sub2.swiftFunc0(30, 7, 32.71, 10, 61, 47, 3, 58);
            Assert.Equal(1290948625416397098, result);
        }

        [Fact]
        public static void TestSwiftType333()
        {
            long result = Swift.StaticMethodsTests.Type70.Type70Sub2.Type70Sub2Sub3.swiftFunc0(9, 59.29, 2, 26, 13, 61);
            Assert.Equal(-3524064562037935227, result);
        }

        [Fact]
        public static void TestSwiftType334()
        {
            long result = Swift.StaticMethodsTests.Type70.Type70Sub2.Type70Sub2Sub3.Type70Sub2Sub3Sub4.swiftFunc0(59, 36.38, 22, 78);
            Assert.Equal(-1858690360864303906, result);
        }

        [Fact]
        public static void TestSwiftType335()
        {
            long result = Swift.StaticMethodsTests.Type71.swiftFunc0(60, 11);
            Assert.Equal(-334487821673350542, result);
        }

        [Fact]
        public static void TestSwiftType336()
        {
            long result = Swift.StaticMethodsTests.Type71.Type71Sub2.swiftFunc0(13, 72.40, 62, 50, 19, 38, 91, 68, 71);
            Assert.Equal(-7266192344694219418, result);
        }

        [Fact]
        public static void TestSwiftType337()
        {
            long result = Swift.StaticMethodsTests.Type71.Type71Sub2.Type71Sub2Sub3.swiftFunc0(89, 47.78);
            Assert.Equal(8973148690506940174, result);
        }

        [Fact]
        public static void TestSwiftType338()
        {
            long result = Swift.StaticMethodsTests.Type71.Type71Sub2.Type71Sub2Sub3.Type71Sub2Sub3Sub4.swiftFunc0(14, 83);
            Assert.Equal(5224909108301209218, result);
        }

        [Fact]
        public static void TestSwiftType339()
        {
            long result = Swift.StaticMethodsTests.Type71.Type71Sub2.Type71Sub2Sub3.Type71Sub2Sub3Sub4.Type71Sub2Sub3Sub4Sub5.swiftFunc0(58, 39.99, 42, 66.21, 72, 57);
            Assert.Equal(2602199087732930739, result);
        }

        [Fact]
        public static void TestSwiftType340()
        {
            long result = Swift.StaticMethodsTests.Type72.swiftFunc0(38, 86, 82, 95);
            Assert.Equal(-5092613311314256162, result);
        }

        [Fact]
        public static void TestSwiftType341()
        {
            long result = Swift.StaticMethodsTests.Type72.Type72Sub2.swiftFunc0(82, 18, 7);
            Assert.Equal(4592629004123936242, result);
        }

        [Fact]
        public static void TestSwiftType342()
        {
            long result = Swift.StaticMethodsTests.Type72.Type72Sub2.Type72Sub2Sub3.swiftFunc0(64, 87, 19, 8, 93, 53, 60.71);
            Assert.Equal(2961133842124433026, result);
        }

        [Fact]
        public static void TestSwiftType343()
        {
            long result = Swift.StaticMethodsTests.Type72.Type72Sub2.Type72Sub2Sub3.Type72Sub2Sub3Sub4.swiftFunc0(66, 1, 76);
            Assert.Equal(-1678085087134853366, result);
        }

        [Fact]
        public static void TestSwiftType344()
        {
            long result = Swift.StaticMethodsTests.Type72.Type72Sub2.Type72Sub2Sub3.Type72Sub2Sub3Sub4.Type72Sub2Sub3Sub4Sub5.swiftFunc0(27, 96, 81, 21, 1, 49, 91, 16, 91, 76);
            Assert.Equal(-1105421971532693998, result);
        }

        [Fact]
        public static void TestSwiftType345()
        {
            long result = Swift.StaticMethodsTests.Type73.swiftFunc0(37, 10, 80, 28, 24, 57.14, 77, 60, 36.12);
            Assert.Equal(-2832997306306529749, result);
        }

        [Fact]
        public static void TestSwiftType346()
        {
            long result = Swift.StaticMethodsTests.Type73.Type73Sub2.swiftFunc0(51.21, 42, 96);
            Assert.Equal(-6969258921184097051, result);
        }

        [Fact]
        public static void TestSwiftType347()
        {
            long result = Swift.StaticMethodsTests.Type73.Type73Sub2.Type73Sub2Sub3.swiftFunc0(14, 75.28, 66);
            Assert.Equal(279596736138991104, result);
        }

        [Fact]
        public static void TestSwiftType348()
        {
            long result = Swift.StaticMethodsTests.Type74.swiftFunc0(14, 67, 44, 14, 79, 21, 4.74, 64);
            Assert.Equal(5602327263456262086, result);
        }

        [Fact]
        public static void TestSwiftType349()
        {
            long result = Swift.StaticMethodsTests.Type74.Type74Sub2.swiftFunc0(87, 9, 60, 49.43, 9);
            Assert.Equal(-8597986116169910690, result);
        }

        [Fact]
        public static void TestSwiftType350()
        {
            long result = Swift.StaticMethodsTests.Type74.Type74Sub2.Type74Sub2Sub3.swiftFunc0(20, 58, 54, 57.40, 37, 80, 47, 17.36);
            Assert.Equal(8923789696140165270, result);
        }

        [Fact]
        public static void TestSwiftType351()
        {
            long result = Swift.StaticMethodsTests.Type74.Type74Sub2.Type74Sub2Sub3.Type74Sub2Sub3Sub4.swiftFunc0(51);
            Assert.Equal(-1336053951289096330, result);
        }

        [Fact]
        public static void TestSwiftType352()
        {
            long result = Swift.StaticMethodsTests.Type75.swiftFunc0(78.00);
            Assert.Equal(-131802630901555874, result);
        }

        [Fact]
        public static void TestSwiftType353()
        {
            long result = Swift.StaticMethodsTests.Type75.Type75Sub2.swiftFunc0(42, 84, 39, 9, 80, 81);
            Assert.Equal(-6625810836787257284, result);
        }

        [Fact]
        public static void TestSwiftType354()
        {
            long result = Swift.StaticMethodsTests.Type75.Type75Sub2.Type75Sub2Sub3.swiftFunc0(95, 12, 12, 49, 1, 52.30);
            Assert.Equal(5270619647366752580, result);
        }

        [Fact]
        public static void TestSwiftType355()
        {
            long result = Swift.StaticMethodsTests.Type75.Type75Sub2.Type75Sub2Sub3.Type75Sub2Sub3Sub4.swiftFunc0(5, 31, 10);
            Assert.Equal(-1701163299766814043, result);
        }

        [Fact]
        public static void TestSwiftType356()
        {
            long result = Swift.StaticMethodsTests.Type75.Type75Sub2.Type75Sub2Sub3.Type75Sub2Sub3Sub4.Type75Sub2Sub3Sub4Sub5.swiftFunc0(39.66, 1, 11.14, 3, 75, 77, 21, 27, 89.65);
            Assert.Equal(-1550705282006029491, result);
        }

        [Fact]
        public static void TestSwiftType357()
        {
            long result = Swift.StaticMethodsTests.Type76.swiftFunc0(38, 40, 64, 35, 62, 90.14, 3.08, 67);
            Assert.Equal(6681987026482264083, result);
        }

        [Fact]
        public static void TestSwiftType358()
        {
            long result = Swift.StaticMethodsTests.Type76.Type76Sub2.swiftFunc0(15, 19);
            Assert.Equal(-2033315680351275623, result);
        }

        [Fact]
        public static void TestSwiftType359()
        {
            long result = Swift.StaticMethodsTests.Type76.Type76Sub2.Type76Sub2Sub3.swiftFunc0(0.62, 51);
            Assert.Equal(6973787733211351142, result);
        }

        [Fact]
        public static void TestSwiftType360()
        {
            long result = Swift.StaticMethodsTests.Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.swiftFunc0(36.07, 86, 60, 77, 1.01, 21, 59);
            Assert.Equal(3664947251085737281, result);
        }

        [Fact]
        public static void TestSwiftType361()
        {
            long result = Swift.StaticMethodsTests.Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.Type76Sub2Sub3Sub4Sub5.swiftFunc0(77, 56, 87, 81, 65.87, 28, 43.97, 42, 27, 99);
            Assert.Equal(415565234096897685, result);
        }

        [Fact]
        public static void TestSwiftType362()
        {
            long result = Swift.StaticMethodsTests.Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.Type76Sub2Sub3Sub4Sub5.Type76Sub2Sub3Sub4Sub5Sub6.swiftFunc0(81, 53);
            Assert.Equal(295801482863592169, result);
        }

        [Fact]
        public static void TestSwiftType363()
        {
            long result = Swift.StaticMethodsTests.Type77.swiftFunc0(77, 68, 64, 18.20, 55, 73);
            Assert.Equal(-4847418080359704006, result);
        }

        [Fact]
        public static void TestSwiftType364()
        {
            long result = Swift.StaticMethodsTests.Type78.swiftFunc0(68, 10, 39, 67, 23, 45);
            Assert.Equal(-6034347456074523955, result);
        }

        [Fact]
        public static void TestSwiftType365()
        {
            long result = Swift.StaticMethodsTests.Type78.Type78Sub2.swiftFunc0(88.78, 66, 63, 68, 49);
            Assert.Equal(5611645301987577034, result);
        }

        [Fact]
        public static void TestSwiftType366()
        {
            long result = Swift.StaticMethodsTests.Type79.swiftFunc0(76, 34.43, 40.60, 37, 100, 3);
            Assert.Equal(-1960305953532885815, result);
        }

        [Fact]
        public static void TestSwiftType367()
        {
            long result = Swift.StaticMethodsTests.Type79.Type79Sub2.swiftFunc0(8, 29, 79, 11, 20.79, 55, 21.74, 32);
            Assert.Equal(-3785633265034196563, result);
        }

        [Fact]
        public static void TestSwiftType368()
        {
            long result = Swift.StaticMethodsTests.Type79.Type79Sub2.Type79Sub2Sub3.swiftFunc0(2.21, 29, 28, 99.63, 82, 76, 27);
            Assert.Equal(-6551340915042681677, result);
        }

        [Fact]
        public static void TestSwiftType369()
        {
            long result = Swift.StaticMethodsTests.Type79.Type79Sub2.Type79Sub2Sub3.Type79Sub2Sub3Sub4.swiftFunc0(34, 51);
            Assert.Equal(-6949495942111874244, result);
        }

        [Fact]
        public static void TestSwiftType370()
        {
            long result = Swift.StaticMethodsTests.Type80.swiftFunc0(23, 79.84, 60, 12, 82, 42, 76);
            Assert.Equal(-2439014749661513945, result);
        }

        [Fact]
        public static void TestSwiftType371()
        {
            long result = Swift.StaticMethodsTests.Type80.Type80Sub2.swiftFunc0(98, 19.17, 91, 61, 53, 2, 16.52, 34, 63, 48);
            Assert.Equal(-8319675326568653235, result);
        }

        [Fact]
        public static void TestSwiftType372()
        {
            long result = Swift.StaticMethodsTests.Type80.Type80Sub2.Type80Sub2Sub3.swiftFunc0(90, 46, 20, 27, 58);
            Assert.Equal(841649437616840460, result);
        }

        [Fact]
        public static void TestSwiftType373()
        {
            long result = Swift.StaticMethodsTests.Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.swiftFunc0(42, 33, 21, 13, 74, 4.58, 22, 16);
            Assert.Equal(-1740739174720849383, result);
        }

        [Fact]
        public static void TestSwiftType374()
        {
            long result = Swift.StaticMethodsTests.Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.Type80Sub2Sub3Sub4Sub5.swiftFunc0(60.26, 87, 4, 33, 60.41, 73, 79, 66, 31, 18);
            Assert.Equal(-8488642455115438639, result);
        }

        [Fact]
        public static void TestSwiftType375()
        {
            long result = Swift.StaticMethodsTests.Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.Type80Sub2Sub3Sub4Sub5.Type80Sub2Sub3Sub4Sub5Sub6.swiftFunc0(26.41, 57.40, 34.41, 11, 15, 86, 5);
            Assert.Equal(7678767092711717444, result);
        }

        [Fact]
        public static void TestSwiftType376()
        {
            long result = Swift.StaticMethodsTests.Type81.swiftFunc0(85, 4, 21, 23, 9, 13);
            Assert.Equal(-2534172109392825488, result);
        }

        [Fact]
        public static void TestSwiftType377()
        {
            long result = Swift.StaticMethodsTests.Type81.Type81Sub2.swiftFunc0(76.53, 33, 71.34);
            Assert.Equal(-5726993050876658753, result);
        }

        [Fact]
        public static void TestSwiftType378()
        {
            long result = Swift.StaticMethodsTests.Type81.Type81Sub2.Type81Sub2Sub3.swiftFunc0(78, 51, 8);
            Assert.Equal(-5273716212365502184, result);
        }

        [Fact]
        public static void TestSwiftType379()
        {
            long result = Swift.StaticMethodsTests.Type81.Type81Sub2.Type81Sub2Sub3.Type81Sub2Sub3Sub4.swiftFunc0(23.61, 87, 59, 74);
            Assert.Equal(3147555241696120016, result);
        }

        [Fact]
        public static void TestSwiftType380()
        {
            long result = Swift.StaticMethodsTests.Type82.swiftFunc0(88, 47, 95, 72, 72, 54, 23);
            Assert.Equal(4054482155005731604, result);
        }

        [Fact]
        public static void TestSwiftType381()
        {
            long result = Swift.StaticMethodsTests.Type83.swiftFunc0(56, 7, 24, 29, 14.14, 38.88, 98.96, 17, 23);
            Assert.Equal(641368654376073872, result);
        }

        [Fact]
        public static void TestSwiftType382()
        {
            long result = Swift.StaticMethodsTests.Type83.Type83Sub2.swiftFunc0(19.30, 85);
            Assert.Equal(-2432570045665555742, result);
        }

        [Fact]
        public static void TestSwiftType383()
        {
            long result = Swift.StaticMethodsTests.Type83.Type83Sub2.Type83Sub2Sub3.swiftFunc0(40.67, 68.63, 46, 70, 45, 22.40, 41, 93);
            Assert.Equal(6569272560158614758, result);
        }

        [Fact]
        public static void TestSwiftType384()
        {
            long result = Swift.StaticMethodsTests.Type83.Type83Sub2.Type83Sub2Sub3.Type83Sub2Sub3Sub4.swiftFunc0(38, 79.27, 10, 2, 16);
            Assert.Equal(7068325176092208113, result);
        }

        [Fact]
        public static void TestSwiftType385()
        {
            long result = Swift.StaticMethodsTests.Type84.swiftFunc0(73);
            Assert.Equal(4635659444355057900, result);
        }

        [Fact]
        public static void TestSwiftType386()
        {
            long result = Swift.StaticMethodsTests.Type85.swiftFunc0(25, 36, 87, 25, 5, 41.42, 64, 19, 6);
            Assert.Equal(6276434994091822532, result);
        }

        [Fact]
        public static void TestSwiftType387()
        {
            long result = Swift.StaticMethodsTests.Type85.Type85Sub2.swiftFunc0(97, 22, 89);
            Assert.Equal(-7178683098914978415, result);
        }

        [Fact]
        public static void TestSwiftType388()
        {
            long result = Swift.StaticMethodsTests.Type85.Type85Sub2.Type85Sub2Sub3.swiftFunc0(17);
            Assert.Equal(605032694565748564, result);
        }

        [Fact]
        public static void TestSwiftType389()
        {
            long result = Swift.StaticMethodsTests.Type85.Type85Sub2.Type85Sub2Sub3.Type85Sub2Sub3Sub4.swiftFunc0(83, 44, 40.86, 79, 60, 20, 40, 30, 59.43, 70);
            Assert.Equal(2766821019775738384, result);
        }

        [Fact]
        public static void TestSwiftType390()
        {
            long result = Swift.StaticMethodsTests.Type86.swiftFunc0(75, 71, 97, 8, 11);
            Assert.Equal(815233715740760203, result);
        }

        [Fact]
        public static void TestSwiftType391()
        {
            long result = Swift.StaticMethodsTests.Type87.swiftFunc0(75, 10.29, 53.93, 54);
            Assert.Equal(-8969823465370983724, result);
        }

        [Fact]
        public static void TestSwiftType392()
        {
            long result = Swift.StaticMethodsTests.Type87.Type87Sub2.swiftFunc0(68, 54.92, 66.88, 69, 100, 45, 44, 46, 61, 71.47);
            Assert.Equal(-9169579336628190189, result);
        }

        [Fact]
        public static void TestSwiftType393()
        {
            long result = Swift.StaticMethodsTests.Type87.Type87Sub2.Type87Sub2Sub3.swiftFunc0(22, 10, 31, 68, 73, 0, 91, 100, 9.40, 20);
            Assert.Equal(7250428304757445709, result);
        }

        [Fact]
        public static void TestSwiftType394()
        {
            long result = Swift.StaticMethodsTests.Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.swiftFunc0(40, 25);
            Assert.Equal(5586363182792468972, result);
        }

        [Fact]
        public static void TestSwiftType395()
        {
            long result = Swift.StaticMethodsTests.Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.swiftFunc0(75, 64, 32.55, 7, 62, 93.04);
            Assert.Equal(-8193209147041370451, result);
        }

        [Fact]
        public static void TestSwiftType396()
        {
            long result = Swift.StaticMethodsTests.Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.Type87Sub2Sub3Sub4Sub5Sub6.swiftFunc0(3, 94, 77, 95, 69, 83, 87);
            Assert.Equal(2937495561540670839, result);
        }

        [Fact]
        public static void TestSwiftType397()
        {
            long result = Swift.StaticMethodsTests.Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.Type87Sub2Sub3Sub4Sub5Sub6.Type87Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(92, 46, 85, 75, 4, 69.45, 11, 91, 13);
            Assert.Equal(-6860674082066421230, result);
        }

        [Fact]
        public static void TestSwiftType398()
        {
            long result = Swift.StaticMethodsTests.Type88.swiftFunc0(8, 45, 8, 13, 6);
            Assert.Equal(5260672631800259017, result);
        }

        [Fact]
        public static void TestSwiftType399()
        {
            long result = Swift.StaticMethodsTests.Type88.Type88Sub2.swiftFunc0(99, 51, 0.85, 97, 67, 49, 50);
            Assert.Equal(-8810323782677945282, result);
        }

        [Fact]
        public static void TestSwiftType400()
        {
            long result = Swift.StaticMethodsTests.Type88.Type88Sub2.Type88Sub2Sub3.swiftFunc0(0, 46.66, 68, 45, 21, 22);
            Assert.Equal(7727777822775495554, result);
        }

        [Fact]
        public static void TestSwiftType401()
        {
            long result = Swift.StaticMethodsTests.Type88.Type88Sub2.Type88Sub2Sub3.Type88Sub2Sub3Sub4.swiftFunc0(46, 65, 26, 79, 70, 35, 100, 15.84, 85);
            Assert.Equal(-6915587708849107502, result);
        }

        [Fact]
        public static void TestSwiftType402()
        {
            long result = Swift.StaticMethodsTests.Type88.Type88Sub2.Type88Sub2Sub3.Type88Sub2Sub3Sub4.Type88Sub2Sub3Sub4Sub5.swiftFunc0(47, 23.27, 24, 96, 50.05, 51);
            Assert.Equal(-4966739196006501207, result);
        }

        [Fact]
        public static void TestSwiftType403()
        {
            long result = Swift.StaticMethodsTests.Type89.swiftFunc0(61.93, 81);
            Assert.Equal(8654043253704010916, result);
        }

        [Fact]
        public static void TestSwiftType404()
        {
            long result = Swift.StaticMethodsTests.Type89.Type89Sub2.swiftFunc0(10, 24, 82, 38, 18.04, 14);
            Assert.Equal(6519717980839005112, result);
        }

        [Fact]
        public static void TestSwiftType405()
        {
            long result = Swift.StaticMethodsTests.Type89.Type89Sub2.Type89Sub2Sub3.swiftFunc0(89);
            Assert.Equal(3459217808417385212, result);
        }

        [Fact]
        public static void TestSwiftType406()
        {
            long result = Swift.StaticMethodsTests.Type90.swiftFunc0(84, 6, 87, 84, 32, 9, 67);
            Assert.Equal(-4575982977251622046, result);
        }

        [Fact]
        public static void TestSwiftType407()
        {
            long result = Swift.StaticMethodsTests.Type90.Type90Sub2.swiftFunc0(60, 49, 59, 33, 30, 5.32, 47, 77, 73);
            Assert.Equal(5578769012424498626, result);
        }

        [Fact]
        public static void TestSwiftType408()
        {
            long result = Swift.StaticMethodsTests.Type90.Type90Sub2.Type90Sub2Sub3.swiftFunc0(42, 16, 34.91, 78, 38, 44, 36.79, 9.77, 56);
            Assert.Equal(6966325052094053556, result);
        }

        [Fact]
        public static void TestSwiftType409()
        {
            long result = Swift.StaticMethodsTests.Type90.Type90Sub2.Type90Sub2Sub3.Type90Sub2Sub3Sub4.swiftFunc0(4.83, 8, 96);
            Assert.Equal(-509761177789724739, result);
        }

        [Fact]
        public static void TestSwiftType410()
        {
            long result = Swift.StaticMethodsTests.Type90.Type90Sub2.Type90Sub2Sub3.Type90Sub2Sub3Sub4.Type90Sub2Sub3Sub4Sub5.swiftFunc0(100, 11, 55, 24.50, 58);
            Assert.Equal(-5018197425982911939, result);
        }

        [Fact]
        public static void TestSwiftType411()
        {
            long result = Swift.StaticMethodsTests.Type91.swiftFunc0(84, 27, 72);
            Assert.Equal(-6260927431643092310, result);
        }

        [Fact]
        public static void TestSwiftType412()
        {
            long result = Swift.StaticMethodsTests.Type91.Type91Sub2.swiftFunc0(63, 76.01, 62, 36);
            Assert.Equal(-4247982083591395051, result);
        }

        [Fact]
        public static void TestSwiftType413()
        {
            long result = Swift.StaticMethodsTests.Type91.Type91Sub2.Type91Sub2Sub3.swiftFunc0(85, 74, 57.61, 8, 4, 100, 42);
            Assert.Equal(6527952547738705482, result);
        }

        [Fact]
        public static void TestSwiftType414()
        {
            long result = Swift.StaticMethodsTests.Type91.Type91Sub2.Type91Sub2Sub3.Type91Sub2Sub3Sub4.swiftFunc0(74, 28, 76);
            Assert.Equal(7985770896057025413, result);
        }

        [Fact]
        public static void TestSwiftType415()
        {
            long result = Swift.StaticMethodsTests.Type91.Type91Sub2.Type91Sub2Sub3.Type91Sub2Sub3Sub4.Type91Sub2Sub3Sub4Sub5.swiftFunc0(60);
            Assert.Equal(3820921403140653113, result);
        }

        [Fact]
        public static void TestSwiftType416()
        {
            long result = Swift.StaticMethodsTests.Type92.swiftFunc0(4.85, 26, 47, 80, 41, 33, 58, 91);
            Assert.Equal(-3284314349373785262, result);
        }

        [Fact]
        public static void TestSwiftType417()
        {
            long result = Swift.StaticMethodsTests.Type92.Type92Sub2.swiftFunc0(28, 52, 26, 67, 1.38, 49, 85.60, 26);
            Assert.Equal(8769838448971532803, result);
        }

        [Fact]
        public static void TestSwiftType418()
        {
            long result = Swift.StaticMethodsTests.Type92.Type92Sub2.Type92Sub2Sub3.swiftFunc0(14, 77);
            Assert.Equal(180381667452574854, result);
        }

        [Fact]
        public static void TestSwiftType419()
        {
            long result = Swift.StaticMethodsTests.Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.swiftFunc0(68, 70, 7, 3, 31, 60);
            Assert.Equal(3860903177905724486, result);
        }

        [Fact]
        public static void TestSwiftType420()
        {
            long result = Swift.StaticMethodsTests.Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.Type92Sub2Sub3Sub4Sub5.swiftFunc0(34, 46, 33, 55, 47, 80, 12, 18, 51.72);
            Assert.Equal(4680556141452481621, result);
        }

        [Fact]
        public static void TestSwiftType421()
        {
            long result = Swift.StaticMethodsTests.Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.Type92Sub2Sub3Sub4Sub5.Type92Sub2Sub3Sub4Sub5Sub6.swiftFunc0(98, 99, 56, 40, 62, 23.66, 57);
            Assert.Equal(8918745350502716139, result);
        }

        [Fact]
        public static void TestSwiftType422()
        {
            long result = Swift.StaticMethodsTests.Type93.swiftFunc0(11, 9, 66, 12.84, 44, 100, 76, 45, 96);
            Assert.Equal(7440258612429021759, result);
        }

        [Fact]
        public static void TestSwiftType423()
        {
            long result = Swift.StaticMethodsTests.Type93.Type93Sub2.swiftFunc0(94.75, 21, 37, 91, 56, 34, 55.01, 65, 8);
            Assert.Equal(-7783235671149717910, result);
        }

        [Fact]
        public static void TestSwiftType424()
        {
            long result = Swift.StaticMethodsTests.Type93.Type93Sub2.Type93Sub2Sub3.swiftFunc0(94, 27, 0, 52);
            Assert.Equal(-1228974309994701654, result);
        }

        [Fact]
        public static void TestSwiftType425()
        {
            long result = Swift.StaticMethodsTests.Type94.swiftFunc0(87.31);
            Assert.Equal(648102651988741497, result);
        }

        [Fact]
        public static void TestSwiftType426()
        {
            long result = Swift.StaticMethodsTests.Type94.Type94Sub2.swiftFunc0(35);
            Assert.Equal(-5808625042874858702, result);
        }

        [Fact]
        public static void TestSwiftType427()
        {
            long result = Swift.StaticMethodsTests.Type94.Type94Sub2.Type94Sub2Sub3.swiftFunc0(79.14, 23, 68.74, 40.88, 31.98, 47);
            Assert.Equal(535508779590666133, result);
        }

        [Fact]
        public static void TestSwiftType428()
        {
            long result = Swift.StaticMethodsTests.Type94.Type94Sub2.Type94Sub2Sub3.Type94Sub2Sub3Sub4.swiftFunc0(21, 67, 38, 78, 10, 59, 41.43, 86.39, 30, 54);
            Assert.Equal(6840370006887115940, result);
        }

        [Fact]
        public static void TestSwiftType429()
        {
            long result = Swift.StaticMethodsTests.Type94.Type94Sub2.Type94Sub2Sub3.Type94Sub2Sub3Sub4.Type94Sub2Sub3Sub4Sub5.swiftFunc0(94);
            Assert.Equal(818319554451827883, result);
        }

        [Fact]
        public static void TestSwiftType430()
        {
            long result = Swift.StaticMethodsTests.Type95.swiftFunc0(39, 30, 14.68, 76, 57, 45, 68);
            Assert.Equal(-3016789913300536077, result);
        }

        [Fact]
        public static void TestSwiftType431()
        {
            long result = Swift.StaticMethodsTests.Type95.Type95Sub2.swiftFunc0(6, 87, 17, 64);
            Assert.Equal(-9210708301318953955, result);
        }

        [Fact]
        public static void TestSwiftType432()
        {
            long result = Swift.StaticMethodsTests.Type95.Type95Sub2.Type95Sub2Sub3.swiftFunc0(60, 46);
            Assert.Equal(-3232107605133056497, result);
        }

        [Fact]
        public static void TestSwiftType433()
        {
            long result = Swift.StaticMethodsTests.Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.swiftFunc0(25, 38, 29, 5);
            Assert.Equal(-5500828122832595486, result);
        }

        [Fact]
        public static void TestSwiftType434()
        {
            long result = Swift.StaticMethodsTests.Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.Type95Sub2Sub3Sub4Sub5.swiftFunc0(98, 23, 59, 31);
            Assert.Equal(-403789833093848052, result);
        }

        [Fact]
        public static void TestSwiftType435()
        {
            long result = Swift.StaticMethodsTests.Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.Type95Sub2Sub3Sub4Sub5.Type95Sub2Sub3Sub4Sub5Sub6.swiftFunc0(31, 84, 49, 85, 84, 19, 60, 89, 3);
            Assert.Equal(-7442437356528311699, result);
        }

        [Fact]
        public static void TestSwiftType436()
        {
            long result = Swift.StaticMethodsTests.Type96.swiftFunc0(98, 7, 11, 85.08, 20);
            Assert.Equal(1691835474197242426, result);
        }

        [Fact]
        public static void TestSwiftType437()
        {
            long result = Swift.StaticMethodsTests.Type96.Type96Sub2.swiftFunc0(17, 52, 9, 32, 4, 87, 74);
            Assert.Equal(-4901368629152969742, result);
        }

        [Fact]
        public static void TestSwiftType438()
        {
            long result = Swift.StaticMethodsTests.Type96.Type96Sub2.Type96Sub2Sub3.swiftFunc0(68, 62, 4, 33.70, 30, 97, 50, 34, 39, 60);
            Assert.Equal(-3636991592063641174, result);
        }

        [Fact]
        public static void TestSwiftType439()
        {
            long result = Swift.StaticMethodsTests.Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.swiftFunc0(52, 71.85, 96, 82, 61, 41, 20, 33);
            Assert.Equal(5397481060424965145, result);
        }

        [Fact]
        public static void TestSwiftType440()
        {
            long result = Swift.StaticMethodsTests.Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.swiftFunc0(39, 37, 42, 0, 88, 28, 67, 82.35, 78);
            Assert.Equal(-605185230668150482, result);
        }

        [Fact]
        public static void TestSwiftType441()
        {
            long result = Swift.StaticMethodsTests.Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.swiftFunc0(4.44);
            Assert.Equal(2860442675980140193, result);
        }

        [Fact]
        public static void TestSwiftType442()
        {
            long result = Swift.StaticMethodsTests.Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(9, 82, 61, 86.59, 38, 20, 62, 34, 41, 86);
            Assert.Equal(-2409143221206391557, result);
        }

        [Fact]
        public static void TestSwiftType443()
        {
            long result = Swift.StaticMethodsTests.Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(72, 2, 18, 70);
            Assert.Equal(598478454465173457, result);
        }

        [Fact]
        public static void TestSwiftType444()
        {
            long result = Swift.StaticMethodsTests.Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(76, 8);
            Assert.Equal(-750802910854975063, result);
        }

        [Fact]
        public static void TestSwiftType445()
        {
            long result = Swift.StaticMethodsTests.Type97.swiftFunc0(23, 23.77, 13.26);
            Assert.Equal(-7779067967876068949, result);
        }

        [Fact]
        public static void TestSwiftType446()
        {
            long result = Swift.StaticMethodsTests.Type98.swiftFunc0(94, 14, 94, 24, 46.36, 83, 40, 63, 38);
            Assert.Equal(6740833353374069276, result);
        }

        [Fact]
        public static void TestSwiftType447()
        {
            long result = Swift.StaticMethodsTests.Type98.Type98Sub2.swiftFunc0(70, 83, 12, 72.32, 51, 40, 56);
            Assert.Equal(-4277300291582903233, result);
        }

        [Fact]
        public static void TestSwiftType448()
        {
            long result = Swift.StaticMethodsTests.Type99.swiftFunc0(73);
            Assert.Equal(4635659444355057900, result);
        }

        [Fact]
        public static void TestSwiftType449()
        {
            long result = Swift.StaticMethodsTests.Type99.Type99Sub2.swiftFunc0(33, 83);
            Assert.Equal(-7711992276714047289, result);
        }

        [Fact]
        public static void TestSwiftType450()
        {
            long result = Swift.StaticMethodsTests.Type99.Type99Sub2.Type99Sub2Sub3.swiftFunc0(29, 54, 73, 22, 39, 36.33, 83);
            Assert.Equal(7255994502266463888, result);
        }

        [Fact]
        public static void TestSwiftType451()
        {
            long result = Swift.StaticMethodsTests.Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.swiftFunc0(44, 70, 56, 62, 0, 86.28, 8.53, 10, 52.86);
            Assert.Equal(3471684984972755719, result);
        }

        [Fact]
        public static void TestSwiftType452()
        {
            long result = Swift.StaticMethodsTests.Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.Type99Sub2Sub3Sub4Sub5.swiftFunc0(94, 23, 73.47, 60.11, 46, 55);
            Assert.Equal(-7518657982614324457, result);
        }

        [Fact]
        public static void TestSwiftType453()
        {
            long result = Swift.StaticMethodsTests.Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.Type99Sub2Sub3Sub4Sub5.Type99Sub2Sub3Sub4Sub5Sub6.swiftFunc0(89, 12, 37, 7, 8, 54, 62);
            Assert.Equal(-4745752112673179602, result);
        }

        [Fact]
        public static void TestSwiftType454()
        {
            long result = Swift.StaticMethodsTests.Type100.swiftFunc0(0);
            Assert.Equal(590684067820433389, result);
        }

        [Fact]
        public static void TestSwiftType455()
        {
            long result = Swift.StaticMethodsTests.Type100.Type100Sub2.swiftFunc0(71, 64, 55, 41, 54, 49, 67, 26.89, 59, 35);
            Assert.Equal(-5657265487867349989, result);
        }

        [Fact]
        public static void TestSwiftType456()
        {
            long result = Swift.StaticMethodsTests.Type100.Type100Sub2.Type100Sub2Sub3.swiftFunc0(23.65, 28, 40, 48, 61.95, 70, 64, 61, 68.06);
            Assert.Equal(8722873399830605424, result);
        }

        [Fact]
        public static void TestSwiftType457()
        {
            long result = Swift.StaticMethodsTests.Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.swiftFunc0(85.23, 98, 77);
            Assert.Equal(-4371179691031244903, result);
        }

        [Fact]
        public static void TestSwiftType458()
        {
            long result = Swift.StaticMethodsTests.Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.swiftFunc0(48, 79, 19, 59.51, 24, 15, 49, 32);
            Assert.Equal(-3794336876723658367, result);
        }

        [Fact]
        public static void TestSwiftType459()
        {
            long result = Swift.StaticMethodsTests.Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.swiftFunc0(32, 38);
            Assert.Equal(322182842193855259, result);
        }

        [Fact]
        public static void TestSwiftType460()
        {
            long result = Swift.StaticMethodsTests.Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(4, 51, 18, 26.07);
            Assert.Equal(6237885446262797985, result);
        }

        [Fact]
        public static void TestSwiftType461()
        {
            long result = Swift.StaticMethodsTests.Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(65, 0, 36, 13, 100, 4, 27, 13);
            Assert.Equal(-622298591155045021, result);
        }

        [Fact]
        public static void TestSwiftType462()
        {
            long result = Swift.StaticMethodsTests.Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(42.21, 52, 67, 100);
            Assert.Equal(-6521523735227058600, result);
        }

        [Fact]
        public static void TestSwiftType463()
        {
            long result = Swift.StaticMethodsTests.Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(6, 1.90, 24.81, 82, 36.35, 64, 63, 69, 87, 23);
            Assert.Equal(-7180632988021255058, result);
        }

    }
}
