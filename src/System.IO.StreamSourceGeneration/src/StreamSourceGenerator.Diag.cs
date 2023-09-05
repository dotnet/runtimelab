// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace System.IO.StreamSourceGeneration
{
    // If/when this gets integrated into dotnet/runtime, this must be kept in sync with https://github.com/dotnet/runtime/blob/main/docs/project/list-of-diagnostics.md
    public partial class StreamSourceGenerator
    {
        // Stream does not support read or write
        private static readonly DiagnosticDescriptor s_TypeDoesNotImplementReadOrWrite =
            new DiagnosticDescriptor(
                id: "SYSLIB1301",
                title: "Type does not implement any Read or Write",
                messageFormat: "'{0}' does not implement any Read or Write method",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        // Sync-over-async or async-over-sync
        private static readonly DiagnosticDescriptor s_ReadDoingSyncOverAsync =
            new DiagnosticDescriptor(
                id: "SYSLIB1302",
                title: "Stream does Read as sync-over-async",
                messageFormat: "'{0}' does not implement any Read method and hence is doing sync-over-async",
                category: "StreamSourceGen", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ReadAsyncDoingAsyncOverSync =
            new DiagnosticDescriptor(
                id: "SYSLIB1303",
                title: "Stream does ReadAsync as async-over-sync",
                messageFormat: "'{0}' does not implement any ReadAsync method and hence is doing async-over-sync",
                category: "StreamSourceGen", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_WriteDoingSyncOverAsync =
            new DiagnosticDescriptor(
                id: "SYSLIB1304",
                title: "Stream does Write as sync-over-async",
                messageFormat: "'{0}' does not implement any Write method and hence is doing sync-over-async",
                category: "StreamSourceGen", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_WriteAsyncDoingAsyncOverSync =
            new DiagnosticDescriptor(
                id: "SYSLIB1305",
                title: "Stream does WriteAsync as async-over-sync",
                messageFormat: "'{0}' does not implement any WriteAsync method and hence is doing async-over-sync",
                category: "StreamSourceGen", DiagnosticSeverity.Info, isEnabledByDefault: true);

        // Consider Span/Memory overloads for better performance
        private static readonly DiagnosticDescriptor s_ConsiderImplementingReadSpan =
            new DiagnosticDescriptor(
                id: "SYSLIB1306",
                title: "Consider implementing Span-based Read",
                messageFormat: "'{0}' does not implement Read(Span<byte>), for better performance, consider providing an implementation for it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderImplementingWriteSpan =
            new DiagnosticDescriptor(
                id: "SYSLIB1307",
                title: "Consider implementing Span-based Write",
                messageFormat: "'{0}' does not implement Write(ReadOnlySpan<byte>), for better performance, consider providing an implementation for it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderImplementingReadAsyncMemory =
            new DiagnosticDescriptor(
                id: "SYSLIB1308",
                title: "Consider implementing Memory-based ReadAsync",
                messageFormat: "'{0}' does not implement ReadAsync(Memory<byte>, CancellationToken), for better performance, consider providing an implementation for it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderImplementingWriteAsyncMemory =
            new DiagnosticDescriptor(
                id: "SYSLIB1309",
                title: "Consider implementing Memory-based WriteAsync",
                messageFormat: "'{0}' does not implement WriteAsync(ReadOnlyMemory<byte>, CancellationToken), for better performance, consider providing an implementation for it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        // Avoid implementing APM (Begin/End) methods
        private static readonly DiagnosticDescriptor s_AvoidBeginReadEndRead =
            new DiagnosticDescriptor(
                id: "SYSLIB1310",
                title: "Avoid BeingRead or EndRead",
                messageFormat: "'{0}' implements BeginRead or EndRead, Task-based methods should be preferred when possible, consider removing them to allow the source generator to emit an implementation based on Task-based ReadAsync",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_AvoidBeginWriteEndWrite =
            new DiagnosticDescriptor(
                id: "SYSLIB1311",
                title: "Avoid BeingWrite or EndWrite",
                messageFormat: "'{0}' implements BeginWrite or EndWrite, Task-based methods should be preferred when possible, consider removing them to allow the source generator to emit an implementation based on Task-based WriteAsync",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderImplementingFlush =
            new DiagnosticDescriptor(
                id: "SYSLIB1312",
                title: "Consider implementing Flush() to move any buffered data to its destination",
                messageFormat: "'{0}' does not implement Flush but it implements one or more Write method(s), Consider implementing Flush() to move any buffered data to its destination, clear the buffer, or both",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        // Consider removing array-based overloads if span-based are available
        private static readonly DiagnosticDescriptor s_ConsiderRemovingReadBytes =
            new DiagnosticDescriptor(
                id: "SYSLIB1313",
                title: "Consider removing array-based Read",
                messageFormat: "'{0}' implements Read(Span<byte>), consider removing Read(byte[], int, int) to let the source generator handle it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderRemovingReadAsyncBytes =
            new DiagnosticDescriptor(
                id: "SYSLIB1314",
                title: "Consider removing array-based ReadAsync",
                messageFormat: "'{0}' implements ReadAsync(Memory<byte>, CancellationToken), consider removing ReadAsync(byte[], int, int, CancellationToken) to let the source generator handle it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderRemovingWriteBytes =
            new DiagnosticDescriptor(
                id: "SYSLIB1315",
                title: "Consider removing array-based Write",
                messageFormat: "'{0}' implements Write(ReadOnlySpan<byte>), consider removing Write(byte[], int, int) to let the source generator handle it",
                category: "StreamSourceGen",
                DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor s_ConsiderRemovingWriteAsyncBytes =
            new DiagnosticDescriptor(
                id: "SYSLIB1316",
                title: "Consider removing array-based WriteAsync",
                messageFormat: "'{0}' implements WriteAsync(ReadOnlyMemory<byte>, CancellationToken), consider removing WriteAsync(byte[], int, int, CancellationToken) to let the source generator handle it",
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

            bool syncOverAsync = readInfo.GetSyncPreferredMember().IsAsync();
            bool asyncOverSync = !readInfo.GetAsyncPreferredMember().IsAsync();
            Debug.Assert(syncOverAsync != asyncOverSync || (!syncOverAsync && !asyncOverSync), 
                "We can have async-over-sync, sync-over-async, or none, but never both");

            if (syncOverAsync)
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ReadDoingSyncOverAsync, streamTypeInfo));
            }
            else if (!streamTypeInfo.OverriddenMembers.Contains(StreamMember.ReadSpan))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingReadSpan, streamTypeInfo));
            }
            else if (streamTypeInfo.OverriddenMembers.Contains(StreamMember.ReadBytes))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderRemovingReadBytes, streamTypeInfo));
            }

            if (asyncOverSync)
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ReadAsyncDoingAsyncOverSync, streamTypeInfo));
            }
            else if (!streamTypeInfo.OverriddenMembers.Contains(StreamMember.ReadAsyncMemory))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingReadAsyncMemory, streamTypeInfo));
            }
            else if (streamTypeInfo.OverriddenMembers.Contains(StreamMember.ReadAsyncBytes))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderRemovingReadAsyncBytes, streamTypeInfo));
            }
        }

        private static void ReportWriteDiagnostics(SourceProductionContext context, StreamTypeInfo streamTypeInfo)
        {
            Debug.Assert(streamTypeInfo.CanWrite);
            Debug.Assert(streamTypeInfo.WriteInfo is not null);
            StreamCapabilityInfo writeInfo = streamTypeInfo.WriteInfo!;

            bool syncOverAsync = writeInfo.GetSyncPreferredMember().IsAsync();
            bool asyncOverSync = !writeInfo.GetAsyncPreferredMember().IsAsync();
            Debug.Assert(syncOverAsync != asyncOverSync || (!syncOverAsync && !asyncOverSync),
                "We can have async-over-sync, sync-over-async, or none, but never both");

            if (syncOverAsync)
            {
                context.ReportDiagnostic(CreateDiagnostic(s_WriteDoingSyncOverAsync, streamTypeInfo));
            }
            else if (!streamTypeInfo.OverriddenMembers.Contains(StreamMember.WriteSpan))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingWriteSpan, streamTypeInfo));
            }
            else if (streamTypeInfo.OverriddenMembers.Contains(StreamMember.WriteBytes))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderRemovingWriteBytes, streamTypeInfo));
            }

            if (asyncOverSync)
            {
                context.ReportDiagnostic(CreateDiagnostic(s_WriteAsyncDoingAsyncOverSync, streamTypeInfo));
            }
            else if (!streamTypeInfo.OverriddenMembers.Contains(StreamMember.WriteAsyncMemory))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingWriteAsyncMemory, streamTypeInfo));
            }
            else if (streamTypeInfo.OverriddenMembers.Contains(StreamMember.WriteAsyncBytes))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderRemovingWriteAsyncBytes, streamTypeInfo));
            }

            if (!streamTypeInfo.OverriddenMembers.Contains(StreamMember.Flush))
            {
                context.ReportDiagnostic(CreateDiagnostic(s_ConsiderImplementingFlush, streamTypeInfo));
            }
        }

        private static Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, StreamTypeInfo streamTypeInfo)
        {
            Location? location = null;
            IEnumerable<Location>? additionalLocations = null;

            // TODO: improve this to ensure that the First element
            // is the definition with the attribute.
            // S.T.Json does a similar thing, so verify how this works with multiple partial definitions.
            ImmutableArray<Location> locations = streamTypeInfo.TypeSymbol.Locations;
            if (locations.Length > 1)
            {
                location = locations[0];
                additionalLocations = locations.Skip(1);
            }
            else if (locations.Length == 1)
            {
                location = locations[0];
            }

            Debug.Assert(location is not null);

            return Diagnostic.Create(descriptor, location, additionalLocations, streamTypeInfo.TypeSymbol.Name);
        }
    }
}
