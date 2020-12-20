// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Quic.Implementations.Managed;
using System.Net.Quic.Implementations.Managed.Internal;
using System.Net.Quic.Implementations.Managed.Internal.Frames;
using System.Net.Quic.Implementations.Managed.Internal.Parsing;
using System.Net.Quic.Tests.Harness;
using Xunit;
using Xunit.Abstractions;
using AckFrame = System.Net.Quic.Tests.Harness.AckFrame;
using AckFrameImpl = System.Net.Quic.Implementations.Managed.Internal.Frames.AckFrame;
using StreamFrame = System.Net.Quic.Tests.Harness.StreamFrame;

namespace System.Net.Quic.Tests.Frames
{
    public class AckFrameTests : ManualTransmissionQuicTestBase
    {
        public AckFrameTests(ITestOutputHelper output)
            : base(output)
        {
            // all tests start after connection has been established
            EstablishConnection();
        }

        [Fact]
        public void ConnectionCloseWhenAckingFuturePacket()
        {
            Client.Ping();
            Intercept1RttFrame<AckFrame>(Client, Server, ack =>
            {
                // ack one more than intended
                ack.LargestAcknowledged++;
                ack.FirstAckRange++;
            });

            Send1Rtt(Server, Client)
                .ShouldHaveConnectionClose(TransportErrorCode.ProtocolViolation,
                    QuicError.InvalidAckRange,
                    FrameType.Ack);
        }

        [Fact]
        public void ConnectionCloseWhenAckingNegativePacket()
        {
            Client.Ping();
            Intercept1RttFrame<AckFrame>(Client, Server, ack =>
            {
                // ack one more than intended
                ack.FirstAckRange++;
            });

            Send1Rtt(Server, Client)
                .ShouldHaveConnectionClose(TransportErrorCode.ProtocolViolation,
                    QuicError.InvalidAckRange,
                    FrameType.Ack);
        }

        [Fact]
        public void TestNotAckingPastFrames()
        {
            // since PING frames are ack-eliciting, the endpoint should always send an ack frame, leading to each endpoint always acking only the last received packet.
            var sender = Client;
            var receiver = Server;
            for (int i = 0; i < 3; i++)
            {
                sender.Ping();
                var flight = SendFlight(sender, receiver);
                var packet = Assert.IsType<OneRttPacket>(flight.Packets[0]);
                var ack = Assert.Single(packet.Frames.OfType<AckFrame>());

                Assert.Equal(0u, ack.FirstAckRange);
                Assert.Empty(ack.AckRanges);

                var tmp = sender;
                sender = receiver;
                receiver = tmp;
            }
        }

        [Fact]
        public void TestSerializeAckRanges()
        {
            // make sure the end has enough consecutive PNs to guarantee that earlier packets are determined lost
            var received = new[] {2, 3, 4, 7, 8, 10, 13, 14, 15, 16};

            long last = received[^1];

            long ackDelay = 1245;

            RangeSet toAck = new RangeSet();
            foreach (int i in received)
            {
                toAck.Add(i);
            }

            Span<byte> destination = stackalloc byte[128];
            (int bytesWritten, int rangesWritten) = AckFrameImpl.EncodeAckRanges(destination, toAck);

            AckFrameImpl frame = new(last, ackDelay, rangesWritten, toAck[^1].Length - 1,
                destination.Slice(0, bytesWritten), false, 0, 0, 0);

            int read = 0;
            List<AckFrame.AckRange> actual = new List<AckFrame.AckRange>();
            while (read < frame.AckRangesRaw.Length)
            {
                read += QuicPrimitives.TryReadVarInt(frame.AckRangesRaw.Slice(read), out long gap);
                read += QuicPrimitives.TryReadVarInt(frame.AckRangesRaw.Slice(read), out long ack);
                actual.Add(new AckFrame.AckRange{ Acked = ack, Gap = gap});
            }
            Span<RangeSet.Range> decoded = stackalloc RangeSet.Range[toAck.Count];
            Assert.True(frame.TryDecodeAckRanges(decoded));

            Assert.Equal(new[]
            {
                // remember that all numbers are encoded as 1 lesser, and encoding starts from the largest
                new AckFrame.AckRange {Acked = 0, Gap = 1}, // 10
                new AckFrame.AckRange {Acked = 1, Gap = 0}, // 7, 8
                new AckFrame.AckRange {Acked = 2, Gap = 1}, // 2, 3, 4
            }, actual);
        }
    }
}
