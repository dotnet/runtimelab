using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Interop.ManualTypeMarshallingAnalyzer;

using VerifyCS = DllImportGenerator.Test.Verifiers.CSharpAnalyzerVerifier<Microsoft.Interop.ManualTypeMarshallingAnalyzer>;

namespace DllImportGenerator.Test
{
    public class ManualTypeMarshallingAnalyzerTests
    {
        public static IEnumerable<object[]> NonBlittableTypeMarkedBlittableReportsDiagnosticTestData {
            get
            {
                yield return new object[]
                {
                    @"
using System.Runtime.InteropServices;

[BlittableType]
struct S
{
    public bool field;
}
"
                };
                yield return new object[]
                {
                    
@"
using System.Runtime.InteropServices;

[BlittableType]
struct S
{
    public string field;
}
"
                };
            }
        }

        [MemberData(nameof(NonBlittableTypeMarkedBlittableReportsDiagnosticTestData))]
        [Theory]
        public async Task NonBlittableTypeMarkedBlittableReportsDiagnostic(string source)
        {
            var diagnostic = VerifyCS.Diagnostic(BlittableTypeMustBeBlittableRule).WithSpan(4, 2, 4, 15).WithArguments("S");
            await VerifyCS.VerifyAnalyzerAsync(source, diagnostic);
        }

        [Fact]
        public async Task TypeWithBlittablePrimitiveFieldsMarkedBlittableNoDiagnostic()
        {

            string source = @"
using System.Runtime.InteropServices;

[BlittableType]
struct S
{
    public int field;
}
";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task TypeWithBlittableStructFieldsMarkedBlittableNoDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

[BlittableType]
struct S
{
    public T field;
}

[BlittableType]
struct T
{
    public int field;
}
";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task TypeMarkedBlittableWithNonBlittableFieldsMarkedBlittableReportDiagnosticOnFieldTypeDefinition()
        {
            string source = @"
using System.Runtime.InteropServices;

[BlittableType]
struct S
{
    public T field;
}

[BlittableType]
struct T
{
    public bool field;
}
";
            var diagnostic = VerifyCS.Diagnostic(BlittableTypeMustBeBlittableRule).WithSpan(10, 2, 10, 15).WithArguments("T");
            await VerifyCS.VerifyAnalyzerAsync(source, diagnostic);
        }

        [Fact]
        public async Task NonUnmanagedTypeMarkedBlittableReportsDiagnosticOnStructType()
        {
            string source = @"
using System.Runtime.InteropServices;

[BlittableType]
struct S
{
    public T field;
}

[BlittableType]
struct T
{
    public string field;
}
";
            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(BlittableTypeMustBeBlittableRule).WithSpan(4, 2, 4, 15).WithArguments("S"),
                VerifyCS.Diagnostic(BlittableTypeMustBeBlittableRule).WithSpan(10, 2, 10, 15).WithArguments("T"));
        }

        [Fact]
        public async Task BlittableTypeWithNonBlittableStaticFieldDoesNotReportDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

[BlittableType]
struct S
{
    public static string Static;
    public int instance;
}
";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NullNativeTypeReportsDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

[NativeMarshalling(null)]
struct S
{
    public string s;
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustBeNonNullRule).WithSpan(4, 2, 4, 25).WithArguments("S"));
        }

        [Fact]
        public async Task NonNamedNativeTypeReportsDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(int*))]
struct S
{
    public string s;
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustHaveRequiredShapeRule).WithSpan(4, 2, 4, 33).WithArguments("int*", "S"));
        }

        [Fact]
        public async Task NonBlittableNativeTypeReportsDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
struct S
{
    public string s;
}

struct Native
{
    private string value;

    public Native(S s)
    {
        value = s.s;
    }

    public S ToManaged() => new S { s = value };
}";
            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustBeBlittableRule).WithSpan(10, 1, 20, 2).WithArguments("Native", "S"));
        }

        [Fact]
        public async Task ClassNativeTypeReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
