// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class PInvokeTests
    {
        [Fact]
        public static void TestSwiftSmoke()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static int getResult()
                        {
                            Console.Write("Running HelloWorld: ");
                            PInvokeTests.helloWorld();
                            return 42;
                        }
                    }
                }
                """;

            int result = (int)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(42, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftBool()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static bool getResult()
                        {
                            return PInvokeTests.swiftFuncBool(true);
                        }
                    }
                }
                """;

            bool result = (bool)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.True(result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc0()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                             Console.Write("Running SwiftFunc0: ");
                             long result = PInvokeTests.swiftFunc0(233837, -19649, 949339914140650, 515944430, 3611910812477598, 1366498001922882872, 253, 36322, 9433, 4255310654111403863);
                             return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7706330218351441791, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc1()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc1: ");
                            long result = PInvokeTests.swiftFunc1(86085159640584, 8266931072168309631, 1110349398, 923925690, 37808, unchecked((nint)1505729412738639024), unchecked((nint)5378706168286662479), unchecked((nuint)3649715618139795268), 7849893551470522942, 56451, 91, unchecked((nuint)8878053038747921600), 50562, 51, 1727716, -7397, 1565437348, 5586083, 51673832327441982, 1640275483742190655, 242);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3202601456867082324, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc2()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc2: ");
                            long result = PInvokeTests.swiftFunc2(21, 103, 71, 1424520637, 3182953, 59237, 5358256, 185, unchecked((nuint)5971751687364786332), 3252957756581813, 60, 156, 37, 691169209662368818, 619972957, -24591, 1750310134967048045, 3556371, 7350716211230827055, 752486, 13160, 4120692058630618283, 339525547);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(911474180935535301, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc3()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc3: ");
                            long result = PInvokeTests.swiftFunc3(11017, unchecked((nuint)3300243067329724866), unchecked((nint)2197054130026496019), 7, unchecked((nuint)4243803136497835975));
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6350065034291914241, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc4()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc4: ");
                            long result = PInvokeTests.swiftFunc4(123, 109509832281030, 771052005, unchecked((nint)949115070174941197), 409146992556708, 142, 8802835909468694407, 117, 1991550650, 355009119770696130, -1465, 77, 4978285795473493480, 29393, 35783, unchecked((nuint)465708604896456642), unchecked((nint)6140690449828087415), unchecked((nuint)54183433686440276));
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-9091922861563963282, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc5()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc5: ");
                            long result = PInvokeTests.swiftFunc5(unchecked((nint)6739098280890594874), unchecked((nuint)3195600522343554291), 6597877, unchecked((nint)4743060745349392976), 47478, 51136, 685135916, 1498506, unchecked((nint)2513390154774344247), unchecked((nint)6264911547639833584), 97, 172, -1012, 2975748462028333845);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3357359150345247842, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc6()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc6: ");
                            long result = PInvokeTests.swiftFunc6(352858064, 39960, unchecked((nuint)8213487426461212263), unchecked((nuint)8748652475782254131), 4373843, -89, 7725301, 36, 137871115990745445, 114855981505908);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-581969692498632062, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc7()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc7: ");
                            long result = PInvokeTests.swiftFunc7(unchecked((nint)2299287859300346739), 1631989942, 1588438125, 46916, -25, unchecked((nint)2434879470176610838), 1297396924186241, 1780608786176839, unchecked((nint)3111883344385004599), 10818, 1339461357, 8225377, 313504081, 795664767, 40);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4054341816496194551, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc8()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc8: ");
                            long result = PInvokeTests.swiftFunc8(1204549800782266009, -77, 7140941, 5572502, 2067454162041872, 676469398821009314, unchecked((nint)1357719795588198527), -32374, unchecked((nint)3415487707327485172), 3903084861204809, 845624, 90, 1986654521, 114, 214);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2147505143518021575, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc9()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc9: ");
                            long result = PInvokeTests.swiftFunc9(unchecked((nint)3705651731235919791), unchecked((nint)8105306104169745115), 6731, 1253260485, unchecked((nuint)8727468328369013450), 5633339905753787255, 651456, 5746290834850613062, 21104, 1670307274);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3533238385513656508, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc10()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc10: ");
                            long result = PInvokeTests.swiftFunc10(1467110824167985295, 1887615810, 2112733722, 8271177801101005406, 2568471098105086703, 17984, 230, 1774094245, 2124643938);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8515181823957334780, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc11()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc11: ");
                            long result = PInvokeTests.swiftFunc11(unchecked((nuint)4085636179246671464), 3081, -79, -13000, 6737190552584399571, 204, -103, 69, 1527509527, 174, 2113159904681342052, unchecked((nint)4901826652984790117), 22029, 7006945911615348711, 1812624423);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5125817077505710853, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc12()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc12: ");
                            long result = PInvokeTests.swiftFunc12(6965382, 8312380862312650549, unchecked((nint)9165262159588878385), 17928, 1334453663, -18258, 50575, 1175690859854043654, -1563, 1325, -13033, 109, unchecked((nuint)46750205502198598), 22094, 8206581082498717875, 414098, 43, 1413226877351846990, 3077183154579648);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4774074602111830179, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc13()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc13: ");
                            long result = PInvokeTests.swiftFunc13(435831281, unchecked((nint)9152379611666687932), 2121329568, 6151812, 272083164, -25750, 8363795, 2452785801682149491, 70, 1402553126);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8686515529117439727, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc14()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc14: ");
                            long result = PInvokeTests.swiftFunc14(2302358859056615, 1148917190, 3948163471413493, 9149629931189885207, unchecked((nuint)8741258279203724116), 1005305, unchecked((nint)4318208587188430868), -120, 3440736986307467027);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1430703777921650146, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc15()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc15: ");
                            long result = PInvokeTests.swiftFunc15(257588134824930427, unchecked((nint)8797066708053001900));
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7324810059718518437, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc16()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc16: ");
                            long result = PInvokeTests.swiftFunc16(18083, 526, unchecked((nint)9092800655449909603), unchecked((nuint)1805357597792116718), 77133128, 4273714158070562, 3356842482838205306, 222, 9005246081447186762, 18718, 4779021523298290196, 265082826573210908, 1651590145, unchecked((nuint)2901617451727386196), 31673, unchecked((nuint)4505812232449759257), 2268590068495235382, 3672721177958656411, 4626146, 206, -70);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2322427926688559587, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc17()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc17: ");
                            long result = PInvokeTests.swiftFunc17(776236297300099374, unchecked((nuint)83894503467164568), 719637193271577866, 723625097, 8592004555011333063, -3, 160, -45);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5704419938581148490, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc18()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc18: ");
                            long result = PInvokeTests.swiftFunc18(245, unchecked((nint)4458327158518645661), 6050907, 76, unchecked((nint)5568159142839627860), 52, 1144555738, unchecked((nint)8239694963746799753), 148);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7333181440701096551, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc19()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc19: ");
                            long result = PInvokeTests.swiftFunc19(1964143, unchecked((nuint)7587937485891193274), 8151333634567615027, 17, 170165831807253, 870321007, 98, 1362436055, unchecked((nint)3145994526327645870), 1332381167, 27209, 1960414431895653, 3646108149104658576, 29716, 4480774200926903, 3993989, 831696419130641, 16961, unchecked((nint)2895643089791458731), 9040165725126613424, 4805, 2986899981433626);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7514368921355633465, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc20()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc20: ");
                            long result = PInvokeTests.swiftFunc20(2304932402575035, 6462973, 1769831413330094805, 38814, 62195, 52028, 643381139480, 240813148, 917778415, -11908, 43571, 735513651, -110, 1706933298, 141);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4347999520285809529, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc21()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc21: ");
                            long result = PInvokeTests.swiftFunc21(8204, -18311, 1428916154170345344, 7071281865190759971, 7843802, unchecked((nint)5442642178249643511), 43131, 210, 0, 61711, 104, 6457, 984084882, unchecked((nint)4275598409331185780), unchecked((nuint)8085691827548356926), 387, 1692895, 61, 6740747923378741876, 1508742654);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(9056719667499044372, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc22()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc22: ");
                            long result = PInvokeTests.swiftFunc22(538154736774388864, 44188, 7266230885340174627, unchecked((nuint)2194770502849252955), 915, 50317);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2450837469650376012, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc23()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc23: ");
                            long result = PInvokeTests.swiftFunc23(3154474308016267173, -54, 6973009299557427565, 4709618332502314628, 623782756, 41, 758947939616460266, 38, 2132563794773469419, 696661960811599136, 1159078994, 4596149, 17273, 83, 36422, 3498443323232355);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6077835106866375633, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc24()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc24: ");
                            long result = PInvokeTests.swiftFunc24(50216, -31158, 965480864, 31, 1049293008, 8407966859260157806, 20505, 1331992, 4691512, 64675, 7560932795418332291);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7246961535839287248, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc25()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc25: ");
                            long result = PInvokeTests.swiftFunc25(unchecked((nint)9173857460962505190), 15022, -93, 62197911982761328, 767946476832147, 1436697338, 453782736594733, 8607484193451689185, 1800209635, 6244274, 2230568, 54, 1734973, 4474435427886752355, 668020350438063396, 9113309237687218066);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4681650148273269479, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc26()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc26: ");
                            long result = PInvokeTests.swiftFunc26(7995355894864936963, 1657769591981037, 5636685, 49073, 1465261410, 4, 118925102, 1262537, 3410729040016453081, 3116369662775677322, 2298252452836462988, unchecked((nuint)1703297916748788633), 41787, 23);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7896710633380101536, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc27()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc27: ");
                            long result = PInvokeTests.swiftFunc27(1288789349037378, 246, 2006429802, unchecked((nuint)1682808387624676808), 171, 1097881357975166039, -15744, 74, 561093280, unchecked((nuint)2288145815937670417), 1410177917, 3832322133938261341, 247663832, 6775031520610184090, 343414669, 2137107, unchecked((nint)1607566964681057532), 4327003494555178103, -29278, 6636805788201424308, 5472, 610898910012130);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2413801917489038945, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc28()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc28: ");
                            long result = PInvokeTests.swiftFunc28(708394762);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5115695744450024635, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc29()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc29: ");
                            long result = PInvokeTests.swiftFunc29(2548726, 185, 23, -27650, 28, 51928, unchecked((nuint)2820244979335896113), 1835, 4050255123891795, 9, 28133, 1603010545626232827, 26825, -3699, unchecked((nuint)9016733266665724964), unchecked((nuint)2189002664079576114), 3158963993105122993, unchecked((nuint)644656289758397492), 7484719451100568914, 41399);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7218188220935660367, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc30()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc30: ");
                            long result = PInvokeTests.swiftFunc30(2476657055966019, 4347333333421575149, unchecked((nint)7035626728730643182), 246, 330548879, 1030645303, 2185, unchecked((nuint)5053178496758927023), -27715, unchecked((nint)3801555871909422480), 191, unchecked((nint)3437128653478702713), 3235, 1144262746, 3278312, 303215206, 23, 7505126148864499061);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3303407505715961682, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc31()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc31: ");
                            long result = PInvokeTests.swiftFunc31(186, 5011206654908794290, 1339751659, -14, unchecked((nint)213472320278997211), unchecked((nuint)7645863228691077485), 179117817, 195, -105, 11576, -57, 6980306680077378405, 56);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6926745355509484660, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc32()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc32: ");
                            long result = PInvokeTests.swiftFunc32(unchecked((nuint)4591110937711003471), 3095346);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8134759728697468421, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc33()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc33: ");
                            long result = PInvokeTests.swiftFunc33(427594908988451897, 172944849, 2318725425520920129);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8926062754575499112, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc34()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc34: ");
                            long result = PInvokeTests.swiftFunc34(7575837, -41, 1617235683, -68, 8522361601751102184, 1353310291, 1751998216661839, 236);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3916199453627741495, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc35()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc35: ");
                            long result = PInvokeTests.swiftFunc35(657240877, -11, 621151963, 3216313916345125, unchecked((nint)4783457933847136272), -12992, 151);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4225631615746848021, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc36()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc36: ");
                            long result = PInvokeTests.swiftFunc36(unchecked((nint)6825261648432495366), 4924311888837579788, unchecked((nint)2728441732713295124), 8539517680162989134, unchecked((nuint)7911109395014469691), 4271690110707692577);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(9029057458451328084, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc37()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc37: ");
                            long result = PInvokeTests.swiftFunc37(-36, unchecked((nint)95080027562818316), 4100547834833879);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(9091326884382848930, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc38()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc38: ");
                            long result = PInvokeTests.swiftFunc38(5598802284356856503, 3277053653997566283, 59, 153, 3703517767397116, 48);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2966780901945169708, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc39()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc39: ");
                            long result = PInvokeTests.swiftFunc39(-86, 1025206765, unchecked((nint)8854120933376595148), -26660, 1421450910, 1171078919, unchecked((nint)7160130076335475540), 4878908, 3905851, 702664134620078, unchecked((nuint)2925732235852901624), 29717, unchecked((nint)5151891595858119552), 5037163689792046960, -4663, 7608014, 9191594607153046781, 5532);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7464446680392812994, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc40()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc40: ");
                            long result = PInvokeTests.swiftFunc40(9, 1203576001, 2456300054912747306, 60251, 1342361607567757, 1388115385, 3232188961689180037, 54573633, 1934356798, 5319583380828737022, unchecked((nuint)999425289681850697), 792187764);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3563617050423332895, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc41()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc41: ");
                            long result = PInvokeTests.swiftFunc41(8474709311929743705, 12394, unchecked((nint)6127564349538668363), 6333136, 4, 446948525, unchecked((nint)5038104703753360766), 26552, 8206136925734873352, -12, 7199240, unchecked((nuint)8318440149659471267), unchecked((nuint)523184722071542187), 398499589, 1325785459276436, 4573239138788995136, 121, 5590899, 617132760792659437);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2569382956498289470, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc42()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc42: ");
                            long result = PInvokeTests.swiftFunc42(8982298576385374275, 16564, unchecked((nint)1565617731878871304), 4419651110224616435, unchecked((nint)7891202001623571444));
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1108582741386924293, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc43()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc43: ");
                            long result = PInvokeTests.swiftFunc43(175);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5808479907339934850, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc44()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc44: ");
                            long result = PInvokeTests.swiftFunc44(3189698876625689, -18777, 6476950464046275862, 1358084128, 7417, 17593, 610320965697918775, 241147333, 115248210, 4468818817792272374, 17930, 1481953553, -12287, -43, 5647436063971232926);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-234686925954875908, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc45()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc45: ");
                            long result = PInvokeTests.swiftFunc45(197, 183, 6515265205839958632, 9815, 5707972, -4010, 67, 82, 1832502602, 1685291427109048803, 23310, unchecked((nuint)4680356630874532717), -19307);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-9083497234002976264, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc46()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc46: ");
                            long result = PInvokeTests.swiftFunc46(579663255922949977, -114, 61, 950773313, 6516, 1526258723168738010, 6992730720599538682);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7467754277704703568, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc47()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc47: ");
                            long result = PInvokeTests.swiftFunc47(4289393662241141492, 6772140852363500214, 59427, -98);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7149358155385248658, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc48()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc48: ");
                            long result = PInvokeTests.swiftFunc48(unchecked((nuint)2785520541460599046), 19124, unchecked((nint)3936154413078833457), 2531598, -22, 7611587982378297798, 4886415070100568562, 53, 168, 47, -12, 1408044606511344677, 586995963, unchecked((nint)877805028374247435), 1735610, 1829192187, 798426250350098200);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8590814201057560160, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc49()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc49: ");
                            long result = PInvokeTests.swiftFunc49(13797);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(739011484971652047, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc50()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc50: ");
                            long result = PInvokeTests.swiftFunc50(5240299569070422159, 3980, 4966478767025838285, 126, 1448511, 3783312608878806, -32326, 1325886438, 170091605, 1038937165);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3055246540243887734, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc51()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc51: ");
                            long result = PInvokeTests.swiftFunc51(153365390);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7917142179400080853, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc52()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc52: ");
                            long result = PInvokeTests.swiftFunc52(49481, unchecked((nuint)2571534213240358460), 19650, 2104528670, 22899, unchecked((nint)8430209965909078811), -25876, 5387620829724391204, unchecked((nint)1450099608276146285), -18049, unchecked((nint)4326178111882457989), 661345047621579, 62960, 239, 3996267746533686661);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8118257769004209257, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc53()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc53: ");
                            long result = PInvokeTests.swiftFunc53(147266479862857914, unchecked((nint)8878677607688119219), 8593690468919623905, 985107042289460, 3243214921586572926, -18, 5766204873311264178, 2076283, 153, unchecked((nint)4491578823480651614), 31118, 154, 4113143241935276428, 2668467, 2828111477600924337, -48, 822748310022451525, unchecked((nuint)5732324054981972848), unchecked((nint)2079781176213395340), 3554919, 124, unchecked((nint)928745656441981312), -118);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(9075957082830800153, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc54()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc54: ");
                            long result = PInvokeTests.swiftFunc54(13995, 13570, 7335161404114781494, 21761, 4879059693239079259, 191, -83, 336670981, 5585960, 8626184290788542400, 3677, 1958748094, 127, 3173715667118077320, 33889614420216385, 642796371, 1838551347, 13607283572602918, 6503940653653026899, 52, 4879061834664472526, 4455735786978402948, unchecked((nint)5167060653638148074), -59);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(715458900514912094, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc55()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc55: ");
                            long result = PInvokeTests.swiftFunc55(2603647622333053765, 4505865605315186666, 109, 96, 10457, 3407618254032143196, 2771970263176123930, 8387065688735342300, 587214218036297943, 47995, -13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7812796314477300904, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc56()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc56: ");
                            long result = PInvokeTests.swiftFunc56(unchecked((nuint)4635596573752865036), 872443880, 2315186662326589, 7230035846427727261, 3908289, 400472551, unchecked((nint)1465473227822284563), -31971, unchecked((nuint)2826972572414861403), 3888061765805348, 779414124, 1373494226, 65241, -14, unchecked((nint)800185043394788883), 99, 70223058, -76, 226);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3660123537755587162, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc57()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc57: ");
                            long result = PInvokeTests.swiftFunc57(-31909, 6905301, 25023, unchecked((nint)1103857621324234540), 2098823486, 5414734, -11, 926572209, 3200449698799467781, 3679569258896139, 8812281510378648951, unchecked((nuint)4871025154453945009), 152, 62421);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8830493546874923270, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc58()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc58: ");
                            long result = PInvokeTests.swiftFunc58(unchecked((nint)7967575041279499718), 3232788, 14816036, 3383016995295473, unchecked((nuint)4850027613376762027), unchecked((nint)8327795864754336795), 3340754, -3120, 2761192, 3983147687671529407, 71, 5318708, 1392678309);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6514055640091085387, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc59()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc59: ");
                            long result = PInvokeTests.swiftFunc59(44844, unchecked((nuint)1748801304810040008), 30766, unchecked((nuint)4039697629876222207), 1041509849, 58, -4630, 2359663412992532838, 4965740);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5046324847209516867, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc60()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc60: ");
                            long result = PInvokeTests.swiftFunc60(1832792349913741, 412903961769194327, 15449);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8176066941526010601, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc61()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc61: ");
                            long result = PInvokeTests.swiftFunc61(-56, 10, 254, 3947646992575930886, unchecked((nint)1512031355372423197), 376047834, unchecked((nint)1656240652039967673), 22865, 2499705526110532, 44, 1741513168, 221);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8047185703659702100, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc62()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc62: ");
                            long result = PInvokeTests.swiftFunc62(10, 2075487406, -9981, 168, unchecked((nint)6799443207845790064), 16835, 4246786459783416, 99, 2318900356573254122, 4147579480654007864);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6758416630263865563, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc63()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc63: ");
                            long result = PInvokeTests.swiftFunc63(4817099);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-24765264996518815, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc64()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc64: ");
                            long result = PInvokeTests.swiftFunc64(-31400, 33, 1144995603961263);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4496411701938139124, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc65()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc65: ");
                            long result = PInvokeTests.swiftFunc65(343468144996369, 1595701486, 691136339, 377795381, unchecked((nuint)8621456802956657380), 387673204, -79, 684151295, 2702822453080893204, 658117164, 1483498070, 19901, 82, 298593782, 498504311);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7620356050748244213, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc66()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc66: ");
                            long result = PInvokeTests.swiftFunc66(unchecked((nuint)6742646500239271530), 1283101175, unchecked((nuint)971826232915481756), 6531);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6837183037573462724, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc67()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc67: ");
                            long result = PInvokeTests.swiftFunc67(2788572937592617, 59180, 200708656, 9131, 1755490561, 258348099, 254, 779012863187640, 1906037567212321, 544676897, 7911267266539149763, 336384219, 575060377, -11136, 1779482464);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3965211134150981679, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc68()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc68: ");
                            long result = PInvokeTests.swiftFunc68(99561312221799, 1453132808, 1612303761, unchecked((nint)6043673650667392022), unchecked((nuint)560907030475989979));
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8645187640386338150, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc69()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc69: ");
                            long result = PInvokeTests.swiftFunc69(unchecked((nuint)5309972206871421224), 27234, 5023167, 45761, 6425609162827184107);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2766546132850174765, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc70()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc70: ");
                            long result = PInvokeTests.swiftFunc70(265213086, 2024995329946372, 99, unchecked((nuint)6130388454564398915), 13675, unchecked((nint)7672787511778724532), 83667695082967, unchecked((nint)8879102379708862673));
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6730251310408327023, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc71()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc71: ");
                            long result = PInvokeTests.swiftFunc71(unchecked((nuint)7826726408346018358), 3933082761574796313, 1549799158, 1737163241, 4770998022264795192, 3012307, -22318, 174, 3175654294509651, 7095989, unchecked((nint)2671492835533826745), unchecked((nint)4435595869554769711), 3593089457929161, -70, 12103, 1171000858, 142);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4761426221194945322, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc72()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc72: ");
                            long result = PInvokeTests.swiftFunc72(6784686274248571763, 104, unchecked((nint)909285983876097936), 6714220880263670485, 7373872626119376832, -23109, -39, 85, 3722567341893906, 5856612, 11316);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8722701469163367659, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc73()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc73: ");
                            long result = PInvokeTests.swiftFunc73(-31440, 1071353143);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(9091436234605144348, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc74()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc74: ");
                            long result = PInvokeTests.swiftFunc74(8367961236466665848, 3084215, 5133, 881467901, 1349905959, 1058177434, 266815227, 740895977807658292, unchecked((nuint)2510276735562063056), 2731666997695150, 7789234325148051159, 1528039387, 16705, 146766703, 4585584465621462072, 1977);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4564195959279673945, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc75()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc75: ");
                            long result = PInvokeTests.swiftFunc75(1027938307, 56236, 88, 98, 27306, 61342909, 2269015579127872, 1031703529, 8402886576148882920, -68, 3807162);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3369734987080453648, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc76()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc76: ");
                            long result = PInvokeTests.swiftFunc76(301994950391123, 4344776, 104, 2137807671, 171179011, 3134127914468069876, 6656, 42885, unchecked((nint)7737600182044247158), -120, 1033649432, 129875179286116790);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8920640767423704440, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc77()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc77: ");
                            long result = PInvokeTests.swiftFunc77(3316166077206800, 3332468478057987249, 1373131825374120, 4918231, 2744065375713515, 3594016966337642259, -60, 20, -106, 2272, 856759296, unchecked((nuint)411883701353980843), unchecked((nint)932327579092391229), 515885, 42, 29247, 2550, 995856082225857);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6960169366615671879, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc78()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc78: ");
                            long result = PInvokeTests.swiftFunc78(8691571239579681212, -6331, unchecked((nuint)747809074035744802), unchecked((nint)4660686733629536050), -25591, 6155, 378094, 52, 7080577359538810005, 26362, 1774417260, 144, 5160013);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4812301631028745377, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc79()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc79: ");
                            long result = PInvokeTests.swiftFunc79(3768859, 701589732, 49, unchecked((nint)2400132102872573811), 7919338068121439001, 12, 1161810112, 1492596679, 3256298, 150297458, 44, 3359191536348582004, 2501, -3042, 31848, unchecked((nuint)8625178339509965677), 1789284789053154, 6259002624415501110, -23813);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(693619259694162127, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc80()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc80: ");
                            long result = PInvokeTests.swiftFunc80(2729190792419187, 1585764063, 5117419591579829234, 614117500, 1693556822);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4631030647197364647, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc81()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc81: ");
                            long result = PInvokeTests.swiftFunc81(unchecked((nint)8095725324232772887), -11, 1220850298, 2854360776836504, 8343542849265358484, 1016078821622888399, 1083388, 791962662);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8908581242517107527, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc82()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc82: ");
                            long result = PInvokeTests.swiftFunc82(unchecked((nuint)1174997556571304622), 2990610909261926, 18753, 6253511180087050924, 3058091764587841331, 2842978159455375886);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1543576629977717704, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc83()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc83: ");
                            long result = PInvokeTests.swiftFunc83(168003999, 689879558669204554, 3381664931253746938, 82, 2210365248447152, 14007, 50724, 211726992, 4908, 9089, -1517, 793801401, 3942422035006427459, 5203020310498374994, 2464433756321920, 8067802492059811569, 649047218);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4161389211393419243, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc84()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc84: ");
                            long result = PInvokeTests.swiftFunc84(5500564689809982598, 8160193010477217516, unchecked((nint)2621562636476726595), 1925518901041551, 833959);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8984640578940854556, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc85()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc85: ");
                            long result = PInvokeTests.swiftFunc85(1828548277567665, 79, unchecked((nuint)2816133458526686380), 246, unchecked((nuint)8123936957398843594), 1915634045, 4277399425149259, 4236491, 526249560, -1564, 2077376027144570, 96496756, 3426459, unchecked((nuint)4946835975333850900), -16125, 5180091340514231581, 6830, 8017, 16950, 83);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5603269280984392717, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc86()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc86: ");
                            long result = PInvokeTests.swiftFunc86(unchecked((nuint)2690514211977331186), 48174, 5251669033533125188, -41, -118, -26036, 46895, 1006135665982533, -25915, 43319, 4016647159010115823, 161);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-756030944410084256, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc87()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc87: ");
                            long result = PInvokeTests.swiftFunc87(41821, 2106599750261545766, 493830841784699955, unchecked((nint)7791163656720105501), 25, -15830, 286454392, 8274918093536357376, -18788, 6681672249680875943, 49954076158807243, 78, 3875942, -110, 2697976, 2443700317924383, 4382626);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3151224756940080953, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc88()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc88: ");
                            long result = PInvokeTests.swiftFunc88(12483, 401268929, -24340, 3584682894830208318, 4149, -28723, -18310, 2621165654927965, unchecked((nuint)4216156540440558538), 2006613843, 6015933, unchecked((nuint)4129107791356788363), 34682, 185, 4770291906992587002, -97, 91, 196);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3274371447309987255, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc89()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc89: ");
                            long result = PInvokeTests.swiftFunc89(154, unchecked((nint)5210339515636897856), unchecked((nint)3761599239105734389), 18, unchecked((nint)72966313290508081), 4265746, unchecked((nuint)8929551288352689384), -24066, 35491, 2551, unchecked((nint)45491645438357652), 715787386644356803, 4473157306905713, 6702547903250883900, 137061596142255, 4385401769623650480, 3378729933484470887, 1873740829, 8574214966744389441, unchecked((nuint)6446163511298821165), unchecked((nuint)6980694483795543674), 1241824808, 23615, 122);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-737269134554333880, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc90()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc90: ");
                            long result = PInvokeTests.swiftFunc90(79, 14, 1297542167439891848, 7930448, 975812823, 259332537, 21563, 28989);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3441802633846719073, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc91()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc91: ");
                            long result = PInvokeTests.swiftFunc91(6278);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(711186144202003795, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc92()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc92: ");
                            long result = PInvokeTests.swiftFunc92(18225, 35, 24134, 4453695771230697, 3872995088603792387, unchecked((nint)6499933966838367751), 1330188682, 444420882, unchecked((nint)3796465283221572512), 52249, 7652735, 46441, 1927427838, 1860451970, unchecked((nint)4367540142032169587), 4492446);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(9206890599465525240, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc93()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc93: ");
                            long result = PInvokeTests.swiftFunc93(5748645559017654978, 5012895573340412455, 6009269000723558276, 2036630461492010444, 2436544965066504769, -125, 112, 52799, -8246, 1830045846, 1191186, 16965, 394617474747610321, 2155653386650409489, 4259466864793291, -118, -32470, -47);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7367909694938381646, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc94()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc94: ");
                            long result = PInvokeTests.swiftFunc94(unchecked((nuint)8677945289555827317), 6313732, 1990822772, unchecked((nuint)7652693732374651003), unchecked((nint)514619182324762120), 48, unchecked((nuint)4099443960025139442), 1897073213, 27911, 227, unchecked((nuint)3629774823693910128), unchecked((nuint)7475134394365608458), 1341041583, 8490306759261258130);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7957085466204676840, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc95()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc95: ");
                            long result = PInvokeTests.swiftFunc95(767020, -67, 1994628757624347102, 1066179, 675787169564137362, unchecked((nint)9035231335376355925), 647886678, -12302, unchecked((nuint)6982264019182060207), 55835, 7279581463482143007, unchecked((nuint)541979943210980226), 38516, unchecked((nuint)2405396521289532217), 577291409326865, 5810393543186852048, 5570902738989457385, 5, 6674072748987199349, 6807910446229279331, 70704, 749368364527200140);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8941275780625427292, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc96()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc96: ");
                            long result = PInvokeTests.swiftFunc96(492870375181451098, 17857, 2042744158, 2550762577962530718, 2130047, unchecked((nint)7683558322808060031), -76, 85, -42, 8057727507941436393, 182, 44022, 8416140399167561318, 1582924161, 8051228828487128057, 968670026, 1);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2083246537822351760, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc97()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc97: ");
                            long result = PInvokeTests.swiftFunc97(16235, 12, 29, -112, 8233611281498306459, 19, 23310, 115438575, unchecked((nuint)4258580046730992269));
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8647824177212049859, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc98()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc98: ");
                            long result = PInvokeTests.swiftFunc98(4045, 86, 101013943533129, unchecked((nint)7999096616438753438), unchecked((nuint)7026548990347163237), 165, 1089253429, 164, unchecked((nuint)8255391170515879868), 13496, 5513927, 46, 3217265538715926, 717333105, 50429, -9149);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7040925530630314472, result);
            Console.WriteLine("OK");
        }

        [Fact]
        public static void TestSwiftFunc99()
        {
            BindingsGenerator.GenerateBindings("PInvoke/PInvokeTests.abi.json", "PInvoke/");
            var sourceCode = """
                // Copyright (c) Microsoft Corporation.
                // Licensed under the MIT License.

                using System;
                using Swift.PInvokeTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running SwiftFunc99: ");
                            long result = PInvokeTests.swiftFunc99(155, unchecked((nuint)1880700265511668237), 1595962890494032981);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "PInvoke/*.cs" }, 
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7883825139759684683, result);
            Console.WriteLine("OK");
        }
    }
}
