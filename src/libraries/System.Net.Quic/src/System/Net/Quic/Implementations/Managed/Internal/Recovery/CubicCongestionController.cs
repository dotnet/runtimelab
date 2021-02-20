// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net.Quic.Implementations.Managed.Internal.Packets;
using System.Net.Quic.Implementations.Managed.Internal.Tracing;

namespace System.Net.Quic.Implementations.Managed.Internal.Recovery
{
    /// <summary>
    ///     Adaptation of the CUBIC congestion control algorithm for QUIC, based on [RFC8321].
    ///     TODO-RZ: This implementation is incomplete!
    /// </summary>
    internal class CubicCongestionController : ICongestionController
    {
        /// <summary>
        ///     [Beta] Reduction in congestion window when a new loss event is detected. The RECOMMENDED value is 0.7.
        /// </summary>
        private const double LossReductionFactor = 0.7;

        /// <summary>
        ///     [C] Constant denoting the aggressiveness of the loss recovery. It SHOULD be set to 0.4
        /// </summary>
        private const double AggressivenessConstant = 0.4;

        /// <summary>
        ///     [W_max] Maximum size of congestion window before reduction.
        ///     Contrary to RFC, this value is in bytes, not in multiples of maximum datagram size.
        /// </summary>
        private int MaxCongestionWindow { get; set; }

        /// <summary>
        ///     [W_last_max] Previous maximum size of congestion window before reduction.
        ///     Contrary to RFC, this value is in bytes, not in multiples of maximum datagram size.
        /// </summary>
        private int LastMaxCongestionWindow { get; set; }

        /// <summary>
        /// The time period that the above function takes to increase the current window size
        /// to <see cref="MaxCongestionWindow"/> if there are no further congestion events.
        /// </summary>
        private double RecoveryPeriod { get; set; }

        /// <summary>
        ///     Timestamp of last sent packet.
        /// </summary>
        public long LastSendTime { get; set; }


        public void OnPacketSent(RecoveryController recovery, SentPacket packet)
        {
            // See https://github.com/torvalds/linux/commit/30927520dbae297182990bb21d08762bcc35ce1d

            if (LastSendTime > 0 && recovery.BytesInFlight == 0)
            {
                // first transmit when no packets in flight previously (application was idle for a while)
                if (recovery.CongestionRecoveryStartTime > 0)
                {
                    long delta = packet.TimeSent - LastSendTime;
                    if (delta > 0) // do this only for the first packet in a batch
                    {
                        // shift epoch start to keep congestion window growth to cubic curve
                        recovery.CongestionRecoveryStartTime += delta;
                    }
                }
            }

            LastSendTime = packet.TimeSent;

            NewRenoCongestionController.Instance.OnPacketSent(recovery, packet);
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
                recovery.CongestionState = CongestionState.SlowStart;
                recovery.CongestionWindow += packet.BytesSent;
            }
            else
            {
                recovery.CongestionState = CongestionState.CongestionAvoidance;
                Debug.Assert(MaxCongestionWindow > 0);

                // W_cubic(t) = C*(t-K)^3 + W_max (Eq. 1)
                // we need to compute W_cubic(t+RTT);

                // [t]
                long timeInRecovery = now - recovery.CongestionRecoveryStartTime;

                // TODO-RZ: limit by ACKs if in TCP-friendly region

                // phase is [t-K (+RTT)], in seconds
                double phase = Timestamp.GetMillisecondsDouble(
                    timeInRecovery + recovery.SmoothedRtt) / 1000 - RecoveryPeriod;
                phase = phase * phase * phase;
                int cubicWindow =
                    (int) (MaxCongestionWindow +
                           RecoveryController.MaxDatagramSize * AggressivenessConstant * phase);

                int aimdWindow = (int) (MaxCongestionWindow * LossReductionFactor +
                                        3 * (1 - LossReductionFactor) / (1 + LossReductionFactor)
                                        * timeInRecovery / recovery.SmoothedRtt);

                recovery.CongestionWindow = Math.Max(cubicWindow, aimdWindow);
                recovery.CongestionWindow = Math.Max(recovery.CongestionWindow, RecoveryController.MinimumWindowSize);
            }
        }


        private void CollapseCongestionWindow(RecoveryController recovery)
        {
            MaxCongestionWindow = recovery.CongestionWindow;
            LastMaxCongestionWindow = MaxCongestionWindow;

            // reduce slow start threshold using the reduction factor
            recovery.SlowStartThreshold = Math.Max(
                RecoveryController.MinimumWindowSize,
                (int)(recovery.CongestionWindow * LossReductionFactor));

            recovery.CongestionRecoveryStartTime = 0;
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
            MaxCongestionWindow = 0;
            LastMaxCongestionWindow = 0;
            RecoveryPeriod = 0;
            LastSendTime = 0;
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
            // start a new congestion event only if the lost packet was sent after the start of the
            // previous congestion recovery period
            if (recovery.InCongestionRecovery(sentTimestamp))
                return;

            recovery.CongestionRecoveryStartTime = now;

            // Fast convergence
            if (MaxCongestionWindow < LastMaxCongestionWindow) // should we make room for others?
            {
                LastMaxCongestionWindow = MaxCongestionWindow;
                // further reduce the max window
                MaxCongestionWindow = (int) (MaxCongestionWindow * (1 + LossReductionFactor) / 2.0);
            }
            else
            {
                LastMaxCongestionWindow = MaxCongestionWindow;
            }

            // Multiplicative decrease
            MaxCongestionWindow = recovery.CongestionWindow;
            recovery.CongestionWindow = (int)(MaxCongestionWindow * LossReductionFactor);
            recovery.SlowStartThreshold = Math.Max(recovery.CongestionWindow, RecoveryController.MinimumWindowSize);

            // K = cubic_root(W_max*(1-beta_cubic)/C) (Eq. 2)
            // the W_max in the equation is in multiples of minimum segment size
            double W_max = MaxCongestionWindow / (double) RecoveryController.MaxDatagramSize;

            // TODO-RZ: use more efficient function for cube root
            RecoveryPeriod = Math.Pow(
                W_max * (1 - LossReductionFactor) / AggressivenessConstant,
                1.0 / 3);
        }
    }
}
