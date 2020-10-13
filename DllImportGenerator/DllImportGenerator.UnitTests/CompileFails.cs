using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace DllImportGenerator.UnitTests
{
    public class CompileFails
    {
        public static IEnumerable<object[]> CodeSnippetsToCompile()
        {
            yield return new object[] { CodeSnippets.UserDefinedPrefixedAttributes, 3 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<char>(CharSet.Auto), 5 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<char>(CharSet.None), 5 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<char>(CharSet.Ansi), 5 };
        }

        [Theory]
        [MemberData(nameof(CodeSnippetsToCompile))]
        public async Task ValidateSnippets(string source, int failCount)
        {
            Compilation comp = await TestUtils.CreateCompilation(source);
            TestUtils.AssertPreSourceGeneratorCompilation(comp);

            var newComp = TestUtils.RunGenerators(comp, out var generatorDiags, new Microsoft.Interop.DllImportGenerator());

            // Verify the compilation failed with error diagnostics.
            int errorCount = 0;
            errorCount += generatorDiags.Count(d => d.Severity == DiagnosticSeverity.Error);
            errorCount += newComp.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);

            Assert.Equal(failCount, errorCount);
        }
    }
}
