// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using System;
using Xunit;
using Microsoft.Interop.UnitTests;

namespace JSImportGenerator.Unit.Tests
{
    public class Compiles
    {
        public static object[][] AsyncEntryPointCodeSnippetsToCompile =
        [
            [CodeSnippets.PublicMainInPublicClass_Void],
            [CodeSnippets.PublicMainInPublicClass_Void_Args],
            [CodeSnippets.PublicMainInPublicClass_Int],
            [CodeSnippets.PublicMainInPublicClass_Int_Args],
            [CodeSnippets.PublicMainInPublicClass_TaskOfVoid],
            [CodeSnippets.PublicMainInPublicClass_TaskOfVoid_Args],
            [CodeSnippets.PublicMainInPublicClass_TaskOfInt],
            [CodeSnippets.PublicMainInPublicClass_TaskOfInt_Args],
            [CodeSnippets.PrivateMainInPublicClass_Void],
            [CodeSnippets.PrivateMainInPublicClass_Void_Args],
            [CodeSnippets.PrivateMainInPublicClass_Int],
            [CodeSnippets.PrivateMainInPublicClass_Int_Args],
            [CodeSnippets.PrivateMainInPublicClass_TaskOfVoid],
            [CodeSnippets.PrivateMainInPublicClass_TaskOfVoid_Args],
            [CodeSnippets.PrivateMainInPublicClass_TaskOfInt],
            [CodeSnippets.PrivateMainInPublicClass_TaskOfInt_Args],
            [CodeSnippets.PrivateMainInPublicClassInNamespace],
            [CodeSnippets.TopLevelMain],
            [CodeSnippets.TopLevelAsyncMain],
        ];

        [Theory]
        [MemberData(nameof(AsyncEntryPointCodeSnippetsToCompile))]
        public async Task ValidateAsyncEntryPointSnippets(string source)
        {
            Compilation comp = await TestUtils.CreateCompilation(
                source, outputKind: OutputKind.ConsoleApplication, allowUnsafe: true);
            TestUtils.AssertPreSourceGeneratorCompilation(comp);

            var newComp = TestUtils.RunGenerators(comp, out var generatorDiags,
                new Microsoft.Interop.JavaScript.JSExportGenerator());
            Assert.Empty(generatorDiags);
            TestUtils.AssertPostSourceGeneratorCompilation(newComp);
        }

        public static IEnumerable<object[]> CodeSnippetsToCompile()
        {
            yield return new object[] { CodeSnippets.TrivialClassDeclarations };
            yield return new object[] { CodeSnippets.AllDefault };
            yield return new object[] { CodeSnippets.AllAnnotated };
            yield return new object[] { CodeSnippets.DefaultReturnMarshaler<int>() };
            yield return new object[] { CodeSnippets.DefaultReturnMarshaler<byte>() };
            yield return new object[] { CodeSnippets.DefaultReturnMarshaler<bool>() };
            yield return new object[] { CodeSnippets.DefaultReturnMarshaler<char>() };
            yield return new object[] { CodeSnippets.DefaultReturnMarshaler<string>() };
            yield return new object[] { CodeSnippets.DefaultReturnMarshaler<JSObject>() };
            yield return new object[] { CodeSnippets.DefaultReturnMarshaler<Exception>() };
        }


        [Theory]
        [MemberData(nameof(CodeSnippetsToCompile))]
        public async Task ValidateSnippets(string source)
        {
            Compilation comp = await TestUtils.CreateCompilation(source, allowUnsafe: true);
            TestUtils.AssertPreSourceGeneratorCompilation(comp);

            var newComp = TestUtils.RunGenerators(comp, out var generatorDiags,
                new Microsoft.Interop.JavaScript.JSImportGenerator(),
                new Microsoft.Interop.JavaScript.JSExportGenerator());

            // uncomment for debugging JSTestUtils.DumpCode(source, newComp, generatorDiags);

            Assert.Empty(generatorDiags);

            TestUtils.AssertPostSourceGeneratorCompilation(newComp);
        }
    }
}
