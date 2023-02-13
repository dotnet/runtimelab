using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace System.IO.StreamSourceGeneration;

internal class StreamTypeInfo
{
    internal INamedTypeSymbol TypeSymbol { get; }
    internal HashSet<string> OverriddenMembers { get; }
    internal StreamCapabilityInfo? ReadInfo { get; }
    internal StreamCapabilityInfo? WriteInfo { get; }

    public StreamTypeInfo(INamedTypeSymbol typeSymbol)
    {
        TypeSymbol = typeSymbol;
        OverriddenMembers = new HashSet<string>();
            
        foreach (string memberName in Helpers.GetOverriddenMembers(typeSymbol))
        {
            OverriddenMembers.Add(memberName);

            if (!BoilerplateCandidateInfo.CandidatesDictionary.TryGetValue(memberName, out BoilerplateCandidateInfo candidateInfo))
            {
                continue;
            }

            if (candidateInfo.OperationKind is StreamOperationKind.Read or StreamOperationKind.ReadAsync)
            {
                bool isAsync = candidateInfo.OperationKind == StreamOperationKind.ReadAsync;

                if (ReadInfo == null)
                {
                    ReadInfo = new StreamCapabilityInfo(memberName, isAsync);
                }
                else
                {
                    ReadInfo.SetPreferredMemberName(memberName, isAsync, candidateInfo);
                }
            }
            else if (candidateInfo.OperationKind is StreamOperationKind.Write or StreamOperationKind.WriteAsync)
            {
                bool isAsync = candidateInfo.OperationKind == StreamOperationKind.WriteAsync;

                if (WriteInfo == null)
                {
                    WriteInfo = new StreamCapabilityInfo(memberName, isAsync);
                }
                else
                {
                    WriteInfo.SetPreferredMemberName(memberName, isAsync, candidateInfo);
                }
            }
        }
    }
}
