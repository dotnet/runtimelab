﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Xunit;

namespace DllImportGenerator.UnitTests
{
    public class CompileFails
    {
        public static IEnumerable<object[]> CodeSnippetsToCompile()
        {
            // Not GeneratedDllImportAttribute
            yield return new object[] { CodeSnippets.UserDefinedPrefixedAttributes, 0, 3 };

            // No explicit marshalling for char or string
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<char>(), 5, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<string>(), 5, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<char[]>(), 5, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<string[]>(), 5, 0 };
            yield return new object[] { CodeSnippets.PreserveSigFalse<char>(), 3, 0 };
            yield return new object[] { CodeSnippets.PreserveSigFalse<string>(), 3, 0 };

            // Unsupported CharSet
            yield return new object[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<char>(CharSet.Auto), 5, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<char>(CharSet.Ansi), 5, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<char>(CharSet.None), 5, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiersWithCharSet<string>(CharSet.None), 5, 0 };

            // Unsupported UnmanagedType
            yield return new object[] { CodeSnippets.MarshalAsParametersAndModifiers<char>(UnmanagedType.I1), 5, 0 };
            yield return new object[] { CodeSnippets.MarshalAsParametersAndModifiers<char>(UnmanagedType.U1), 5, 0 };
            yield return new object[] { CodeSnippets.MarshalAsParametersAndModifiers<int[]>(UnmanagedType.SafeArray), 10, 0 };

            // Unsupported MarshalAsAttribute usage
            //  * UnmanagedType.CustomMarshaler, MarshalTypeRef, MarshalType, MarshalCookie
            yield return new object[] { CodeSnippets.MarshalAsCustomMarshalerOnTypes, 16, 0 };

            // Unsupported named arguments
            //  * BestFitMapping, ThrowOnUnmappableChar
            yield return new object[] { CodeSnippets.AllDllImportNamedArguments, 2, 0 };

            // LCIDConversion
            yield return new object[] { CodeSnippets.LCIDConversionAttribute, 1, 0 };

            // No size information for array marshalling from unmanaged to managed
            //   * return, out, ref
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<byte[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<sbyte[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<short[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<ushort[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<int[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<uint[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<long[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<ulong[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<float[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<double[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<bool[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<IntPtr[]>(), 3, 0 };
            yield return new object[] { CodeSnippets.BasicParametersAndModifiers<UIntPtr[]>(), 3, 0 };

            // Array with non-integer size param
            yield return new object[] { CodeSnippets.ArrayParameterWithSizeParam<float>(isByRef: false), 1, 0 };
            yield return new object[] { CodeSnippets.ArrayParameterWithSizeParam<double>(isByRef: false), 1, 0 };
            yield return new object[] { CodeSnippets.ArrayParameterWithSizeParam<bool>(isByRef: false), 1, 0 };
        }

        [Theory]
        [MemberData(nameof(CodeSnippetsToCompile))]
        public async Task ValidateSnippets(string source, int expectedGeneratorErrors, int expectedCompilerErrors)
        {
            Compilation comp = await TestUtils.CreateCompilation(source);
            TestUtils.AssertPreSourceGeneratorCompilation(comp);

            var newComp = TestUtils.RunGenerators(comp, out var generatorDiags, new Microsoft.Interop.DllImportGenerator());

            // Verify the compilation failed with errors.
            int generatorErrors = generatorDiags.Count(d => d.Severity == DiagnosticSeverity.Error);
            Assert.Equal(expectedGeneratorErrors, generatorErrors);

            int compilerErrors = newComp.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);
            Assert.Equal(expectedCompilerErrors, compilerErrors);
        }
    }
}
