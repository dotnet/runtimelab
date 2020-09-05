using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace DllImportGenerator.Test
{
    public class Compiles
    {
        public static IEnumerable<object[]> CodeSnippetsToCompile()
        {
            yield return new[] { CodeSnippets.TrivialClassDeclarations };
            yield return new[] { CodeSnippets.TrivialStructDeclarations };
            yield return new[] { CodeSnippets.MultipleAttributes };
            yield return new[] { CodeSnippets.NestedNamespace };
            yield return new[] { CodeSnippets.NestedTypes };
            yield return new[] { CodeSnippets.UserDefinedEntryPoint };
            yield return new[] { CodeSnippets.AllDllImportNamedArguments };
            yield return new[] { CodeSnippets.DefaultParameters };
            yield return new[] { CodeSnippets.UseCSharpFeaturesForConstants };
            yield return new[] { CodeSnippets.MarshalAsAttributeOnTypes };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<byte>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<sbyte>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<short>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<ushort>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<int>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<uint>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<long>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<ulong>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<float>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<double>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<bool>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<char>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<string>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<IntPtr>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<UIntPtr>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<byte[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<sbyte[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<short[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<ushort[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<int[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<uint[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<long[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<ulong[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<float[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<double[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<bool[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<char[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<string[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<IntPtr[]>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<UIntPtr[]>() };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<bool>(UnmanagedType.Bool) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<bool>(UnmanagedType.VariantBool) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<bool>(UnmanagedType.I1) };
            yield return new[] { CodeSnippets.EnumParameters };
        }

        [Theory]
        [MemberData(nameof(CodeSnippetsToCompile))]
        public async Task ValidateSnippets(string source)
        {
            Compilation comp = await TestUtils.CreateCompilation(source);
            TestUtils.AssertPreSourceGeneratorCompilation(comp);

            var newComp = TestUtils.RunGenerators(comp, out var generatorDiags, new Microsoft.Interop.DllImportGenerator());
            Assert.Empty(generatorDiags);

            var newCompDiags = newComp.GetDiagnostics();
            Assert.Empty(newCompDiags);
        }
    }
}
