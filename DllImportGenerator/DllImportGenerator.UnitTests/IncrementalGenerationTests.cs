using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Interop.DllImportGenerator;

namespace DllImportGenerator.UnitTests
{
    public class IncrementalGenerationTests
    {
        [Fact]
        public async Task AddingNewUnrelatedType_DoesNotRegenerateSource()
        {
            string source = CodeSnippets.BasicParametersAndModifiers<int>();

            Compilation comp1 = await TestUtils.CreateCompilation(source);

            Microsoft.Interop.DllImportGenerator generator = new();
            GeneratorDriver driver = TestUtils.CreateDriver(comp1, null, new[] { generator });

            driver = driver.RunGenerators(comp1);

            generator.IncrementalTracker = new IncrementalityTracker();

            Compilation comp2 = comp1.AddSyntaxTrees(CSharpSyntaxTree.ParseText("struct Foo {}", new CSharpParseOptions(LanguageVersion.Preview)));
            driver.RunGenerators(comp2);

            Assert.All(generator.IncrementalTracker.ExecutedSteps, step =>
            {
                Assert.Equal(IncrementalityTracker.StepName.GenerateSingleStub, step.Step);
            });
        }
    }
}
