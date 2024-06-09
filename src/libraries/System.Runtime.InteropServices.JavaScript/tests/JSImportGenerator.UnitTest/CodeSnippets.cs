﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace JSImportGenerator.Unit.Tests
{
    internal static class CodeSnippets
    {
        public static readonly string PublicMainInPublicClass_Void = """
            //PublicMainInPublicClass_Void
            public static class Program { public static void Main() { } }
            """;

        public static readonly string PublicMainInPublicClass_Void_Args = """
            //PublicMainInPublicClass_Void_Args
            public static class Program { public static void Main(string[] args) { } }
            """;

        public static readonly string PublicMainInPublicClass_Int = """
            //PublicMainInPublicClass_Int
            public static class Program { public static int Main() => 0; }
            """;

        public static readonly string PublicMainInPublicClass_Int_Args = """
            //PublicMainInPublicClass_Int_Args
            public static class Program { public static int Main(string[] args) => 0; }
            """;

        public static readonly string PublicMainInPublicClass_TaskOfVoid = """
            //PublicMainInPublicClass_TaskOfVoid
            using System.Threading.Tasks;
            public static class Program { public async static Task Main() { await Task.Yield(); } }
            """;

        public static readonly string PublicMainInPublicClass_TaskOfVoid_Args = """
            //PublicMainInPublicClass_TaskOfVoid_Args
            using System.Threading.Tasks;
            public static class Program { public async static Task Main(string[] args) { await Task.Yield(); } }
            """;

        public static readonly string PublicMainInPublicClass_TaskOfInt = """
            //PublicMainInPublicClass_TaskOfInt
            using System.Threading.Tasks;
            public static class Program
            {
                public async static Task<int> Main()
                {
                    await Task.Yield();
                    return 0;
                }
            }
            """;

        public static readonly string PublicMainInPublicClass_TaskOfInt_Args = """
            //PublicMainInPublicClass_TaskOfInt_Args
            using System.Threading.Tasks;
            public static class Program
            {
                public async static Task<int> Main(string[] args)
                {
                    await Task.Yield();
                    return 0;
                }
            }
            """;

        public static readonly string PrivateMainInPublicClass_Void = """
            //PrivateMainInPublicClass_Void
            public static partial class Program { private static void Main() { } }
            """;

        public static readonly string PrivateMainInPublicClass_Void_Args = """
            //PrivateMainInPublicClass_Void_Args
            public static partial class Program { private static void Main(string[] args) { } }
            """;

        public static readonly string PrivateMainInPublicClass_Int = """
            //PrivateMainInPublicClass_Int
            public static partial class Program { private static int Main() => 0; }
            """;

        public static readonly string PrivateMainInPublicClass_Int_Args = """
            //PrivateMainInPublicClass_Int_Args
            public static partial class Program { private static int Main(string[] args) => 0; }
            """;

        public static readonly string PrivateMainInPublicClass_TaskOfVoid = """
            //PrivateMainInPublicClass_TaskOfVoid
            using System.Threading.Tasks;
            public static partial class Program { private async static Task Main() { await Task.Yield(); } }
            """;

        public static readonly string PrivateMainInPublicClass_TaskOfVoid_Args = """
            //PrivateMainInPublicClass_TaskOfVoid_Args
            using System.Threading.Tasks;
            public static partial class Program { private async static Task Main(string[] args) { await Task.Yield(); } }
            """;

        public static readonly string PrivateMainInPublicClass_TaskOfInt = """
            //PrivateMainInPublicClass_TaskOfInt
            using System.Threading.Tasks;
            public static partial class Program
            {
                private async static Task<int> Main()
                {
                    await Task.Yield();
                    return 0;
                }
            }
            """;

        public static readonly string PrivateMainInPublicClass_TaskOfInt_Args = """
            //PrivateMainInPublicClass_TaskOfInt_Args
            using System.Threading.Tasks;
            public static partial class Program
            {
                private async static Task<int> Main(string[] args)
                {
                    await Task.Yield();
                    return 0;
                }
            }
            """;

        public static readonly string PrivateMainInPublicClassInNamespace = """
            //PrivateMainInPublicClassInNamespace
            namespace Test
            {
                public static partial class Program
                {
                    private static void Main(string[] args) { }
                }
            }
            """;

        public static readonly string TopLevelMain = """
            //TopLevelMain
            System.Console.WriteLine("Entry point called");
            """;

        public static readonly string TopLevelAsyncMain = """
            //TopLevelAsyncMain
            using System.Threading.Tasks;
            await Task.Yield();
            """;

        public static readonly string MainInFileClass = """
            //MainInFileClass
            namespace Test
            {
                file class Program { public static void Main() { } }
            }
            """;

        public static readonly string MainInNestedClass = """
            //MainInNestedClass
            namespace Test
            {
                public class Outer
                {
                    public class Inner
                    {
                        public static void Main() { }
                    }
                }
            }
            """;

        public static readonly string PrivateMainInStaticClassInNamespace = """
            //PrivateMainInStaticClass
            namespace Test
            {
                public static class Program
                {
                    private static void Main() { }
                }
            }
            """;

        public static readonly string AllDefault = """
            //AllDefault
            using System;
            using System.Runtime.InteropServices.JavaScript;
            using System.Threading.Tasks;
            partial class Basic
            {
                [JSImport("DoesNotExist")]
                internal static partial void Relaxed(
                    string a1,
                    Exception ex,
                    bool ab, double a6, byte a2, char a3, short a4, float a5, IntPtr a7,
                    bool? nab, double? na6, byte? na2, char? na3, short? na4, float? na5, IntPtr? na7,
                    Task<string> ta1,
                    Task<Exception> tex,
                    Task<bool> tab,
                    Task<double> ta6,
                    Task<byte> ta2,
                    Task<char> ta3,
                    Task<short> ta4,
                    Task<float> ta5,
                    Task<IntPtr> ta7,
                    JSObject jso,
                    string[] aa1, byte[] aab, double[] aad, int[] aai
                );
            }
            """;

        public static readonly string AllAnnotated = """
            //AllAnnotated
            using System;
            using System.Runtime.InteropServices.JavaScript;
            using System.Threading.Tasks;
            partial class Basic
            {
                [JSImport("DoesNotExist")]
                internal static partial void Annotated(
                    [JSMarshalAs<JSType.Any>] object a1,
                    [JSMarshalAs<JSType.Number>] long a2,
                    [JSMarshalAs<JSType.BigInt>] long a3,
                    [JSMarshalAs<JSType.Function>] Action a4,
                    [JSMarshalAs<JSType.Function<JSType.Number>>] Func<int> a5,
                    [JSMarshalAs<JSType.MemoryView>] Span<byte> a6,
                    [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> a7,
                    [JSMarshalAs<JSType.Promise<JSType.Any>>] Task<object> a8,
                    [JSMarshalAs<JSType.Array<JSType.Any>>] object[] a9,
                    [JSMarshalAs<JSType.Date>] DateTime a10,
                    [JSMarshalAs<JSType.Date>] DateTimeOffset a11,
                    [JSMarshalAs<JSType.Promise<JSType.Date>>] Task<DateTime> a12,
                    [JSMarshalAs<JSType.Promise<JSType.Date>>] Task<DateTimeOffset> a13,
                    [JSMarshalAs<JSType.Promise<JSType.Number>>] Task<long> a14,
                    [JSMarshalAs<JSType.Promise<JSType.BigInt>>] Task<long> a15
                );
            }
            """;

        public static readonly string AllMissing = """
            //AllMissing
            using System;
            using System.Runtime.InteropServices.JavaScript;
            using System.Threading.Tasks;
            partial class Basic
            {
                [JSImport("DoesNotExist")]
                internal static partial void Missing(
                    object a1,
                    long a2,
                    long a3,
                    Action a4,
                    Func<int> a5,
                    Span<byte> a6,
                    ArraySegment<byte> a7,
                    Task<object> a8,
                    object[] a9,
                    DateTime a10,
                    DateTimeOffset a11,
                    Task<DateTime> a12,
                    Task<DateTimeOffset> a13,
                    Task<long> a14,
                    Task<long> a15
                );
            }
            """;

        public static readonly string InOutRef = """
            //InOutRef
            using System;
            using System.Runtime.InteropServices.JavaScript;
            using System.Threading.Tasks;
            partial class Basic
            {
                [JSImport("DoesNotExist")]
                internal static partial void InOutRef(
                    out int a1,
                    in int a2,
                    ref int a3
                );
            }
            """;

        public static readonly string AllUnsupported = """
            //AllUnsupported
            using System;
            using System.Runtime.InteropServices.JavaScript;
            using System.Threading.Tasks;
            partial class Basic
            {
                [JSImport("DoesNotExist")]
                internal static partial void Missing(
                    Func<Action> a1,
                    Func<int,int,int,int,int> a2,
                    Span<char> a3,
                    ArraySegment<char> a4,
                    Task<object[]> a5,
                    ulong a6,
                    sbyte a7,
                    ushort a8,
                    uint a9
                );
            }
            """;


        public static readonly string TrivialClassDeclarations = """
            //TrivialClassDeclarations
            using System.Runtime.InteropServices.JavaScript;
            partial class Basic
            {
                [JSImportAttribute("DoesNotExist")]
                public static partial void Import1();

                [JSImport("DoesNotExist")]
                public static partial void Import2();

                [System.Runtime.InteropServices.JavaScript.JSImportAttribute("DoesNotExist")]
                public static partial void Import3();

                [System.Runtime.InteropServices.JavaScript.JSImport("DoesNotExist")]
                public static partial void Import4();

                [JSExportAttribute()]
                public static void Export1(){}

                [JSExport()]
                public static void Export2(){}

                [System.Runtime.InteropServices.JavaScript.JSExportAttribute]
                public static void Export3(){}

                [System.Runtime.InteropServices.JavaScript.JSExport]
                public static void Export4(){}

            }
            """;
        public static string DefaultReturnMarshaler<T>() => DefaultReturnMarshaler(typeof(T).ToString());

        public static string DefaultReturnMarshaler(string type) => $$"""
            //DefaultReturnMarshaler<{{type}}>
            using System.Runtime.InteropServices.JavaScript;
            partial class Basic
            {
                [JSImport("DoesNotExist")]
                public static partial {{type}} Import1();
            
                [JSExport()]
                public static {{type}} Export1(){ throw null; }
            }
            """;

    }
}
