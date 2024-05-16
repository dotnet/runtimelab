// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Swift.StaticMethodsTests;

namespace Test {
    public class MainClass {
        public static int Main(string[] args)
        {
            return 0;
        }
        public static long SwiftType0()
        {
            long result = Type1.swiftFunc0(9, 57);
            return result;
        }
        
        public static long SwiftType1()
        {
            long result = Type1.Type1Sub2.swiftFunc0(30);
            return result;
        }
        
        public static long SwiftType2()
        {
            long result = Type1.Type1Sub2.Type1Sub2Sub3.swiftFunc0(47, 19, 43, 56, 17, 57, 45, 77, 7);
            return result;
        }
        
        public static long SwiftType3()
        {
            long result = Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.swiftFunc0(10, 65, 2, 68.10, 84, 17, 75, 89, 94, 28);
            return result;
        }
        
        public static long SwiftType4()
        {
            long result = Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.Type1Sub2Sub3Sub4Sub5.swiftFunc0(38, 55, 77, 5, 37);
            return result;
        }
        
        public static long SwiftType5()
        {
            long result = Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.Type1Sub2Sub3Sub4Sub5.Type1Sub2Sub3Sub4Sub5Sub6.swiftFunc0(12, 99, 15, 55, 52, 25, 59, 22, 5, 73.35);
            return result;
        }
        
        public static long SwiftType6()
        {
            long result = Type2.swiftFunc0(83, 32, 43, 15, 46, 4);
            return result;
        }
        
        public static long SwiftType7()
        {
            long result = Type2.Type2Sub2.swiftFunc0(97, 12, 53);
            return result;
        }
        
        public static long SwiftType8()
        {
            long result = Type2.Type2Sub2.Type2Sub2Sub3.swiftFunc0(85, 19, 5, 30, 75.30, 53, 42, 50);
            return result;
        }
        
        public static long SwiftType9()
        {
            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.swiftFunc0(22, 12, 62, 99, 10, 82, 19, 11, 36);
            return result;
        }
        
        public static long SwiftType10()
        {
            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.swiftFunc0(71, 21, 76, 44.79, 13, 6.64, 85, 90, 88.02);
            return result;
        }
        
        public static long SwiftType11()
        {
            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.swiftFunc0(1, 66, 48, 11, 74.86, 29, 2);
            return result;
        }
        
        public static long SwiftType12()
        {
            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.Type2Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(54, 28.87, 1, 56, 41.63);
            return result;
        }
        
        public static long SwiftType13()
        {
            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.Type2Sub2Sub3Sub4Sub5Sub6Sub7.Type2Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(5, 48.78, 13.27, 28, 60.92);
            return result;
        }
        
        public static long SwiftType14()
        {
            long result = Type3.swiftFunc0(64, 48, 10, 20, 57, 18, 98);
            return result;
        }
        
        public static long SwiftType15()
        {
            long result = Type3.Type3Sub2.swiftFunc0(15.44, 57, 64.31, 35, 67, 12, 96, 52.06, 19);
            return result;
        }
        
        public static long SwiftType16()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.swiftFunc0(96);
            return result;
        }
        