struct S
{
    public string s;
}

class Native
{
    private IntPtr value;

    public Native(S s)
    {
    }

    public S ToManaged() => new S();
}";
            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustBeBlittableRule).WithSpan(11, 1, 20, 2).WithArguments("Native", "S"));
        }

        [Fact]
        public async Task BlittableNativeTypeDoesNotReportDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
struct S
{
    public string s;
}

[BlittableType]
struct Native
{
    private IntPtr value;

    public Native(S s)
    {
        value = IntPtr.Zero;
    }

    public S ToManaged() => new S();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task BlittableNativeWithNonBlittableValuePropertyReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
struct S
{
    public string s;
}

[BlittableType]
struct Native
{
    private IntPtr value;

    public Native(S s)
    {
        value = IntPtr.Zero;
    }

    public S ToManaged() => new S();

    public string Value { get => null; set {} }
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustBeBlittableRule).WithSpan(23, 5, 23, 48).WithArguments("string", "S"));
        }

        [Fact]
        public async Task NonBlittableNativeTypeWithBlittableValuePropertyDoesNotReportDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
struct S
{
    public string s;
}

struct Native
{
    private string value;

    public Native(S s)
    {
        value = s.s;
    }

    public S ToManaged() => new S() { s = value };

    public IntPtr Value { get => IntPtr.Zero; set {} }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task ClassNativeTypeWithValuePropertyReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
struct S
{
    public string s;
}

class Native
{
    private string value;

    public Native(S s)
    {
        value = s.s;
    }

    public S ToManaged() => new S() { s = value };

    public IntPtr Value { get => IntPtr.Zero; set {} }
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustHaveRequiredShapeRule).WithSpan(11, 1, 23, 2).WithArguments("Native", "S"));
        }

        [Fact]
        public async Task NonBlittableGetPinnableReferenceReturnTypeReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public char c;

    public ref char GetPinnableReference() => ref c;
}

unsafe struct Native
{
    private IntPtr value;

    public Native(S s)
    {
        value = IntPtr.Zero;
    }

    public S ToManaged() => new S();

    public IntPtr Value { get => IntPtr.Zero; set {} }
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(GetPinnableReferenceReturnTypeBlittableRule).WithSpan(10, 5, 10, 53));
        }

        
        [Fact]
        public async Task BlittableGetPinnableReferenceReturnTypeDoesNotReportDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public byte c;

    public ref byte GetPinnableReference() => ref c;
}

unsafe struct Native
{
    private IntPtr value;

    public Native(S s) : this()
    {
        value = IntPtr.Zero;
    }

    public S ToManaged() => new S();

    public IntPtr Value { get => IntPtr.Zero; set {} }
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }
        
        [Fact]
        public async Task TypeWithGetPinnableReferenceNonPointerReturnTypeReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public byte c;

    public ref byte GetPinnableReference() => ref c;
}

unsafe struct Native
{
    private IntPtr value;

    public Native(S s) : this()
    {
        value = IntPtr.Zero;
    }

    public S ToManaged() => new S();

    public int Value { get => 0; set {} }
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustBePointerSizedRule).WithSpan(24, 5, 24, 42).WithArguments("int", "S"));
        }

        [Fact]
        public async Task BlittableValueTypeWithNoFieldsDoesNotReportDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[BlittableType]
struct S
{
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NativeTypeWithNoMarshallingMethodsReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public byte c;
}

[BlittableType]
struct Native
{
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustHaveRequiredShapeRule).WithSpan(11, 1, 14, 2).WithArguments("Native", "S"));
        }

        [Fact]
        public async Task NativeTypeWithOnlyConstructorDoesNotReportDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public byte c;
}

[BlittableType]
struct Native
{
    public Native(S s) {}
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NativeTypeWithOnlyToManagedMethodDoesNotReportDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public byte c;
}

