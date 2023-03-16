// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using System.Diagnostics;

namespace System.IO.StreamSourceGeneration
{
    public partial class StreamSourceGen
    {
        // Stream does not support read or write
        private static readonly DiagnosticDescriptor s_TypeDoesNotImplementReadOrWrite =
            new DiagnosticDescriptor(
                id: "FOOBAR001",
                title: "Type does not implement any Read or Write",
                messageFormat: "'{0}' does not implement any Read or Write method",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        // Sync-over-async or async-over-sync
        private static readonly DiagnosticDescriptor s_ReadDoingAsyncOverSync =
            new DiagnosticDescriptor(
                id: "FOOBAR002",
                title: "Stream does Read as async-over-sync",
                messageFormat: "'{0}' does not implement any Read method and hence is doing async-over-sync",
                category: "StreamSourceGen", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ReadAsyncDoingSyncOverAsync =
            new DiagnosticDescriptor(
                id: "FOOBAR003",
                title: "Stream does ReadAsync as sync-over-async",
                messageFormat: "'{0}' does not implement any ReadAsync method and hence is doing sync-over-async",
                category: "StreamSourceGen", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_WriteDoingAsyncOverSync =
            new DiagnosticDescriptor(
                id: "FOOBAR004",
                title: "Stream does Write as async-over-sync",
                messageFormat: "'{0}' does not implement any Write method and hence is doing async-over-sync",
                category: "StreamSourceGen", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_WriteAsyncDoingSyncOverAsync =
            new DiagnosticDescriptor(
                id: "FOOBAR005",
                title: "Stream does WriteAsync as sync-over-async",
                messageFormat: "'{0}' does not implement any WriteAsync method and hence is doing sync-over-async",
                category: "StreamSourceGen", DiagnosticSeverity.Info, isEnabledByDefault: true);

        // Consider Span/Memory overloads for better performance
        private static readonly DiagnosticDescriptor s_ConsiderImplementingReadSpan =
            new DiagnosticDescriptor(
                id: "FOOBAR006",
                title: "Consider implementing Read(Span<byte>)",
                messageFormat: "'{0}' does not implement Read(Span<byte>), for better performance, consider providing an implementation for Read(Span<byte>) and make other Read methods overloads to it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderImplementingWriteReadOnlySpan =
            new DiagnosticDescriptor(
                id: "FOOBAR007",
                title: "Consider implementing Write(ReadOnlySpan<byte>)",
                messageFormat: "'{0}' does not implement Write(ReadOnlySpan<byte>), for better performance, consider providing an implementation for Write(ReadOnlySpan<byte>) and make other Write overloads defer to it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderImplementingReadAsyncMemory =
            new DiagnosticDescriptor(
                id: "FOOBAR008",
                title: "Consider implementing ReadAsync(Memory<byte>, CancellationToken)",
                messageFormat: "'{0}' does not implement ReadAsync(Memory<byte>, CancellationToken), for better performance, consider providing an implementation for ReadAsync(Memory<byte>, CancellationToken) and make other ReadAsync overloads defer to it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderImplementingWriteAsyncReadOnlyMemory =
            new DiagnosticDescriptor(
                id: "FOOBAR009",
                title: "Consider implementing WriteAsync(ReadOnlyMemory<byte>, CancellationToken)",
                messageFormat: "'{0}' does not implement WriteAsync(ReadOnlyMemory<byte>, CancellationToken), for better performance, consider providing an implementation for WriteAsync(ReadOnlyMemory<byte>, CancellationToken) and make other WriteAsync overloads defer to it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        // Avoid implementing APM (Begin/End) methods
        private static readonly DiagnosticDescriptor s_AvoidBeginReadEndRead =
            new DiagnosticDescriptor(
                id: "FOOBAR010",
                title: "Avoid BeingRead or EndRead",
                messageFormat: "'{0}' implements BeginRead or EndRead, Task-based methods shoud be preferred when possible, consider removing them to allow the source generator to emit an implementation based on Task-based ReadAsync",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_AvoidBeginWriteEndWrite =
            new DiagnosticDescriptor(
                id: "FOOBAR011",
                title: "Avoid BeingWrite or EndWrite",
                messageFormat: "'{0}' implements BeingWrite or EndWrite, Task-based methods shoud be preferred when possible, consider removing them to allow the source generator to emit an implementation based on Task-based WriteAsync",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderImplementingFlush =
            new DiagnosticDescriptor(
                id: "FOOBAR012",
                title: "Consider implementing Flush() to move any buffered data to its destination",
                messageFormat: "'{0}' does not implement Flush but it implements one or more Write method(s), Consider implementing Flush() to move any buffered data to its destination, clear the buffer, or both",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static void ReportDiagnostics(SourceProductionContext context, StreamTypeInfo streamTypeInfo)
        {
            if (!streamTypeInfo.CanRead && !streamTypeInfo.CanWrite)
            {
                context.ReportDiagnostic(CreateDiagnostic(s_TypeDoesNotImplementReadOrWrite, streamTypeInfo));
            }
            else
            {
                if (streamTypeInfo.CanRead)
                {
                    ReportReadDiagnostics(context, streamTypeInfo);
                }

                if (streamTypeInfo.CanWrite)
                {
                    ReportWriteDiagnostics(context, streamTypeInfo);
                }
            }

            if (streamTypeInfo.OverriddenMembers.Contains(StreamMember.BeginRead) ||
                streamTypeInfo.OverriddenMembers.Contains(StreamMember.EndRead))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_AvoidBeginReadEndRead, streamTypeInfo));
            }

            if (streamTypeInfo.OverriddenMembers.Contains(StreamMember.BeginWrite) ||
                streamTypeInfo.OverriddenMembers.Contains(StreamMember.EndWrite))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_AvoidBeginWriteEndWrite, streamTypeInfo));
            }
        }

        private static void ReportReadDiagnostics(SourceProductionContext context, StreamTypeInfo streamTypeInfo)
        {
            Debug.Assert(streamTypeInfo.CanRead);
            Debug.Assert(streamTypeInfo.ReadInfo is not null);
            StreamCapabilityInfo readInfo = streamTypeInfo.ReadInfo!;

            bool asyncOverSync = readInfo.GetSyncPreferredMember().IsAsync();
            bool syncOverAsync = !readInfo.GetAsyncPreferredMember().IsAsync();
            Debug.Assert(asyncOverSync != syncOverAsync);

            if (asyncOverSync)
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ReadDoingAsyncOverSync, streamTypeInfo));
            }

            if (syncOverAsync)
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ReadAsyncDoingSyncOverAsync, streamTypeInfo));
            }

