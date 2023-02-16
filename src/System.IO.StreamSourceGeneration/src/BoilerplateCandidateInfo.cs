// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Constants = System.IO.StreamSourceGeneration.StreamBoilerplateConstants;

namespace System.IO.StreamSourceGeneration
{
    internal readonly struct BoilerplateCandidateInfo
    {
        private static readonly List<BoilerplateCandidateInfo> s_boilerplateGenerationCandidates = new List<BoilerplateCandidateInfo>
        {
            // Read
            new(StreamMembersConstants.ReadByte, StreamOperationKind.Read, Constants.ReadByteTemplate, Constants.ReadByteArrayUnsupported),
            new(StreamMembersConstants.ReadSpan, StreamOperationKind.Read, Constants.ReadSpanTemplate, Constants.ReadSpanUnsupported, true),
            new(StreamMembersConstants.ReadAsyncByte, StreamOperationKind.ReadAsync, Constants.ReadAsyncByteTemplate, Constants.ReadAsyncByteArrayUnsupported),
            new(StreamMembersConstants.ReadAsyncMemory, StreamOperationKind.ReadAsync, Constants.ReadAsyncMemoryTemplate, Constants.ReadAsyncMemoryUnsupported, true),
            // Write
            new(StreamMembersConstants.WriteByte, StreamOperationKind.Write, Constants.WriteByteTemplate, Constants.WriteByteArrayUnsupported),
            new(StreamMembersConstants.WriteSpan, StreamOperationKind.Write, Constants.WriteSpanTemplate, Constants.WriteSpanUnsupported, true),
            new(StreamMembersConstants.WriteAsyncByte, StreamOperationKind.WriteAsync, Constants.WriteAsyncByteTemplate, Constants.WriteAsyncByteArrayUnsupported),
            new(StreamMembersConstants.WriteAsyncMemory, StreamOperationKind.WriteAsync, Constants.WriteAsyncMemoryTemplate, Constants.WriteAsyncMemoryUnsupported, true),
            // Others
            new(StreamMembersConstants.CanRead, StreamOperationKind.None, Constants.CanRead, Constants.CanReadUnsupported),
            new(StreamMembersConstants.CanSeek, StreamOperationKind.None, Constants.CanSeek, Constants.CanSeekUnsupported),
            new(StreamMembersConstants.CanWrite, StreamOperationKind.None, Constants.CanWrite, Constants.CanWriteUnsupported),
            new(StreamMembersConstants.BeginRead, StreamOperationKind.None, Constants.BeginRead, Constants.BeginReadUnsupported),
            new(StreamMembersConstants.BeginWrite, StreamOperationKind.None, Constants.BeginWrite, Constants.BeginWriteUnsupported),
            new(StreamMembersConstants.EndRead, StreamOperationKind.None, Constants.EndRead, Constants.EndReadUnsupported),
            new(StreamMembersConstants.EndWrite, StreamOperationKind.None, Constants.EndWrite, Constants.EndWriteUnsupported),
            new(StreamMembersConstants.Seek, StreamOperationKind.Seek, null, Constants.SeekUnsupported),
            new(StreamMembersConstants.SetLength, StreamOperationKind.None, null, Constants.SetLengthUnsupported),
            new(StreamMembersConstants.Length, StreamOperationKind.None, null, Constants.LengthUnsupported),
            new(StreamMembersConstants.Position, StreamOperationKind.None, null, Constants.PositionUnsupported),
        };

        private static readonly Dictionary<string, BoilerplateCandidateInfo> s_lookupBoilerplateGenerationCandidates = CandidatesList.ToDictionary(x => x.Name);

        internal readonly string Name;
        internal readonly StreamOperationKind OperationKind;
        internal readonly string? Boilerplate;
        internal readonly string BoilerplateForUnsupported;
        internal readonly bool HasPriority;

        internal BoilerplateCandidateInfo(string name, StreamOperationKind operation, string? boilerplate, string boilerplateForUnsupported, bool hasPriority = false)
        {
            Name = name;
            OperationKind = operation;
            Boilerplate = boilerplate;
            BoilerplateForUnsupported = boilerplateForUnsupported;
            HasPriority = hasPriority;
        }

        internal static Dictionary<string, BoilerplateCandidateInfo> CandidatesDictionary => s_lookupBoilerplateGenerationCandidates;
        internal static List<BoilerplateCandidateInfo> CandidatesList => s_boilerplateGenerationCandidates;
    }
}