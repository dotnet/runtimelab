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
    }
}