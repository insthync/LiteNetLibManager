namespace LiteNetLibManager
{
    public class RttCalculator
    {
        public long Rtt { get; internal set; }
        public long LastPongTime { get; internal set; }
        public long TotalRtt { get; internal set; }
        public int RttCount { get; internal set; }
        public long TimestampOffsets { get; internal set; }
        public long LocalTimestamp { get => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
        public long PeerTimestamp { get => LocalTimestamp + TimestampOffsets; }

        public void OnPong(PongMessage message)
        {
            if (LastPongTime < message.pongTime)
            {
                LastPongTime = message.pongTime;
                long rtt = LocalTimestamp - message.pingTime;
                if (RttCount > 10)
                {
                    TotalRtt = Rtt;
                    RttCount = 1;
                }
                TotalRtt += rtt;
                RttCount++;
                Rtt = TotalRtt / RttCount;
                // Calculate time offsets by peer time, local time and RTT
                TimestampOffsets = message.pongTime - LocalTimestamp + (Rtt / 2);
            }
        }

        public void Reset()
        {
            Rtt = 0;
            LastPongTime = 0;
            TotalRtt = 0;
            RttCount = 0;
            TimestampOffsets = 0;
        }

        public PingMessage GetPingMessage()
        {
            return new PingMessage()
            {
                pingTime = LocalTimestamp,
            };
        }

        public PongMessage GetPongMessage(PingMessage pingMessage)
        {
            return new PongMessage()
            {
                pingTime = pingMessage.pingTime,
                pongTime = LocalTimestamp,
            };
        }
    }
}
