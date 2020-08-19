using System.Threading.Tasks;
using Xunit;
using static Microsoft.Interop.ManualTypeMarshallingAnalyzer;

using VerifyCS = DllImportGenerator.Test.Verifiers.CSharpAnalyzerVerifier<Microsoft.Interop.ManualTypeMarshallingAnalyzer>;

namespace DllImportGenerator.Test
{
    public class ManualTypeMarshallingAnalyzerTests
    {
        [Fact]
        public async Task NonBlittableTypeMarkedBlittableReportsDiagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;

[BlittableType]
struct S
{
    public string field;
}
";

            var diagnostic = VerifyCS.Diagnostic(BlittableTypeMustBeBlittableRule).WithSpan(4, 2, 4, 15).WithArguments("S");
            await VerifyCS.VerifyAnalyzerAsync(source, diagnostic);
        }
    }
}