        public static long SwiftType17()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.swiftFunc0(18, 60);
            return result;
        }
        
        public static long SwiftType18()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.swiftFunc0(17, 4, 63.40);
            return result;
        }
        
        public static long SwiftType19()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.swiftFunc0(48, 14, 10.64, 2, 43, 8.57);
            return result;
        }
        
        public static long SwiftType20()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(0.69);
            return result;
        }
        
        public static long SwiftType21()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(60.15, 75, 37, 70, 13, 44);
            return result;
        }
        
        public static long SwiftType22()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(35, 74.04, 98, 8);
            return result;
        }
        
        public static long SwiftType23()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(45, 48, 20, 65, 98, 74, 81);
            return result;
        }
        
        public static long SwiftType24()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0(9);
            return result;
        }
        
        public static long SwiftType25()
        {
            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11Sub12.swiftFunc0(98, 74.69, 32.29, 13, 53);
            return result;
        }
        
        public static long SwiftType26()
        {
            long result = Type4.swiftFunc0(51, 53, 38.12, 86, 91.96, 20, 82, 73, 38, 56);
            return result;
        }
        
        public static long SwiftType27()
        {
            long result = Type5.swiftFunc0(0, 82.91, 50, 84, 85, 90, 87, 49);
            return result;
        }
        
        public static long SwiftType28()
        {
            long result = Type5.Type5Sub2.swiftFunc0(77, 64, 77, 57, 60, 68, 61.72, 38, 77, 4.87);
            return result;
        }
        
        public static long SwiftType29()
        {
            long result = Type6.swiftFunc0(51, 81, 5, 12, 12, 19);
            return result;
        }
        
        public static long SwiftType30()
        {
            long result = Type6.Type6Sub2.swiftFunc0(68, 97, 7, 1, 63, 9, 43, 73);
            return result;
        }
        
        public static long SwiftType31()
        {
            long result = Type6.Type6Sub2.Type6Sub2Sub3.swiftFunc0(84, 74, 75, 56);
            return result;
        }
        
        public static long SwiftType32()
        {
            long result = Type6.Type6Sub2.Type6Sub2Sub3.Type6Sub2Sub3Sub4.swiftFunc0(58, 96, 45, 73);
            return result;
        }
        
        public static long SwiftType33()
        {
            long result = Type6.Type6Sub2.Type6Sub2Sub3.Type6Sub2Sub3Sub4.Type6Sub2Sub3Sub4Sub5.swiftFunc0(1, 81, 27, 91, 76, 66.80, 60, 42, 11, 76);
            return result;
        }
        
        public static long SwiftType34()
        {
            long result = Type7.swiftFunc0(64, 87, 60, 64);
            return result;
        }
        
        public static long SwiftType35()
        {
            long result = Type7.Type7Sub2.swiftFunc0(28, 54, 53);
            return result;
        }
        
        public static long SwiftType36()
        {
            long result = Type7.Type7Sub2.Type7Sub2Sub3.swiftFunc0(43, 97, 52, 24, 30.99, 26);
            return result;
        }
        
        public static long SwiftType37()
        {
            long result = Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.swiftFunc0(100, 26.83, 24, 81, 65, 75, 92.49, 100, 4);
            return result;
        }
        
        public static long SwiftType38()
        {
            long result = Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.Type7Sub2Sub3Sub4Sub5.swiftFunc0(68, 100, 74.14, 61);
            return result;
        }
        
        public static long SwiftType39()
        {
            long result = Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.Type7Sub2Sub3Sub4Sub5.Type7Sub2Sub3Sub4Sub5Sub6.swiftFunc0(66, 37, 26, 66, 10, 34, 3.57, 94, 84);
            return result;
        }
        
        public static long SwiftType40()
        {
            long result = Type8.swiftFunc0(43, 99, 22.16);
            return result;
        }
        
        public static long SwiftType41()
        {
            long result = Type8.Type8Sub2.swiftFunc0(28, 62, 100);
            return result;
        }
        
        public static long SwiftType42()
        {
            long result = Type8.Type8Sub2.Type8Sub2Sub3.swiftFunc0(67, 18, 9, 23, 64, 87, 35, 26, 49, 92);
            return result;
        }
        
        public static long SwiftType43()
        {
            long result = Type8.Type8Sub2.Type8Sub2Sub3.Type8Sub2Sub3Sub4.swiftFunc0(13, 10, 40, 37.89, 71);
            return result;
        }
        
        public static long SwiftType44()
        {
            long result = Type8.Type8Sub2.Type8Sub2Sub3.Type8Sub2Sub3Sub4.Type8Sub2Sub3Sub4Sub5.swiftFunc0(71, 38.64, 24, 43, 69, 60.10);
            return result;
        }
        
        public static long SwiftType45()
        {
            long result = Type9.swiftFunc0(20, 74, 55, 56, 72, 12);
            return result;
        }
        
        public static long SwiftType46()
        {
            long result = Type9.Type9Sub2.swiftFunc0(29, 88, 15.50, 99, 55);
            return result;
        }
        
        public static long SwiftType47()
        {
            long result = Type9.Type9Sub2.Type9Sub2Sub3.swiftFunc0(24, 77, 0, 19, 67, 12, 32.25, 84);
            return result;
        }
        
        public static long SwiftType48()
        {
            long result = Type10.swiftFunc0(46, 35, 43, 3, 1, 72, 3, 5);
            return result;
        }
        
        public static long SwiftType49()
        {
            long result = Type10.Type10Sub2.swiftFunc0(45, 3, 97, 48, 100);
            return result;
        }
        
        public static long SwiftType50()
        {
            long result = Type10.Type10Sub2.Type10Sub2Sub3.swiftFunc0(99, 64, 18.04, 48);
            return result;
        }
        
        public static long SwiftType51()
        {
            long result = Type11.swiftFunc0(33, 7);
            return result;
        }
        
        public static long SwiftType52()
        {
            long result = Type11.Type11Sub2.swiftFunc0(35);
            return result;
        }
        
        public static long SwiftType53()
        {
            long result = Type11.Type11Sub2.Type11Sub2Sub3.swiftFunc0(77, 76, 16.20, 96, 15.68, 67, 51, 76, 73);
            return result;
        }
        
        public static long SwiftType54()
        {
            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.swiftFunc0(60, 68.52, 47, 63, 77, 14, 73.90, 69, 1, 81);
            return result;
        }
        
        public static long SwiftType55()
        {
            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.swiftFunc0(53, 33, 87.06);
            return result;
        }
        
        public static long SwiftType56()
        {
            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.swiftFunc0(90, 75, 50, 38);
            return result;
        }
        
        public static long SwiftType57()
        {
            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.Type11Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(58.29, 93.54, 8, 68);
            return result;
        }
        
        public static long SwiftType58()
        {
            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.Type11Sub2Sub3Sub4Sub5Sub6Sub7.Type11Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(5.14, 6, 80, 77, 27, 23, 3, 19, 94, 77);
            return result;
        }
        
        public static long SwiftType59()
        {
            long result = Type12.swiftFunc0(40.61, 36, 29, 91, 96, 86);
            return result;
        }
        
        public static long SwiftType60()
        {
            long result = Type12.Type12Sub2.swiftFunc0(71, 39, 34.07);
            return result;
        }
        
        public static long SwiftType61()
        {
            long result = Type13.swiftFunc0(9, 9, 60.59, 96, 66, 61.51, 53, 82);
            return result;
        }
        
        public static long SwiftType62()
        {
            long result = Type13.Type13Sub2.swiftFunc0(55, 100);
            return result;
        }
        
        public static long SwiftType63()
        {
            long result = Type13.Type13Sub2.Type13Sub2Sub3.swiftFunc0(17, 21.05, 47, 13, 99);
            return result;
        }
        
        public static long SwiftType64()
        {
            long result = Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.swiftFunc0(70, 0, 87, 57);
            return result;
        }
        
        public static long SwiftType65()
        {
            long result = Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.swiftFunc0(28, 69, 10.54, 64, 13, 17.41, 81, 33.57);
            return result;
        }
        
        public static long SwiftType66()
        {
            long result = Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.Type13Sub2Sub3Sub4Sub5Sub6.swiftFunc0(38, 3);
            return result;
        }
        
        public static long SwiftType67()
        {
            long result = Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.Type13Sub2Sub3Sub4Sub5Sub6.Type13Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(56, 68, 67.66);
            return result;
        }
        
        public static long SwiftType68()
        {
            long result = Type14.swiftFunc0(39, 11.26, 72, 88, 24, 27, 48, 76, 31.63);
            return result;
        }
        
        public static long SwiftType69()
        {
            long result = Type15.swiftFunc0(64, 20, 8, 36, 74, 77, 91, 24.38, 27);
            return result;
        }
        
        public static long SwiftType70()
        {
            long result = Type15.Type15Sub2.swiftFunc0(84.10, 66.46, 5, 11, 10, 95, 26, 26, 35);
            return result;
        }
        
        public static long SwiftType71()
        {
            long result = Type15.Type15Sub2.Type15Sub2Sub3.swiftFunc0(27, 16, 78.80, 78, 20, 25, 0.02, 78, 80);
            return result;
        }
        
        public static long SwiftType72()
        {
            long result = Type16.swiftFunc0(40, 39);
            return result;
        }
        
        public static long SwiftType73()
        {
            long result = Type16.Type16Sub2.swiftFunc0(41.36, 29.18, 79, 61.72, 98.56, 29, 1, 31);
            return result;
        }
        
        public static long SwiftType74()
        {
            long result = Type17.swiftFunc0(4, 16, 89);
            return result;
        }
        
        public static long SwiftType75()
        {
            long result = Type17.Type17Sub2.swiftFunc0(22, 60, 28, 68);
            return result;
        }
        
        public static long SwiftType76()
        {
            long result = Type17.Type17Sub2.Type17Sub2Sub3.swiftFunc0(7, 91, 5, 68, 92, 13, 98, 37.01);
            return result;
        }
        
        public static long SwiftType77()
        {
            long result = Type17.Type17Sub2.Type17Sub2Sub3.Type17Sub2Sub3Sub4.swiftFunc0(50, 4, 24, 41.75);
            return result;
        }
        
        public static long SwiftType78()
        {
            long result = Type18.swiftFunc0(60.67, 91, 20, 7.71, 51, 69, 15);
            return result;
        }
        
        public static long SwiftType79()
        {
            long result = Type18.Type18Sub2.swiftFunc0(37, 78, 60, 35);
            return result;
        }
        
        public static long SwiftType80()
        {
            long result = Type19.swiftFunc0(42, 94, 99, 14, 61);
            return result;
        }
        
        public static long SwiftType81()
        {
            long result = Type19.Type19Sub2.swiftFunc0(60.53, 26.15, 83, 10.19, 58, 44, 51);
            return result;
        }
        
        public static long SwiftType82()
        {
            long result = Type19.Type19Sub2.Type19Sub2Sub3.swiftFunc0(73.94, 46, 18, 4.69, 93, 59, 16, 58);
            return result;
        }
        
        public static long SwiftType83()
        {
            long result = Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.swiftFunc0(37.20, 5, 15.16, 42.91, 52.97, 51);
            return result;
        }
        
        public static long SwiftType84()
        {
            long result = Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.swiftFunc0(10, 88, 33.80, 22.70, 13);
            return result;
        }
        
        public static long SwiftType85()
        {
            long result = Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.Type19Sub2Sub3Sub4Sub5Sub6.swiftFunc0(84);
            return result;
        }
        
        public static long SwiftType86()
        {
            long result = Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.Type19Sub2Sub3Sub4Sub5Sub6.Type19Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(27, 48.79, 63, 71.75, 39, 50, 29, 70.13);
            return result;
        }
        
        public static long SwiftType87()
        {
            long result = Type20.swiftFunc0(11, 54, 49);
            return result;
        }
        
        public static long SwiftType88()
        {
            long result = Type20.Type20Sub2.swiftFunc0(12, 51, 32, 98);
            return result;
        }
        
        public static long SwiftType89()
        {
            long result = Type20.Type20Sub2.Type20Sub2Sub3.swiftFunc0(36, 65, 84, 75, 51, 50, 56, 47.91, 7);
            return result;
        }
        
        public static long SwiftType90()
        {
            long result = Type20.Type20Sub2.Type20Sub2Sub3.Type20Sub2Sub3Sub4.swiftFunc0(37, 91, 82, 69, 52, 76.67, 17, 13);
            return result;
        }
        
        public static long SwiftType91()
        {
            long result = Type20.Type20Sub2.Type20Sub2Sub3.Type20Sub2Sub3Sub4.Type20Sub2Sub3Sub4Sub5.swiftFunc0(50);
            return result;
        }
        
        public static long SwiftType92()
        {
            long result = Type21.swiftFunc0(65, 90, 100, 91, 36, 62, 36, 41);
            return result;
        }
        
        public static long SwiftType93()
        {
            long result = Type21.Type21Sub2.swiftFunc0(88, 24, 51, 11, 70.97, 37, 34, 2);
            return result;
        }
        
        public static long SwiftType94()
        {
            long result = Type21.Type21Sub2.Type21Sub2Sub3.swiftFunc0(33, 85, 69, 8.53, 99);
            return result;
        }
        
        public static long SwiftType95()
        {
            long result = Type22.swiftFunc0(65.66, 93, 44, 53.51, 78, 39, 69, 32.52, 34);
            return result;
        }
        
        public static long SwiftType96()
        {
            long result = Type22.Type22Sub2.swiftFunc0(23);
            return result;
        }
        
        public static long SwiftType97()
        {
            long result = Type22.Type22Sub2.Type22Sub2Sub3.swiftFunc0(78, 99);
            return result;
        }
        
        public static long SwiftType98()
        {
            long result = Type23.swiftFunc0(52, 2.14, 5.51, 27, 73.58, 26, 59, 64, 19, 81);
            return result;
        }
        
        public static long SwiftType99()
        {
            long result = Type23.Type23Sub2.swiftFunc0(52, 14, 89, 47, 29, 100, 6, 58, 2);
            return result;
        }
        
        public static long SwiftType100()
        {
            long result = Type23.Type23Sub2.Type23Sub2Sub3.swiftFunc0(17, 13, 66, 35.35, 75);
            return result;
        }
        
        public static long SwiftType101()
        {
            long result = Type24.swiftFunc0(31);
            return result;
        }
        
        public static long SwiftType102()
        {
            long result = Type24.Type24Sub2.swiftFunc0(53, 18.02, 65, 18, 65);
            return result;
        }
        
        public static long SwiftType103()
        {
            long result = Type24.Type24Sub2.Type24Sub2Sub3.swiftFunc0(62, 79, 41, 59, 66, 17, 42);
            return result;
        }
        
        public static long SwiftType104()
        {
            long result = Type24.Type24Sub2.Type24Sub2Sub3.Type24Sub2Sub3Sub4.swiftFunc0(4);
            return result;
        }
        
        public static long SwiftType105()
        {
            long result = Type24.Type24Sub2.Type24Sub2Sub3.Type24Sub2Sub3Sub4.Type24Sub2Sub3Sub4Sub5.swiftFunc0(69, 34, 6, 85, 100);
            return result;
        }
        
        public static long SwiftType106()
        {
            long result = Type25.swiftFunc0(67, 33.68, 78, 47.50, 51, 93.80, 62, 46);
            return result;
        }
        
        public static long SwiftType107()
        {
            long result = Type25.Type25Sub2.swiftFunc0(74, 38, 88, 46, 37, 45, 99, 40, 55, 12);
            return result;
        }
        
        public static long SwiftType108()
        {
            long result = Type25.Type25Sub2.Type25Sub2Sub3.swiftFunc0(86, 16, 98, 29);
            return result;
        }
        
        public static long SwiftType109()
        {
            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.swiftFunc0(73, 16, 22);
            return result;
        }
        
        public static long SwiftType110()
        {
            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.swiftFunc0(55, 71, 97, 47, 11, 34);
            return result;
        }
        
        public static long SwiftType111()
        {
            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.swiftFunc0(5, 73, 48, 8, 82.30);
            return result;
        }
        
        public static long SwiftType112()
        {
            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.Type25Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(13, 41, 14, 20, 87.13, 100);
            return result;
        }
        
        public static long SwiftType113()
        {
            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.Type25Sub2Sub3Sub4Sub5Sub6Sub7.Type25Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(18.14, 78, 48);
            return result;
        }
        
        public static long SwiftType114()
        {
            long result = Type26.swiftFunc0(68, 90, 39, 90);
            return result;
        }
        
        public static long SwiftType115()
        {
            long result = Type27.swiftFunc0(61, 88, 44, 32, 82);
            return result;
        }
        
        public static long SwiftType116()
        {
            long result = Type27.Type27Sub2.swiftFunc0(75);
            return result;
        }
        
        public static long SwiftType117()
        {
            long result = Type27.Type27Sub2.Type27Sub2Sub3.swiftFunc0(53, 82, 31);
            return result;
        }
        
        public static long SwiftType118()
        {
            long result = Type27.Type27Sub2.Type27Sub2Sub3.Type27Sub2Sub3Sub4.swiftFunc0(36, 55, 14, 28, 67.94, 12, 9, 79);
            return result;
        }
        
        public static long SwiftType119()
        {
            long result = Type27.Type27Sub2.Type27Sub2Sub3.Type27Sub2Sub3Sub4.Type27Sub2Sub3Sub4Sub5.swiftFunc0(38.25, 34.89, 45, 79, 61, 95, 30.73, 9, 87, 21);
            return result;
        }
        
        public static long SwiftType120()
        {
            long result = Type28.swiftFunc0(55, 41, 78, 3.10, 28);
            return result;
        }
        
        public static long SwiftType121()
        {
            long result = Type28.Type28Sub2.swiftFunc0(37, 43, 71, 67);
            return result;
        }
        
        public static long SwiftType122()
        {
            long result = Type28.Type28Sub2.Type28Sub2Sub3.swiftFunc0(62, 40, 99, 87, 71, 6, 90, 15, 45);
            return result;
        }
        
        public static long SwiftType123()
        {
            long result = Type28.Type28Sub2.Type28Sub2Sub3.Type28Sub2Sub3Sub4.swiftFunc0(5, 0, 8);
            return result;
        }
        
        public static long SwiftType124()
        {
            long result = Type28.Type28Sub2.Type28Sub2Sub3.Type28Sub2Sub3Sub4.Type28Sub2Sub3Sub4Sub5.swiftFunc0(77.98, 64, 76, 30, 28, 72, 33, 22, 64, 83);
            return result;
        }
        
        public static long SwiftType125()
        {
            long result = Type29.swiftFunc0(61.61, 89, 1, 9, 19, 23, 35, 97.07, 19, 97);
            return result;
        }
        
        public static long SwiftType126()
        {
            long result = Type29.Type29Sub2.swiftFunc0(68.09, 4, 79.03);
            return result;
        }
        
        public static long SwiftType127()
        {
            long result = Type29.Type29Sub2.Type29Sub2Sub3.swiftFunc0(40, 68, 25, 54, 29.74, 43, 11, 53);
            return result;
        }
        
        public static long SwiftType128()
        {
            long result = Type29.Type29Sub2.Type29Sub2Sub3.Type29Sub2Sub3Sub4.swiftFunc0(31, 50, 5.28, 30, 31, 26.71, 90, 28, 15);
            return result;
        }
        
        public static long SwiftType129()
        {
            long result = Type30.swiftFunc0(24.73, 31.60, 31, 95.56, 26.29, 94, 56, 85, 37);
            return result;
        }
        
        public static long SwiftType130()
        {
            long result = Type30.Type30Sub2.swiftFunc0(53, 35, 77.32, 32, 24);
            return result;
        }
        
        public static long SwiftType131()
        {
            long result = Type30.Type30Sub2.Type30Sub2Sub3.swiftFunc0(40, 26, 6, 79, 97, 23, 94, 41);
            return result;
        }
        
        public static long SwiftType132()
        {
            long result = Type31.swiftFunc0(22, 22.07, 46, 19, 63.41, 80, 32, 72);
            return result;
        }
        
        public static long SwiftType133()
        {
            long result = Type31.Type31Sub2.swiftFunc0(25, 73, 18, 45, 85, 57, 86);
            return result;
        }
        
        public static long SwiftType134()
        {
            long result = Type31.Type31Sub2.Type31Sub2Sub3.swiftFunc0(69, 64, 6, 59.17, 6, 42, 28);
            return result;
        }
        
        public static long SwiftType135()
        {
            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.swiftFunc0(67, 80, 91, 94, 13, 44, 38, 27, 38);
            return result;
        }
        
        public static long SwiftType136()
        {
            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.swiftFunc0(48.86, 94);
            return result;
        }
        
        public static long SwiftType137()
        {
            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.swiftFunc0(37, 100, 63, 70, 92, 79, 6);
            return result;
        }
        
        public static long SwiftType138()
        {
            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.Type31Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(28.04);
            return result;
        }
        
        public static long SwiftType139()
        {
            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.Type31Sub2Sub3Sub4Sub5Sub6Sub7.Type31Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(60, 9.42, 66, 88.47, 53, 68, 23.71, 41, 98);
            return result;
        }
        
        public static long SwiftType140()
        {
            long result = Type32.swiftFunc0(87, 92, 98, 50, 10.13, 68, 59, 10, 59);
            return result;
        }
        
        public static long SwiftType141()
        {
            long result = Type32.Type32Sub2.swiftFunc0(61.68, 15, 82, 33, 95.38, 11.56, 50, 69.24);
            return result;
        }
        
        public static long SwiftType142()
        {
            long result = Type33.swiftFunc0(10, 89);
            return result;
        }
        
        public static long SwiftType143()
        {
            long result = Type33.Type33Sub2.swiftFunc0(63, 100, 11, 96, 54, 81, 98);
            return result;
        }
        
        public static long SwiftType144()
        {
            long result = Type33.Type33Sub2.Type33Sub2Sub3.swiftFunc0(62, 55.58, 45, 7.82, 32, 96);
            return result;
        }
        
        public static long SwiftType145()
        {
            long result = Type33.Type33Sub2.Type33Sub2Sub3.Type33Sub2Sub3Sub4.swiftFunc0(75, 22, 33, 24, 81, 58, 43.18, 20);
            return result;
        }
        
        public static long SwiftType146()
        {
            long result = Type34.swiftFunc0(89, 4, 46, 56, 38);
            return result;
        }
        
        public static long SwiftType147()
        {
            long result = Type34.Type34Sub2.swiftFunc0(17.48, 96, 11, 72, 35);
            return result;
        }
        
        public static long SwiftType148()
        {
            long result = Type34.Type34Sub2.Type34Sub2Sub3.swiftFunc0(41);
            return result;
        }
        
        public static long SwiftType149()
        {
            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.swiftFunc0(3.97, 47, 5.29, 1, 70, 17);
            return result;
        }
        
        public static long SwiftType150()
        {
            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.swiftFunc0(97, 34.50, 100, 15, 70, 64, 43.93);
            return result;
        }
        
        public static long SwiftType151()
        {
            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.swiftFunc0(64, 56, 14, 89.68, 58, 2, 83, 63, 67);
            return result;
        }
        
        public static long SwiftType152()
        {
            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(71, 69, 26);
            return result;
        }
        
        public static long SwiftType153()
        {
            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(37, 66.74);
            return result;
        }
        
        public static long SwiftType154()
        {
            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(83, 16, 98, 9.87, 66, 62, 81);
            return result;
        }
        
        public static long SwiftType155()
        {
            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(61, 16, 77, 40, 86, 1);
            return result;
        }
        
        public static long SwiftType156()
        {
            long result = Type35.swiftFunc0(40, 42, 83, 71, 66, 26, 67);
            return result;
        }
        
        public static long SwiftType157()
        {
            long result = Type35.Type35Sub2.swiftFunc0(99, 87, 9, 11.35, 96.05);
            return result;
        }
        
        public static long SwiftType158()
        {
            long result = Type35.Type35Sub2.Type35Sub2Sub3.swiftFunc0(28);
            return result;
        }
        
        public static long SwiftType159()
        {
            long result = Type35.Type35Sub2.Type35Sub2Sub3.Type35Sub2Sub3Sub4.swiftFunc0(46.79, 41.75, 93, 48, 25, 44, 60, 34, 4);
            return result;
        }
        
        public static long SwiftType160()
        {
            long result = Type35.Type35Sub2.Type35Sub2Sub3.Type35Sub2Sub3Sub4.Type35Sub2Sub3Sub4Sub5.swiftFunc0(86);
            return result;
        }
        
        public static long SwiftType161()
        {
            long result = Type36.swiftFunc0(74, 35, 82.42, 68.23, 4, 3, 23, 46, 100);
            return result;
        }
        
        public static long SwiftType162()
        {
            long result = Type36.Type36Sub2.swiftFunc0(99, 31, 71, 50, 8, 73.31, 95, 20.19, 18);
            return result;
        }
        
        public static long SwiftType163()
        {
            long result = Type36.Type36Sub2.Type36Sub2Sub3.swiftFunc0(91, 95, 19.35, 0, 75, 55.48, 11, 58);
            return result;
        }
        
        public static long SwiftType164()
        {
            long result = Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.swiftFunc0(76.20);
            return result;
        }
        
        public static long SwiftType165()
        {
            long result = Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.Type36Sub2Sub3Sub4Sub5.swiftFunc0(20);
            return result;
        }
        
        public static long SwiftType166()
        {
            long result = Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.Type36Sub2Sub3Sub4Sub5.Type36Sub2Sub3Sub4Sub5Sub6.swiftFunc0(70, 13.23, 18, 20);
            return result;
        }
        
        public static long SwiftType167()
        {
            long result = Type37.swiftFunc0(100, 10.01);
            return result;
        }
        
        public static long SwiftType168()
        {
            long result = Type37.Type37Sub2.swiftFunc0(20, 89, 79, 41, 20, 3);
            return result;
        }
        
        public static long SwiftType169()
        {
            long result = Type37.Type37Sub2.Type37Sub2Sub3.swiftFunc0(50, 41);
            return result;
        }
        
        public static long SwiftType170()
        {
            long result = Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.swiftFunc0(96.38);
            return result;
        }
        
        public static long SwiftType171()
        {
            long result = Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.swiftFunc0(55, 19, 58, 73, 41.32);
            return result;
        }
        
        public static long SwiftType172()
        {
            long result = Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.Type37Sub2Sub3Sub4Sub5Sub6.swiftFunc0(81, 17.14, 34.21, 69, 48, 99, 95, 18, 49.15, 5.13);
            return result;
        }
        
        public static long SwiftType173()
        {
            long result = Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.Type37Sub2Sub3Sub4Sub5Sub6.Type37Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(85);
            return result;
        }
        
        public static long SwiftType174()
        {
            long result = Type38.swiftFunc0(24, 5, 86, 34, 31, 12, 27, 57.54, 63);
            return result;
        }
        
        public static long SwiftType175()
        {
            long result = Type38.Type38Sub2.swiftFunc0(30, 79.43, 70, 81, 18.21, 53);
            return result;
        }
        
        public static long SwiftType176()
        {
            long result = Type38.Type38Sub2.Type38Sub2Sub3.swiftFunc0(61, 55.43, 37, 32.95, 47, 7);
            return result;
        }
        
        public static long SwiftType177()
        {
            long result = Type38.Type38Sub2.Type38Sub2Sub3.Type38Sub2Sub3Sub4.swiftFunc0(17, 88, 61, 12, 71, 1, 88);
            return result;
        }
        
        public static long SwiftType178()
        {
            long result = Type38.Type38Sub2.Type38Sub2Sub3.Type38Sub2Sub3Sub4.Type38Sub2Sub3Sub4Sub5.swiftFunc0(7, 72, 61);
            return result;
        }
        
        public static long SwiftType179()
        {
            long result = Type39.swiftFunc0(71, 29, 18, 74, 19, 1, 39, 57);
            return result;
        }
        
        public static long SwiftType180()
        {
            long result = Type39.Type39Sub2.swiftFunc0(21.10, 39, 24, 9, 89);
            return result;
        }
        
        public static long SwiftType181()
        {
            long result = Type39.Type39Sub2.Type39Sub2Sub3.swiftFunc0(6, 17, 13, 88, 58, 50, 35.88, 11.76, 25, 79);
            return result;
        }
        
        public static long SwiftType182()
        {
            long result = Type40.swiftFunc0(11, 22, 81.72, 92, 11, 94, 87, 40);
            return result;
        }
        
        public static long SwiftType183()
        {
            long result = Type40.Type40Sub2.swiftFunc0(62, 6, 74, 41, 45, 50, 36, 39, 76);
            return result;
        }
        
        public static long SwiftType184()
        {
            long result = Type40.Type40Sub2.Type40Sub2Sub3.swiftFunc0(86, 76, 47, 68, 18, 2.53, 83, 62.25);
            return result;
        }
        
        public static long SwiftType185()
        {
            long result = Type40.Type40Sub2.Type40Sub2Sub3.Type40Sub2Sub3Sub4.swiftFunc0(26.49, 9, 20, 93);
            return result;
        }
        
        public static long SwiftType186()
        {
            long result = Type41.swiftFunc0(20, 37, 96, 53, 19, 58.48, 5, 87, 72, 37);
            return result;
        }
        
        public static long SwiftType187()
        {
            long result = Type42.swiftFunc0(57, 13, 75.72, 75, 74, 79, 43.53);
            return result;
        }
        
        public static long SwiftType188()
        {
            long result = Type42.Type42Sub2.swiftFunc0(22, 25, 98, 30, 88, 98, 43, 23.85, 45, 23);
            return result;
        }
        
        public static long SwiftType189()
        {
            long result = Type42.Type42Sub2.Type42Sub2Sub3.swiftFunc0(100, 40);
            return result;
        }
        
        public static long SwiftType190()
        {
            long result = Type42.Type42Sub2.Type42Sub2Sub3.Type42Sub2Sub3Sub4.swiftFunc0(54, 22, 29, 75, 76, 33, 63, 33, 14);
            return result;
        }
        
        public static long SwiftType191()
        {
            long result = Type43.swiftFunc0(59, 99, 7);
            return result;
        }
        
        public static long SwiftType192()
        {
            long result = Type43.Type43Sub2.swiftFunc0(12, 11, 55, 16, 76, 86);
            return result;
        }
        
        public static long SwiftType193()
        {
            long result = Type43.Type43Sub2.Type43Sub2Sub3.swiftFunc0(25, 79, 72.86, 38, 46, 24);
            return result;
        }
        
        public static long SwiftType194()
        {
            long result = Type44.swiftFunc0(64, 46, 32, 81, 24, 8, 100);
            return result;
        }
        
        public static long SwiftType195()
        {
            long result = Type44.Type44Sub2.swiftFunc0(95);
            return result;
        }
        
        public static long SwiftType196()
        {
            long result = Type44.Type44Sub2.Type44Sub2Sub3.swiftFunc0(78, 72, 10, 5, 34, 32, 65.87, 88, 36);
            return result;
        }
        
        public static long SwiftType197()
        {
            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.swiftFunc0(60.98, 15, 72, 20, 0.43, 65, 21, 71, 12.86, 32);
            return result;
        }
        
        public static long SwiftType198()
        {
            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.swiftFunc0(29.61);
            return result;
        }
        
        public static long SwiftType199()
        {
            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.swiftFunc0(5, 52, 40, 64, 84, 64, 19, 68.39, 71);
            return result;
        }
        
        public static long SwiftType200()
        {
            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(59, 37, 37, 1, 86, 52, 97, 23.50, 41);
            return result;
        }
        
        public static long SwiftType201()
        {
            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(56, 23, 75, 33, 90, 38, 17.65, 38, 13);
            return result;
        }
        
        public static long SwiftType202()
        {
            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(95.21, 34, 32, 15, 20, 69, 95);
            return result;
        }
        
        public static long SwiftType203()
        {
            long result = Type45.swiftFunc0(3, 73, 19.95, 70, 35, 47, 1, 35.40, 98.95, 92);
            return result;
        }
        
        public static long SwiftType204()
        {
            long result = Type45.Type45Sub2.swiftFunc0(22, 39.68, 50, 39, 49, 81.21);
            return result;
        }
        
        public static long SwiftType205()
        {
            long result = Type45.Type45Sub2.Type45Sub2Sub3.swiftFunc0(98, 57, 43.08, 89, 51, 44.56, 30);
            return result;
        }
        
        public static long SwiftType206()
        {
            long result = Type46.swiftFunc0(78, 48, 90, 12, 80.62);
            return result;
        }
        
        public static long SwiftType207()
        {
            long result = Type46.Type46Sub2.swiftFunc0(99, 21);
            return result;
        }
        
        public static long SwiftType208()
        {
            long result = Type46.Type46Sub2.Type46Sub2Sub3.swiftFunc0(100, 32, 10, 47, 60, 32, 1, 99.92);
            return result;
        }
        
        public static long SwiftType209()
        {
            long result = Type46.Type46Sub2.Type46Sub2Sub3.Type46Sub2Sub3Sub4.swiftFunc0(69, 85.30, 26, 44, 41, 35.15);
            return result;
        }
        
        public static long SwiftType210()
        {
            long result = Type47.swiftFunc0(77.59, 16.71);
            return result;
        }
        
        public static long SwiftType211()
        {
            long result = Type47.Type47Sub2.swiftFunc0(88, 85, 21, 37, 75, 87, 67.88, 11.38, 43);
            return result;
        }
        
        public static long SwiftType212()
        {
            long result = Type47.Type47Sub2.Type47Sub2Sub3.swiftFunc0(62, 98);
            return result;
        }
        
        public static long SwiftType213()
        {
            long result = Type48.swiftFunc0(33, 16, 99, 15, 32, 24);
            return result;
        }
        
        public static long SwiftType214()
        {
            long result = Type48.Type48Sub2.swiftFunc0(77, 28, 68, 12, 14, 15, 43, 80);
            return result;
        }
        
        public static long SwiftType215()
        {
            long result = Type48.Type48Sub2.Type48Sub2Sub3.swiftFunc0(73, 89.03, 46, 76, 89.07, 83);
            return result;
        }
        
        public static long SwiftType216()
        {
            long result = Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.swiftFunc0(77, 50, 49, 90.87, 83);
            return result;
        }
        
        public static long SwiftType217()
        {
            long result = Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.Type48Sub2Sub3Sub4Sub5.swiftFunc0(80, 36, 36, 34, 61, 18, 16, 31, 65, 45);
            return result;
        }
        
        public static long SwiftType218()
        {
            long result = Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.Type48Sub2Sub3Sub4Sub5.Type48Sub2Sub3Sub4Sub5Sub6.swiftFunc0(12, 63.75);
            return result;
        }
        
        public static long SwiftType219()
        {
            long result = Type49.swiftFunc0(66, 61, 26, 14.45, 9, 2, 34);
            return result;
        }
        
        public static long SwiftType220()
        {
            long result = Type49.Type49Sub2.swiftFunc0(38, 58, 31, 88, 40);
            return result;
        }
        
        public static long SwiftType221()
        {
            long result = Type49.Type49Sub2.Type49Sub2Sub3.swiftFunc0(91, 22, 23, 86.07);
            return result;
        }
        
        public static long SwiftType222()
        {
            long result = Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.swiftFunc0(79.64, 64, 100, 10, 86, 34, 57.79);
            return result;
        }
        
        public static long SwiftType223()
        {
            long result = Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.swiftFunc0(94, 48.44, 61, 6.13, 43.63, 23, 60, 19.43);
            return result;
        }
        
        public static long SwiftType224()
        {
            long result = Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.Type49Sub2Sub3Sub4Sub5Sub6.swiftFunc0(98, 89, 61, 82, 57, 72);
            return result;
        }
        
        public static long SwiftType225()
        {
            long result = Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.Type49Sub2Sub3Sub4Sub5Sub6.Type49Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(76, 4, 45);
            return result;
        }
        
        public static long SwiftType226()
        {
            long result = Type50.swiftFunc0(32, 21, 6, 75, 7.43, 56, 8, 36);
            return result;
        }
        
        public static long SwiftType227()
        {
            long result = Type50.Type50Sub2.swiftFunc0(16, 98, 8, 9.02);
            return result;
        }
        
        public static long SwiftType228()
        {
            long result = Type51.swiftFunc0(74, 78, 77, 12, 24, 90);
            return result;
        }
        
        public static long SwiftType229()
        {
            long result = Type51.Type51Sub2.swiftFunc0(24);
            return result;
        }
        
        public static long SwiftType230()
        {
            long result = Type51.Type51Sub2.Type51Sub2Sub3.swiftFunc0(48, 1, 24, 76);
            return result;
        }
        
        public static long SwiftType231()
        {
            long result = Type51.Type51Sub2.Type51Sub2Sub3.Type51Sub2Sub3Sub4.swiftFunc0(74, 19, 34, 37, 8, 18, 91, 72);
            return result;
        }
        
        public static long SwiftType232()
        {
            long result = Type51.Type51Sub2.Type51Sub2Sub3.Type51Sub2Sub3Sub4.Type51Sub2Sub3Sub4Sub5.swiftFunc0(54, 65, 47, 6, 39, 29, 3, 77, 34);
            return result;
        }
        
        public static long SwiftType233()
        {
            long result = Type52.swiftFunc0(99, 84, 83.32, 44, 15);
            return result;
        }
        
        public static long SwiftType234()
        {
            long result = Type52.Type52Sub2.swiftFunc0(20, 82.67, 82, 71, 36, 53, 39.05, 73, 100);
            return result;
        }
        
        public static long SwiftType235()
        {
            long result = Type52.Type52Sub2.Type52Sub2Sub3.swiftFunc0(81, 59, 87, 71, 88, 15, 59, 38, 69);
            return result;
        }
        
        public static long SwiftType236()
        {
            long result = Type52.Type52Sub2.Type52Sub2Sub3.Type52Sub2Sub3Sub4.swiftFunc0(35, 91, 13.36, 35, 26.72);
            return result;
        }
        
        public static long SwiftType237()
        {
            long result = Type53.swiftFunc0(51);
            return result;
        }
        
        public static long SwiftType238()
        {
            long result = Type53.Type53Sub2.swiftFunc0(63, 9.78, 11, 0, 44, 28, 93, 89, 74, 55);
            return result;
        }
        
        public static long SwiftType239()
        {
            long result = Type54.swiftFunc0(37);
            return result;
        }
        
        public static long SwiftType240()
        {
            long result = Type54.Type54Sub2.swiftFunc0(28);
            return result;
        }
        
        public static long SwiftType241()
        {
            long result = Type54.Type54Sub2.Type54Sub2Sub3.swiftFunc0(97, 48.70, 83);
            return result;
        }
        
        public static long SwiftType242()
        {
            long result = Type54.Type54Sub2.Type54Sub2Sub3.Type54Sub2Sub3Sub4.swiftFunc0(29, 26, 25);
            return result;
        }
        
        public static long SwiftType243()
        {
            long result = Type55.swiftFunc0(96);
            return result;
        }
        
        public static long SwiftType244()
        {
            long result = Type55.Type55Sub2.swiftFunc0(43, 76, 76, 43, 13, 40);
            return result;
        }
        
        public static long SwiftType245()
        {
            long result = Type55.Type55Sub2.Type55Sub2Sub3.swiftFunc0(23, 84, 17, 22, 85.99, 67, 49, 62, 49);
            return result;
        }
        
        public static long SwiftType246()
        {
            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.swiftFunc0(24, 88.62, 100);
            return result;
        }
        
        public static long SwiftType247()
        {
            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.swiftFunc0(42, 18, 58, 98, 53, 19.39, 88, 20, 34);
            return result;
        }
        
        public static long SwiftType248()
        {
            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.swiftFunc0(73, 61);
            return result;
        }
        
        public static long SwiftType249()
        {
            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(97, 72, 36, 23, 92);
            return result;
        }
        
        public static long SwiftType250()
        {
            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(89, 88, 33.17, 91, 70, 12, 99, 67, 40, 97);
            return result;
        }
        
        public static long SwiftType251()
        {
            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(36, 65);
            return result;
        }
        
        public static long SwiftType252()
        {
            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(53.32, 95.33, 90, 14, 13, 30, 35, 16.69, 13, 44);
            return result;
        }
        
        public static long SwiftType253()
        {
            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0(56);
            return result;
        }
        
        public static long SwiftType254()
        {
            long result = Type56.swiftFunc0(53);
            return result;
        }
        
        public static long SwiftType255()
        {
            long result = Type56.Type56Sub2.swiftFunc0(40, 13, 41);
            return result;
        }
        
        public static long SwiftType256()
        {
            long result = Type56.Type56Sub2.Type56Sub2Sub3.swiftFunc0(58, 41);
            return result;
        }
        
        public static long SwiftType257()
        {
            long result = Type57.swiftFunc0(77, 19, 72, 42, 63, 5, 62.77, 25, 27);
            return result;
        }
        
        public static long SwiftType258()
        {
            long result = Type57.Type57Sub2.swiftFunc0(28, 46, 2, 85.90, 45, 59, 19, 75, 56);
            return result;
        }
        
        public static long SwiftType259()
        {
            long result = Type57.Type57Sub2.Type57Sub2Sub3.swiftFunc0(0, 35.78, 8, 54, 81, 61, 28, 91.93, 63.64);
            return result;
        }
        
        public static long SwiftType260()
        {
            long result = Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.swiftFunc0(21, 56.03);
            return result;
        }
        
        public static long SwiftType261()
        {
            long result = Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.Type57Sub2Sub3Sub4Sub5.swiftFunc0(21, 63.14, 67, 75, 63, 98.20, 4);
            return result;
        }
        
        public static long SwiftType262()
        {
            long result = Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.Type57Sub2Sub3Sub4Sub5.Type57Sub2Sub3Sub4Sub5Sub6.swiftFunc0(43);
            return result;
        }
        
        public static long SwiftType263()
        {
            long result = Type58.swiftFunc0(9, 79, 96, 21.65, 48, 67, 31);
            return result;
        }
        
        public static long SwiftType264()
        {
            long result = Type58.Type58Sub2.swiftFunc0(14, 49);
            return result;
        }
        
        public static long SwiftType265()
        {
            long result = Type59.swiftFunc0(70, 89, 13, 88, 3, 78.19, 90.91, 17, 20.88, 48);
            return result;
        }
        
        public static long SwiftType266()
        {
            long result = Type59.Type59Sub2.swiftFunc0(70);
            return result;
        }
        
        public static long SwiftType267()
        {
            long result = Type59.Type59Sub2.Type59Sub2Sub3.swiftFunc0(97);
            return result;
        }
        
        public static long SwiftType268()
        {
            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.swiftFunc0(53, 60, 90, 65, 63, 28, 98);
            return result;
        }
        
        public static long SwiftType269()
        {
            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.swiftFunc0(94, 18, 48, 2.84, 35, 26);
            return result;
        }
        
        public static long SwiftType270()
        {
            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.swiftFunc0(3);
            return result;
        }
        
        public static long SwiftType271()
        {
            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(13.31, 73, 3, 75, 84);
            return result;
        }
        
        public static long SwiftType272()
        {
            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(50, 56.68, 62);
            return result;
        }
        
        public static long SwiftType273()
        {
            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(79, 72, 69, 84.00, 43.48, 29);
            return result;
        }
        
        public static long SwiftType274()
        {
            long result = Type60.swiftFunc0(54, 18.20, 28, 87, 37, 95, 28);
            return result;
        }
        
        public static long SwiftType275()
        {
            long result = Type60.Type60Sub2.swiftFunc0(92, 68, 30.60, 0, 68, 1);
            return result;
        }
        
        public static long SwiftType276()
        {
            long result = Type60.Type60Sub2.Type60Sub2Sub3.swiftFunc0(4, 3, 46, 96, 69, 64, 84, 98.80);
            return result;
        }
        
        public static long SwiftType277()
        {
            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.swiftFunc0(15, 23.46, 35, 12, 39);
            return result;
        }
        
        public static long SwiftType278()
        {
            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.swiftFunc0(78.84, 75, 32);
            return result;
        }
        
        public static long SwiftType279()
        {
            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.swiftFunc0(13);
            return result;
        }
        
        public static long SwiftType280()
        {
            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(12, 46);
            return result;
        }
        
        public static long SwiftType281()
        {
            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(93.44, 2, 73, 58, 20, 45, 25);
            return result;
        }
        
        public static long SwiftType282()
        {
            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(89, 58);
            return result;
        }
        
        public static long SwiftType283()
        {
            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(60, 56, 27, 84, 83, 23);
            return result;
        }
        
        public static long SwiftType284()
        {
            long result = Type61.swiftFunc0(71, 17, 1, 97, 76, 4.01);
            return result;
        }
        
        public static long SwiftType285()
        {
            long result = Type61.Type61Sub2.swiftFunc0(32, 38, 37, 31, 48);
            return result;
        }
        
        public static long SwiftType286()
        {
            long result = Type62.swiftFunc0(84, 26, 86, 32, 87);
            return result;
        }
        
        public static long SwiftType287()
        {
            long result = Type62.Type62Sub2.swiftFunc0(81.64, 92);
            return result;
        }
        
        public static long SwiftType288()
        {
            long result = Type62.Type62Sub2.Type62Sub2Sub3.swiftFunc0(39, 46, 50, 7);
            return result;
        }
        
        public static long SwiftType289()
        {
            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.swiftFunc0(50, 36, 63, 41, 49, 82, 71, 40.84, 59);
            return result;
        }
        
        public static long SwiftType290()
        {
            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.swiftFunc0(58, 30, 99, 57, 31, 20, 18, 27.06);
            return result;
        }
        
        public static long SwiftType291()
        {
            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.swiftFunc0(64, 5, 70);
            return result;
        }
        
        public static long SwiftType292()
        {
            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.Type62Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(85, 62, 99, 62, 40, 98, 73);
            return result;
        }
        
        public static long SwiftType293()
        {
            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.Type62Sub2Sub3Sub4Sub5Sub6Sub7.Type62Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(35, 53, 16, 98.23);
            return result;
        }
        
        public static long SwiftType294()
        {
            long result = Type63.swiftFunc0(63, 55, 51, 93, 13, 0, 85, 15, 47);
            return result;
        }
        
        public static long SwiftType295()
        {
            long result = Type63.Type63Sub2.swiftFunc0(10, 5, 93, 25);
            return result;
        }
        
        public static long SwiftType296()
        {
            long result = Type63.Type63Sub2.Type63Sub2Sub3.swiftFunc0(9, 47, 9, 64, 31, 32, 2, 46);
            return result;
        }
        
        public static long SwiftType297()
        {
            long result = Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.swiftFunc0(26, 53, 69, 66, 62);
            return result;
        }
        
        public static long SwiftType298()
        {
            long result = Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.Type63Sub2Sub3Sub4Sub5.swiftFunc0(19, 3.92, 20, 10, 95, 6);
            return result;
        }
        
        public static long SwiftType299()
        {
            long result = Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.Type63Sub2Sub3Sub4Sub5.Type63Sub2Sub3Sub4Sub5Sub6.swiftFunc0(38, 10);
            return result;
        }
        
        public static long SwiftType300()
        {
            long result = Type64.swiftFunc0(99, 17, 74);
            return result;
        }
        
        public static long SwiftType301()
        {
            long result = Type64.Type64Sub2.swiftFunc0(94, 85.81, 54.20, 79, 53, 63.54);
            return result;
        }
        
        public static long SwiftType302()
        {
            long result = Type64.Type64Sub2.Type64Sub2Sub3.swiftFunc0(8);
            return result;
        }
        
        public static long SwiftType303()
        {
            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.swiftFunc0(8, 39, 42, 48, 62);
            return result;
        }
        
        public static long SwiftType304()
        {
            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.swiftFunc0(37.04, 93, 19, 25, 0, 81);
            return result;
        }
        
        public static long SwiftType305()
        {
            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.swiftFunc0(89, 50.15, 58);
            return result;
        }
        
        public static long SwiftType306()
        {
            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(46, 18, 49, 46);
            return result;
        }
        
        public static long SwiftType307()
        {
            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(97);
            return result;
        }
        
        public static long SwiftType308()
        {
            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(63, 67, 67, 41, 22, 82, 73, 3, 22);
            return result;
        }
        
        public static long SwiftType309()
        {
            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(26, 67, 98.92, 29, 9, 40);
            return result;
        }
        
        public static long SwiftType310()
        {
            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0(85, 22, 50.88, 61, 79.13, 51);
            return result;
        }
        
        public static long SwiftType311()
        {
            long result = Type65.swiftFunc0(14.48, 16);
            return result;
        }
        
        public static long SwiftType312()
        {
            long result = Type65.Type65Sub2.swiftFunc0(62, 60, 70, 32);
            return result;
        }
        
        public static long SwiftType313()
        {
            long result = Type65.Type65Sub2.Type65Sub2Sub3.swiftFunc0(32, 9, 83, 38.46, 94, 60, 24);
            return result;
        }
        
        public static long SwiftType314()
        {
            long result = Type65.Type65Sub2.Type65Sub2Sub3.Type65Sub2Sub3Sub4.swiftFunc0(69, 26, 96, 50.91, 51, 52, 77, 46, 84, 54);
            return result;
        }
        
        public static long SwiftType315()
        {
            long result = Type65.Type65Sub2.Type65Sub2Sub3.Type65Sub2Sub3Sub4.Type65Sub2Sub3Sub4Sub5.swiftFunc0(41.85, 13);
            return result;
        }
        
        public static long SwiftType316()
        {
            long result = Type66.swiftFunc0(35.67);
            return result;
        }
        
        public static long SwiftType317()
        {
            long result = Type66.Type66Sub2.swiftFunc0(84);
            return result;
        }
        
        public static long SwiftType318()
        {
            long result = Type67.swiftFunc0(17, 37, 94, 30, 56);
            return result;
        }
        
        public static long SwiftType319()
        {
            long result = Type67.Type67Sub2.swiftFunc0(47);
            return result;
        }
        
        public static long SwiftType320()
        {
            long result = Type67.Type67Sub2.Type67Sub2Sub3.swiftFunc0(82, 60, 21, 72, 43, 58, 65, 5, 31.82);
            return result;
        }
        
        public static long SwiftType321()
        {
            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.swiftFunc0(54, 84, 27, 33.07, 33, 81, 37);
            return result;
        }
        
        public static long SwiftType322()
        {
            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.swiftFunc0(16, 98, 6, 82.49, 91);
            return result;
        }
        
        public static long SwiftType323()
        {
            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.swiftFunc0(82, 29, 23, 37, 7.37, 95.44, 54, 63.89, 75);
            return result;
        }
        
        public static long SwiftType324()
        {
            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.Type67Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(92.17, 5, 55.78, 62, 35, 22, 59);
            return result;
        }
        
        public static long SwiftType325()
        {
            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.Type67Sub2Sub3Sub4Sub5Sub6Sub7.Type67Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(70);
            return result;
        }
        
        public static long SwiftType326()
        {
            long result = Type68.swiftFunc0(83, 26, 27, 31, 45);
            return result;
        }
        
        public static long SwiftType327()
        {
            long result = Type68.Type68Sub2.swiftFunc0(35, 27, 16, 9, 81, 84);
            return result;
        }
        
        public static long SwiftType328()
        {
            long result = Type68.Type68Sub2.Type68Sub2Sub3.swiftFunc0(15, 47, 77, 51, 98.93);
            return result;
        }
        
        public static long SwiftType329()
        {
            long result = Type68.Type68Sub2.Type68Sub2Sub3.Type68Sub2Sub3Sub4.swiftFunc0(15, 97, 28, 78, 22, 74, 52, 72, 27.99, 96);
            return result;
        }
        
        public static long SwiftType330()
        {
            long result = Type69.swiftFunc0(99, 79);
            return result;
        }
        
        public static long SwiftType331()
        {
            long result = Type70.swiftFunc0(43.27, 51, 14, 56, 37);
            return result;
        }
        
        public static long SwiftType332()
        {
            long result = Type70.Type70Sub2.swiftFunc0(30, 7, 32.71, 10, 61, 47, 3, 58);
            return result;
        }
        
        public static long SwiftType333()
        {
            long result = Type70.Type70Sub2.Type70Sub2Sub3.swiftFunc0(9, 59.29, 2, 26, 13, 61);
            return result;
        }
        
        public static long SwiftType334()
        {
            long result = Type70.Type70Sub2.Type70Sub2Sub3.Type70Sub2Sub3Sub4.swiftFunc0(59, 36.38, 22, 78);
            return result;
        }
        
        public static long SwiftType335()
        {
            long result = Type71.swiftFunc0(60, 11);
            return result;
        }
        
        public static long SwiftType336()
        {
            long result = Type71.Type71Sub2.swiftFunc0(13, 72.40, 62, 50, 19, 38, 91, 68, 71);
            return result;
        }
        
        public static long SwiftType337()
        {
            long result = Type71.Type71Sub2.Type71Sub2Sub3.swiftFunc0(89, 47.78);
            return result;
        }
        
        public static long SwiftType338()
        {
            long result = Type71.Type71Sub2.Type71Sub2Sub3.Type71Sub2Sub3Sub4.swiftFunc0(14, 83);
            return result;
        }
        
        public static long SwiftType339()
        {
            long result = Type71.Type71Sub2.Type71Sub2Sub3.Type71Sub2Sub3Sub4.Type71Sub2Sub3Sub4Sub5.swiftFunc0(58, 39.99, 42, 66.21, 72, 57);
            return result;
        }
        
        public static long SwiftType340()
        {
            long result = Type72.swiftFunc0(38, 86, 82, 95);
            return result;
        }
        
        public static long SwiftType341()
        {
            long result = Type72.Type72Sub2.swiftFunc0(82, 18, 7);
            return result;
        }
        
        public static long SwiftType342()
        {
            long result = Type72.Type72Sub2.Type72Sub2Sub3.swiftFunc0(64, 87, 19, 8, 93, 53, 60.71);
            return result;
        }
        
        public static long SwiftType343()
        {
            long result = Type72.Type72Sub2.Type72Sub2Sub3.Type72Sub2Sub3Sub4.swiftFunc0(66, 1, 76);
            return result;
        }
        
        public static long SwiftType344()
        {
            long result = Type72.Type72Sub2.Type72Sub2Sub3.Type72Sub2Sub3Sub4.Type72Sub2Sub3Sub4Sub5.swiftFunc0(27, 96, 81, 21, 1, 49, 91, 16, 91, 76);
            return result;
        }
        
        public static long SwiftType345()
        {
            long result = Type73.swiftFunc0(37, 10, 80, 28, 24, 57.14, 77, 60, 36.12);
            return result;
        }
        
        public static long SwiftType346()
        {
            long result = Type73.Type73Sub2.swiftFunc0(51.21, 42, 96);
            return result;
        }
        
        public static long SwiftType347()
        {
            long result = Type73.Type73Sub2.Type73Sub2Sub3.swiftFunc0(14, 75.28, 66);
            return result;
        }
        
        public static long SwiftType348()
        {
            long result = Type74.swiftFunc0(14, 67, 44, 14, 79, 21, 4.74, 64);
            return result;
        }
        
        public static long SwiftType349()
        {
            long result = Type74.Type74Sub2.swiftFunc0(87, 9, 60, 49.43, 9);
            return result;
        }
        
        public static long SwiftType350()
        {
            long result = Type74.Type74Sub2.Type74Sub2Sub3.swiftFunc0(20, 58, 54, 57.40, 37, 80, 47, 17.36);
            return result;
        }
        
        public static long SwiftType351()
        {
            long result = Type74.Type74Sub2.Type74Sub2Sub3.Type74Sub2Sub3Sub4.swiftFunc0(51);
            return result;
        }
        
        public static long SwiftType352()
        {
            long result = Type75.swiftFunc0(78.00);
            return result;
        }
        
        public static long SwiftType353()
        {
            long result = Type75.Type75Sub2.swiftFunc0(42, 84, 39, 9, 80, 81);
            return result;
        }
        
        public static long SwiftType354()
        {
            long result = Type75.Type75Sub2.Type75Sub2Sub3.swiftFunc0(95, 12, 12, 49, 1, 52.30);
            return result;
        }
        
        public static long SwiftType355()
        {
            long result = Type75.Type75Sub2.Type75Sub2Sub3.Type75Sub2Sub3Sub4.swiftFunc0(5, 31, 10);
            return result;
        }
        
        public static long SwiftType356()
        {
            long result = Type75.Type75Sub2.Type75Sub2Sub3.Type75Sub2Sub3Sub4.Type75Sub2Sub3Sub4Sub5.swiftFunc0(39.66, 1, 11.14, 3, 75, 77, 21, 27, 89.65);
            return result;
        }
        
        public static long SwiftType357()
        {
            long result = Type76.swiftFunc0(38, 40, 64, 35, 62, 90.14, 3.08, 67);
            return result;
        }
        
        public static long SwiftType358()
        {
            long result = Type76.Type76Sub2.swiftFunc0(15, 19);
            return result;
        }
        
        public static long SwiftType359()
        {
            long result = Type76.Type76Sub2.Type76Sub2Sub3.swiftFunc0(0.62, 51);
            return result;
        }
        
        public static long SwiftType360()
        {
            long result = Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.swiftFunc0(36.07, 86, 60, 77, 1.01, 21, 59);
            return result;
        }
        
        public static long SwiftType361()
        {
            long result = Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.Type76Sub2Sub3Sub4Sub5.swiftFunc0(77, 56, 87, 81, 65.87, 28, 43.97, 42, 27, 99);
            return result;
        }
        
        public static long SwiftType362()
        {
            long result = Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.Type76Sub2Sub3Sub4Sub5.Type76Sub2Sub3Sub4Sub5Sub6.swiftFunc0(81, 53);
            return result;
        }
        
        public static long SwiftType363()
        {
            long result = Type77.swiftFunc0(77, 68, 64, 18.20, 55, 73);
            return result;
        }
        
        public static long SwiftType364()
        {
            long result = Type78.swiftFunc0(68, 10, 39, 67, 23, 45);
            return result;
        }
        
        public static long SwiftType365()
        {
            long result = Type78.Type78Sub2.swiftFunc0(88.78, 66, 63, 68, 49);
            return result;
        }
        
        public static long SwiftType366()
        {
            long result = Type79.swiftFunc0(76, 34.43, 40.60, 37, 100, 3);
            return result;
        }
        
        public static long SwiftType367()
        {
            long result = Type79.Type79Sub2.swiftFunc0(8, 29, 79, 11, 20.79, 55, 21.74, 32);
            return result;
        }
        
        public static long SwiftType368()
        {
            long result = Type79.Type79Sub2.Type79Sub2Sub3.swiftFunc0(2.21, 29, 28, 99.63, 82, 76, 27);
            return result;
        }
        
        public static long SwiftType369()
        {
            long result = Type79.Type79Sub2.Type79Sub2Sub3.Type79Sub2Sub3Sub4.swiftFunc0(34, 51);
            return result;
        }
        
        public static long SwiftType370()
        {
            long result = Type80.swiftFunc0(23, 79.84, 60, 12, 82, 42, 76);
            return result;
        }
        
        public static long SwiftType371()
        {
            long result = Type80.Type80Sub2.swiftFunc0(98, 19.17, 91, 61, 53, 2, 16.52, 34, 63, 48);
            return result;
        }
        
        public static long SwiftType372()
        {
            long result = Type80.Type80Sub2.Type80Sub2Sub3.swiftFunc0(90, 46, 20, 27, 58);
            return result;
        }
        
        public static long SwiftType373()
        {
            long result = Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.swiftFunc0(42, 33, 21, 13, 74, 4.58, 22, 16);
            return result;
        }
        
        public static long SwiftType374()
        {
            long result = Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.Type80Sub2Sub3Sub4Sub5.swiftFunc0(60.26, 87, 4, 33, 60.41, 73, 79, 66, 31, 18);
            return result;
        }
        
        public static long SwiftType375()
        {
            long result = Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.Type80Sub2Sub3Sub4Sub5.Type80Sub2Sub3Sub4Sub5Sub6.swiftFunc0(26.41, 57.40, 34.41, 11, 15, 86, 5);
            return result;
        }
        
        public static long SwiftType376()
        {
            long result = Type81.swiftFunc0(85, 4, 21, 23, 9, 13);
            return result;
        }
        
        public static long SwiftType377()
        {
            long result = Type81.Type81Sub2.swiftFunc0(76.53, 33, 71.34);
            return result;
        }
        
        public static long SwiftType378()
        {
            long result = Type81.Type81Sub2.Type81Sub2Sub3.swiftFunc0(78, 51, 8);
            return result;
        }
        
        public static long SwiftType379()
        {
            long result = Type81.Type81Sub2.Type81Sub2Sub3.Type81Sub2Sub3Sub4.swiftFunc0(23.61, 87, 59, 74);
            return result;
        }
        
        public static long SwiftType380()
        {
            long result = Type82.swiftFunc0(88, 47, 95, 72, 72, 54, 23);
            return result;
        }
        
        public static long SwiftType381()
        {
            long result = Type83.swiftFunc0(56, 7, 24, 29, 14.14, 38.88, 98.96, 17, 23);
            return result;
        }
        
        public static long SwiftType382()
        {
            long result = Type83.Type83Sub2.swiftFunc0(19.30, 85);
            return result;
        }
        
        public static long SwiftType383()
        {
            long result = Type83.Type83Sub2.Type83Sub2Sub3.swiftFunc0(40.67, 68.63, 46, 70, 45, 22.40, 41, 93);
            return result;
        }
        
        public static long SwiftType384()
        {
            long result = Type83.Type83Sub2.Type83Sub2Sub3.Type83Sub2Sub3Sub4.swiftFunc0(38, 79.27, 10, 2, 16);
            return result;
        }
        
        public static long SwiftType385()
        {
            long result = Type84.swiftFunc0(73);
            return result;
        }
        
        public static long SwiftType386()
        {
            long result = Type85.swiftFunc0(25, 36, 87, 25, 5, 41.42, 64, 19, 6);
            return result;
        }
        
        public static long SwiftType387()
        {
            long result = Type85.Type85Sub2.swiftFunc0(97, 22, 89);
            return result;
        }
        
        public static long SwiftType388()
        {
            long result = Type85.Type85Sub2.Type85Sub2Sub3.swiftFunc0(17);
            return result;
        }
        
        public static long SwiftType389()
        {
            long result = Type85.Type85Sub2.Type85Sub2Sub3.Type85Sub2Sub3Sub4.swiftFunc0(83, 44, 40.86, 79, 60, 20, 40, 30, 59.43, 70);
            return result;
        }
        
        public static long SwiftType390()
        {
            long result = Type86.swiftFunc0(75, 71, 97, 8, 11);
            return result;
        }
        
        public static long SwiftType391()
        {
            long result = Type87.swiftFunc0(75, 10.29, 53.93, 54);
            return result;
        }
        
        public static long SwiftType392()
        {
            long result = Type87.Type87Sub2.swiftFunc0(68, 54.92, 66.88, 69, 100, 45, 44, 46, 61, 71.47);
            return result;
        }
        
        public static long SwiftType393()
        {
            long result = Type87.Type87Sub2.Type87Sub2Sub3.swiftFunc0(22, 10, 31, 68, 73, 0, 91, 100, 9.40, 20);
            return result;
        }
        
        public static long SwiftType394()
        {
            long result = Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.swiftFunc0(40, 25);
            return result;
        }
        
        public static long SwiftType395()
        {
            long result = Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.swiftFunc0(75, 64, 32.55, 7, 62, 93.04);
            return result;
        }
        
        public static long SwiftType396()
        {
            long result = Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.Type87Sub2Sub3Sub4Sub5Sub6.swiftFunc0(3, 94, 77, 95, 69, 83, 87);
            return result;
        }
        
        public static long SwiftType397()
        {
            long result = Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.Type87Sub2Sub3Sub4Sub5Sub6.Type87Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(92, 46, 85, 75, 4, 69.45, 11, 91, 13);
            return result;
        }
        
        public static long SwiftType398()
        {
            long result = Type88.swiftFunc0(8, 45, 8, 13, 6);
            return result;
        }
        
        public static long SwiftType399()
        {
            long result = Type88.Type88Sub2.swiftFunc0(99, 51, 0.85, 97, 67, 49, 50);
            return result;
        }
        
        public static long SwiftType400()
        {
            long result = Type88.Type88Sub2.Type88Sub2Sub3.swiftFunc0(0, 46.66, 68, 45, 21, 22);
            return result;
        }
        
        public static long SwiftType401()
        {
            long result = Type88.Type88Sub2.Type88Sub2Sub3.Type88Sub2Sub3Sub4.swiftFunc0(46, 65, 26, 79, 70, 35, 100, 15.84, 85);
            return result;
        }
        
        public static long SwiftType402()
        {
            long result = Type88.Type88Sub2.Type88Sub2Sub3.Type88Sub2Sub3Sub4.Type88Sub2Sub3Sub4Sub5.swiftFunc0(47, 23.27, 24, 96, 50.05, 51);
            return result;
        }
        
        public static long SwiftType403()
        {
            long result = Type89.swiftFunc0(61.93, 81);
            return result;
        }
        
        public static long SwiftType404()
        {
            long result = Type89.Type89Sub2.swiftFunc0(10, 24, 82, 38, 18.04, 14);
            return result;
        }
        
        public static long SwiftType405()
        {
            long result = Type89.Type89Sub2.Type89Sub2Sub3.swiftFunc0(89);
            return result;
        }
        
        public static long SwiftType406()
        {
            long result = Type90.swiftFunc0(84, 6, 87, 84, 32, 9, 67);
            return result;
        }
        
        public static long SwiftType407()
        {
            long result = Type90.Type90Sub2.swiftFunc0(60, 49, 59, 33, 30, 5.32, 47, 77, 73);
            return result;
        }
        
        public static long SwiftType408()
        {
            long result = Type90.Type90Sub2.Type90Sub2Sub3.swiftFunc0(42, 16, 34.91, 78, 38, 44, 36.79, 9.77, 56);
            return result;
        }
        
        public static long SwiftType409()
        {
            long result = Type90.Type90Sub2.Type90Sub2Sub3.Type90Sub2Sub3Sub4.swiftFunc0(4.83, 8, 96);
            return result;
        }
        
        public static long SwiftType410()
        {
            long result = Type90.Type90Sub2.Type90Sub2Sub3.Type90Sub2Sub3Sub4.Type90Sub2Sub3Sub4Sub5.swiftFunc0(100, 11, 55, 24.50, 58);
            return result;
        }
        
        public static long SwiftType411()
        {
            long result = Type91.swiftFunc0(84, 27, 72);
            return result;
        }
        
        public static long SwiftType412()
        {
            long result = Type91.Type91Sub2.swiftFunc0(63, 76.01, 62, 36);
            return result;
        }
        
        public static long SwiftType413()
        {
            long result = Type91.Type91Sub2.Type91Sub2Sub3.swiftFunc0(85, 74, 57.61, 8, 4, 100, 42);
            return result;
        }
        
        public static long SwiftType414()
        {
            long result = Type91.Type91Sub2.Type91Sub2Sub3.Type91Sub2Sub3Sub4.swiftFunc0(74, 28, 76);
            return result;
        }
        
        public static long SwiftType415()
        {
            long result = Type91.Type91Sub2.Type91Sub2Sub3.Type91Sub2Sub3Sub4.Type91Sub2Sub3Sub4Sub5.swiftFunc0(60);
            return result;
        }
        
        public static long SwiftType416()
        {
            long result = Type92.swiftFunc0(4.85, 26, 47, 80, 41, 33, 58, 91);
            return result;
        }
        
        public static long SwiftType417()
        {
            long result = Type92.Type92Sub2.swiftFunc0(28, 52, 26, 67, 1.38, 49, 85.60, 26);
            return result;
        }
        
        public static long SwiftType418()
        {
            long result = Type92.Type92Sub2.Type92Sub2Sub3.swiftFunc0(14, 77);
            return result;
        }
        
        public static long SwiftType419()
        {
            long result = Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.swiftFunc0(68, 70, 7, 3, 31, 60);
            return result;
        }
        
        public static long SwiftType420()
        {
            long result = Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.Type92Sub2Sub3Sub4Sub5.swiftFunc0(34, 46, 33, 55, 47, 80, 12, 18, 51.72);
            return result;
        }
        
        public static long SwiftType421()
        {
            long result = Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.Type92Sub2Sub3Sub4Sub5.Type92Sub2Sub3Sub4Sub5Sub6.swiftFunc0(98, 99, 56, 40, 62, 23.66, 57);
            return result;
        }
        
        public static long SwiftType422()
        {
            long result = Type93.swiftFunc0(11, 9, 66, 12.84, 44, 100, 76, 45, 96);
            return result;
        }
        
        public static long SwiftType423()
        {
            long result = Type93.Type93Sub2.swiftFunc0(94.75, 21, 37, 91, 56, 34, 55.01, 65, 8);
            return result;
        }
        
        public static long SwiftType424()
        {
            long result = Type93.Type93Sub2.Type93Sub2Sub3.swiftFunc0(94, 27, 0, 52);
            return result;
        }
        
        public static long SwiftType425()
        {
            long result = Type94.swiftFunc0(87.31);
            return result;
        }
        
        public static long SwiftType426()
        {
            long result = Type94.Type94Sub2.swiftFunc0(35);
            return result;
        }
        
        public static long SwiftType427()
        {
            long result = Type94.Type94Sub2.Type94Sub2Sub3.swiftFunc0(79.14, 23, 68.74, 40.88, 31.98, 47);
            return result;
        }
        
        public static long SwiftType428()
        {
            long result = Type94.Type94Sub2.Type94Sub2Sub3.Type94Sub2Sub3Sub4.swiftFunc0(21, 67, 38, 78, 10, 59, 41.43, 86.39, 30, 54);
            return result;
        }
        
        public static long SwiftType429()
        {
            long result = Type94.Type94Sub2.Type94Sub2Sub3.Type94Sub2Sub3Sub4.Type94Sub2Sub3Sub4Sub5.swiftFunc0(94);
            return result;
        }
        
        public static long SwiftType430()
        {
            long result = Type95.swiftFunc0(39, 30, 14.68, 76, 57, 45, 68);
            return result;
        }
        
        public static long SwiftType431()
        {
            long result = Type95.Type95Sub2.swiftFunc0(6, 87, 17, 64);
            return result;
        }
        
        public static long SwiftType432()
        {
            long result = Type95.Type95Sub2.Type95Sub2Sub3.swiftFunc0(60, 46);
            return result;
        }
        
        public static long SwiftType433()
        {
            long result = Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.swiftFunc0(25, 38, 29, 5);
            return result;
        }
        
        public static long SwiftType434()
        {
            long result = Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.Type95Sub2Sub3Sub4Sub5.swiftFunc0(98, 23, 59, 31);
            return result;
        }
        
        public static long SwiftType435()
        {
            long result = Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.Type95Sub2Sub3Sub4Sub5.Type95Sub2Sub3Sub4Sub5Sub6.swiftFunc0(31, 84, 49, 85, 84, 19, 60, 89, 3);
            return result;
        }
        
        public static long SwiftType436()
        {
            long result = Type96.swiftFunc0(98, 7, 11, 85.08, 20);
            return result;
        }
        
        public static long SwiftType437()
        {
            long result = Type96.Type96Sub2.swiftFunc0(17, 52, 9, 32, 4, 87, 74);
            return result;
        }
        
        public static long SwiftType438()
        {
            long result = Type96.Type96Sub2.Type96Sub2Sub3.swiftFunc0(68, 62, 4, 33.70, 30, 97, 50, 34, 39, 60);
            return result;
        }
        
        public static long SwiftType439()
        {
            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.swiftFunc0(52, 71.85, 96, 82, 61, 41, 20, 33);
            return result;
        }
        
        public static long SwiftType440()
        {
            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.swiftFunc0(39, 37, 42, 0, 88, 28, 67, 82.35, 78);
            return result;
        }
        
        public static long SwiftType441()
        {
            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.swiftFunc0(4.44);
            return result;
        }
        
        public static long SwiftType442()
        {
            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(9, 82, 61, 86.59, 38, 20, 62, 34, 41, 86);
            return result;
        }
        
        public static long SwiftType443()
        {
            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(72, 2, 18, 70);
            return result;
        }
        
        public static long SwiftType444()
        {
            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(76, 8);
            return result;
        }
        
        public static long SwiftType445()
        {
            long result = Type97.swiftFunc0(23, 23.77, 13.26);
            return result;
        }
        
        public static long SwiftType446()
        {
            long result = Type98.swiftFunc0(94, 14, 94, 24, 46.36, 83, 40, 63, 38);
            return result;
        }
        
        public static long SwiftType447()
        {
            long result = Type98.Type98Sub2.swiftFunc0(70, 83, 12, 72.32, 51, 40, 56);
            return result;
        }
        
        public static long SwiftType448()
        {
            long result = Type99.swiftFunc0(73);
            return result;
        }
        
        public static long SwiftType449()
        {
            long result = Type99.Type99Sub2.swiftFunc0(33, 83);
            return result;
        }
        
        public static long SwiftType450()
        {
            long result = Type99.Type99Sub2.Type99Sub2Sub3.swiftFunc0(29, 54, 73, 22, 39, 36.33, 83);
            return result;
        }
        
        public static long SwiftType451()
        {
            long result = Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.swiftFunc0(44, 70, 56, 62, 0, 86.28, 8.53, 10, 52.86);
            return result;
        }
        
        public static long SwiftType452()
        {
            long result = Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.Type99Sub2Sub3Sub4Sub5.swiftFunc0(94, 23, 73.47, 60.11, 46, 55);
            return result;
        }
        
        public static long SwiftType453()
        {
            long result = Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.Type99Sub2Sub3Sub4Sub5.Type99Sub2Sub3Sub4Sub5Sub6.swiftFunc0(89, 12, 37, 7, 8, 54, 62);
            return result;
        }
        
        public static long SwiftType454()
        {
            long result = Type100.swiftFunc0(0);
            return result;
        }
        
        public static long SwiftType455()
        {
            long result = Type100.Type100Sub2.swiftFunc0(71, 64, 55, 41, 54, 49, 67, 26.89, 59, 35);
            return result;
        }
        
        public static long SwiftType456()
        {
            long result = Type100.Type100Sub2.Type100Sub2Sub3.swiftFunc0(23.65, 28, 40, 48, 61.95, 70, 64, 61, 68.06);
            return result;
        }
        
        public static long SwiftType457()
        {
            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.swiftFunc0(85.23, 98, 77);
            return result;
        }
        
        public static long SwiftType458()
        {
            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.swiftFunc0(48, 79, 19, 59.51, 24, 15, 49, 32);
            return result;
        }
        
        public static long SwiftType459()
        {
            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.swiftFunc0(32, 38);
            return result;
        }
        
        public static long SwiftType460()
        {
            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(4, 51, 18, 26.07);
            return result;
        }
        
        public static long SwiftType461()
        {
            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(65, 0, 36, 13, 100, 4, 27, 13);
            return result;
        }
        
        public static long SwiftType462()
        {
            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(42.21, 52, 67, 100);
            return result;
        }
        
        public static long SwiftType463()
        {
            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(6, 1.90, 24.81, 82, 36.35, 64, 63, 69, 87, 23);
            return result;
        }
    }
}