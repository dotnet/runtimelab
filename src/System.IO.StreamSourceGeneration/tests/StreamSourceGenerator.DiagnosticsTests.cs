// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Xunit;

namespace System.IO.StreamSourceGeneration.Tests
{
    public class StreamSourceGeneratorDiagnosticsTests
    {
        [Fact]
        public void DoesNotImplementAnyReadOrWrite()
        {
            string source = """
                using System.IO;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream { }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement any Read or Write method");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Fact]
        public void ImplementsReadAsyncBytesButNotRead() => 
            ImplementsReadAsyncButNotRead("""
                using System.IO;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    { 
                        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                        {
                            return Task.FromResult(0);
                        }
                    }
                }
                """);


        [Fact]
        public void ImplementsReadAsyncSpanButNotRead() => 
            ImplementsReadAsyncButNotRead("""
                using System;
                using System.IO;
                using System.Threading.Tasks;
                using System.Threading;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                        {
                            return ValueTask.FromResult(0);
                        }
                    }
                }
                """);

        private void ImplementsReadAsyncButNotRead(string source)
        {
            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement any Read method and hence is doing sync-over-async");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Fact]
        public void ImplementsReadBytesButNotReadAsync()
            => ImplementsReadButNotReadAsync("""
                using System.IO;
                
                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override int Read(byte[] buffer, int offset, int count)
                        {
                            return 0;
                        }
                    }
                }
                """);


        [Fact]
        public void ImplementsReadSpanButNotReadAsync()
            => ImplementsReadButNotReadAsync("""
                using System;
                using System.IO;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override int Read(Span<byte> buffer)
                        {
                            return 0;
                        }
                    }
                }
                """);

        private void ImplementsReadButNotReadAsync(string source)
        {
            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement any ReadAsync method and hence is doing async-over-sync");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Fact]
        public void ImplementsWriteAsyncBytesButNotWrite()
            => ImplementsWriteAsyncButNotWrite("""
                using System.IO;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) 
                        { 
                            return Task.CompletedTask;
                        }
                    }
                }
                """);

        [Fact]
        public void ImplementsWriteAsyncSpanButNotWrite()
            => ImplementsWriteAsyncButNotWrite("""
                using System;
                using System.IO;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) 
                        { 
                            return ValueTask.CompletedTask;
                        }
                    }
                }
                """);

        private void ImplementsWriteAsyncButNotWrite(string source)
        {
            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement any Write method and hence is doing sync-over-async");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Fact]
        public void ImplementsWriteBytesButNotWriteAsync()
            => ImplementsWriteButNotWriteAsync("""
                using System.IO;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override void Write(byte[] buffer, int offset, int count) { }
                    }
                }
                """);

        [Fact]
        public void ImplementsWriteSpanButNotWriteAsync()
            => ImplementsWriteButNotWriteAsync("""
                using System;
                using System.IO;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override void Write(ReadOnlySpan<byte> buffer) { }
                    }
                }
                """);

        private void ImplementsWriteButNotWriteAsync(string source)
        {
            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement any WriteAsync method and hence is doing async-over-sync");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Fact]
        public void DoesNotImplementReadSpan()
        {
            string source = """
                using System.IO;
                
                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override int Read(byte[] buffer, int offset, int count)
                        {
                            return 0;
                        }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement Read(Span<byte>), for better performance, consider providing an implementation for it");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Fact]
        public void DoesNotImplementWriteSpan()
        {
            string source = """
                using System.IO;
                
                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override void Write(byte[] buffer, int offset, int count) { }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement Write(ReadOnlySpan<byte>), for better performance, consider providing an implementation for it");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }
        [Fact]
        public void DoesNotImplementReadAsyncMemory()
        {
            string source = """
                using System.IO;
                using System.Threading;
                using System.Threading.Tasks;
                
                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    { 
                        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                        {
                            return Task.FromResult(0);
                        }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement ReadAsync(Memory<byte>, CancellationToken), for better performance, consider providing an implementation for it");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Fact]
        public void DoesNotImplementWriteAsyncMemory()
        {
            string source = """
                using System.IO;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
                        { 
                            return Task.CompletedTask;
                        }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement WriteAsync(ReadOnlyMemory<byte>, CancellationToken), for better performance, consider providing an implementation for it");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Theory]
        [InlineData("public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) { return null; }", AsyncResultOperationKind.Read)]
        [InlineData("public override int EndRead(IAsyncResult asyncResult) { return default; }", AsyncResultOperationKind.Read)]
        [InlineData("public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) { return null; }", AsyncResultOperationKind.Write)]
        [InlineData("public override void EndWrite(IAsyncResult asyncResult) { }", AsyncResultOperationKind.Write)]
        public void AvoidBeginEndAsyncResultMethods(string method, AsyncResultOperationKind kind)
        {
            string source = $$"""
                using System;
                using System.IO;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        {{method}}
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: string.Format(
                    "'MyStream' implements Begin{0} or End{0}, Task-based methods should be preferred when possible, consider removing them to allow the source generator to emit an implementation based on Task-based {0}Async",
                    kind));

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        public enum AsyncResultOperationKind
        {
            Read,
            Write,
        }

        [Theory]
        [InlineData("public override void Write(byte[] buffer, int offset, int count) { }")]
        [InlineData("public override void Write(ReadOnlySpan<byte> buffer) { }")]
        [InlineData("public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { return Task.CompletedTask; }")]
        [InlineData("public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { return ValueTask.CompletedTask; }")]
        public void ImplementsAnyWriteButNotFlush(string method)
        {
            string source = $$"""
                using System;
                using System.IO;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        {{method}}
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement Flush but it implements one or more Write method(s), Consider implementing Flush() to move any buffered data to its destination, clear the buffer, or both");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Theory]
        [InlineData("public override void Write(byte[] buffer, int offset, int count) { }")]
        [InlineData("public override void Write(ReadOnlySpan<byte> buffer) { }")]
        [InlineData("public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { return Task.CompletedTask; }")]
        [InlineData("public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { return ValueTask.CompletedTask; }")]
        public void ImplementsAnyWriteAndFlush_NoWarn(string method)
        {
            string source = $$"""
                using System;
                using System.IO;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        {{method}}

                        public override void Flush() { }
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: "'MyStream' does not implement Flush but it implements one or more Write method(s), Consider implementing Flush() to move any buffered data to its destination, clear the buffer, or both");

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.DoesNotContain(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Theory]
        [InlineData(
            "public override int Read(Span<byte> buffer) { return 0; }",
            "public override int Read(byte[] buffer, int offset, int count) { return 0; }",
            "'MyStream' implements Read(Span<byte>), consider removing Read(byte[], int, int) to let the source generator handle it")]
        [InlineData(
            "public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { return ValueTask.FromResult(0); }",
            "public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { return Task.FromResult(0); }",
            "'MyStream' implements ReadAsync(Memory<byte>, CancellationToken), consider removing ReadAsync(byte[], int, int, CancellationToken) to let the source generator handle it")]
        [InlineData(
            "public override void Write(ReadOnlySpan<byte> buffer) { }",
            "public override void Write(byte[] buffer, int offset, int count) { }",
            "'MyStream' implements Write(ReadOnlySpan<byte>), consider removing Write(byte[], int, int) to let the source generator handle it")]
        [InlineData(
            "public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { return ValueTask.CompletedTask; }",
            "public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { return Task.CompletedTask; }",
            "'MyStream' implements WriteAsync(ReadOnlyMemory<byte>, CancellationToken), consider removing WriteAsync(byte[], int, int, CancellationToken) to let the source generator handle it")]
        public void ImplementsSpanBasedAndArrayBasedMethods_ConsiderRemovingArrayBased(string spanMethod, string arrayMethod, string diagnosticMessage)
        {

            string source = $$"""
                using System;
                using System.IO;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        {{spanMethod}}

                        {{arrayMethod}}
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: diagnosticMessage);

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.Contains(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        [Theory]
        [InlineData(
            "public override int Read(Span<byte> buffer) { return 0; }",
            "'MyStream' implements Read(Span<byte>), consider removing Read(byte[], int, int) to let the source generator handle it")]
        [InlineData(
            "public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { return ValueTask.FromResult(0); }",
            "'MyStream' implements ReadAsync(Memory<byte>, CancellationToken), consider removing ReadAsync(byte[], int, int, CancellationToken) to let the source generator handle it")]
        [InlineData(
            "public override void Write(ReadOnlySpan<byte> buffer) { }",
            "'MyStream' implements Write(ReadOnlySpan<byte>), consider removing Write(byte[], int, int) to let the source generator handle it")]
        [InlineData(
            "public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { return ValueTask.CompletedTask; }",
            "'MyStream' implements WriteAsync(ReadOnlyMemory<byte>, CancellationToken), consider removing WriteAsync(byte[], int, int, CancellationToken) to let the source generator handle it")]
        public void ImplementsSpanBasedMethodOnly_NoWarn(string spanMethod, string diagnosticMessage)
        {

            string source = $$"""
                using System;
                using System.IO;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Test
                {
                    [GenerateStreamBoilerplate]
                    public partial class MyStream : Stream
                    {
                        {{spanMethod}}
                    }
                }
                """;

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            DiagnosticData expectedDiagnostic = new(
                severity: DiagnosticSeverity.Info,
                location: compilation.GetSymbolsWithName("MyStream").First().Locations.First(),
                message: diagnosticMessage);

            ImmutableArray<Diagnostic> diagnostics = CompilationHelper.RunSourceGenerator(compilation);
            Assert.DoesNotContain(expectedDiagnostic, DiagnosticData.FromDiagnostics(diagnostics));
        }

        private record struct DiagnosticData(DiagnosticSeverity Severity, string FilePath, LinePositionSpan LinePositionSpan, string Message)
        {
            public DiagnosticData(DiagnosticSeverity severity, Location location, string message)
                : this(severity, location.SourceTree?.FilePath ?? "", location.GetLineSpan().Span, TrimCultureSensitiveMessage(message))
            {
            }

            // for non-English runs, trim the message content since it might be translated.
            private static string TrimCultureSensitiveMessage(string message) => s_IsEnglishCulture ? message : "";
            private readonly static bool s_IsEnglishCulture = CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase);

            public static IEnumerable<DiagnosticData> FromDiagnostics(IEnumerable<Diagnostic> diagnostics)
                => diagnostics.Select(d => new DiagnosticData(d.Severity, d.Location, d.GetMessage()));
        }
    }
}
