// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.IO.StreamSourceGeneration
{
    internal class StreamCapabilityInfo
    {
        private StreamMember? _syncPreferredMember;
        private StreamMember? _asyncPreferredMember;

        public StreamCapabilityInfo(StreamMember preferredMember, bool isAsync)
        {
            if (isAsync)
            {
                _asyncPreferredMember = preferredMember;
            }
            else
            {
                _syncPreferredMember = preferredMember;
            }
        }

        internal void EvaluatePreferredMember(StreamMember member, bool isAsync, BoilerplateCandidateInfo candidateInfo)
        {
            if (isAsync)
            {
                if (_asyncPreferredMember == null || candidateInfo.HasPriority)
                {
                    _asyncPreferredMember = member;
                }
            }
            else
            {
                if (_syncPreferredMember == null || candidateInfo.HasPriority)
                {
                    _syncPreferredMember = member;
                }
            }
        }

        internal StreamMember GetSyncPreferredMember()
        {
            StreamMember? retVal = _syncPreferredMember ?? _asyncPreferredMember;
            Debug.Assert(retVal != null, "Both properties can't be null.");
            return retVal!.Value;
        }

        internal StreamMember GetAsyncPreferredMember()
        {
            StreamMember? retVal = _asyncPreferredMember ?? _syncPreferredMember;
            Debug.Assert(retVal != null, "Both properties can't be null.");
            return retVal!.Value;
        }
    }
}