[BlittableType]
struct Native
{
    public S ToManaged() => new S();
}";

            await VerifyCS.VerifyAnalyzerAsync(source);
        }
        
        [Fact]
        public async Task NativeTypeWithOnlyStackallocConstructorReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public byte c;
}

[BlittableType]
struct Native
{
    public Native(S s, Span<byte> buffer) {}
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(StackallocMarshallingShouldSupportAllocatingMarshallingFallbackRule).WithSpan(11, 1, 15, 2).WithArguments("Native"));
        }

        [Fact]
        public async Task TypeWithOnlyNativeStackallocConstructorAndGetPinnableReferenceReportsDiagnostics()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public byte c;
    public ref byte GetPinnableReference() => ref c;
}

struct Native
{
    public Native(S s, Span<byte> buffer) {}

    public IntPtr Value => IntPtr.Zero;
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(StackallocMarshallingShouldSupportAllocatingMarshallingFallbackRule).WithSpan(12, 1, 17, 2).WithArguments("Native"),
                VerifyCS.Diagnostic(GetPinnableReferenceShouldSupportAllocatingMarshallingFallbackRule).WithSpan(5, 2, 5, 35).WithArguments("S", "Native"));
        }

        [Fact]
        public async Task NativeTypeWithConstructorAndSetOnlyValuePropertyReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public byte c;
}

struct Native
{
    public Native(S s) {}

    public IntPtr Value { set {} }
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(ValuePropertyMustHaveGetterRule).WithSpan(15, 5, 15, 35).WithArguments("Native"));
        }

        [Fact]
        public async Task NativeTypeWithToManagedAndGetOnlyValuePropertyReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

[NativeMarshalling(typeof(Native))]
class S
{
    public byte c;
}

struct Native
{
    public S ToManaged() => new S();

    public IntPtr Value => IntPtr.Zero;
}";

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(ValuePropertyMustHaveSetterRule).WithSpan(15, 5, 15, 40).WithArguments("Native"));
        }
        
        [Fact]
        public async Task BlittableNativeTypeOnMarshalUsingParameterDoesNotReportDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

struct S
{
    public string s;
}

[BlittableType]
struct Native
{
    private IntPtr value;

    public Native(S s)
    {
        value = IntPtr.Zero;
    }

    public S ToManaged() => new S();
}


static class Test
{
    static void Foo([MarshalUsing(typeof(Native))] S s)
    {}
}
";
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NonBlittableNativeTypeOnMarshalUsingParameterReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

struct S
{
    public string s;
}

struct Native
{
    private string value;

    public Native(S s) : this()
    {
    }

    public S ToManaged() => new S();
}


static class Test
{
    static void Foo([MarshalUsing(typeof(Native))] S s)
    {}
}
";
            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustBeBlittableRule).WithSpan(10, 1, 19, 2).WithArguments("Native", "S"));
        }

        [Fact]
        public async Task NonBlittableNativeTypeOnMarshalUsingReturnReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

struct S
{
    public string s;
}

struct Native
{
    private string value;

    public Native(S s) : this()
    {
    }

    public S ToManaged() => new S();
}


static class Test
{
    [return: MarshalUsing(typeof(Native))]
    static S Foo() => new S();
}
";
            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustBeBlittableRule).WithSpan(10, 1, 19, 2).WithArguments("Native", "S"));
        }

        [Fact]
        public async Task NonBlittableNativeTypeOnMarshalUsingFieldReportsDiagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;

struct S
{
    public string s;
}

struct Native
{
    private string value;

    public Native(S s) : this()
    {
    }

    public S ToManaged() => new S();
}


struct Test
{
    [MarshalUsing(typeof(Native))]
    S s;
}
";
            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(NativeTypeMustBeBlittableRule).WithSpan(10, 1, 19, 2).WithArguments("Native", "S"));
        }
    }
}