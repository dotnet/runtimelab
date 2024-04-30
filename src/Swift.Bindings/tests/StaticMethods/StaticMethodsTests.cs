// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace BindingsGeneration.Tests
{
    public class StaticMethodsTests
    {
        [Fact]
        public static void TestType1swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type1.swiftFunc0: ");
                            long result = Type1.swiftFunc0(9, 57);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1302454221810123473, result);
            Console.WriteLine("OK");
        }
        
 
       [Fact]
        public static void TestType1Type1Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type1.Type1Sub2.swiftFunc0: ");
                            long result = Type1.Type1Sub2.swiftFunc0(30);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(914300229919721579, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType1Type1Sub2Type1Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type1.Type1Sub2.Type1Sub2Sub3.swiftFunc0: ");
                            long result = Type1.Type1Sub2.Type1Sub2Sub3.swiftFunc0(47, 19, 43, 56, 17, 57, 45, 77, 7);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3606159555131430051, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType1Type1Sub2Type1Sub2Sub3Type1Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.swiftFunc0(10, 65, 2, 68.10, 84, 17, 75, 89, 94, 28);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(9085678888513549564, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType1Type1Sub2Type1Sub2Sub3Type1Sub2Sub3Sub4Type1Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.Type1Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.Type1Sub2Sub3Sub4Sub5.swiftFunc0(38, 55, 77, 5, 37);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-9013520609104109583, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType1Type1Sub2Type1Sub2Sub3Type1Sub2Sub3Sub4Type1Sub2Sub3Sub4Sub5Type1Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.Type1Sub2Sub3Sub4Sub5.Type1Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type1.Type1Sub2.Type1Sub2Sub3.Type1Sub2Sub3Sub4.Type1Sub2Sub3Sub4Sub5.Type1Sub2Sub3Sub4Sub5Sub6.swiftFunc0(12, 99, 15, 55, 52, 25, 59, 22, 5, 73.35);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(27416593309743651, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type2.swiftFunc0: ");
                            long result = Type2.swiftFunc0(83, 32, 43, 15, 46, 4);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3661604645194525580, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType2Type2Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type2.Type2Sub2.swiftFunc0: ");
                            long result = Type2.Type2Sub2.swiftFunc0(97, 12, 53);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3025493081346654563, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType2Type2Sub2Type2Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type2.Type2Sub2.Type2Sub2Sub3.swiftFunc0: ");
                            long result = Type2.Type2Sub2.Type2Sub2Sub3.swiftFunc0(85, 19, 5, 30, 75.30, 53, 42, 50);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7677466411347177033, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType2Type2Sub2Type2Sub2Sub3Type2Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.swiftFunc0(22, 12, 62, 99, 10, 82, 19, 11, 36);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2253623701143287732, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType2Type2Sub2Type2Sub2Sub3Type2Sub2Sub3Sub4Type2Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.swiftFunc0(71, 21, 76, 44.79, 13, 6.64, 85, 90, 88.02);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8019726010431750353, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType2Type2Sub2Type2Sub2Sub3Type2Sub2Sub3Sub4Type2Sub2Sub3Sub4Sub5Type2Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.swiftFunc0(1, 66, 48, 11, 74.86, 29, 2);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3146418414537113518, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType2Type2Sub2Type2Sub2Sub3Type2Sub2Sub3Sub4Type2Sub2Sub3Sub4Sub5Type2Sub2Sub3Sub4Sub5Sub6Type2Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.Type2Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.Type2Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(54, 28.87, 1, 56, 41.63);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1803324178910069028, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType2Type2Sub2Type2Sub2Sub3Type2Sub2Sub3Sub4Type2Sub2Sub3Sub4Sub5Type2Sub2Sub3Sub4Sub5Sub6Type2Sub2Sub3Sub4Sub5Sub6Sub7Type2Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.Type2Sub2Sub3Sub4Sub5Sub6Sub7.Type2Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type2.Type2Sub2.Type2Sub2Sub3.Type2Sub2Sub3Sub4.Type2Sub2Sub3Sub4Sub5.Type2Sub2Sub3Sub4Sub5Sub6.Type2Sub2Sub3Sub4Sub5Sub6Sub7.Type2Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(5, 48.78, 13.27, 28, 60.92);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4689617795014579452, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.swiftFunc0: ");
                            long result = Type3.swiftFunc0(64, 48, 10, 20, 57, 18, 98);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6289251196731842658, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.swiftFunc0: ");
                            long result = Type3.Type3Sub2.swiftFunc0(15.44, 57, 64.31, 35, 67, 12, 96, 52.06, 19);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(391791165974922649, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.swiftFunc0(96);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(621294471543772429, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3Type3Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.swiftFunc0(18, 60);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(607854041403170315, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3Type3Sub2Sub3Sub4Type3Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.swiftFunc0(17, 4, 63.40);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3483333800069613251, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3Type3Sub2Sub3Sub4Type3Sub2Sub3Sub4Sub5Type3Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.swiftFunc0(48, 14, 10.64, 2, 43, 8.57);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5674147374399610801, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3Type3Sub2Sub3Sub4Type3Sub2Sub3Sub4Sub5Type3Sub2Sub3Sub4Sub5Sub6Type3Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(0.69);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5106234148504633478, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3Type3Sub2Sub3Sub4Type3Sub2Sub3Sub4Sub5Type3Sub2Sub3Sub4Sub5Sub6Type3Sub2Sub3Sub4Sub5Sub6Sub7Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(60.15, 75, 37, 70, 13, 44);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(246133781151241632, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3Type3Sub2Sub3Sub4Type3Sub2Sub3Sub4Sub5Type3Sub2Sub3Sub4Sub5Sub6Type3Sub2Sub3Sub4Sub5Sub6Sub7Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(35, 74.04, 98, 8);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(9123574604720661329, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3Type3Sub2Sub3Sub4Type3Sub2Sub3Sub4Sub5Type3Sub2Sub3Sub4Sub5Sub6Type3Sub2Sub3Sub4Sub5Sub6Sub7Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(45, 48, 20, 65, 98, 74, 81);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3515963896618663036, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3Type3Sub2Sub3Sub4Type3Sub2Sub3Sub4Sub5Type3Sub2Sub3Sub4Sub5Sub6Type3Sub2Sub3Sub4Sub5Sub6Sub7Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0(9);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-9105318085603802964, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType3Type3Sub2Type3Sub2Sub3Type3Sub2Sub3Sub4Type3Sub2Sub3Sub4Sub5Type3Sub2Sub3Sub4Sub5Sub6Type3Sub2Sub3Sub4Sub5Sub6Sub7Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11Sub12swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11Sub12.swiftFunc0: ");
                            long result = Type3.Type3Sub2.Type3Sub2Sub3.Type3Sub2Sub3Sub4.Type3Sub2Sub3Sub4Sub5.Type3Sub2Sub3Sub4Sub5Sub6.Type3Sub2Sub3Sub4Sub5Sub6Sub7.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11Sub12.swiftFunc0(98, 74.69, 32.29, 13, 53);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8436092602422195875, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type4.swiftFunc0: ");
                            long result = Type4.swiftFunc0(51, 53, 38.12, 86, 91.96, 20, 82, 73, 38, 56);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4414091805691157982, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type5.swiftFunc0: ");
                            long result = Type5.swiftFunc0(0, 82.91, 50, 84, 85, 90, 87, 49);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5482643438640306451, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType5Type5Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type5.Type5Sub2.swiftFunc0: ");
                            long result = Type5.Type5Sub2.swiftFunc0(77, 64, 77, 57, 60, 68, 61.72, 38, 77, 4.87);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7531537036341229865, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type6.swiftFunc0: ");
                            long result = Type6.swiftFunc0(51, 81, 5, 12, 12, 19);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-110785621188401307, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType6Type6Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type6.Type6Sub2.swiftFunc0: ");
                            long result = Type6.Type6Sub2.swiftFunc0(68, 97, 7, 1, 63, 9, 43, 73);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4661143371282223404, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType6Type6Sub2Type6Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type6.Type6Sub2.Type6Sub2Sub3.swiftFunc0: ");
                            long result = Type6.Type6Sub2.Type6Sub2Sub3.swiftFunc0(84, 74, 75, 56);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5437539143661417520, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType6Type6Sub2Type6Sub2Sub3Type6Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type6.Type6Sub2.Type6Sub2Sub3.Type6Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type6.Type6Sub2.Type6Sub2Sub3.Type6Sub2Sub3Sub4.swiftFunc0(58, 96, 45, 73);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8564140570796838325, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType6Type6Sub2Type6Sub2Sub3Type6Sub2Sub3Sub4Type6Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type6.Type6Sub2.Type6Sub2Sub3.Type6Sub2Sub3Sub4.Type6Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type6.Type6Sub2.Type6Sub2Sub3.Type6Sub2Sub3Sub4.Type6Sub2Sub3Sub4Sub5.swiftFunc0(1, 81, 27, 91, 76, 66.80, 60, 42, 11, 76);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(127893875284315364, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type7.swiftFunc0: ");
                            long result = Type7.swiftFunc0(64, 87, 60, 64);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1670198715257327092, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType7Type7Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type7.Type7Sub2.swiftFunc0: ");
                            long result = Type7.Type7Sub2.swiftFunc0(28, 54, 53);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-580800586705400168, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType7Type7Sub2Type7Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type7.Type7Sub2.Type7Sub2Sub3.swiftFunc0: ");
                            long result = Type7.Type7Sub2.Type7Sub2Sub3.swiftFunc0(43, 97, 52, 24, 30.99, 26);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6671430655764225195, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType7Type7Sub2Type7Sub2Sub3Type7Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.swiftFunc0(100, 26.83, 24, 81, 65, 75, 92.49, 100, 4);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2879189024178887556, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType7Type7Sub2Type7Sub2Sub3Type7Sub2Sub3Sub4Type7Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.Type7Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.Type7Sub2Sub3Sub4Sub5.swiftFunc0(68, 100, 74.14, 61);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-57788390475521057, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType7Type7Sub2Type7Sub2Sub3Type7Sub2Sub3Sub4Type7Sub2Sub3Sub4Sub5Type7Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.Type7Sub2Sub3Sub4Sub5.Type7Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type7.Type7Sub2.Type7Sub2Sub3.Type7Sub2Sub3Sub4.Type7Sub2Sub3Sub4Sub5.Type7Sub2Sub3Sub4Sub5Sub6.swiftFunc0(66, 37, 26, 66, 10, 34, 3.57, 94, 84);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8161332097230727509, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type8.swiftFunc0: ");
                            long result = Type8.swiftFunc0(43, 99, 22.16);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7939635022138285726, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType8Type8Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type8.Type8Sub2.swiftFunc0: ");
                            long result = Type8.Type8Sub2.swiftFunc0(28, 62, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5638617594007136487, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType8Type8Sub2Type8Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type8.Type8Sub2.Type8Sub2Sub3.swiftFunc0: ");
                            long result = Type8.Type8Sub2.Type8Sub2Sub3.swiftFunc0(67, 18, 9, 23, 64, 87, 35, 26, 49, 92);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6488902911358175373, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType8Type8Sub2Type8Sub2Sub3Type8Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type8.Type8Sub2.Type8Sub2Sub3.Type8Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type8.Type8Sub2.Type8Sub2Sub3.Type8Sub2Sub3Sub4.swiftFunc0(13, 10, 40, 37.89, 71);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8040904851637460412, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType8Type8Sub2Type8Sub2Sub3Type8Sub2Sub3Sub4Type8Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type8.Type8Sub2.Type8Sub2Sub3.Type8Sub2Sub3Sub4.Type8Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type8.Type8Sub2.Type8Sub2Sub3.Type8Sub2Sub3Sub4.Type8Sub2Sub3Sub4Sub5.swiftFunc0(71, 38.64, 24, 43, 69, 60.10);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7336859208496404469, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type9.swiftFunc0: ");
                            long result = Type9.swiftFunc0(20, 74, 55, 56, 72, 12);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6345937011278591838, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType9Type9Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type9.Type9Sub2.swiftFunc0: ");
                            long result = Type9.Type9Sub2.swiftFunc0(29, 88, 15.50, 99, 55);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5325095042452577111, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType9Type9Sub2Type9Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type9.Type9Sub2.Type9Sub2Sub3.swiftFunc0: ");
                            long result = Type9.Type9Sub2.Type9Sub2Sub3.swiftFunc0(24, 77, 0, 19, 67, 12, 32.25, 84);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-861931995212022056, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType10swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type10.swiftFunc0: ");
                            long result = Type10.swiftFunc0(46, 35, 43, 3, 1, 72, 3, 5);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7332186240727164655, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType10Type10Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type10.Type10Sub2.swiftFunc0: ");
                            long result = Type10.Type10Sub2.swiftFunc0(45, 3, 97, 48, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5015549653232840682, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType10Type10Sub2Type10Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type10.Type10Sub2.Type10Sub2Sub3.swiftFunc0: ");
                            long result = Type10.Type10Sub2.Type10Sub2Sub3.swiftFunc0(99, 64, 18.04, 48);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1759411520443392441, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType11swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type11.swiftFunc0: ");
                            long result = Type11.swiftFunc0(33, 7);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4361279221094392059, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType11Type11Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type11.Type11Sub2.swiftFunc0: ");
                            long result = Type11.Type11Sub2.swiftFunc0(35);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1699583181824442426, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType11Type11Sub2Type11Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type11.Type11Sub2.Type11Sub2Sub3.swiftFunc0: ");
                            long result = Type11.Type11Sub2.Type11Sub2Sub3.swiftFunc0(77, 76, 16.20, 96, 15.68, 67, 51, 76, 73);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8575366906149444658, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType11Type11Sub2Type11Sub2Sub3Type11Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.swiftFunc0(60, 68.52, 47, 63, 77, 14, 73.90, 69, 1, 81);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5229487626593371452, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType11Type11Sub2Type11Sub2Sub3Type11Sub2Sub3Sub4Type11Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.swiftFunc0(53, 33, 87.06);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3892962395563826067, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType11Type11Sub2Type11Sub2Sub3Type11Sub2Sub3Sub4Type11Sub2Sub3Sub4Sub5Type11Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.swiftFunc0(90, 75, 50, 38);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2208890062033214998, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType11Type11Sub2Type11Sub2Sub3Type11Sub2Sub3Sub4Type11Sub2Sub3Sub4Sub5Type11Sub2Sub3Sub4Sub5Sub6Type11Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.Type11Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.Type11Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(58.29, 93.54, 8, 68);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2497407779594054558, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType11Type11Sub2Type11Sub2Sub3Type11Sub2Sub3Sub4Type11Sub2Sub3Sub4Sub5Type11Sub2Sub3Sub4Sub5Sub6Type11Sub2Sub3Sub4Sub5Sub6Sub7Type11Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.Type11Sub2Sub3Sub4Sub5Sub6Sub7.Type11Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type11.Type11Sub2.Type11Sub2Sub3.Type11Sub2Sub3Sub4.Type11Sub2Sub3Sub4Sub5.Type11Sub2Sub3Sub4Sub5Sub6.Type11Sub2Sub3Sub4Sub5Sub6Sub7.Type11Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(5.14, 6, 80, 77, 27, 23, 3, 19, 94, 77);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(324174718095320818, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType12swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type12.swiftFunc0: ");
                            long result = Type12.swiftFunc0(40.61, 36, 29, 91, 96, 86);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1332509595342217973, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType12Type12Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type12.Type12Sub2.swiftFunc0: ");
                            long result = Type12.Type12Sub2.swiftFunc0(71, 39, 34.07);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4081230995526660101, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType13swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type13.swiftFunc0: ");
                            long result = Type13.swiftFunc0(9, 9, 60.59, 96, 66, 61.51, 53, 82);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3737054516232390576, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType13Type13Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type13.Type13Sub2.swiftFunc0: ");
                            long result = Type13.Type13Sub2.swiftFunc0(55, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2635903538728387146, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType13Type13Sub2Type13Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type13.Type13Sub2.Type13Sub2Sub3.swiftFunc0: ");
                            long result = Type13.Type13Sub2.Type13Sub2Sub3.swiftFunc0(17, 21.05, 47, 13, 99);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6625241565325616919, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType13Type13Sub2Type13Sub2Sub3Type13Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.swiftFunc0(70, 0, 87, 57);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2829834324768398881, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType13Type13Sub2Type13Sub2Sub3Type13Sub2Sub3Sub4Type13Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.swiftFunc0(28, 69, 10.54, 64, 13, 17.41, 81, 33.57);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4954353219915555130, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType13Type13Sub2Type13Sub2Sub3Type13Sub2Sub3Sub4Type13Sub2Sub3Sub4Sub5Type13Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.Type13Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.Type13Sub2Sub3Sub4Sub5Sub6.swiftFunc0(38, 3);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7241753510039977648, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType13Type13Sub2Type13Sub2Sub3Type13Sub2Sub3Sub4Type13Sub2Sub3Sub4Sub5Type13Sub2Sub3Sub4Sub5Sub6Type13Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.Type13Sub2Sub3Sub4Sub5Sub6.Type13Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type13.Type13Sub2.Type13Sub2Sub3.Type13Sub2Sub3Sub4.Type13Sub2Sub3Sub4Sub5.Type13Sub2Sub3Sub4Sub5Sub6.Type13Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(56, 68, 67.66);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1832824433293148886, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType14swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type14.swiftFunc0: ");
                            long result = Type14.swiftFunc0(39, 11.26, 72, 88, 24, 27, 48, 76, 31.63);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(901199712025494241, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType15swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type15.swiftFunc0: ");
                            long result = Type15.swiftFunc0(64, 20, 8, 36, 74, 77, 91, 24.38, 27);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7394107227683775943, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType15Type15Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type15.Type15Sub2.swiftFunc0: ");
                            long result = Type15.Type15Sub2.swiftFunc0(84.10, 66.46, 5, 11, 10, 95, 26, 26, 35);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1357280204861671662, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType15Type15Sub2Type15Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type15.Type15Sub2.Type15Sub2Sub3.swiftFunc0: ");
                            long result = Type15.Type15Sub2.Type15Sub2Sub3.swiftFunc0(27, 16, 78.80, 78, 20, 25, 0.02, 78, 80);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6209167671701393392, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType16swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type16.swiftFunc0: ");
                            long result = Type16.swiftFunc0(40, 39);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2016241838007125046, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType16Type16Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type16.Type16Sub2.swiftFunc0: ");
                            long result = Type16.Type16Sub2.swiftFunc0(41.36, 29.18, 79, 61.72, 98.56, 29, 1, 31);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2590768908127775473, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType17swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type17.swiftFunc0: ");
                            long result = Type17.swiftFunc0(4, 16, 89);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3415693461695316082, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType17Type17Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type17.Type17Sub2.swiftFunc0: ");
                            long result = Type17.Type17Sub2.swiftFunc0(22, 60, 28, 68);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1052970797754173925, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType17Type17Sub2Type17Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type17.Type17Sub2.Type17Sub2Sub3.swiftFunc0: ");
                            long result = Type17.Type17Sub2.Type17Sub2Sub3.swiftFunc0(7, 91, 5, 68, 92, 13, 98, 37.01);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1284170383645516782, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType17Type17Sub2Type17Sub2Sub3Type17Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type17.Type17Sub2.Type17Sub2Sub3.Type17Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type17.Type17Sub2.Type17Sub2Sub3.Type17Sub2Sub3Sub4.swiftFunc0(50, 4, 24, 41.75);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3361603965782237971, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType18swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type18.swiftFunc0: ");
                            long result = Type18.swiftFunc0(60.67, 91, 20, 7.71, 51, 69, 15);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7618842638074074063, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType18Type18Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type18.Type18Sub2.swiftFunc0: ");
                            long result = Type18.Type18Sub2.swiftFunc0(37, 78, 60, 35);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2001881504172846833, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType19swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type19.swiftFunc0: ");
                            long result = Type19.swiftFunc0(42, 94, 99, 14, 61);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8780417345600432609, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType19Type19Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type19.Type19Sub2.swiftFunc0: ");
                            long result = Type19.Type19Sub2.swiftFunc0(60.53, 26.15, 83, 10.19, 58, 44, 51);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8146835476385947337, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType19Type19Sub2Type19Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type19.Type19Sub2.Type19Sub2Sub3.swiftFunc0: ");
                            long result = Type19.Type19Sub2.Type19Sub2Sub3.swiftFunc0(73.94, 46, 18, 4.69, 93, 59, 16, 58);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8856268358096150912, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType19Type19Sub2Type19Sub2Sub3Type19Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.swiftFunc0(37.20, 5, 15.16, 42.91, 52.97, 51);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1319882132930383678, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType19Type19Sub2Type19Sub2Sub3Type19Sub2Sub3Sub4Type19Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.swiftFunc0(10, 88, 33.80, 22.70, 13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-508146693585361758, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType19Type19Sub2Type19Sub2Sub3Type19Sub2Sub3Sub4Type19Sub2Sub3Sub4Sub5Type19Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.Type19Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.Type19Sub2Sub3Sub4Sub5Sub6.swiftFunc0(84);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2649507594516546671, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType19Type19Sub2Type19Sub2Sub3Type19Sub2Sub3Sub4Type19Sub2Sub3Sub4Sub5Type19Sub2Sub3Sub4Sub5Sub6Type19Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.Type19Sub2Sub3Sub4Sub5Sub6.Type19Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type19.Type19Sub2.Type19Sub2Sub3.Type19Sub2Sub3Sub4.Type19Sub2Sub3Sub4Sub5.Type19Sub2Sub3Sub4Sub5Sub6.Type19Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(27, 48.79, 63, 71.75, 39, 50, 29, 70.13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8088375796594482986, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType20swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type20.swiftFunc0: ");
                            long result = Type20.swiftFunc0(11, 54, 49);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1442750423798503241, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType20Type20Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type20.Type20Sub2.swiftFunc0: ");
                            long result = Type20.Type20Sub2.swiftFunc0(12, 51, 32, 98);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7158341730093042416, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType20Type20Sub2Type20Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type20.Type20Sub2.Type20Sub2Sub3.swiftFunc0: ");
                            long result = Type20.Type20Sub2.Type20Sub2Sub3.swiftFunc0(36, 65, 84, 75, 51, 50, 56, 47.91, 7);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(553531604612512814, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType20Type20Sub2Type20Sub2Sub3Type20Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type20.Type20Sub2.Type20Sub2Sub3.Type20Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type20.Type20Sub2.Type20Sub2Sub3.Type20Sub2Sub3Sub4.swiftFunc0(37, 91, 82, 69, 52, 76.67, 17, 13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(669600114590149160, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType20Type20Sub2Type20Sub2Sub3Type20Sub2Sub3Sub4Type20Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type20.Type20Sub2.Type20Sub2Sub3.Type20Sub2Sub3Sub4.Type20Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type20.Type20Sub2.Type20Sub2Sub3.Type20Sub2Sub3Sub4.Type20Sub2Sub3Sub4Sub5.swiftFunc0(50);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(577292016191472559, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType21swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type21.swiftFunc0: ");
                            long result = Type21.swiftFunc0(65, 90, 100, 91, 36, 62, 36, 41);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8168985653034001556, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType21Type21Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type21.Type21Sub2.swiftFunc0: ");
                            long result = Type21.Type21Sub2.swiftFunc0(88, 24, 51, 11, 70.97, 37, 34, 2);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4511150573256535639, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType21Type21Sub2Type21Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type21.Type21Sub2.Type21Sub2Sub3.swiftFunc0: ");
                            long result = Type21.Type21Sub2.Type21Sub2Sub3.swiftFunc0(33, 85, 69, 8.53, 99);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(762831339959854365, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType22swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type22.swiftFunc0: ");
                            long result = Type22.swiftFunc0(65.66, 93, 44, 53.51, 78, 39, 69, 32.52, 34);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1749138547312475471, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType22Type22Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type22.Type22Sub2.swiftFunc0: ");
                            long result = Type22.Type22Sub2.swiftFunc0(23);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(603119544333039874, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType22Type22Sub2Type22Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type22.Type22Sub2.Type22Sub2Sub3.swiftFunc0: ");
                            long result = Type22.Type22Sub2.Type22Sub2Sub3.swiftFunc0(78, 99);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3825727416281544568, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType23swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type23.swiftFunc0: ");
                            long result = Type23.swiftFunc0(52, 2.14, 5.51, 27, 73.58, 26, 59, 64, 19, 81);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2876779109167562933, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType23Type23Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type23.Type23Sub2.swiftFunc0: ");
                            long result = Type23.Type23Sub2.swiftFunc0(52, 14, 89, 47, 29, 100, 6, 58, 2);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8604643581634903352, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType23Type23Sub2Type23Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type23.Type23Sub2.Type23Sub2Sub3.swiftFunc0: ");
                            long result = Type23.Type23Sub2.Type23Sub2Sub3.swiftFunc0(17, 13, 66, 35.35, 75);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5183461739495323880, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType24swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type24.swiftFunc0: ");
                            long result = Type24.swiftFunc0(31);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3700353538232897146, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType24Type24Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type24.Type24Sub2.swiftFunc0: ");
                            long result = Type24.Type24Sub2.swiftFunc0(53, 18.02, 65, 18, 65);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1815728824758141834, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType24Type24Sub2Type24Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type24.Type24Sub2.Type24Sub2Sub3.swiftFunc0: ");
                            long result = Type24.Type24Sub2.Type24Sub2Sub3.swiftFunc0(62, 79, 41, 59, 66, 17, 42);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6044068917807042715, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType24Type24Sub2Type24Sub2Sub3Type24Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type24.Type24Sub2.Type24Sub2Sub3.Type24Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type24.Type24Sub2.Type24Sub2Sub3.Type24Sub2Sub3Sub4.swiftFunc0(4);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3658393639098834511, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType24Type24Sub2Type24Sub2Sub3Type24Sub2Sub3Sub4Type24Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type24.Type24Sub2.Type24Sub2Sub3.Type24Sub2Sub3Sub4.Type24Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type24.Type24Sub2.Type24Sub2Sub3.Type24Sub2Sub3Sub4.Type24Sub2Sub3Sub4Sub5.swiftFunc0(69, 34, 6, 85, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3377392297168521549, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType25swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type25.swiftFunc0: ");
                            long result = Type25.swiftFunc0(67, 33.68, 78, 47.50, 51, 93.80, 62, 46);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7100411163736428415, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType25Type25Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type25.Type25Sub2.swiftFunc0: ");
                            long result = Type25.Type25Sub2.swiftFunc0(74, 38, 88, 46, 37, 45, 99, 40, 55, 12);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3587794744717933961, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType25Type25Sub2Type25Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type25.Type25Sub2.Type25Sub2Sub3.swiftFunc0: ");
                            long result = Type25.Type25Sub2.Type25Sub2Sub3.swiftFunc0(86, 16, 98, 29);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6240892100988090868, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType25Type25Sub2Type25Sub2Sub3Type25Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.swiftFunc0(73, 16, 22);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5642305488203428102, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType25Type25Sub2Type25Sub2Sub3Type25Sub2Sub3Sub4Type25Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.swiftFunc0(55, 71, 97, 47, 11, 34);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3231178403858758072, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType25Type25Sub2Type25Sub2Sub3Type25Sub2Sub3Sub4Type25Sub2Sub3Sub4Sub5Type25Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.swiftFunc0(5, 73, 48, 8, 82.30);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2276280640263091297, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType25Type25Sub2Type25Sub2Sub3Type25Sub2Sub3Sub4Type25Sub2Sub3Sub4Sub5Type25Sub2Sub3Sub4Sub5Sub6Type25Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.Type25Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.Type25Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(13, 41, 14, 20, 87.13, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6280951790717052089, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType25Type25Sub2Type25Sub2Sub3Type25Sub2Sub3Sub4Type25Sub2Sub3Sub4Sub5Type25Sub2Sub3Sub4Sub5Sub6Type25Sub2Sub3Sub4Sub5Sub6Sub7Type25Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.Type25Sub2Sub3Sub4Sub5Sub6Sub7.Type25Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type25.Type25Sub2.Type25Sub2Sub3.Type25Sub2Sub3Sub4.Type25Sub2Sub3Sub4Sub5.Type25Sub2Sub3Sub4Sub5Sub6.Type25Sub2Sub3Sub4Sub5Sub6Sub7.Type25Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(18.14, 78, 48);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7596813894285301944, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType26swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type26.swiftFunc0: ");
                            long result = Type26.swiftFunc0(68, 90, 39, 90);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(684334554920709908, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType27swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type27.swiftFunc0: ");
                            long result = Type27.swiftFunc0(61, 88, 44, 32, 82);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3833906899286760162, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType27Type27Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type27.Type27Sub2.swiftFunc0: ");
                            long result = Type27.Type27Sub2.swiftFunc0(75);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(660514051314300574, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType27Type27Sub2Type27Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type27.Type27Sub2.Type27Sub2Sub3.swiftFunc0: ");
                            long result = Type27.Type27Sub2.Type27Sub2Sub3.swiftFunc0(53, 82, 31);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(586766441905616701, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType27Type27Sub2Type27Sub2Sub3Type27Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type27.Type27Sub2.Type27Sub2Sub3.Type27Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type27.Type27Sub2.Type27Sub2Sub3.Type27Sub2Sub3Sub4.swiftFunc0(36, 55, 14, 28, 67.94, 12, 9, 79);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1437306852202736988, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType27Type27Sub2Type27Sub2Sub3Type27Sub2Sub3Sub4Type27Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type27.Type27Sub2.Type27Sub2Sub3.Type27Sub2Sub3Sub4.Type27Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type27.Type27Sub2.Type27Sub2Sub3.Type27Sub2Sub3Sub4.Type27Sub2Sub3Sub4Sub5.swiftFunc0(38.25, 34.89, 45, 79, 61, 95, 30.73, 9, 87, 21);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7840710793526771328, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType28swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type28.swiftFunc0: ");
                            long result = Type28.swiftFunc0(55, 41, 78, 3.10, 28);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1966007234001659582, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType28Type28Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type28.Type28Sub2.swiftFunc0: ");
                            long result = Type28.Type28Sub2.swiftFunc0(37, 43, 71, 67);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-121329981534870825, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType28Type28Sub2Type28Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type28.Type28Sub2.Type28Sub2Sub3.swiftFunc0: ");
                            long result = Type28.Type28Sub2.Type28Sub2Sub3.swiftFunc0(62, 40, 99, 87, 71, 6, 90, 15, 45);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1415111888184892374, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType28Type28Sub2Type28Sub2Sub3Type28Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type28.Type28Sub2.Type28Sub2Sub3.Type28Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type28.Type28Sub2.Type28Sub2Sub3.Type28Sub2Sub3Sub4.swiftFunc0(5, 0, 8);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6001975047106517528, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType28Type28Sub2Type28Sub2Sub3Type28Sub2Sub3Sub4Type28Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type28.Type28Sub2.Type28Sub2Sub3.Type28Sub2Sub3Sub4.Type28Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type28.Type28Sub2.Type28Sub2Sub3.Type28Sub2Sub3Sub4.Type28Sub2Sub3Sub4Sub5.swiftFunc0(77.98, 64, 76, 30, 28, 72, 33, 22, 64, 83);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7933515710817159940, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType29swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type29.swiftFunc0: ");
                            long result = Type29.swiftFunc0(61.61, 89, 1, 9, 19, 23, 35, 97.07, 19, 97);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8015621203482153651, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType29Type29Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type29.Type29Sub2.swiftFunc0: ");
                            long result = Type29.Type29Sub2.swiftFunc0(68.09, 4, 79.03);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4395505961823925134, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType29Type29Sub2Type29Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type29.Type29Sub2.Type29Sub2Sub3.swiftFunc0: ");
                            long result = Type29.Type29Sub2.Type29Sub2Sub3.swiftFunc0(40, 68, 25, 54, 29.74, 43, 11, 53);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2207693396529891830, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType29Type29Sub2Type29Sub2Sub3Type29Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type29.Type29Sub2.Type29Sub2Sub3.Type29Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type29.Type29Sub2.Type29Sub2Sub3.Type29Sub2Sub3Sub4.swiftFunc0(31, 50, 5.28, 30, 31, 26.71, 90, 28, 15);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1970446111156807291, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType30swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type30.swiftFunc0: ");
                            long result = Type30.swiftFunc0(24.73, 31.60, 31, 95.56, 26.29, 94, 56, 85, 37);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-788990220686720046, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType30Type30Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type30.Type30Sub2.swiftFunc0: ");
                            long result = Type30.Type30Sub2.swiftFunc0(53, 35, 77.32, 32, 24);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-898048570311494372, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType30Type30Sub2Type30Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type30.Type30Sub2.Type30Sub2Sub3.swiftFunc0: ");
                            long result = Type30.Type30Sub2.Type30Sub2Sub3.swiftFunc0(40, 26, 6, 79, 97, 23, 94, 41);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8899105243716276813, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType31swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type31.swiftFunc0: ");
                            long result = Type31.swiftFunc0(22, 22.07, 46, 19, 63.41, 80, 32, 72);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8488353822900473986, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType31Type31Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type31.Type31Sub2.swiftFunc0: ");
                            long result = Type31.Type31Sub2.swiftFunc0(25, 73, 18, 45, 85, 57, 86);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3435889942386083384, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType31Type31Sub2Type31Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type31.Type31Sub2.Type31Sub2Sub3.swiftFunc0: ");
                            long result = Type31.Type31Sub2.Type31Sub2Sub3.swiftFunc0(69, 64, 6, 59.17, 6, 42, 28);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8815603871699215989, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType31Type31Sub2Type31Sub2Sub3Type31Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.swiftFunc0(67, 80, 91, 94, 13, 44, 38, 27, 38);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3729506638839213907, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType31Type31Sub2Type31Sub2Sub3Type31Sub2Sub3Sub4Type31Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.swiftFunc0(48.86, 94);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1154091763175769141, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType31Type31Sub2Type31Sub2Sub3Type31Sub2Sub3Sub4Type31Sub2Sub3Sub4Sub5Type31Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.swiftFunc0(37, 100, 63, 70, 92, 79, 6);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5716785126267550184, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType31Type31Sub2Type31Sub2Sub3Type31Sub2Sub3Sub4Type31Sub2Sub3Sub4Sub5Type31Sub2Sub3Sub4Sub5Sub6Type31Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.Type31Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.Type31Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(28.04);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4989739197202666758, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType31Type31Sub2Type31Sub2Sub3Type31Sub2Sub3Sub4Type31Sub2Sub3Sub4Sub5Type31Sub2Sub3Sub4Sub5Sub6Type31Sub2Sub3Sub4Sub5Sub6Sub7Type31Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.Type31Sub2Sub3Sub4Sub5Sub6Sub7.Type31Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type31.Type31Sub2.Type31Sub2Sub3.Type31Sub2Sub3Sub4.Type31Sub2Sub3Sub4Sub5.Type31Sub2Sub3Sub4Sub5Sub6.Type31Sub2Sub3Sub4Sub5Sub6Sub7.Type31Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(60, 9.42, 66, 88.47, 53, 68, 23.71, 41, 98);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5976199894150725410, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType32swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type32.swiftFunc0: ");
                            long result = Type32.swiftFunc0(87, 92, 98, 50, 10.13, 68, 59, 10, 59);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6889201745455097965, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType32Type32Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type32.Type32Sub2.swiftFunc0: ");
                            long result = Type32.Type32Sub2.swiftFunc0(61.68, 15, 82, 33, 95.38, 11.56, 50, 69.24);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2313199209249006652, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType33swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type33.swiftFunc0: ");
                            long result = Type33.swiftFunc0(10, 89);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3367964750273980708, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType33Type33Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type33.Type33Sub2.swiftFunc0: ");
                            long result = Type33.Type33Sub2.swiftFunc0(63, 100, 11, 96, 54, 81, 98);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3487868267203425370, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType33Type33Sub2Type33Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type33.Type33Sub2.Type33Sub2Sub3.swiftFunc0: ");
                            long result = Type33.Type33Sub2.Type33Sub2Sub3.swiftFunc0(62, 55.58, 45, 7.82, 32, 96);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2096831882286791291, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType33Type33Sub2Type33Sub2Sub3Type33Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type33.Type33Sub2.Type33Sub2Sub3.Type33Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type33.Type33Sub2.Type33Sub2Sub3.Type33Sub2Sub3Sub4.swiftFunc0(75, 22, 33, 24, 81, 58, 43.18, 20);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5226086631818805671, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.swiftFunc0: ");
                            long result = Type34.swiftFunc0(89, 4, 46, 56, 38);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5648887699739103852, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34Type34Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.Type34Sub2.swiftFunc0: ");
                            long result = Type34.Type34Sub2.swiftFunc0(17.48, 96, 11, 72, 35);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7005217645564374753, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34Type34Sub2Type34Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.Type34Sub2.Type34Sub2Sub3.swiftFunc0: ");
                            long result = Type34.Type34Sub2.Type34Sub2Sub3.swiftFunc0(41);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5808618445805089436, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34Type34Sub2Type34Sub2Sub3Type34Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.swiftFunc0(3.97, 47, 5.29, 1, 70, 17);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-9158574050224105016, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34Type34Sub2Type34Sub2Sub3Type34Sub2Sub3Sub4Type34Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.swiftFunc0(97, 34.50, 100, 15, 70, 64, 43.93);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5916600624380327077, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34Type34Sub2Type34Sub2Sub3Type34Sub2Sub3Sub4Type34Sub2Sub3Sub4Sub5Type34Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.swiftFunc0(64, 56, 14, 89.68, 58, 2, 83, 63, 67);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2648923089381979377, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34Type34Sub2Type34Sub2Sub3Type34Sub2Sub3Sub4Type34Sub2Sub3Sub4Sub5Type34Sub2Sub3Sub4Sub5Sub6Type34Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(71, 69, 26);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-452105228537114039, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34Type34Sub2Type34Sub2Sub3Type34Sub2Sub3Sub4Type34Sub2Sub3Sub4Sub5Type34Sub2Sub3Sub4Sub5Sub6Type34Sub2Sub3Sub4Sub5Sub6Sub7Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(37, 66.74);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6797445698059191175, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34Type34Sub2Type34Sub2Sub3Type34Sub2Sub3Sub4Type34Sub2Sub3Sub4Sub5Type34Sub2Sub3Sub4Sub5Sub6Type34Sub2Sub3Sub4Sub5Sub6Sub7Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0: ");
                            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(83, 16, 98, 9.87, 66, 62, 81);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(608794204138285486, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType34Type34Sub2Type34Sub2Sub3Type34Sub2Sub3Sub4Type34Sub2Sub3Sub4Sub5Type34Sub2Sub3Sub4Sub5Sub6Type34Sub2Sub3Sub4Sub5Sub6Sub7Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0: ");
                            long result = Type34.Type34Sub2.Type34Sub2Sub3.Type34Sub2Sub3Sub4.Type34Sub2Sub3Sub4Sub5.Type34Sub2Sub3Sub4Sub5Sub6.Type34Sub2Sub3Sub4Sub5Sub6Sub7.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(61, 16, 77, 40, 86, 1);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4082797397364962438, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType35swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type35.swiftFunc0: ");
                            long result = Type35.swiftFunc0(40, 42, 83, 71, 66, 26, 67);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7906006689784259384, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType35Type35Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type35.Type35Sub2.swiftFunc0: ");
                            long result = Type35.Type35Sub2.swiftFunc0(99, 87, 9, 11.35, 96.05);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8659889919274962482, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType35Type35Sub2Type35Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type35.Type35Sub2.Type35Sub2Sub3.swiftFunc0: ");
                            long result = Type35.Type35Sub2.Type35Sub2Sub3.swiftFunc0(28);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1468038131265307737, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType35Type35Sub2Type35Sub2Sub3Type35Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type35.Type35Sub2.Type35Sub2Sub3.Type35Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type35.Type35Sub2.Type35Sub2Sub3.Type35Sub2Sub3Sub4.swiftFunc0(46.79, 41.75, 93, 48, 25, 44, 60, 34, 4);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4715570166168922954, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType35Type35Sub2Type35Sub2Sub3Type35Sub2Sub3Sub4Type35Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type35.Type35Sub2.Type35Sub2Sub3.Type35Sub2Sub3Sub4.Type35Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type35.Type35Sub2.Type35Sub2Sub3.Type35Sub2Sub3Sub4.Type35Sub2Sub3Sub4Sub5.swiftFunc0(86);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(665296926896072299, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType36swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type36.swiftFunc0: ");
                            long result = Type36.swiftFunc0(74, 35, 82.42, 68.23, 4, 3, 23, 46, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5413008670567133046, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType36Type36Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type36.Type36Sub2.swiftFunc0: ");
                            long result = Type36.Type36Sub2.swiftFunc0(99, 31, 71, 50, 8, 73.31, 95, 20.19, 18);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6739692845823709352, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType36Type36Sub2Type36Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type36.Type36Sub2.Type36Sub2Sub3.swiftFunc0: ");
                            long result = Type36.Type36Sub2.Type36Sub2Sub3.swiftFunc0(91, 95, 19.35, 0, 75, 55.48, 11, 58);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7925510834824462180, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType36Type36Sub2Type36Sub2Sub3Type36Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.swiftFunc0(76.20);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2675248728736638871, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType36Type36Sub2Type36Sub2Sub3Type36Sub2Sub3Sub4Type36Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.Type36Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.Type36Sub2Sub3Sub4Sub5.swiftFunc0(20);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2056258949234144081, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType36Type36Sub2Type36Sub2Sub3Type36Sub2Sub3Sub4Type36Sub2Sub3Sub4Sub5Type36Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.Type36Sub2Sub3Sub4Sub5.Type36Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type36.Type36Sub2.Type36Sub2Sub3.Type36Sub2Sub3Sub4.Type36Sub2Sub3Sub4Sub5.Type36Sub2Sub3Sub4Sub5Sub6.swiftFunc0(70, 13.23, 18, 20);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6400995704552108755, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType37swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type37.swiftFunc0: ");
                            long result = Type37.swiftFunc0(100, 10.01);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-29956367469261099, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType37Type37Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type37.Type37Sub2.swiftFunc0: ");
                            long result = Type37.Type37Sub2.swiftFunc0(20, 89, 79, 41, 20, 3);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3643954138502892509, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType37Type37Sub2Type37Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type37.Type37Sub2.Type37Sub2Sub3.swiftFunc0: ");
                            long result = Type37.Type37Sub2.Type37Sub2Sub3.swiftFunc0(50, 41);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1918912945413304390, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType37Type37Sub2Type37Sub2Sub3Type37Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.swiftFunc0(96.38);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4387239261931382494, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType37Type37Sub2Type37Sub2Sub3Type37Sub2Sub3Sub4Type37Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.swiftFunc0(55, 19, 58, 73, 41.32);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(415493127137062773, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType37Type37Sub2Type37Sub2Sub3Type37Sub2Sub3Sub4Type37Sub2Sub3Sub4Sub5Type37Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.Type37Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.Type37Sub2Sub3Sub4Sub5Sub6.swiftFunc0(81, 17.14, 34.21, 69, 48, 99, 95, 18, 49.15, 5.13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4112515667169269651, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType37Type37Sub2Type37Sub2Sub3Type37Sub2Sub3Sub4Type37Sub2Sub3Sub4Sub5Type37Sub2Sub3Sub4Sub5Sub6Type37Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.Type37Sub2Sub3Sub4Sub5Sub6.Type37Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type37.Type37Sub2.Type37Sub2Sub3.Type37Sub2Sub3Sub4.Type37Sub2Sub3Sub4Sub5.Type37Sub2Sub3Sub4Sub5Sub6.Type37Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(85);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(662427201547009264, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType38swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type38.swiftFunc0: ");
                            long result = Type38.swiftFunc0(24, 5, 86, 34, 31, 12, 27, 57.54, 63);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(9195655655750290121, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType38Type38Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type38.Type38Sub2.swiftFunc0: ");
                            long result = Type38.Type38Sub2.swiftFunc0(30, 79.43, 70, 81, 18.21, 53);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8866160771291525329, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType38Type38Sub2Type38Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type38.Type38Sub2.Type38Sub2Sub3.swiftFunc0: ");
                            long result = Type38.Type38Sub2.Type38Sub2Sub3.swiftFunc0(61, 55.43, 37, 32.95, 47, 7);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7919806487363423519, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType38Type38Sub2Type38Sub2Sub3Type38Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type38.Type38Sub2.Type38Sub2Sub3.Type38Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type38.Type38Sub2.Type38Sub2Sub3.Type38Sub2Sub3Sub4.swiftFunc0(17, 88, 61, 12, 71, 1, 88);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3938287191714746229, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType38Type38Sub2Type38Sub2Sub3Type38Sub2Sub3Sub4Type38Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type38.Type38Sub2.Type38Sub2Sub3.Type38Sub2Sub3Sub4.Type38Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type38.Type38Sub2.Type38Sub2Sub3.Type38Sub2Sub3Sub4.Type38Sub2Sub3Sub4Sub5.swiftFunc0(7, 72, 61);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8766743431031461175, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType39swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type39.swiftFunc0: ");
                            long result = Type39.swiftFunc0(71, 29, 18, 74, 19, 1, 39, 57);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8986509835952913005, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType39Type39Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type39.Type39Sub2.swiftFunc0: ");
                            long result = Type39.Type39Sub2.swiftFunc0(21.10, 39, 24, 9, 89);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3590691304079810474, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType39Type39Sub2Type39Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type39.Type39Sub2.Type39Sub2Sub3.swiftFunc0: ");
                            long result = Type39.Type39Sub2.Type39Sub2Sub3.swiftFunc0(6, 17, 13, 88, 58, 50, 35.88, 11.76, 25, 79);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7724744106161839777, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType40swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type40.swiftFunc0: ");
                            long result = Type40.swiftFunc0(11, 22, 81.72, 92, 11, 94, 87, 40);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2229816267186466030, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType40Type40Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type40.Type40Sub2.swiftFunc0: ");
                            long result = Type40.Type40Sub2.swiftFunc0(62, 6, 74, 41, 45, 50, 36, 39, 76);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4118228357113608828, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType40Type40Sub2Type40Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type40.Type40Sub2.Type40Sub2Sub3.swiftFunc0: ");
                            long result = Type40.Type40Sub2.Type40Sub2Sub3.swiftFunc0(86, 76, 47, 68, 18, 2.53, 83, 62.25);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1804145342247858854, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType40Type40Sub2Type40Sub2Sub3Type40Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type40.Type40Sub2.Type40Sub2Sub3.Type40Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type40.Type40Sub2.Type40Sub2Sub3.Type40Sub2Sub3Sub4.swiftFunc0(26.49, 9, 20, 93);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7704759995325073605, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType41swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type41.swiftFunc0: ");
                            long result = Type41.swiftFunc0(20, 37, 96, 53, 19, 58.48, 5, 87, 72, 37);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1372448214341753544, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType42swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type42.swiftFunc0: ");
                            long result = Type42.swiftFunc0(57, 13, 75.72, 75, 74, 79, 43.53);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1971274345882451651, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType42Type42Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type42.Type42Sub2.swiftFunc0: ");
                            long result = Type42.Type42Sub2.swiftFunc0(22, 25, 98, 30, 88, 98, 43, 23.85, 45, 23);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4574023028842821245, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType42Type42Sub2Type42Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type42.Type42Sub2.Type42Sub2Sub3.swiftFunc0: ");
                            long result = Type42.Type42Sub2.Type42Sub2Sub3.swiftFunc0(100, 40);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4929146646809221225, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType42Type42Sub2Type42Sub2Sub3Type42Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type42.Type42Sub2.Type42Sub2Sub3.Type42Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type42.Type42Sub2.Type42Sub2Sub3.Type42Sub2Sub3Sub4.swiftFunc0(54, 22, 29, 75, 76, 33, 63, 33, 14);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8349326794474127128, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType43swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type43.swiftFunc0: ");
                            long result = Type43.swiftFunc0(59, 99, 7);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8460397198155335392, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType43Type43Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type43.Type43Sub2.swiftFunc0: ");
                            long result = Type43.Type43Sub2.swiftFunc0(12, 11, 55, 16, 76, 86);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1356384574701834659, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType43Type43Sub2Type43Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type43.Type43Sub2.Type43Sub2Sub3.swiftFunc0: ");
                            long result = Type43.Type43Sub2.Type43Sub2Sub3.swiftFunc0(25, 79, 72.86, 38, 46, 24);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3156535411293896611, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType44swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type44.swiftFunc0: ");
                            long result = Type44.swiftFunc0(64, 46, 32, 81, 24, 8, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6472602285402761050, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType44Type44Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type44.Type44Sub2.swiftFunc0: ");
                            long result = Type44.Type44Sub2.swiftFunc0(95);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7737348280147095578, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType44Type44Sub2Type44Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type44.Type44Sub2.Type44Sub2Sub3.swiftFunc0: ");
                            long result = Type44.Type44Sub2.Type44Sub2Sub3.swiftFunc0(78, 72, 10, 5, 34, 32, 65.87, 88, 36);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3183280857369603368, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType44Type44Sub2Type44Sub2Sub3Type44Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.swiftFunc0(60.98, 15, 72, 20, 0.43, 65, 21, 71, 12.86, 32);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1357428895534966919, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType44Type44Sub2Type44Sub2Sub3Type44Sub2Sub3Sub4Type44Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.swiftFunc0(29.61);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1224872778054606228, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType44Type44Sub2Type44Sub2Sub3Type44Sub2Sub3Sub4Type44Sub2Sub3Sub4Sub5Type44Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.swiftFunc0(5, 52, 40, 64, 84, 64, 19, 68.39, 71);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(731988194140412712, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType44Type44Sub2Type44Sub2Sub3Type44Sub2Sub3Sub4Type44Sub2Sub3Sub4Sub5Type44Sub2Sub3Sub4Sub5Sub6Type44Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(59, 37, 37, 1, 86, 52, 97, 23.50, 41);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8751287127369919344, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType44Type44Sub2Type44Sub2Sub3Type44Sub2Sub3Sub4Type44Sub2Sub3Sub4Sub5Type44Sub2Sub3Sub4Sub5Sub6Type44Sub2Sub3Sub4Sub5Sub6Sub7Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(56, 23, 75, 33, 90, 38, 17.65, 38, 13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1578096571707272808, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType44Type44Sub2Type44Sub2Sub3Type44Sub2Sub3Sub4Type44Sub2Sub3Sub4Sub5Type44Sub2Sub3Sub4Sub5Sub6Type44Sub2Sub3Sub4Sub5Sub6Sub7Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0: ");
                            long result = Type44.Type44Sub2.Type44Sub2Sub3.Type44Sub2Sub3Sub4.Type44Sub2Sub3Sub4Sub5.Type44Sub2Sub3Sub4Sub5Sub6.Type44Sub2Sub3Sub4Sub5Sub6Sub7.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(95.21, 34, 32, 15, 20, 69, 95);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1564978127064141375, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType45swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type45.swiftFunc0: ");
                            long result = Type45.swiftFunc0(3, 73, 19.95, 70, 35, 47, 1, 35.40, 98.95, 92);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8806294851017938129, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType45Type45Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type45.Type45Sub2.swiftFunc0: ");
                            long result = Type45.Type45Sub2.swiftFunc0(22, 39.68, 50, 39, 49, 81.21);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(829863142919886848, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType45Type45Sub2Type45Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type45.Type45Sub2.Type45Sub2Sub3.swiftFunc0: ");
                            long result = Type45.Type45Sub2.Type45Sub2Sub3.swiftFunc0(98, 57, 43.08, 89, 51, 44.56, 30);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6909175346769209936, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType46swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type46.swiftFunc0: ");
                            long result = Type46.swiftFunc0(78, 48, 90, 12, 80.62);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7275121935272093171, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType46Type46Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type46.Type46Sub2.swiftFunc0: ");
                            long result = Type46.Type46Sub2.swiftFunc0(99, 21);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7930005622093663545, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType46Type46Sub2Type46Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type46.Type46Sub2.Type46Sub2Sub3.swiftFunc0: ");
                            long result = Type46.Type46Sub2.Type46Sub2Sub3.swiftFunc0(100, 32, 10, 47, 60, 32, 1, 99.92);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4327506764072823738, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType46Type46Sub2Type46Sub2Sub3Type46Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type46.Type46Sub2.Type46Sub2Sub3.Type46Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type46.Type46Sub2.Type46Sub2Sub3.Type46Sub2Sub3Sub4.swiftFunc0(69, 85.30, 26, 44, 41, 35.15);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5871068301339653861, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType47swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type47.swiftFunc0: ");
                            long result = Type47.swiftFunc0(77.59, 16.71);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3617576866883394702, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType47Type47Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type47.Type47Sub2.swiftFunc0: ");
                            long result = Type47.Type47Sub2.swiftFunc0(88, 85, 21, 37, 75, 87, 67.88, 11.38, 43);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8797535796924509829, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType47Type47Sub2Type47Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type47.Type47Sub2.Type47Sub2Sub3.swiftFunc0: ");
                            long result = Type47.Type47Sub2.Type47Sub2Sub3.swiftFunc0(62, 98);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-9093135547841380823, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType48swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type48.swiftFunc0: ");
                            long result = Type48.swiftFunc0(33, 16, 99, 15, 32, 24);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7370059868804831648, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType48Type48Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type48.Type48Sub2.swiftFunc0: ");
                            long result = Type48.Type48Sub2.swiftFunc0(77, 28, 68, 12, 14, 15, 43, 80);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1571987122778653432, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType48Type48Sub2Type48Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type48.Type48Sub2.Type48Sub2Sub3.swiftFunc0: ");
                            long result = Type48.Type48Sub2.Type48Sub2Sub3.swiftFunc0(73, 89.03, 46, 76, 89.07, 83);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3759391477887331378, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType48Type48Sub2Type48Sub2Sub3Type48Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.swiftFunc0(77, 50, 49, 90.87, 83);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5546215590190126240, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType48Type48Sub2Type48Sub2Sub3Type48Sub2Sub3Sub4Type48Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.Type48Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.Type48Sub2Sub3Sub4Sub5.swiftFunc0(80, 36, 36, 34, 61, 18, 16, 31, 65, 45);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5515496509618210499, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType48Type48Sub2Type48Sub2Sub3Type48Sub2Sub3Sub4Type48Sub2Sub3Sub4Sub5Type48Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.Type48Sub2Sub3Sub4Sub5.Type48Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type48.Type48Sub2.Type48Sub2Sub3.Type48Sub2Sub3Sub4.Type48Sub2Sub3Sub4Sub5.Type48Sub2Sub3Sub4Sub5Sub6.swiftFunc0(12, 63.75);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3389738121929217758, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType49swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type49.swiftFunc0: ");
                            long result = Type49.swiftFunc0(66, 61, 26, 14.45, 9, 2, 34);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(204164697602924849, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType49Type49Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type49.Type49Sub2.swiftFunc0: ");
                            long result = Type49.Type49Sub2.swiftFunc0(38, 58, 31, 88, 40);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4843295259536981670, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType49Type49Sub2Type49Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type49.Type49Sub2.Type49Sub2Sub3.swiftFunc0: ");
                            long result = Type49.Type49Sub2.Type49Sub2Sub3.swiftFunc0(91, 22, 23, 86.07);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1203075211124201782, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType49Type49Sub2Type49Sub2Sub3Type49Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.swiftFunc0(79.64, 64, 100, 10, 86, 34, 57.79);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(987652878952837373, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType49Type49Sub2Type49Sub2Sub3Type49Sub2Sub3Sub4Type49Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.swiftFunc0(94, 48.44, 61, 6.13, 43.63, 23, 60, 19.43);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8597106858903601342, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType49Type49Sub2Type49Sub2Sub3Type49Sub2Sub3Sub4Type49Sub2Sub3Sub4Sub5Type49Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.Type49Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.Type49Sub2Sub3Sub4Sub5Sub6.swiftFunc0(98, 89, 61, 82, 57, 72);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6448247700166283672, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType49Type49Sub2Type49Sub2Sub3Type49Sub2Sub3Sub4Type49Sub2Sub3Sub4Sub5Type49Sub2Sub3Sub4Sub5Sub6Type49Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.Type49Sub2Sub3Sub4Sub5Sub6.Type49Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type49.Type49Sub2.Type49Sub2Sub3.Type49Sub2Sub3Sub4.Type49Sub2Sub3Sub4Sub5.Type49Sub2Sub3Sub4Sub5Sub6.Type49Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(76, 4, 45);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6150748742965086152, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType50swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type50.swiftFunc0: ");
                            long result = Type50.swiftFunc0(32, 21, 6, 75, 7.43, 56, 8, 36);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7248006303634400941, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType50Type50Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type50.Type50Sub2.swiftFunc0: ");
                            long result = Type50.Type50Sub2.swiftFunc0(16, 98, 8, 9.02);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1410935449793688018, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType51swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type51.swiftFunc0: ");
                            long result = Type51.swiftFunc0(74, 78, 77, 12, 24, 90);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8849936543451192470, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType51Type51Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type51.Type51Sub2.swiftFunc0: ");
                            long result = Type51.Type51Sub2.swiftFunc0(24);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5808564569735307097, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType51Type51Sub2Type51Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type51.Type51Sub2.Type51Sub2Sub3.swiftFunc0: ");
                            long result = Type51.Type51Sub2.Type51Sub2Sub3.swiftFunc0(48, 1, 24, 76);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5915561197349509280, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType51Type51Sub2Type51Sub2Sub3Type51Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type51.Type51Sub2.Type51Sub2Sub3.Type51Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type51.Type51Sub2.Type51Sub2Sub3.Type51Sub2Sub3Sub4.swiftFunc0(74, 19, 34, 37, 8, 18, 91, 72);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5213807528371859026, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType51Type51Sub2Type51Sub2Sub3Type51Sub2Sub3Sub4Type51Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type51.Type51Sub2.Type51Sub2Sub3.Type51Sub2Sub3Sub4.Type51Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type51.Type51Sub2.Type51Sub2Sub3.Type51Sub2Sub3Sub4.Type51Sub2Sub3Sub4Sub5.swiftFunc0(54, 65, 47, 6, 39, 29, 3, 77, 34);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5650716389767694265, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType52swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type52.swiftFunc0: ");
                            long result = Type52.swiftFunc0(99, 84, 83.32, 44, 15);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1943851521608495833, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType52Type52Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type52.Type52Sub2.swiftFunc0: ");
                            long result = Type52.Type52Sub2.swiftFunc0(20, 82.67, 82, 71, 36, 53, 39.05, 73, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8696900450471275766, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType52Type52Sub2Type52Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type52.Type52Sub2.Type52Sub2Sub3.swiftFunc0: ");
                            long result = Type52.Type52Sub2.Type52Sub2Sub3.swiftFunc0(81, 59, 87, 71, 88, 15, 59, 38, 69);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(208110160139832832, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType52Type52Sub2Type52Sub2Sub3Type52Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type52.Type52Sub2.Type52Sub2Sub3.Type52Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type52.Type52Sub2.Type52Sub2Sub3.Type52Sub2Sub3Sub4.swiftFunc0(35, 91, 13.36, 35, 26.72);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6610413077972723642, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType53swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type53.swiftFunc0: ");
                            long result = Type53.swiftFunc0(51);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2876024817762115114, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType53Type53Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type53.Type53Sub2.swiftFunc0: ");
                            long result = Type53.Type53Sub2.swiftFunc0(63, 9.78, 11, 0, 44, 28, 93, 89, 74, 55);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5694387538268430038, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType54swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type54.swiftFunc0: ");
                            long result = Type54.swiftFunc0(37);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3353268450079572736, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType54Type54Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type54.Type54Sub2.swiftFunc0: ");
                            long result = Type54.Type54Sub2.swiftFunc0(28);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(609815570147520289, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType54Type54Sub2Type54Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type54.Type54Sub2.Type54Sub2Sub3.swiftFunc0: ");
                            long result = Type54.Type54Sub2.Type54Sub2Sub3.swiftFunc0(97, 48.70, 83);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7778548124421051982, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType54Type54Sub2Type54Sub2Sub3Type54Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type54.Type54Sub2.Type54Sub2Sub3.Type54Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type54.Type54Sub2.Type54Sub2Sub3.Type54Sub2Sub3Sub4.swiftFunc0(29, 26, 25);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8994000051989817089, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.swiftFunc0: ");
                            long result = Type55.swiftFunc0(96);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8637665132542722587, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.swiftFunc0: ");
                            long result = Type55.Type55Sub2.swiftFunc0(43, 76, 76, 43, 13, 40);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4511397732638447968, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2Type55Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.Type55Sub2Sub3.swiftFunc0: ");
                            long result = Type55.Type55Sub2.Type55Sub2Sub3.swiftFunc0(23, 84, 17, 22, 85.99, 67, 49, 62, 49);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(750836114795011244, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2Type55Sub2Sub3Type55Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.swiftFunc0(24, 88.62, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8978418421514884105, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2Type55Sub2Sub3Type55Sub2Sub3Sub4Type55Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.swiftFunc0(42, 18, 58, 98, 53, 19.39, 88, 20, 34);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4788927712479296194, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2Type55Sub2Sub3Type55Sub2Sub3Sub4Type55Sub2Sub3Sub4Sub5Type55Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.swiftFunc0(73, 61);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5053614839553447791, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2Type55Sub2Sub3Type55Sub2Sub3Sub4Type55Sub2Sub3Sub4Sub5Type55Sub2Sub3Sub4Sub5Sub6Type55Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(97, 72, 36, 23, 92);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6394764035969715939, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2Type55Sub2Sub3Type55Sub2Sub3Sub4Type55Sub2Sub3Sub4Sub5Type55Sub2Sub3Sub4Sub5Sub6Type55Sub2Sub3Sub4Sub5Sub6Sub7Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(89, 88, 33.17, 91, 70, 12, 99, 67, 40, 97);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1538523573391109700, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2Type55Sub2Sub3Type55Sub2Sub3Sub4Type55Sub2Sub3Sub4Sub5Type55Sub2Sub3Sub4Sub5Sub6Type55Sub2Sub3Sub4Sub5Sub6Sub7Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0: ");
                            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(36, 65);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6038112166435015472, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2Type55Sub2Sub3Type55Sub2Sub3Sub4Type55Sub2Sub3Sub4Sub5Type55Sub2Sub3Sub4Sub5Sub6Type55Sub2Sub3Sub4Sub5Sub6Sub7Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0: ");
                            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(53.32, 95.33, 90, 14, 13, 30, 35, 16.69, 13, 44);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2194148373478066058, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType55Type55Sub2Type55Sub2Sub3Type55Sub2Sub3Sub4Type55Sub2Sub3Sub4Sub5Type55Sub2Sub3Sub4Sub5Sub6Type55Sub2Sub3Sub4Sub5Sub6Sub7Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0: ");
                            long result = Type55.Type55Sub2.Type55Sub2Sub3.Type55Sub2Sub3Sub4.Type55Sub2Sub3Sub4Sub5.Type55Sub2Sub3Sub4Sub5Sub6.Type55Sub2Sub3Sub4Sub5Sub6Sub7.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0(56);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5696561042698540867, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType56swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type56.swiftFunc0: ");
                            long result = Type56.swiftFunc0(53);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5808614047758576592, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType56Type56Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type56.Type56Sub2.swiftFunc0: ");
                            long result = Type56.Type56Sub2.swiftFunc0(40, 13, 41);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5879231729607274961, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType56Type56Sub2Type56Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type56.Type56Sub2.Type56Sub2Sub3.swiftFunc0: ");
                            long result = Type56.Type56Sub2.Type56Sub2Sub3.swiftFunc0(58, 41);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7448087530822462958, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType57swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type57.swiftFunc0: ");
                            long result = Type57.swiftFunc0(77, 19, 72, 42, 63, 5, 62.77, 25, 27);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1383735803969955367, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType57Type57Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type57.Type57Sub2.swiftFunc0: ");
                            long result = Type57.Type57Sub2.swiftFunc0(28, 46, 2, 85.90, 45, 59, 19, 75, 56);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1391155564802257753, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType57Type57Sub2Type57Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type57.Type57Sub2.Type57Sub2Sub3.swiftFunc0: ");
                            long result = Type57.Type57Sub2.Type57Sub2Sub3.swiftFunc0(0, 35.78, 8, 54, 81, 61, 28, 91.93, 63.64);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7701306734874843396, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType57Type57Sub2Type57Sub2Sub3Type57Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.swiftFunc0(21, 56.03);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(210589735848477449, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType57Type57Sub2Type57Sub2Sub3Type57Sub2Sub3Sub4Type57Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.Type57Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.Type57Sub2Sub3Sub4Sub5.swiftFunc0(21, 63.14, 67, 75, 63, 98.20, 4);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3584437386805933162, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType57Type57Sub2Type57Sub2Sub3Type57Sub2Sub3Sub4Type57Sub2Sub3Sub4Sub5Type57Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.Type57Sub2Sub3Sub4Sub5.Type57Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type57.Type57Sub2.Type57Sub2Sub3.Type57Sub2Sub3Sub4.Type57Sub2Sub3Sub4Sub5.Type57Sub2Sub3Sub4Sub5Sub6.swiftFunc0(43);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1324056366855609618, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType58swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type58.swiftFunc0: ");
                            long result = Type58.swiftFunc0(9, 79, 96, 21.65, 48, 67, 31);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4841518573318706584, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType58Type58Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type58.Type58Sub2.swiftFunc0: ");
                            long result = Type58.Type58Sub2.swiftFunc0(14, 49);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1968512687697882966, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType59swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type59.swiftFunc0: ");
                            long result = Type59.swiftFunc0(70, 89, 13, 88, 3, 78.19, 90.91, 17, 20.88, 48);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5399180687717383068, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType59Type59Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type59.Type59Sub2.swiftFunc0: ");
                            long result = Type59.Type59Sub2.swiftFunc0(70);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2991564855356304835, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType59Type59Sub2Type59Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type59.Type59Sub2.Type59Sub2Sub3.swiftFunc0: ");
                            long result = Type59.Type59Sub2.Type59Sub2Sub3.swiftFunc0(97);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6016726080209032828, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType59Type59Sub2Type59Sub2Sub3Type59Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.swiftFunc0(53, 60, 90, 65, 63, 28, 98);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4659456914887193846, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType59Type59Sub2Type59Sub2Sub3Type59Sub2Sub3Sub4Type59Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.swiftFunc0(94, 18, 48, 2.84, 35, 26);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3979182145248251593, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType59Type59Sub2Type59Sub2Sub3Type59Sub2Sub3Sub4Type59Sub2Sub3Sub4Sub5Type59Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.swiftFunc0(3);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(591640642936787734, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType59Type59Sub2Type59Sub2Sub3Type59Sub2Sub3Sub4Type59Sub2Sub3Sub4Sub5Type59Sub2Sub3Sub4Sub5Sub6Type59Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(13.31, 73, 3, 75, 84);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5044478833016954148, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType59Type59Sub2Type59Sub2Sub3Type59Sub2Sub3Sub4Type59Sub2Sub3Sub4Sub5Type59Sub2Sub3Sub4Sub5Sub6Type59Sub2Sub3Sub4Sub5Sub6Sub7Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(50, 56.68, 62);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8121508732296114185, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType59Type59Sub2Type59Sub2Sub3Type59Sub2Sub3Sub4Type59Sub2Sub3Sub4Sub5Type59Sub2Sub3Sub4Sub5Sub6Type59Sub2Sub3Sub4Sub5Sub6Sub7Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0: ");
                            long result = Type59.Type59Sub2.Type59Sub2Sub3.Type59Sub2Sub3Sub4.Type59Sub2Sub3Sub4Sub5.Type59Sub2Sub3Sub4Sub5Sub6.Type59Sub2Sub3Sub4Sub5Sub6Sub7.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(79, 72, 69, 84.00, 43.48, 29);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5264772612232238808, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.swiftFunc0: ");
                            long result = Type60.swiftFunc0(54, 18.20, 28, 87, 37, 95, 28);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2231327004938290454, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60Type60Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.Type60Sub2.swiftFunc0: ");
                            long result = Type60.Type60Sub2.swiftFunc0(92, 68, 30.60, 0, 68, 1);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3773240513285678005, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60Type60Sub2Type60Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.Type60Sub2.Type60Sub2Sub3.swiftFunc0: ");
                            long result = Type60.Type60Sub2.Type60Sub2Sub3.swiftFunc0(4, 3, 46, 96, 69, 64, 84, 98.80);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4935582694013642489, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60Type60Sub2Type60Sub2Sub3Type60Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.swiftFunc0(15, 23.46, 35, 12, 39);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1326027808488383381, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60Type60Sub2Type60Sub2Sub3Type60Sub2Sub3Sub4Type60Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.swiftFunc0(78.84, 75, 32);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8938060229869367453, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60Type60Sub2Type60Sub2Sub3Type60Sub2Sub3Sub4Type60Sub2Sub3Sub4Sub5Type60Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.swiftFunc0(13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(593553793169496424, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60Type60Sub2Type60Sub2Sub3Type60Sub2Sub3Sub4Type60Sub2Sub3Sub4Sub5Type60Sub2Sub3Sub4Sub5Sub6Type60Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(12, 46);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1691025257347504405, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60Type60Sub2Type60Sub2Sub3Type60Sub2Sub3Sub4Type60Sub2Sub3Sub4Sub5Type60Sub2Sub3Sub4Sub5Sub6Type60Sub2Sub3Sub4Sub5Sub6Sub7Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(93.44, 2, 73, 58, 20, 45, 25);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6310027403672872531, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60Type60Sub2Type60Sub2Sub3Type60Sub2Sub3Sub4Type60Sub2Sub3Sub4Sub5Type60Sub2Sub3Sub4Sub5Sub6Type60Sub2Sub3Sub4Sub5Sub6Sub7Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0: ");
                            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(89, 58);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2912831909921751162, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType60Type60Sub2Type60Sub2Sub3Type60Sub2Sub3Sub4Type60Sub2Sub3Sub4Sub5Type60Sub2Sub3Sub4Sub5Sub6Type60Sub2Sub3Sub4Sub5Sub6Sub7Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0: ");
                            long result = Type60.Type60Sub2.Type60Sub2Sub3.Type60Sub2Sub3Sub4.Type60Sub2Sub3Sub4Sub5.Type60Sub2Sub3Sub4Sub5Sub6.Type60Sub2Sub3Sub4Sub5Sub6Sub7.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(60, 56, 27, 84, 83, 23);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7930192212614880088, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType61swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type61.swiftFunc0: ");
                            long result = Type61.swiftFunc0(71, 17, 1, 97, 76, 4.01);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2189305905996890980, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType61Type61Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type61.Type61Sub2.swiftFunc0: ");
                            long result = Type61.Type61Sub2.swiftFunc0(32, 38, 37, 31, 48);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1160194257063564797, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType62swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type62.swiftFunc0: ");
                            long result = Type62.swiftFunc0(84, 26, 86, 32, 87);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3025651207962572234, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType62Type62Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type62.Type62Sub2.swiftFunc0: ");
                            long result = Type62.Type62Sub2.swiftFunc0(81.64, 92);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6755609106063950752, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType62Type62Sub2Type62Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type62.Type62Sub2.Type62Sub2Sub3.swiftFunc0: ");
                            long result = Type62.Type62Sub2.Type62Sub2Sub3.swiftFunc0(39, 46, 50, 7);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7656311073493574779, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType62Type62Sub2Type62Sub2Sub3Type62Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.swiftFunc0(50, 36, 63, 41, 49, 82, 71, 40.84, 59);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2867324145537522437, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType62Type62Sub2Type62Sub2Sub3Type62Sub2Sub3Sub4Type62Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.swiftFunc0(58, 30, 99, 57, 31, 20, 18, 27.06);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5289008638091810662, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType62Type62Sub2Type62Sub2Sub3Type62Sub2Sub3Sub4Type62Sub2Sub3Sub4Sub5Type62Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.swiftFunc0(64, 5, 70);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8072638257956225766, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType62Type62Sub2Type62Sub2Sub3Type62Sub2Sub3Sub4Type62Sub2Sub3Sub4Sub5Type62Sub2Sub3Sub4Sub5Sub6Type62Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.Type62Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.Type62Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(85, 62, 99, 62, 40, 98, 73);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6876973284445166072, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType62Type62Sub2Type62Sub2Sub3Type62Sub2Sub3Sub4Type62Sub2Sub3Sub4Sub5Type62Sub2Sub3Sub4Sub5Sub6Type62Sub2Sub3Sub4Sub5Sub6Sub7Type62Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.Type62Sub2Sub3Sub4Sub5Sub6Sub7.Type62Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type62.Type62Sub2.Type62Sub2Sub3.Type62Sub2Sub3Sub4.Type62Sub2Sub3Sub4Sub5.Type62Sub2Sub3Sub4Sub5Sub6.Type62Sub2Sub3Sub4Sub5Sub6Sub7.Type62Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(35, 53, 16, 98.23);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6693787192806373363, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType63swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type63.swiftFunc0: ");
                            long result = Type63.swiftFunc0(63, 55, 51, 93, 13, 0, 85, 15, 47);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3926953705818647021, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType63Type63Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type63.Type63Sub2.swiftFunc0: ");
                            long result = Type63.Type63Sub2.swiftFunc0(10, 5, 93, 25);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5961805883595131218, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType63Type63Sub2Type63Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type63.Type63Sub2.Type63Sub2Sub3.swiftFunc0: ");
                            long result = Type63.Type63Sub2.Type63Sub2Sub3.swiftFunc0(9, 47, 9, 64, 31, 32, 2, 46);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3074085374913202105, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType63Type63Sub2Type63Sub2Sub3Type63Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.swiftFunc0(26, 53, 69, 66, 62);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1071541657854385635, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType63Type63Sub2Type63Sub2Sub3Type63Sub2Sub3Sub4Type63Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.Type63Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.Type63Sub2Sub3Sub4Sub5.swiftFunc0(19, 3.92, 20, 10, 95, 6);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5722720525946162898, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType63Type63Sub2Type63Sub2Sub3Type63Sub2Sub3Sub4Type63Sub2Sub3Sub4Sub5Type63Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.Type63Sub2Sub3Sub4Sub5.Type63Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type63.Type63Sub2.Type63Sub2Sub3.Type63Sub2Sub3Sub4.Type63Sub2Sub3Sub4Sub5.Type63Sub2Sub3Sub4Sub5Sub6.swiftFunc0(38, 10);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3855690635967189595, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.swiftFunc0: ");
                            long result = Type64.swiftFunc0(99, 17, 74);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4906832853580219999, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.swiftFunc0: ");
                            long result = Type64.Type64Sub2.swiftFunc0(94, 85.81, 54.20, 79, 53, 63.54);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8957339717014574618, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2Type64Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.Type64Sub2Sub3.swiftFunc0: ");
                            long result = Type64.Type64Sub2.Type64Sub2Sub3.swiftFunc0(8);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6873002678636213555, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2Type64Sub2Sub3Type64Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.swiftFunc0(8, 39, 42, 48, 62);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7700397584519272242, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2Type64Sub2Sub3Type64Sub2Sub3Sub4Type64Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.swiftFunc0(37.04, 93, 19, 25, 0, 81);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6935852189365092809, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2Type64Sub2Sub3Type64Sub2Sub3Sub4Type64Sub2Sub3Sub4Sub5Type64Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.swiftFunc0(89, 50.15, 58);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1189941486758588483, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2Type64Sub2Sub3Type64Sub2Sub3Sub4Type64Sub2Sub3Sub4Sub5Type64Sub2Sub3Sub4Sub5Sub6Type64Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(46, 18, 49, 46);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6798776298972722462, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2Type64Sub2Sub3Type64Sub2Sub3Sub4Type64Sub2Sub3Sub4Sub5Type64Sub2Sub3Sub4Sub5Sub6Type64Sub2Sub3Sub4Sub5Sub6Sub7Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(97);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7576763534199239620, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2Type64Sub2Sub3Type64Sub2Sub3Sub4Type64Sub2Sub3Sub4Sub5Type64Sub2Sub3Sub4Sub5Sub6Type64Sub2Sub3Sub4Sub5Sub6Sub7Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0: ");
                            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(63, 67, 67, 41, 22, 82, 73, 3, 22);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3092344266969342041, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2Type64Sub2Sub3Type64Sub2Sub3Sub4Type64Sub2Sub3Sub4Sub5Type64Sub2Sub3Sub4Sub5Sub6Type64Sub2Sub3Sub4Sub5Sub6Sub7Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0: ");
                            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(26, 67, 98.92, 29, 9, 40);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5296476420954873497, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType64Type64Sub2Type64Sub2Sub3Type64Sub2Sub3Sub4Type64Sub2Sub3Sub4Sub5Type64Sub2Sub3Sub4Sub5Sub6Type64Sub2Sub3Sub4Sub5Sub6Sub7Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0: ");
                            long result = Type64.Type64Sub2.Type64Sub2Sub3.Type64Sub2Sub3Sub4.Type64Sub2Sub3Sub4Sub5.Type64Sub2Sub3Sub4Sub5Sub6.Type64Sub2Sub3Sub4Sub5Sub6Sub7.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11.swiftFunc0(85, 22, 50.88, 61, 79.13, 51);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3879585883908093839, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType65swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type65.swiftFunc0: ");
                            long result = Type65.swiftFunc0(14.48, 16);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4420149166872491203, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType65Type65Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type65.Type65Sub2.swiftFunc0: ");
                            long result = Type65.Type65Sub2.swiftFunc0(62, 60, 70, 32);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7127809210547205315, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType65Type65Sub2Type65Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type65.Type65Sub2.Type65Sub2Sub3.swiftFunc0: ");
                            long result = Type65.Type65Sub2.Type65Sub2Sub3.swiftFunc0(32, 9, 83, 38.46, 94, 60, 24);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1718548579748602589, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType65Type65Sub2Type65Sub2Sub3Type65Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type65.Type65Sub2.Type65Sub2Sub3.Type65Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type65.Type65Sub2.Type65Sub2Sub3.Type65Sub2Sub3Sub4.swiftFunc0(69, 26, 96, 50.91, 51, 52, 77, 46, 84, 54);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6752650262500334937, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType65Type65Sub2Type65Sub2Sub3Type65Sub2Sub3Sub4Type65Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type65.Type65Sub2.Type65Sub2Sub3.Type65Sub2Sub3Sub4.Type65Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type65.Type65Sub2.Type65Sub2Sub3.Type65Sub2Sub3Sub4.Type65Sub2Sub3Sub4Sub5.swiftFunc0(41.85, 13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1669901293133599845, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType66swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type66.swiftFunc0: ");
                            long result = Type66.swiftFunc0(35.67);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5780391959566276186, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType66Type66Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type66.Type66Sub2.swiftFunc0: ");
                            long result = Type66.Type66Sub2.swiftFunc0(84);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2649507594516546671, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType67swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type67.swiftFunc0: ");
                            long result = Type67.swiftFunc0(17, 37, 94, 30, 56);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3843114812536738695, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType67Type67Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type67.Type67Sub2.swiftFunc0: ");
                            long result = Type67.Type67Sub2.swiftFunc0(47);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(564856539678866074, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType67Type67Sub2Type67Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type67.Type67Sub2.Type67Sub2Sub3.swiftFunc0: ");
                            long result = Type67.Type67Sub2.Type67Sub2Sub3.swiftFunc0(82, 60, 21, 72, 43, 58, 65, 5, 31.82);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4930850372708223179, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType67Type67Sub2Type67Sub2Sub3Type67Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.swiftFunc0(54, 84, 27, 33.07, 33, 81, 37);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4846025311995925664, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType67Type67Sub2Type67Sub2Sub3Type67Sub2Sub3Sub4Type67Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.swiftFunc0(16, 98, 6, 82.49, 91);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4834024210082438503, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType67Type67Sub2Type67Sub2Sub3Type67Sub2Sub3Sub4Type67Sub2Sub3Sub4Sub5Type67Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.swiftFunc0(82, 29, 23, 37, 7.37, 95.44, 54, 63.89, 75);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4047060341357433926, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType67Type67Sub2Type67Sub2Sub3Type67Sub2Sub3Sub4Type67Sub2Sub3Sub4Sub5Type67Sub2Sub3Sub4Sub5Sub6Type67Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.Type67Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.Type67Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(92.17, 5, 55.78, 62, 35, 22, 59);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6818112558983887280, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType67Type67Sub2Type67Sub2Sub3Type67Sub2Sub3Sub4Type67Sub2Sub3Sub4Sub5Type67Sub2Sub3Sub4Sub5Sub6Type67Sub2Sub3Sub4Sub5Sub6Sub7Type67Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.Type67Sub2Sub3Sub4Sub5Sub6Sub7.Type67Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type67.Type67Sub2.Type67Sub2Sub3.Type67Sub2Sub3Sub4.Type67Sub2Sub3Sub4Sub5.Type67Sub2Sub3Sub4Sub5Sub6.Type67Sub2Sub3Sub4Sub5Sub6Sub7.Type67Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(70);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(649991725034402779, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType68swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type68.swiftFunc0: ");
                            long result = Type68.swiftFunc0(83, 26, 27, 31, 45);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5970781214029323595, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType68Type68Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type68.Type68Sub2.swiftFunc0: ");
                            long result = Type68.Type68Sub2.swiftFunc0(35, 27, 16, 9, 81, 84);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2941980381236318257, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType68Type68Sub2Type68Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type68.Type68Sub2.Type68Sub2Sub3.swiftFunc0: ");
                            long result = Type68.Type68Sub2.Type68Sub2Sub3.swiftFunc0(15, 47, 77, 51, 98.93);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8914815933085291204, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType68Type68Sub2Type68Sub2Sub3Type68Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type68.Type68Sub2.Type68Sub2Sub3.Type68Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type68.Type68Sub2.Type68Sub2Sub3.Type68Sub2Sub3Sub4.swiftFunc0(15, 97, 28, 78, 22, 74, 52, 72, 27.99, 96);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4858187451163638360, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType69swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type69.swiftFunc0: ");
                            long result = Type69.swiftFunc0(99, 79);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3277769636860925079, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType70swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type70.swiftFunc0: ");
                            long result = Type70.swiftFunc0(43.27, 51, 14, 56, 37);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3902262935105108267, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType70Type70Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type70.Type70Sub2.swiftFunc0: ");
                            long result = Type70.Type70Sub2.swiftFunc0(30, 7, 32.71, 10, 61, 47, 3, 58);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1290948625416397098, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType70Type70Sub2Type70Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type70.Type70Sub2.Type70Sub2Sub3.swiftFunc0: ");
                            long result = Type70.Type70Sub2.Type70Sub2Sub3.swiftFunc0(9, 59.29, 2, 26, 13, 61);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3524064562037935227, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType70Type70Sub2Type70Sub2Sub3Type70Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type70.Type70Sub2.Type70Sub2Sub3.Type70Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type70.Type70Sub2.Type70Sub2Sub3.Type70Sub2Sub3Sub4.swiftFunc0(59, 36.38, 22, 78);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1858690360864303906, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType71swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type71.swiftFunc0: ");
                            long result = Type71.swiftFunc0(60, 11);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-334487821673350542, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType71Type71Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type71.Type71Sub2.swiftFunc0: ");
                            long result = Type71.Type71Sub2.swiftFunc0(13, 72.40, 62, 50, 19, 38, 91, 68, 71);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7266192344694219418, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType71Type71Sub2Type71Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type71.Type71Sub2.Type71Sub2Sub3.swiftFunc0: ");
                            long result = Type71.Type71Sub2.Type71Sub2Sub3.swiftFunc0(89, 47.78);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8973148690506940174, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType71Type71Sub2Type71Sub2Sub3Type71Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type71.Type71Sub2.Type71Sub2Sub3.Type71Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type71.Type71Sub2.Type71Sub2Sub3.Type71Sub2Sub3Sub4.swiftFunc0(14, 83);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5224909108301209218, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType71Type71Sub2Type71Sub2Sub3Type71Sub2Sub3Sub4Type71Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type71.Type71Sub2.Type71Sub2Sub3.Type71Sub2Sub3Sub4.Type71Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type71.Type71Sub2.Type71Sub2Sub3.Type71Sub2Sub3Sub4.Type71Sub2Sub3Sub4Sub5.swiftFunc0(58, 39.99, 42, 66.21, 72, 57);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2602199087732930739, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType72swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type72.swiftFunc0: ");
                            long result = Type72.swiftFunc0(38, 86, 82, 95);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5092613311314256162, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType72Type72Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type72.Type72Sub2.swiftFunc0: ");
                            long result = Type72.Type72Sub2.swiftFunc0(82, 18, 7);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4592629004123936242, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType72Type72Sub2Type72Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type72.Type72Sub2.Type72Sub2Sub3.swiftFunc0: ");
                            long result = Type72.Type72Sub2.Type72Sub2Sub3.swiftFunc0(64, 87, 19, 8, 93, 53, 60.71);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2961133842124433026, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType72Type72Sub2Type72Sub2Sub3Type72Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type72.Type72Sub2.Type72Sub2Sub3.Type72Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type72.Type72Sub2.Type72Sub2Sub3.Type72Sub2Sub3Sub4.swiftFunc0(66, 1, 76);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1678085087134853366, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType72Type72Sub2Type72Sub2Sub3Type72Sub2Sub3Sub4Type72Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type72.Type72Sub2.Type72Sub2Sub3.Type72Sub2Sub3Sub4.Type72Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type72.Type72Sub2.Type72Sub2Sub3.Type72Sub2Sub3Sub4.Type72Sub2Sub3Sub4Sub5.swiftFunc0(27, 96, 81, 21, 1, 49, 91, 16, 91, 76);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1105421971532693998, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType73swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type73.swiftFunc0: ");
                            long result = Type73.swiftFunc0(37, 10, 80, 28, 24, 57.14, 77, 60, 36.12);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2832997306306529749, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType73Type73Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type73.Type73Sub2.swiftFunc0: ");
                            long result = Type73.Type73Sub2.swiftFunc0(51.21, 42, 96);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6969258921184097051, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType73Type73Sub2Type73Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type73.Type73Sub2.Type73Sub2Sub3.swiftFunc0: ");
                            long result = Type73.Type73Sub2.Type73Sub2Sub3.swiftFunc0(14, 75.28, 66);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(279596736138991104, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType74swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type74.swiftFunc0: ");
                            long result = Type74.swiftFunc0(14, 67, 44, 14, 79, 21, 4.74, 64);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5602327263456262086, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType74Type74Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type74.Type74Sub2.swiftFunc0: ");
                            long result = Type74.Type74Sub2.swiftFunc0(87, 9, 60, 49.43, 9);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8597986116169910690, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType74Type74Sub2Type74Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type74.Type74Sub2.Type74Sub2Sub3.swiftFunc0: ");
                            long result = Type74.Type74Sub2.Type74Sub2Sub3.swiftFunc0(20, 58, 54, 57.40, 37, 80, 47, 17.36);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8923789696140165270, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType74Type74Sub2Type74Sub2Sub3Type74Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type74.Type74Sub2.Type74Sub2Sub3.Type74Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type74.Type74Sub2.Type74Sub2Sub3.Type74Sub2Sub3Sub4.swiftFunc0(51);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1336053951289096330, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType75swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type75.swiftFunc0: ");
                            long result = Type75.swiftFunc0(78.00);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-131802630901555874, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType75Type75Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type75.Type75Sub2.swiftFunc0: ");
                            long result = Type75.Type75Sub2.swiftFunc0(42, 84, 39, 9, 80, 81);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6625810836787257284, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType75Type75Sub2Type75Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type75.Type75Sub2.Type75Sub2Sub3.swiftFunc0: ");
                            long result = Type75.Type75Sub2.Type75Sub2Sub3.swiftFunc0(95, 12, 12, 49, 1, 52.30);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5270619647366752580, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType75Type75Sub2Type75Sub2Sub3Type75Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type75.Type75Sub2.Type75Sub2Sub3.Type75Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type75.Type75Sub2.Type75Sub2Sub3.Type75Sub2Sub3Sub4.swiftFunc0(5, 31, 10);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1701163299766814043, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType75Type75Sub2Type75Sub2Sub3Type75Sub2Sub3Sub4Type75Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type75.Type75Sub2.Type75Sub2Sub3.Type75Sub2Sub3Sub4.Type75Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type75.Type75Sub2.Type75Sub2Sub3.Type75Sub2Sub3Sub4.Type75Sub2Sub3Sub4Sub5.swiftFunc0(39.66, 1, 11.14, 3, 75, 77, 21, 27, 89.65);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1550705282006029491, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType76swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type76.swiftFunc0: ");
                            long result = Type76.swiftFunc0(38, 40, 64, 35, 62, 90.14, 3.08, 67);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6681987026482264083, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType76Type76Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type76.Type76Sub2.swiftFunc0: ");
                            long result = Type76.Type76Sub2.swiftFunc0(15, 19);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2033315680351275623, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType76Type76Sub2Type76Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type76.Type76Sub2.Type76Sub2Sub3.swiftFunc0: ");
                            long result = Type76.Type76Sub2.Type76Sub2Sub3.swiftFunc0(0.62, 51);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6973787733211351142, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType76Type76Sub2Type76Sub2Sub3Type76Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.swiftFunc0(36.07, 86, 60, 77, 1.01, 21, 59);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3664947251085737281, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType76Type76Sub2Type76Sub2Sub3Type76Sub2Sub3Sub4Type76Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.Type76Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.Type76Sub2Sub3Sub4Sub5.swiftFunc0(77, 56, 87, 81, 65.87, 28, 43.97, 42, 27, 99);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(415565234096897685, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType76Type76Sub2Type76Sub2Sub3Type76Sub2Sub3Sub4Type76Sub2Sub3Sub4Sub5Type76Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.Type76Sub2Sub3Sub4Sub5.Type76Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type76.Type76Sub2.Type76Sub2Sub3.Type76Sub2Sub3Sub4.Type76Sub2Sub3Sub4Sub5.Type76Sub2Sub3Sub4Sub5Sub6.swiftFunc0(81, 53);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(295801482863592169, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType77swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type77.swiftFunc0: ");
                            long result = Type77.swiftFunc0(77, 68, 64, 18.20, 55, 73);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4847418080359704006, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType78swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type78.swiftFunc0: ");
                            long result = Type78.swiftFunc0(68, 10, 39, 67, 23, 45);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6034347456074523955, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType78Type78Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type78.Type78Sub2.swiftFunc0: ");
                            long result = Type78.Type78Sub2.swiftFunc0(88.78, 66, 63, 68, 49);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5611645301987577034, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType79swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type79.swiftFunc0: ");
                            long result = Type79.swiftFunc0(76, 34.43, 40.60, 37, 100, 3);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1960305953532885815, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType79Type79Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type79.Type79Sub2.swiftFunc0: ");
                            long result = Type79.Type79Sub2.swiftFunc0(8, 29, 79, 11, 20.79, 55, 21.74, 32);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3785633265034196563, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType79Type79Sub2Type79Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type79.Type79Sub2.Type79Sub2Sub3.swiftFunc0: ");
                            long result = Type79.Type79Sub2.Type79Sub2Sub3.swiftFunc0(2.21, 29, 28, 99.63, 82, 76, 27);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6551340915042681677, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType79Type79Sub2Type79Sub2Sub3Type79Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type79.Type79Sub2.Type79Sub2Sub3.Type79Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type79.Type79Sub2.Type79Sub2Sub3.Type79Sub2Sub3Sub4.swiftFunc0(34, 51);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6949495942111874244, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType80swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type80.swiftFunc0: ");
                            long result = Type80.swiftFunc0(23, 79.84, 60, 12, 82, 42, 76);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2439014749661513945, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType80Type80Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type80.Type80Sub2.swiftFunc0: ");
                            long result = Type80.Type80Sub2.swiftFunc0(98, 19.17, 91, 61, 53, 2, 16.52, 34, 63, 48);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8319675326568653235, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType80Type80Sub2Type80Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type80.Type80Sub2.Type80Sub2Sub3.swiftFunc0: ");
                            long result = Type80.Type80Sub2.Type80Sub2Sub3.swiftFunc0(90, 46, 20, 27, 58);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(841649437616840460, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType80Type80Sub2Type80Sub2Sub3Type80Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.swiftFunc0(42, 33, 21, 13, 74, 4.58, 22, 16);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1740739174720849383, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType80Type80Sub2Type80Sub2Sub3Type80Sub2Sub3Sub4Type80Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.Type80Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.Type80Sub2Sub3Sub4Sub5.swiftFunc0(60.26, 87, 4, 33, 60.41, 73, 79, 66, 31, 18);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8488642455115438639, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType80Type80Sub2Type80Sub2Sub3Type80Sub2Sub3Sub4Type80Sub2Sub3Sub4Sub5Type80Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.Type80Sub2Sub3Sub4Sub5.Type80Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type80.Type80Sub2.Type80Sub2Sub3.Type80Sub2Sub3Sub4.Type80Sub2Sub3Sub4Sub5.Type80Sub2Sub3Sub4Sub5Sub6.swiftFunc0(26.41, 57.40, 34.41, 11, 15, 86, 5);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7678767092711717444, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType81swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type81.swiftFunc0: ");
                            long result = Type81.swiftFunc0(85, 4, 21, 23, 9, 13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2534172109392825488, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType81Type81Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type81.Type81Sub2.swiftFunc0: ");
                            long result = Type81.Type81Sub2.swiftFunc0(76.53, 33, 71.34);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5726993050876658753, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType81Type81Sub2Type81Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type81.Type81Sub2.Type81Sub2Sub3.swiftFunc0: ");
                            long result = Type81.Type81Sub2.Type81Sub2Sub3.swiftFunc0(78, 51, 8);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5273716212365502184, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType81Type81Sub2Type81Sub2Sub3Type81Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type81.Type81Sub2.Type81Sub2Sub3.Type81Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type81.Type81Sub2.Type81Sub2Sub3.Type81Sub2Sub3Sub4.swiftFunc0(23.61, 87, 59, 74);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3147555241696120016, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType82swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type82.swiftFunc0: ");
                            long result = Type82.swiftFunc0(88, 47, 95, 72, 72, 54, 23);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4054482155005731604, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType83swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type83.swiftFunc0: ");
                            long result = Type83.swiftFunc0(56, 7, 24, 29, 14.14, 38.88, 98.96, 17, 23);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(641368654376073872, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType83Type83Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type83.Type83Sub2.swiftFunc0: ");
                            long result = Type83.Type83Sub2.swiftFunc0(19.30, 85);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2432570045665555742, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType83Type83Sub2Type83Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type83.Type83Sub2.Type83Sub2Sub3.swiftFunc0: ");
                            long result = Type83.Type83Sub2.Type83Sub2Sub3.swiftFunc0(40.67, 68.63, 46, 70, 45, 22.40, 41, 93);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6569272560158614758, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType83Type83Sub2Type83Sub2Sub3Type83Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type83.Type83Sub2.Type83Sub2Sub3.Type83Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type83.Type83Sub2.Type83Sub2Sub3.Type83Sub2Sub3Sub4.swiftFunc0(38, 79.27, 10, 2, 16);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7068325176092208113, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType84swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type84.swiftFunc0: ");
                            long result = Type84.swiftFunc0(73);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4635659444355057900, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType85swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type85.swiftFunc0: ");
                            long result = Type85.swiftFunc0(25, 36, 87, 25, 5, 41.42, 64, 19, 6);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6276434994091822532, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType85Type85Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type85.Type85Sub2.swiftFunc0: ");
                            long result = Type85.Type85Sub2.swiftFunc0(97, 22, 89);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7178683098914978415, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType85Type85Sub2Type85Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type85.Type85Sub2.Type85Sub2Sub3.swiftFunc0: ");
                            long result = Type85.Type85Sub2.Type85Sub2Sub3.swiftFunc0(17);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(605032694565748564, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType85Type85Sub2Type85Sub2Sub3Type85Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type85.Type85Sub2.Type85Sub2Sub3.Type85Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type85.Type85Sub2.Type85Sub2Sub3.Type85Sub2Sub3Sub4.swiftFunc0(83, 44, 40.86, 79, 60, 20, 40, 30, 59.43, 70);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2766821019775738384, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType86swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type86.swiftFunc0: ");
                            long result = Type86.swiftFunc0(75, 71, 97, 8, 11);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(815233715740760203, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType87swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type87.swiftFunc0: ");
                            long result = Type87.swiftFunc0(75, 10.29, 53.93, 54);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8969823465370983724, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType87Type87Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type87.Type87Sub2.swiftFunc0: ");
                            long result = Type87.Type87Sub2.swiftFunc0(68, 54.92, 66.88, 69, 100, 45, 44, 46, 61, 71.47);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-9169579336628190189, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType87Type87Sub2Type87Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type87.Type87Sub2.Type87Sub2Sub3.swiftFunc0: ");
                            long result = Type87.Type87Sub2.Type87Sub2Sub3.swiftFunc0(22, 10, 31, 68, 73, 0, 91, 100, 9.40, 20);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7250428304757445709, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType87Type87Sub2Type87Sub2Sub3Type87Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.swiftFunc0(40, 25);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5586363182792468972, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType87Type87Sub2Type87Sub2Sub3Type87Sub2Sub3Sub4Type87Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.swiftFunc0(75, 64, 32.55, 7, 62, 93.04);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8193209147041370451, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType87Type87Sub2Type87Sub2Sub3Type87Sub2Sub3Sub4Type87Sub2Sub3Sub4Sub5Type87Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.Type87Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.Type87Sub2Sub3Sub4Sub5Sub6.swiftFunc0(3, 94, 77, 95, 69, 83, 87);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2937495561540670839, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType87Type87Sub2Type87Sub2Sub3Type87Sub2Sub3Sub4Type87Sub2Sub3Sub4Sub5Type87Sub2Sub3Sub4Sub5Sub6Type87Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.Type87Sub2Sub3Sub4Sub5Sub6.Type87Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type87.Type87Sub2.Type87Sub2Sub3.Type87Sub2Sub3Sub4.Type87Sub2Sub3Sub4Sub5.Type87Sub2Sub3Sub4Sub5Sub6.Type87Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(92, 46, 85, 75, 4, 69.45, 11, 91, 13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6860674082066421230, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType88swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type88.swiftFunc0: ");
                            long result = Type88.swiftFunc0(8, 45, 8, 13, 6);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5260672631800259017, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType88Type88Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type88.Type88Sub2.swiftFunc0: ");
                            long result = Type88.Type88Sub2.swiftFunc0(99, 51, 0.85, 97, 67, 49, 50);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-8810323782677945282, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType88Type88Sub2Type88Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type88.Type88Sub2.Type88Sub2Sub3.swiftFunc0: ");
                            long result = Type88.Type88Sub2.Type88Sub2Sub3.swiftFunc0(0, 46.66, 68, 45, 21, 22);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7727777822775495554, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType88Type88Sub2Type88Sub2Sub3Type88Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type88.Type88Sub2.Type88Sub2Sub3.Type88Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type88.Type88Sub2.Type88Sub2Sub3.Type88Sub2Sub3Sub4.swiftFunc0(46, 65, 26, 79, 70, 35, 100, 15.84, 85);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6915587708849107502, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType88Type88Sub2Type88Sub2Sub3Type88Sub2Sub3Sub4Type88Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type88.Type88Sub2.Type88Sub2Sub3.Type88Sub2Sub3Sub4.Type88Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type88.Type88Sub2.Type88Sub2Sub3.Type88Sub2Sub3Sub4.Type88Sub2Sub3Sub4Sub5.swiftFunc0(47, 23.27, 24, 96, 50.05, 51);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4966739196006501207, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType89swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type89.swiftFunc0: ");
                            long result = Type89.swiftFunc0(61.93, 81);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8654043253704010916, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType89Type89Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type89.Type89Sub2.swiftFunc0: ");
                            long result = Type89.Type89Sub2.swiftFunc0(10, 24, 82, 38, 18.04, 14);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6519717980839005112, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType89Type89Sub2Type89Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type89.Type89Sub2.Type89Sub2Sub3.swiftFunc0: ");
                            long result = Type89.Type89Sub2.Type89Sub2Sub3.swiftFunc0(89);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3459217808417385212, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType90swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type90.swiftFunc0: ");
                            long result = Type90.swiftFunc0(84, 6, 87, 84, 32, 9, 67);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4575982977251622046, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType90Type90Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type90.Type90Sub2.swiftFunc0: ");
                            long result = Type90.Type90Sub2.swiftFunc0(60, 49, 59, 33, 30, 5.32, 47, 77, 73);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5578769012424498626, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType90Type90Sub2Type90Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type90.Type90Sub2.Type90Sub2Sub3.swiftFunc0: ");
                            long result = Type90.Type90Sub2.Type90Sub2Sub3.swiftFunc0(42, 16, 34.91, 78, 38, 44, 36.79, 9.77, 56);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6966325052094053556, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType90Type90Sub2Type90Sub2Sub3Type90Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type90.Type90Sub2.Type90Sub2Sub3.Type90Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type90.Type90Sub2.Type90Sub2Sub3.Type90Sub2Sub3Sub4.swiftFunc0(4.83, 8, 96);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-509761177789724739, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType90Type90Sub2Type90Sub2Sub3Type90Sub2Sub3Sub4Type90Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type90.Type90Sub2.Type90Sub2Sub3.Type90Sub2Sub3Sub4.Type90Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type90.Type90Sub2.Type90Sub2Sub3.Type90Sub2Sub3Sub4.Type90Sub2Sub3Sub4Sub5.swiftFunc0(100, 11, 55, 24.50, 58);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5018197425982911939, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType91swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type91.swiftFunc0: ");
                            long result = Type91.swiftFunc0(84, 27, 72);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6260927431643092310, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType91Type91Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type91.Type91Sub2.swiftFunc0: ");
                            long result = Type91.Type91Sub2.swiftFunc0(63, 76.01, 62, 36);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4247982083591395051, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType91Type91Sub2Type91Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type91.Type91Sub2.Type91Sub2Sub3.swiftFunc0: ");
                            long result = Type91.Type91Sub2.Type91Sub2Sub3.swiftFunc0(85, 74, 57.61, 8, 4, 100, 42);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6527952547738705482, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType91Type91Sub2Type91Sub2Sub3Type91Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type91.Type91Sub2.Type91Sub2Sub3.Type91Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type91.Type91Sub2.Type91Sub2Sub3.Type91Sub2Sub3Sub4.swiftFunc0(74, 28, 76);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7985770896057025413, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType91Type91Sub2Type91Sub2Sub3Type91Sub2Sub3Sub4Type91Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type91.Type91Sub2.Type91Sub2Sub3.Type91Sub2Sub3Sub4.Type91Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type91.Type91Sub2.Type91Sub2Sub3.Type91Sub2Sub3Sub4.Type91Sub2Sub3Sub4Sub5.swiftFunc0(60);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3820921403140653113, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType92swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type92.swiftFunc0: ");
                            long result = Type92.swiftFunc0(4.85, 26, 47, 80, 41, 33, 58, 91);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3284314349373785262, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType92Type92Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type92.Type92Sub2.swiftFunc0: ");
                            long result = Type92.Type92Sub2.swiftFunc0(28, 52, 26, 67, 1.38, 49, 85.60, 26);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8769838448971532803, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType92Type92Sub2Type92Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type92.Type92Sub2.Type92Sub2Sub3.swiftFunc0: ");
                            long result = Type92.Type92Sub2.Type92Sub2Sub3.swiftFunc0(14, 77);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(180381667452574854, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType92Type92Sub2Type92Sub2Sub3Type92Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.swiftFunc0(68, 70, 7, 3, 31, 60);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3860903177905724486, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType92Type92Sub2Type92Sub2Sub3Type92Sub2Sub3Sub4Type92Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.Type92Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.Type92Sub2Sub3Sub4Sub5.swiftFunc0(34, 46, 33, 55, 47, 80, 12, 18, 51.72);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4680556141452481621, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType92Type92Sub2Type92Sub2Sub3Type92Sub2Sub3Sub4Type92Sub2Sub3Sub4Sub5Type92Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.Type92Sub2Sub3Sub4Sub5.Type92Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type92.Type92Sub2.Type92Sub2Sub3.Type92Sub2Sub3Sub4.Type92Sub2Sub3Sub4Sub5.Type92Sub2Sub3Sub4Sub5Sub6.swiftFunc0(98, 99, 56, 40, 62, 23.66, 57);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8918745350502716139, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType93swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type93.swiftFunc0: ");
                            long result = Type93.swiftFunc0(11, 9, 66, 12.84, 44, 100, 76, 45, 96);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7440258612429021759, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType93Type93Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type93.Type93Sub2.swiftFunc0: ");
                            long result = Type93.Type93Sub2.swiftFunc0(94.75, 21, 37, 91, 56, 34, 55.01, 65, 8);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7783235671149717910, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType93Type93Sub2Type93Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type93.Type93Sub2.Type93Sub2Sub3.swiftFunc0: ");
                            long result = Type93.Type93Sub2.Type93Sub2Sub3.swiftFunc0(94, 27, 0, 52);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-1228974309994701654, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType94swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type94.swiftFunc0: ");
                            long result = Type94.swiftFunc0(87.31);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(648102651988741497, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType94Type94Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type94.Type94Sub2.swiftFunc0: ");
                            long result = Type94.Type94Sub2.swiftFunc0(35);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5808625042874858702, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType94Type94Sub2Type94Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type94.Type94Sub2.Type94Sub2Sub3.swiftFunc0: ");
                            long result = Type94.Type94Sub2.Type94Sub2Sub3.swiftFunc0(79.14, 23, 68.74, 40.88, 31.98, 47);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(535508779590666133, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType94Type94Sub2Type94Sub2Sub3Type94Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type94.Type94Sub2.Type94Sub2Sub3.Type94Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type94.Type94Sub2.Type94Sub2Sub3.Type94Sub2Sub3Sub4.swiftFunc0(21, 67, 38, 78, 10, 59, 41.43, 86.39, 30, 54);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6840370006887115940, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType94Type94Sub2Type94Sub2Sub3Type94Sub2Sub3Sub4Type94Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type94.Type94Sub2.Type94Sub2Sub3.Type94Sub2Sub3Sub4.Type94Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type94.Type94Sub2.Type94Sub2Sub3.Type94Sub2Sub3Sub4.Type94Sub2Sub3Sub4Sub5.swiftFunc0(94);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(818319554451827883, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType95swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type95.swiftFunc0: ");
                            long result = Type95.swiftFunc0(39, 30, 14.68, 76, 57, 45, 68);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3016789913300536077, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType95Type95Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type95.Type95Sub2.swiftFunc0: ");
                            long result = Type95.Type95Sub2.swiftFunc0(6, 87, 17, 64);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-9210708301318953955, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType95Type95Sub2Type95Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type95.Type95Sub2.Type95Sub2Sub3.swiftFunc0: ");
                            long result = Type95.Type95Sub2.Type95Sub2Sub3.swiftFunc0(60, 46);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3232107605133056497, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType95Type95Sub2Type95Sub2Sub3Type95Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.swiftFunc0(25, 38, 29, 5);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5500828122832595486, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType95Type95Sub2Type95Sub2Sub3Type95Sub2Sub3Sub4Type95Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.Type95Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.Type95Sub2Sub3Sub4Sub5.swiftFunc0(98, 23, 59, 31);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-403789833093848052, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType95Type95Sub2Type95Sub2Sub3Type95Sub2Sub3Sub4Type95Sub2Sub3Sub4Sub5Type95Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.Type95Sub2Sub3Sub4Sub5.Type95Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type95.Type95Sub2.Type95Sub2Sub3.Type95Sub2Sub3Sub4.Type95Sub2Sub3Sub4Sub5.Type95Sub2Sub3Sub4Sub5Sub6.swiftFunc0(31, 84, 49, 85, 84, 19, 60, 89, 3);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7442437356528311699, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType96swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type96.swiftFunc0: ");
                            long result = Type96.swiftFunc0(98, 7, 11, 85.08, 20);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(1691835474197242426, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType96Type96Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type96.Type96Sub2.swiftFunc0: ");
                            long result = Type96.Type96Sub2.swiftFunc0(17, 52, 9, 32, 4, 87, 74);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4901368629152969742, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType96Type96Sub2Type96Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type96.Type96Sub2.Type96Sub2Sub3.swiftFunc0: ");
                            long result = Type96.Type96Sub2.Type96Sub2Sub3.swiftFunc0(68, 62, 4, 33.70, 30, 97, 50, 34, 39, 60);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3636991592063641174, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType96Type96Sub2Type96Sub2Sub3Type96Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.swiftFunc0(52, 71.85, 96, 82, 61, 41, 20, 33);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(5397481060424965145, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType96Type96Sub2Type96Sub2Sub3Type96Sub2Sub3Sub4Type96Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.swiftFunc0(39, 37, 42, 0, 88, 28, 67, 82.35, 78);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-605185230668150482, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType96Type96Sub2Type96Sub2Sub3Type96Sub2Sub3Sub4Type96Sub2Sub3Sub4Sub5Type96Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.swiftFunc0(4.44);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(2860442675980140193, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType96Type96Sub2Type96Sub2Sub3Type96Sub2Sub3Sub4Type96Sub2Sub3Sub4Sub5Type96Sub2Sub3Sub4Sub5Sub6Type96Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(9, 82, 61, 86.59, 38, 20, 62, 34, 41, 86);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-2409143221206391557, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType96Type96Sub2Type96Sub2Sub3Type96Sub2Sub3Sub4Type96Sub2Sub3Sub4Sub5Type96Sub2Sub3Sub4Sub5Sub6Type96Sub2Sub3Sub4Sub5Sub6Sub7Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(72, 2, 18, 70);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(598478454465173457, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType96Type96Sub2Type96Sub2Sub3Type96Sub2Sub3Sub4Type96Sub2Sub3Sub4Sub5Type96Sub2Sub3Sub4Sub5Sub6Type96Sub2Sub3Sub4Sub5Sub6Sub7Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0: ");
                            long result = Type96.Type96Sub2.Type96Sub2Sub3.Type96Sub2Sub3Sub4.Type96Sub2Sub3Sub4Sub5.Type96Sub2Sub3Sub4Sub5Sub6.Type96Sub2Sub3Sub4Sub5Sub6Sub7.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(76, 8);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-750802910854975063, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType97swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type97.swiftFunc0: ");
                            long result = Type97.swiftFunc0(23, 23.77, 13.26);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7779067967876068949, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType98swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type98.swiftFunc0: ");
                            long result = Type98.swiftFunc0(94, 14, 94, 24, 46.36, 83, 40, 63, 38);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6740833353374069276, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType98Type98Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type98.Type98Sub2.swiftFunc0: ");
                            long result = Type98.Type98Sub2.swiftFunc0(70, 83, 12, 72.32, 51, 40, 56);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4277300291582903233, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType99swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type99.swiftFunc0: ");
                            long result = Type99.swiftFunc0(73);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(4635659444355057900, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType99Type99Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type99.Type99Sub2.swiftFunc0: ");
                            long result = Type99.Type99Sub2.swiftFunc0(33, 83);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7711992276714047289, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType99Type99Sub2Type99Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type99.Type99Sub2.Type99Sub2Sub3.swiftFunc0: ");
                            long result = Type99.Type99Sub2.Type99Sub2Sub3.swiftFunc0(29, 54, 73, 22, 39, 36.33, 83);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(7255994502266463888, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType99Type99Sub2Type99Sub2Sub3Type99Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.swiftFunc0(44, 70, 56, 62, 0, 86.28, 8.53, 10, 52.86);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(3471684984972755719, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType99Type99Sub2Type99Sub2Sub3Type99Sub2Sub3Sub4Type99Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.Type99Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.Type99Sub2Sub3Sub4Sub5.swiftFunc0(94, 23, 73.47, 60.11, 46, 55);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7518657982614324457, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType99Type99Sub2Type99Sub2Sub3Type99Sub2Sub3Sub4Type99Sub2Sub3Sub4Sub5Type99Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.Type99Sub2Sub3Sub4Sub5.Type99Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type99.Type99Sub2.Type99Sub2Sub3.Type99Sub2Sub3Sub4.Type99Sub2Sub3Sub4Sub5.Type99Sub2Sub3Sub4Sub5Sub6.swiftFunc0(89, 12, 37, 7, 8, 54, 62);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4745752112673179602, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.swiftFunc0: ");
                            long result = Type100.swiftFunc0(0);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(590684067820433389, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100Type100Sub2swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.Type100Sub2.swiftFunc0: ");
                            long result = Type100.Type100Sub2.swiftFunc0(71, 64, 55, 41, 54, 49, 67, 26.89, 59, 35);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-5657265487867349989, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100Type100Sub2Type100Sub2Sub3swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.Type100Sub2.Type100Sub2Sub3.swiftFunc0: ");
                            long result = Type100.Type100Sub2.Type100Sub2Sub3.swiftFunc0(23.65, 28, 40, 48, 61.95, 70, 64, 61, 68.06);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(8722873399830605424, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100Type100Sub2Type100Sub2Sub3Type100Sub2Sub3Sub4swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.swiftFunc0: ");
                            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.swiftFunc0(85.23, 98, 77);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-4371179691031244903, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100Type100Sub2Type100Sub2Sub3Type100Sub2Sub3Sub4Type100Sub2Sub3Sub4Sub5swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.swiftFunc0: ");
                            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.swiftFunc0(48, 79, 19, 59.51, 24, 15, 49, 32);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-3794336876723658367, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100Type100Sub2Type100Sub2Sub3Type100Sub2Sub3Sub4Type100Sub2Sub3Sub4Sub5Type100Sub2Sub3Sub4Sub5Sub6swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.swiftFunc0: ");
                            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.swiftFunc0(32, 38);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(322182842193855259, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100Type100Sub2Type100Sub2Sub3Type100Sub2Sub3Sub4Type100Sub2Sub3Sub4Sub5Type100Sub2Sub3Sub4Sub5Sub6Type100Sub2Sub3Sub4Sub5Sub6Sub7swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0: ");
                            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.swiftFunc0(4, 51, 18, 26.07);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(6237885446262797985, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100Type100Sub2Type100Sub2Sub3Type100Sub2Sub3Sub4Type100Sub2Sub3Sub4Sub5Type100Sub2Sub3Sub4Sub5Sub6Type100Sub2Sub3Sub4Sub5Sub6Sub7Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0: ");
                            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.swiftFunc0(65, 0, 36, 13, 100, 4, 27, 13);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-622298591155045021, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100Type100Sub2Type100Sub2Sub3Type100Sub2Sub3Sub4Type100Sub2Sub3Sub4Sub5Type100Sub2Sub3Sub4Sub5Sub6Type100Sub2Sub3Sub4Sub5Sub6Sub7Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0: ");
                            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.swiftFunc0(42.21, 52, 67, 100);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-6521523735227058600, result);
            Console.WriteLine("OK");
        }
        
        [Fact]
        public static void TestType100Type100Sub2Type100Sub2Sub3Type100Sub2Sub3Sub4Type100Sub2Sub3Sub4Sub5Type100Sub2Sub3Sub4Sub5Sub6Type100Sub2Sub3Sub4Sub5Sub6Sub7Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10swiftFunc0()
        {
            BindingsGenerator.GenerateBindings("StaticMethods/StaticMethodsTests.abi.json", "StaticMethods/");
            var sourceCode = """
                using System;
                using Swift.StaticMethodsTests;

                namespace Test {
                    public class MainClass {
                        public static int Main(string[] args)
                        {
                            return 0;
                        }
                        public static long getResult()
                        {
                            Console.Write("Running Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0: ");
                            long result = Type100.Type100Sub2.Type100Sub2Sub3.Type100Sub2Sub3Sub4.Type100Sub2Sub3Sub4Sub5.Type100Sub2Sub3Sub4Sub5Sub6.Type100Sub2Sub3Sub4Sub5Sub6Sub7.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9.Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10.swiftFunc0(6, 1.90, 24.81, 82, 36.35, 64, 63, 69, 87, 23);
                            return result;
                        }
                    }
                }
                """;

            long result = (long)TestsHelper.CompileAndExecute(
                new string [] { "StaticMethods/*.cs" },
                new string [] { sourceCode },
                new string [] { },
                "Test.MainClass", "getResult", new object[] { });
            Assert.Equal(-7180632988021255058, result);
            Console.WriteLine("OK");
        }
    }
}
