// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.IO.StreamSourceGeneration
{
    internal class StreamCapabilityInfo
    {
        internal string? SyncPreferredMemberName { get; set; }
        internal string? AsyncPreferredMemberName { get; set; }

        public StreamCapabilityInfo(string preferredMemberName, bool isAsync)
        {
            if (isAsync)
            {
                AsyncPreferredMemberName = preferredMemberName;
            }
            else
            {
                SyncPreferredMemberName = preferredMemberName;
            }
        }

        internal void SetPreferredMemberName(string memberName, bool isAsync, BoilerplateCandidateInfo candidateInfo)
        {
            if (isAsync)
            {
                if (AsyncPreferredMemberName == null || candidateInfo.HasPriority)
                {
                    AsyncPreferredMemberName = memberName;
                }
            }
            else
            {
                if (SyncPreferredMemberName == null || candidateInfo.HasPriority)
                {
                    SyncPreferredMemberName = memberName;
                }
            }
        }

        internal string GetPreferredMemberName(bool isAsync)
        {
            string? retVal = isAsync ?
                AsyncPreferredMemberName ?? SyncPreferredMemberName :
                SyncPreferredMemberName ?? AsyncPreferredMemberName;

            Debug.Assert(retVal != null, "Both properties can't be null.");
            return retVal!;
        }
    }
}