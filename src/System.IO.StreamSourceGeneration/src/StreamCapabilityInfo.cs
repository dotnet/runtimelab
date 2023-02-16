// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.IO.StreamSourceGeneration
{
    internal class StreamCapabilityInfo
    {
        internal StreamMember? SyncPreferredMember { get; set; }
        internal StreamMember? AsyncPreferredMember { get; set; }

        public StreamCapabilityInfo(StreamMember preferredMember, bool isAsync)
        {
            if (isAsync)
            {
                AsyncPreferredMember = preferredMember;
            }
            else
            {
                SyncPreferredMember = preferredMember;
            }
        }

        internal void EvaluatePreferredMember(StreamMember member, bool isAsync, BoilerplateCandidateInfo candidateInfo)
        {
            if (isAsync)
            {
                if (AsyncPreferredMember == null || candidateInfo.HasPriority)
                {
                    AsyncPreferredMember = member;
                }
            }
            else
            {
                if (SyncPreferredMember == null || candidateInfo.HasPriority)
                {
                    SyncPreferredMember = member;
                }
            }
        }

        internal StreamMember GetPreferredMember(bool isAsync)
        {
            StreamMember? retVal = isAsync ?
                AsyncPreferredMember ?? SyncPreferredMember :
                SyncPreferredMember ?? AsyncPreferredMember;

            Debug.Assert(retVal != null, "Both properties can't be null.");
            return retVal!.Value;
        }
    }
}