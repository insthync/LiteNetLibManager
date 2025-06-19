namespace LiteNetLibManager
{
    public class RttCalculator
    {
        public long Rtt { get; internal set; }
        public long LocalTimestamp { get => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
        public long PeerTimestamp { get => LocalTimestamp + _timestampOffsets; }

        private int _rttCount;
        private long _totalRtt;
        private long _latestPongTime;
        private long _timestampOffsets;

        public void OnPong(PongMessage message)
        {
            long rtt = LocalTimestamp - message.pingTime;
            if (_rttCount > 10)
            {
                _totalRtt = Rtt;
                _rttCount = 1;
            }
            _totalRtt += rtt;
            _rttCount++;
            Rtt = _totalRtt / _rttCount;
            // Calculate time offsets by peer time, local time and RTT
            if (_latestPongTime > message.pongTime)
                return;
            _latestPongTime = message.pongTime;
            long newTimestamp = message.pongTime + (Rtt / 2);
            _timestampOffsets = newTimestamp - LocalTimestamp;
        }

        public void Reset()
        {
            Rtt = 0;
            _latestPongTime = 0;
            _totalRtt = 0;
            _rttCount = 0;
            _timestampOffsets = 0;
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
