// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using TbdParsing;
using TbdParsing.Models;
using Xunit;

namespace BindingsGeneration.Tests
{
    public class TbdParserTests : IClassFixture<TbdParserTests.TestFixture>
    {
        private readonly TestFixture _fixture;
        private static string _tbdFilePath;
        private static TbdParser _tbdParser;
        private static string _mockTbdFilePath;

        public TbdParserTests(TestFixture fixture)
        {
            _fixture = fixture;
        }

        public class TestFixture : IDisposable
        {
            static TestFixture()
            {
                InitializeResources();
            }

            private static void InitializeResources()
            {
                _tbdFilePath = "/Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk/System/Library/Frameworks/Foundation.framework/Foundation.tbd";
                _tbdParser = new TbdParser();

                // Create a mock TBD file for testing
                _mockTbdFilePath = Path.GetTempFileName();
                File.WriteAllText(_mockTbdFilePath, @"--- !tapi-tbd
tbd-version:     4
targets:         [ x86_64-macos, arm64-macos, arm64e-macos ]
install-name:    '/System/Library/Frameworks/TestFramework.framework/TestFramework'
swift-abi-version: 7
exports:         
  - targets:         [ x86_64-macos, arm64-macos, arm64e-macos ]
    symbols:         [ '_$s4Test5ClassCMa', '_$s4Test5ClassCMn', '_$s4Test5ClassCfD' ]
    objc-classes:    [ TestClass1, TestClass2 ]
    objc-ivars:      [ TestClass1._property ]
");
            }

            [Fact]
            public static void AssureTbdExists()
            {
                Assert.False(String.IsNullOrEmpty(_tbdFilePath));
                Assert.True(File.Exists(_tbdFilePath));
            }

            [Fact]
            public static void ParseFile()
            {
                TbdFile tbdFile = _tbdParser.ParseFile(_tbdFilePath);
                Assert.NotNull(tbdFile);
            }

            [Fact]
            public static void ValidateFoundationTbdContents()
            {
                TbdFile tbdFile = _tbdParser.ParseFile(_tbdFilePath);

                // Basic properties
                Assert.Equal(4, tbdFile.Version); // Foundation.tbd uses version 4
                Assert.NotEmpty(tbdFile.Targets);

                // Validate targets - Foundation.tbd has specific targets
                Assert.Contains("x86_64-macos", tbdFile.Targets);
                Assert.Contains("arm64-macos", tbdFile.Targets);
                Assert.Contains("arm64e-macos", tbdFile.Targets);
                Assert.Contains("x86_64-maccatalyst", tbdFile.Targets);
                Assert.Contains("arm64-maccatalyst", tbdFile.Targets);
                Assert.Contains("arm64e-maccatalyst", tbdFile.Targets);

                // Validate install name
                Assert.Contains("Foundation", tbdFile.InstallName);
                Assert.Equal("/System/Library/Frameworks/Foundation.framework/Versions/C/Foundation", tbdFile.InstallName);

                // Validate Swift ABI version
                Assert.Equal(7, tbdFile.SwiftAbiVersion);

                // Validate exports structure
                Assert.NotEmpty(tbdFile.Exports);

                // Validate that each export has targets and symbols
                Assert.All(tbdFile.Exports, export =>
                {
                    Assert.NotEmpty(export.Targets);
                    Assert.NotEmpty(export.Symbols);
                });
            }

            [Fact]
            public static void TestMockTbdFile()
            {
                TbdFile tbdFile = _tbdParser.ParseFile(_mockTbdFilePath);

                // Verify basic properties
                Assert.Equal(4, tbdFile.Version);
                Assert.Equal(3, tbdFile.Targets.Count);
                Assert.Equal("/System/Library/Frameworks/TestFramework.framework/TestFramework", tbdFile.InstallName);
                Assert.Equal(7, tbdFile.SwiftAbiVersion);

                // Verify exports
                Assert.Single(tbdFile.Exports);
                var export = tbdFile.Exports[0];

                // Verify targets
                Assert.Equal(3, export.Targets.Count);
                Assert.Contains("x86_64-macos", export.Targets);
                Assert.Contains("arm64-macos", export.Targets);
                Assert.Contains("arm64e-macos", export.Targets);

                // Verify symbols
                Assert.Equal(3, export.Symbols.Count);

                // Verify Swift symbols
                Assert.Equal(3, export.SwiftSymbols.ToList().Count);
                Assert.Contains(export.SwiftSymbols, s => s.Name == "_$s4Test5ClassCMa");
                Assert.Contains(export.SwiftSymbols, s => s.Name == "_$s4Test5ClassCMn");
                Assert.Contains(export.SwiftSymbols, s => s.Name == "_$s4Test5ClassCfD");

                // Verify Objective-C symbols
                Assert.Empty(export.ObjectiveCSymbols);

                // Verify other symbols
                Assert.Empty(export.OtherSymbols);

                // Verify Objective-C classes
                Assert.Equal(2, export.ObjcClasses.Count);
                Assert.Contains("TestClass1", export.ObjcClasses);
                Assert.Contains("TestClass2", export.ObjcClasses);

                // Verify Objective-C ivars
                Assert.Single(export.ObjcIvars);
                Assert.Contains("TestClass1._property", export.ObjcIvars);
            }

            [Fact]
            public static void TestFileNotFound()
            {
                var nonExistentFile = "/path/to/nonexistent.tbd";
                Assert.Throws<FileNotFoundException>(() => _tbdParser.ParseFile(nonExistentFile));
            }

            [Fact]
            public static void TestInvalidTbdFormat()
            {
                // Create an invalid TBD file
                string invalidTbdPath = Path.GetTempFileName();
                File.WriteAllText(invalidTbdPath, "This is not a valid TBD file");

                try
                {
                    Assert.Throws<ParsingException>(() => _tbdParser.ParseFile(invalidTbdPath));
                }
                finally
                {
                    // Clean up temporary file
                    File.Delete(invalidTbdPath);
                }
            }

            public void Dispose()
            {
                // Clean up the mock TBD file
                if (File.Exists(_mockTbdFilePath))
                {
                    File.Delete(_mockTbdFilePath);
                }
            }
        }
    }
}