            if (!asyncOverSync && !streamTypeInfo.OverriddenMembers.Contains(StreamMember.ReadSpan))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingReadSpan, streamTypeInfo));
            }

            if (!syncOverAsync && !streamTypeInfo.OverriddenMembers.Contains(StreamMember.ReadAsyncMemory))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingReadAsyncMemory, streamTypeInfo));
            }
        }

        private static void ReportWriteDiagnostics(SourceProductionContext context, StreamTypeInfo streamTypeInfo)
        {
            Debug.Assert(streamTypeInfo.CanWrite);
            Debug.Assert(streamTypeInfo.WriteInfo is not null);
            StreamCapabilityInfo writeInfo = streamTypeInfo.WriteInfo!;

            bool asyncOverSync = writeInfo.GetSyncPreferredMember().IsAsync();
            bool syncOverAsync = !writeInfo.GetAsyncPreferredMember().IsAsync();
            Debug.Assert(asyncOverSync != syncOverAsync);

            if (asyncOverSync)
            {
                context.ReportDiagnostic(CreateDiagnostic(s_WriteDoingAsyncOverSync, streamTypeInfo));
            }

            if (syncOverAsync)
            {
                context.ReportDiagnostic(CreateDiagnostic(s_WriteAsyncDoingSyncOverAsync, streamTypeInfo));
            }

            if (!asyncOverSync && !streamTypeInfo.OverriddenMembers.Contains(StreamMember.WriteSpan))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingWriteReadOnlySpan, streamTypeInfo));
            }

            if (!syncOverAsync && !streamTypeInfo.OverriddenMembers.Contains(StreamMember.WriteAsyncMemory))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingWriteAsyncReadOnlyMemory, streamTypeInfo));
            }

            if (!streamTypeInfo.OverriddenMembers.Contains(StreamMember.Flush))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingFlush, streamTypeInfo));
            }
        }

        private static Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, StreamTypeInfo streamTypeInfo)
            => Diagnostic.Create(descriptor, Location.None, streamTypeInfo.TypeSymbol.Name);
    }
}
