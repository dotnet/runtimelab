using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace DllImportGenerator.UnitTests
{
    public class Compiles
    {
        public static IEnumerable<object[]> CodeSnippetsToCompile_NoDiagnostics()
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
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<IntPtr>() };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers<UIntPtr>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<byte>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<sbyte>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<short>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<ushort>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<int>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<uint>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<long>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<ulong>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<float>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<double>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<bool>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<IntPtr>() };
            yield return new[] { CodeSnippets.ArrayParametersAndModifiers<UIntPtr>() };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<byte>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<sbyte>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<short>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<ushort>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<int>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<uint>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<long>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<ulong>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<IntPtr>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<UIntPtr>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<byte>(isByRef: true) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<sbyte>(isByRef: true) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<short>(isByRef: true) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<ushort>(isByRef: true) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<int>(isByRef: true) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<uint>(isByRef: true) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<long>(isByRef: true) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<ulong>(isByRef: true) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<IntPtr>(isByRef: true) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<UIntPtr>(isByRef: true) };
            yield return new[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<char>(CharSet.Unicode) };
            yield return new[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<string>(CharSet.Unicode) };
            //yield return new[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<string>(CharSet.Ansi) };
            //yield return new[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<string>(CharSet.Auto) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<bool>(UnmanagedType.Bool) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<bool>(UnmanagedType.VariantBool) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<bool>(UnmanagedType.I1) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<char>(UnmanagedType.I2) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<char>(UnmanagedType.U2) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<string>(UnmanagedType.LPWStr) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<string>(UnmanagedType.LPTStr) };
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<string>(UnmanagedType.LPUTF8Str) };
            //yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<string>(UnmanagedType.LPStr) };
            yield return new[] { CodeSnippets.ArrayParameterWithNestedMarshalInfo<string>(UnmanagedType.LPWStr) };
            yield return new[] { CodeSnippets.ArrayParameterWithNestedMarshalInfo<string>(UnmanagedType.LPUTF8Str) };
            //yield return new[] { CodeSnippets.EnumParameters };
            yield return new[] { CodeSnippets.PreserveSigFalseVoidReturn };
            yield return new[] { CodeSnippets.PreserveSigFalse<byte>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<sbyte>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<short>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<ushort>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<int>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<uint>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<long>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<ulong>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<float>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<double>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<bool>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<IntPtr>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<UIntPtr>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<byte[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<sbyte[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<short[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<ushort[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<int[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<uint[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<long[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<ulong[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<float[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<double[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<bool[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<char[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<string[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<IntPtr[]>() };
            //yield return new[] { CodeSnippets.PreserveSigFalse<UIntPtr[]>() };
            yield return new[] { CodeSnippets.DelegateParametersAndModifiers };
            yield return new[] { CodeSnippets.DelegateMarshalAsParametersAndModifiers };
            yield return new[] { CodeSnippets.BlittableStructParametersAndModifiers };
            yield return new[] { CodeSnippets.GenericBlittableStructParametersAndModifiers };
            yield return new[] { CodeSnippets.BasicParametersAndModifiers("Microsoft.Win32.SafeHandles.SafeFileHandle") };
            yield return new[] { CodeSnippets.CustomStructMarshallingParametersAndModifiers };
            yield return new[] { CodeSnippets.CustomStructMarshallingStackallocParametersAndModifiers };
            yield return new[] { CodeSnippets.CustomStructMarshallingStackallocValuePropertyParametersAndModifiers };
            yield return new[] { CodeSnippets.CustomStructMarshallingValuePropertyParametersAndModifiers };
            yield return new[] { CodeSnippets.CustomStructMarshallingPinnableParametersAndModifiers };
            yield return new[] { CodeSnippets.CustomStructMarshallingByRefValuePropertyIn };
        }

        public static IEnumerable<object[]> CodeSnippetsToCompile_WithDiagnostics()
        {
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
            
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<float>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<double>(isByRef: false) };
            yield return new[] { CodeSnippets.ArrayParameterWithSizeParam<bool>(isByRef: false) };

            yield return new[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<string>(CharSet.Ansi) };
            yield return new[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<string>(CharSet.Auto) };
            
            yield return new[] { CodeSnippets.MarshalAsParametersAndModifiers<string>(UnmanagedType.LPStr) };
            yield return new[] { CodeSnippets.ArrayParameterWithNestedMarshalInfo<string>(UnmanagedType.LPStr) };

            yield return new[] { CodeSnippets.EnumParameters };

            yield return new[] { CodeSnippets.PreserveSigFalse<byte[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<sbyte[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<short[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<ushort[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<int[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<uint[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<long[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<ulong[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<float[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<double[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<bool[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<char[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<string[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<IntPtr[]>() };
            yield return new[] { CodeSnippets.PreserveSigFalse<UIntPtr[]>() };
            yield return new[] { CodeSnippets.CustomStructMarshallingByRefValuePropertyRefOutReturn };
        }

        [Theory]
        [MemberData(nameof(CodeSnippetsToCompile_NoDiagnostics))]
        public async Task ValidateSnippets_NoDiagnostics(string source)
        {
            Compilation comp = await TestUtils.CreateCompilation(source);
            TestUtils.AssertPreSourceGeneratorCompilation(comp);

            var newComp = TestUtils.RunGenerators(comp, out var generatorDiags, new Microsoft.Interop.DllImportGenerator());
            Assert.Empty(generatorDiags);

            var newCompDiags = newComp.GetDiagnostics();
            Assert.Empty(newCompDiags);
        }

        [Theory]
        [MemberData(nameof(CodeSnippetsToCompile_WithDiagnostics))]
        public async Task ValidateSnippets_WithDiagnostics(string source)
        {
            Compilation comp = await TestUtils.CreateCompilation(source);
            TestUtils.AssertPreSourceGeneratorCompilation(comp);

            var newComp = TestUtils.RunGenerators(comp, out var generatorDiags, new Microsoft.Interop.DllImportGenerator());
            Assert.NotEmpty(generatorDiags);
            Assert.All(generatorDiags, d => Assert.StartsWith(Microsoft.Interop.GeneratorDiagnostics.Ids.Prefix, d.Id));

            var newCompDiags = newComp.GetDiagnostics();
            Assert.Empty(newCompDiags);
        }
    }
}
