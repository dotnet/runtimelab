// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace System.IO.StreamSourceGeneration
{
    internal class StreamTypeInfo
    {
        internal INamedTypeSymbol TypeSymbol { get; }
        internal HashSet<StreamMember> OverriddenMembers { get; }
        internal StreamCapabilityInfo? ReadInfo { get; }
        internal StreamCapabilityInfo? WriteInfo { get; }
        internal bool CanRead => ReadInfo is not null;
        internal bool CanWrite => WriteInfo is not null;
        internal bool CanSeek => OverriddenMembers.Contains(StreamMember.Seek);

        public StreamTypeInfo(INamedTypeSymbol typeSymbol)
        {
            TypeSymbol = typeSymbol;
            OverriddenMembers = new HashSet<StreamMember>();

            foreach (string memberName in GetOverriddenMembers(typeSymbol))
            {
                if (!BoilerplateCandidateInfo.CandidatesDictionary.TryGetValue(memberName, out BoilerplateCandidateInfo candidateInfo))
                {
                    continue;
                }

                StreamMember streamMember = candidateInfo.StreamMember;
                OverriddenMembers.Add(streamMember);

                if (streamMember.IsRead())
                {
                    if (ReadInfo == null)
                    {
                        ReadInfo = new StreamCapabilityInfo(streamMember, streamMember.IsAsync());
                    }
                    else
                    {
                        ReadInfo.EvaluatePreferredMember(streamMember, streamMember.IsAsync(), candidateInfo);
                    }
                }
                else if (streamMember.IsWrite())
                {
                    if (WriteInfo == null)
                    {
                        WriteInfo = new StreamCapabilityInfo(streamMember, streamMember.IsAsync());
                    }
                    else
                    {
                        WriteInfo.EvaluatePreferredMember(streamMember, streamMember.IsAsync(), candidateInfo);
                    }
                }
            }
        }

        internal static IEnumerable<string> GetOverriddenMembers(ITypeSymbol symbol)
        {
            return symbol.GetMembers().Select(m => GetOverriddenMember(m)?.ToDisplayString()).Where(s => s != null)!;

            static ISymbol? GetOverriddenMember(ISymbol member)
                => member switch
                {
                    IMethodSymbol method => method.OverriddenMethod,
                    IPropertySymbol property => property.OverriddenProperty,
                    _ => null
                };
        }
    }
}