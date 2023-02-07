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
            
        foreach (string memberName in StreamSourceGen.GetOverriddenMembers(typeSymbol))
        {
            if (memberName.Contains("Read") && memberName != StreamMembersConstants.CanRead)
            {
                BoilerplateCandidateInfo candidateInfo = BoilerplateCandidateInfo.CandidatesDictionary[memberName];
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
            else if (memberName.Contains("Write") && memberName != StreamMembersConstants.CanWrite)
            {
                BoilerplateCandidateInfo candidateInfo = BoilerplateCandidateInfo.CandidatesDictionary[memberName];
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

            OverriddenMembers.Add(memberName);
        }
    }
}
