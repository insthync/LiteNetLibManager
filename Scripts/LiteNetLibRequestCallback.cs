using LiteNetLib.Utils;
using System;

namespace LiteNetLibManager
{
    public class LiteNetLibRequestCallback
    {
        public uint AckId { get; protected set; }
        public long RequestTime { get; protected set; }
        public long Duration { get; protected set; }
        public LiteNetLibResponseHandler ResponseHandler { get; protected set; }
        public ResponseDelegate ResponseDelegate { get; protected set; }

        public LiteNetLibRequestCallback(
            uint ackId,
            long duration,
            LiteNetLibResponseHandler responseHandler,
            ResponseDelegate responseDelegate)
        {
            AckId = ackId;
            RequestTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Duration = duration;
            ResponseHandler = responseHandler;
            ResponseDelegate = responseDelegate;
        }

        public bool ResponseTimeout()
        {
            if (Duration > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - RequestTime >= Duration)
            {
                ResponseHandler.InvokeResponse(0, null, AckResponseCode.Timeout, ResponseDelegate);
                if (ResponseDelegate != null)
                    ResponseDelegate.Invoke(AckResponseCode.Timeout, null);
                return true;
            }
            return false;
        }

        public void Response(long connectionId, NetDataReader reader, AckResponseCode responseCode)
        {
            ResponseHandler.InvokeResponse(connectionId, reader, responseCode, ResponseDelegate);
        }
    }
}
