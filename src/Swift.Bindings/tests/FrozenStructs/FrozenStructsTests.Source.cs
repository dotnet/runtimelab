// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Swift.FrozenStructsTests;

namespace Test {
    public class MainClass {
        public static int Main(string[] args)
        {
            return 0;
        }
        public static long[] SwiftFrozenStruct0()
        {
                Console.Write("Running SwiftStruct0: ");
                F0_S0 f0_s0 = new F0_S0(1.0, 2, 3);
                Console.WriteLine($"F0_S0: {f0_s0.hashValue()}");
                F0_S1 f0_s1 = new F0_S1(4);
                Console.WriteLine($"F0_S1: {f0_s1.hashValue()}");
                F0_S2 f0_s2 = new F0_S2(5.0f);
                Console.WriteLine($"F0_S2: {f0_s2.hashValue()}");
                long result = FrozenStructsTests.swiftFunc0(-23758, 148652722, 3833542748216839160, 21987, new F0_S0(3425626963407448, 989224444, 55562), new F0_S1(1751696348434043356), 14, new F0_S2(1047842));
                return new long[] { f0_s0.hashValue(), f0_s1.hashValue(), f0_s2.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct1()
        {
            Console.Write("Running SwiftStruct1: ");
            F1_S0 f1_s0 = new F1_S0(6, 7.0, 8, 9, 10);
            F1_S1 f1_s1 = new F1_S1(101);
            F1_S2 f1_s2 = new F1_S2(102);
            long result = FrozenStructsTests.swiftFunc1(new F1_S0(6106136698885217102, 6195715435808, 121, 676336729, 51621), 121, new F1_S1(101), new F1_S2(-11974));
            return new long[] { f1_s0.hashValue(), f1_s1.hashValue(), f1_s2.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct2()
        {
            Console.Write("Running SwiftStruct2: ");
            F2_S0 f2_s0 = new F2_S0(11, 12);
            F2_S1 f2_s1 = new F2_S1(13, 14, 15, 16, 17);
            F2_S2_S0_S0 f2_s2_s0_s0 = new F2_S2_S0_S0(18);
            F2_S3 f2_s3 = new F2_S3(103);
            F2_S4 f2_s4 = new F2_S4(16, 17);
            F2_S5 f2_s5 = new F2_S5(18.0f);
            long result = FrozenStructsTests.swiftFunc2(1467471118999515177, -1109, 1443466834, new F2_S0(unchecked((nint)8641951469425609828), unchecked((nuint)3263825339460718643)), 6, 42857709, new F2_S1(6855376760105631967, 2087467091, 25810, 2495195821026007124, 62146), new F2_S2(new F2_S2_S0(new F2_S2_S0_S0(unchecked((nint)561009218247569242)))), 46110, 7547287, new F2_S3(34), new F2_S4(203178131, unchecked((nuint)8676866947888134131)), new F2_S5(7890213), 5623254678629817168);
            return new long[] { f2_s0.hashValue(), f2_s1.hashValue(), f2_s2_s0_s0.hashValue(), f2_s3.hashValue(), f2_s4.hashValue(), f2_s5.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct3()
        {
            Console.Write("Running SwiftStruct3: ");
            F3_S0_S0 f3_s0_s0 = new F3_S0_S0(19, 20);
            F3_S0 f3_s0 = new F3_S0(21, f3_s0_s0, 22);
            F3_S1 f3_s1 = new F3_S1(23, 24.0f);
            F3_S2 f3_s2 = new F3_S2(25.0f);
            F3_S3 f3_s3 = new F3_S3(104, 26);
            F3_S4 f3_s4 = new F3_S4(27, 28.0f, 29);
            F3_S5 f3_s5 = new F3_S5(30, 31);
            F3_S6_S0 f3_s6_s0 = new F3_S6_S0(32, 105);
            F3_S6 f3_s6 = new F3_S6(f3_s6_s0, 33, 34);
            F3_S7 f3_s7 = new F3_S7(35);
            long result = FrozenStructsTests.swiftFunc3(unchecked((nint)3764414362291906102), new F3_S0(23, new F3_S0_S0(unchecked((nint)3007367655161186204), 549733154), 38928730), new F3_S1(338326426991485790, 7517271), 4025506815523052, unchecked((nint)431338169919855088), new F3_S2(7888763), new F3_S3(57, unchecked((nint)8933588466514096604)), new F3_S4(unchecked((nuint)7769316271655125502), 1663231, 27333), new F3_S5(887161443, 4368322322535461551), 32477, 948591564, new F3_S6(new F3_S6_S0(7033, 124), 67, 221), unchecked((nint)6195032215974632640), new F3_S7(4076570630190469380));
            return new long[] { f3_s0_s0.hashValue(), f3_s0.hashValue(), f3_s1.hashValue(), f3_s2.hashValue(), f3_s3.hashValue(), f3_s4.hashValue(), f3_s5.hashValue(), f3_s6_s0.hashValue(), f3_s6.hashValue(), f3_s7.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct4()
        {
            Console.Write("Running SwiftStruct4: ");
            F4_S0 f4_s0 = new F4_S0(36, 37, 38);
            F4_S1_S0 f4_s1_s0 = new F4_S1_S0(39);
            F4_S1 f4_s1 = new F4_S1(f4_s1_s0, 40.0f);
            F4_S2_S0 f4_s2_s0 = new F4_S2_S0(41);
            F4_S2 f4_s2 = new F4_S2(f4_s2_s0, 42);
            F4_S3 f4_s3 = new F4_S3(43, 44, 45);
            long result = FrozenStructsTests.swiftFunc4(unchecked((nint)7962207922494873063), new F4_S0(16887, 11193, 20997), unchecked((nuint)938043702598629976), 8692646626431098135, -16, 1244033228990732, new F4_S1(new F4_S1_S0(274421021), 7037264), 154, 1187166500, 1096514224, 7283010216047805604, new F4_S2(new F4_S2_S0(unchecked((nint)3285810526807361976)), unchecked((nint)2934841899954168407)), 3384, unchecked((nint)4857017836321530071), new F4_S3(9030480386017125399, 5466901523025762626, 3430278619936831574), 234522698);
            return new long[] { f4_s0.hashValue(), f4_s1_s0.hashValue(), f4_s1.hashValue(), f4_s2_s0.hashValue(), f4_s2.hashValue(), f4_s3.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct5()
        {
            Console.Write("Running SwiftStruct5: ");
            F5_S0 f5_s0 = new F5_S0(46);
            long result = FrozenStructsTests.swiftFunc5(unchecked((nuint)425569624776371773), 8077063517132296390, 126, new F5_S0(unchecked((nuint)8032431538406335990)));
            return new long[] { f5_s0.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct6()
        {
            Console.Write("Running SwiftStruct6: ");
            F6_S0 f6_s0 = new F6_S0(47, 48, 49);
            F6_S1 f6_s1 = new F6_S1(50, 51.0f);
            F6_S2_S0 f6_s2_s0 = new F6_S2_S0(52.0);
            F6_S2 f6_s2 = new F6_S2(f6_s2_s0, 53);
            F6_S3 f6_s3 = new F6_S3(54.0, 55.0, 56);
            F6_S4 f6_s4 = new F6_S4(57);
            F6_S5 f6_s5 = new F6_S5(58);
            long result = FrozenStructsTests.swiftFunc6(7742402881449217499, new F6_S0(158138445, unchecked((nint)4280990415451108676), 220), new F6_S1(unchecked((nint)7698928046973811162), 478730), unchecked((nuint)7348396082620937303), 76, 638113630, new F6_S2(new F6_S2_S0(55341051405503), 61378), 8235930, -20241, new F6_S3(318363825012010, 3586735152618866, 6630554942616673404), 46432, 744827194985602, 1973021571, new F6_S4(103), new F6_S5(-5345));
            return new long[] { f6_s0.hashValue(), f6_s1.hashValue(), f6_s2_s0.hashValue(), f6_s2.hashValue(), f6_s3.hashValue(), f6_s4.hashValue(), f6_s5.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct7()
        {
            Console.Write("Running SwiftStruct7: ");
            F7_S0 f7_s0 = new F7_S0(59, 60);
            F7_S1 f7_s1 = new F7_S1(61);
            long result = FrozenStructsTests.swiftFunc7(6953928391541094904, unchecked((nint)2531714261502554653), 224, new F7_S0(14482, unchecked((nint)4704842847707480837)), new F7_S1(148), 659764805);
            return new long[] { f7_s0.hashValue(), f7_s1.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct8()
        {
            Console.Write("Running SwiftStruct8: ");
            F8_S0 f8_s0 = new F8_S0(62);
            long result = FrozenStructsTests.swiftFunc8(48505, unchecked((nuint)8758330817072549915), 7130, 4163773298933598697, new F8_S0(1934119180), 2843311260726166700);
            return new long[] { f8_s0.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct9()
        {
            Console.Write("Running SwiftStruct9: ");
            F9_S0 f9_s0 = new F9_S0(63.0);
            F9_S1 f9_s1 = new F9_S1(64);
            long result = FrozenStructsTests.swiftFunc9(3214937834123081267, 6846768, new F9_S0(1713527158921541), 25670, new F9_S1(1650872599), 39910);
            return new long[] { f9_s0.hashValue(), f9_s1.hashValue(), result };
        }

        public static long[] SwiftFrozenStruct10()
        {
            Console.Write("Running SwiftStruct10: ");
            F10_S0 f10_s0 = new F10_S0(65, 66);
            F10_S1 f10_s1 = new F10_S1(67.0f, 68, 69);
            F10_S2 f10_s2 = new F10_S2(70, 71);
            F10_S3 f10_s3 = new F10_S3(72.0f);
            F10_S4 f10_s4 = new F10_S4(73);
            long result = FrozenStructsTests.swiftFunc10(57914, 11968, new F10_S0(155502634291755209, 2096010440), 1373054541331378384, 2401784, -16, 9038689080810964859, 521869082023571496, 8919173990791765137, 4890513, 1113752036, 1477591037, 1463349953238439103, 7521124889381630793, new F10_S1(620783, 33, unchecked((nuint)1209731409858919135)), 1560688600815438014, new F10_S2(unchecked((nuint)2244178273746563479), 4252696983313269084), new F10_S3(6539550), new F10_S4(1264398289929487498));
            return new long[] { f10_s0.hashValue(), f10_s1.hashValue(), f10_s2.hashValue(), f10_s3.hashValue(), f10_s4.hashValue(), result };
        }
    }
}
