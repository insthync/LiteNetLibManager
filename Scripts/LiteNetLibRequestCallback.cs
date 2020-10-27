using LiteNetLib.Utils;
using System;

namespace LiteNetLibManager
{
    public class LiteNetLibRequestCallback
    {
        public uint AckId { get; protected set; }
        public long RequestTime { get; protected set; }
        public long Duration { get; protected set; }
        public LiteNetLibRequestHandler RequestHandler { get; protected set; }

        public LiteNetLibRequestCallback(
            uint ackId,
            long duration,
            LiteNetLibRequestHandler requestHandler)
        {
            AckId = ackId;
            RequestTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Duration = duration;
            RequestHandler = requestHandler;
        }

        public bool ResponseTimeout()
        {
            if (Duration > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - RequestTime >= Duration)
            {
                RequestHandler.InvokeResponse(0, null, AckResponseCode.Timeout);
                return true;
            }
            return false;
        }

        public void Response(long connectionId, NetDataReader reader, AckResponseCode responseCode)
        {
            RequestHandler.InvokeResponse(connectionId, reader, responseCode);
        }
    }
}
