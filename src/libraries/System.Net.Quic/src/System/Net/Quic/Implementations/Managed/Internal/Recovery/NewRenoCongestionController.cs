// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net.Quic.Implementations.Managed.Internal.Packets;
using System.Net.Quic.Implementations.Managed.Internal.Tracing;

namespace System.Net.Quic.Implementations.Managed.Internal.Recovery
{
    /// <summary>
    ///     Implementation of the reference congestion control algorithm for QUIC based on the [RECOVERY] draft.
    /// </summary>
    internal class NewRenoCongestionController : ICongestionController
    {
        internal static readonly NewRenoCongestionController Instance = new NewRenoCongestionController();

        private NewRenoCongestionController()
        {
        }

        /// <summary>
        ///     Reduction in congestion window when a new loss event is detected. The RECOMMENDED value is 0.5.
        /// </summary>
        internal const double LossReductionFactor = 0.5;

        /// <summary>
        ///     Period of time for persistent congestion to be established, specified as the PTO multiplier.
        /// </summary>
        internal const int PersistentCongestionThreshold = 3;

        public void OnPacketSent(RecoveryController recovery, SentPacket packet)
        {
            Debug.Assert(packet.InFlight);
            recovery.BytesInFlight += packet.BytesSent;
        }

        public void OnPacketAcked(RecoveryController recovery, SentPacket packet, long now)
        {
            Debug.Assert(packet.InFlight);
            recovery.BytesInFlight -= packet.BytesSent;

            if (recovery.InCongestionRecovery(packet.TimeSent))
            {
                // do not increase congestion window in recovery period.
                return;
            }

            // TODO-RZ: Do not increase congestion window if limited by flow control or application has not supplied
            // enough data to saturate the connection
            if (recovery.IsApplicationOrFlowControlLimited)
                return;

            if (recovery.CongestionWindow < recovery.SlowStartThreshold)
            {
                // slow start
                recovery.CongestionWindow += packet.BytesSent;
                recovery.CongestionState = CongestionState.SlowStart;
            }
            else
            {
                // congestion avoidance
                recovery.CongestionWindow += RecoveryController.MaxDatagramSize * packet.BytesSent / recovery.CongestionWindow;
                recovery.CongestionState = CongestionState.CongestionAvoidance;
            }
        }

        private void CollapseCongestionWindow(RecoveryController recovery)
        {
            recovery.CongestionWindow = RecoveryController.MinimumWindowSize;
        }

        public void OnPacketsLost(RecoveryController recovery, Span<SentPacket> lostPackets, long now)
        {
            foreach (var packet in lostPackets)
            {
                Debug.Assert(packet.InFlight);
                recovery.BytesInFlight -= packet.BytesSent;
            }

            if (lostPackets.IsEmpty)
            {
                return;
            }

            var lastPacket = lostPackets[^1];

            OnCongestionEvent(recovery, lastPacket.TimeSent, now);

            if (InPersistentCongestion(lastPacket))
            {
                CollapseCongestionWindow(recovery);
            }
        }

        public void Reset()
        {
            // do nothing, we do not store any extra state here
        }

        private bool InPersistentCongestion(SentPacket largestLostPacket)
        {
            // var pto = SmoothedRtt.Ticks + Math.Max(4 * RttVariation.Ticks, Recovery.TimerGranularity.Ticks) +
            // MaxAckDelay.Ticks;
            // var congestionPeriod = pto * Recovery.PersistentCongestionThreshold;
            // TODO-RZ: determine if all packets in the time period before the newest lost packet, including the edges
            // are marked lost
            return false;
        }

        internal void OnCongestionEvent(RecoveryController recovery, long sentTimestamp, long now)
        {
            // start a new congestion event if packet was sent after the start of the previous congestion recovery period
            if (recovery.InCongestionRecovery(sentTimestamp))
                return;

            recovery.CongestionRecoveryStartTime = now;
            recovery.CongestionWindow = Math.Max(RecoveryController.MinimumWindowSize, (int)(recovery.CongestionWindow * LossReductionFactor));
            recovery.SlowStartThreshold = recovery.CongestionWindow;
        }
    }
}
