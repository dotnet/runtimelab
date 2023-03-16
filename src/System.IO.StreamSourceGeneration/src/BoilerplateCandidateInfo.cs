// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using BpConsts = System.IO.StreamSourceGeneration.StreamBoilerplateConstants;
using MConsts = System.IO.StreamSourceGeneration.StreamMembersConstants;

namespace System.IO.StreamSourceGeneration
{
    internal readonly struct BoilerplateCandidateInfo
    {
        private static readonly List<BoilerplateCandidateInfo> s_boilerplateGenerationCandidates = new List<BoilerplateCandidateInfo>
        {
            // Read
            new(MConsts.ReadBytes, StreamMember.ReadBytes, BpConsts.ReadBytesTemplate, BpConsts.ReadBytesUnsupported),
            new(MConsts.ReadSpan, StreamMember.ReadSpan, BpConsts.ReadSpanTemplate, BpConsts.ReadSpanUnsupported, hasPriority: true),
            new(MConsts.ReadAsyncBytes, StreamMember.ReadAsyncBytes, BpConsts.ReadAsyncBytesTemplate, BpConsts.ReadAsyncBytesUnsupported),
            new(MConsts.ReadAsyncMemory, StreamMember.ReadAsyncMemory, BpConsts.ReadAsyncMemoryTemplate, BpConsts.ReadAsyncMemoryUnsupported, true),
            // Write
            new(MConsts.WriteBytes, StreamMember.WriteBytes, BpConsts.WriteBytesTemplate, BpConsts.WriteBytesUnsupported),
            new(MConsts.WriteSpan, StreamMember.WriteSpan, BpConsts.WriteSpanTemplate, BpConsts.WriteSpanUnsupported, true),
            new(MConsts.WriteAsyncBytes, StreamMember.WriteAsyncBytes, BpConsts.WriteAsyncBytesTemplate, BpConsts.WriteAsyncBytesUnsupported),
            new(MConsts.WriteAsyncMemory, StreamMember.WriteAsyncMemory, BpConsts.WriteAsyncMemoryTemplate, BpConsts.WriteAsyncMemoryUnsupported, true),
            // Others
            new(MConsts.ReadByte, StreamMember.ReadByte, BpConsts.ReadByteCallsToReadSpan, null),
            new(MConsts.WriteByte, StreamMember.WriteByte, BpConsts.WriteByteCallsToWriteSpan, null),
            new(MConsts.CanRead, StreamMember.CanRead, BpConsts.CanRead, BpConsts.CanReadUnsupported),
            new(MConsts.CanSeek, StreamMember.CanSeek, BpConsts.CanSeek, BpConsts.CanSeekUnsupported),
            new(MConsts.CanWrite, StreamMember.CanWrite, BpConsts.CanWrite, BpConsts.CanWriteUnsupported),
            new(MConsts.BeginRead, StreamMember.BeginRead, BpConsts.BeginRead, BpConsts.BeginReadUnsupported),
            new(MConsts.BeginWrite, StreamMember.BeginWrite, BpConsts.BeginWrite, BpConsts.BeginWriteUnsupported),
            new(MConsts.EndRead, StreamMember.EndRead, BpConsts.EndRead, BpConsts.EndReadUnsupported),
            new(MConsts.EndWrite, StreamMember.EndWrite, BpConsts.EndWrite, BpConsts.EndWriteUnsupported),
            new(MConsts.Seek, StreamMember.Seek, null, BpConsts.SeekUnsupported),
            new(MConsts.SetLength, StreamMember.SetLength, null, BpConsts.SetLengthUnsupported),
            new(MConsts.Length, StreamMember.Length, null, BpConsts.LengthUnsupported),
            new(MConsts.Position, StreamMember.Position, null, BpConsts.PositionUnsupported),
            new(MConsts.Flush, StreamMember.Flush, BpConsts.Flush, null),
        };

        private static readonly Dictionary<string, BoilerplateCandidateInfo> s_lookupBoilerplateGenerationCandidates = CandidatesList.ToDictionary(x => x.Name);

        internal readonly string Name;
        internal readonly StreamMember StreamMember;
        internal readonly string? Boilerplate;
        internal readonly string? BoilerplateForUnsupported;
        internal readonly bool HasPriority;

        internal BoilerplateCandidateInfo(string name, StreamMember operation, string? boilerplate, string? boilerplateForUnsupported, bool hasPriority = false)
        {
            Name = name;
            StreamMember = operation;
            Boilerplate = boilerplate;
            BoilerplateForUnsupported = boilerplateForUnsupported;
            HasPriority = hasPriority;
        }

        internal static Dictionary<string, BoilerplateCandidateInfo> CandidatesDictionary => s_lookupBoilerplateGenerationCandidates;
        internal static List<BoilerplateCandidateInfo> CandidatesList => s_boilerplateGenerationCandidates;
    }
}