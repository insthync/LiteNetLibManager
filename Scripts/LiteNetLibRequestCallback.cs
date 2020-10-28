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
        public ExtraResponseDelegate ExtraResponseCallback { get; protected set; }

        public LiteNetLibRequestCallback(
            uint ackId,
            long duration,
            LiteNetLibResponseHandler responseHandler,
            ExtraResponseDelegate extraResponseCallback)
        {
            AckId = ackId;
            RequestTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Duration = duration;
            ResponseHandler = responseHandler;
            ExtraResponseCallback = extraResponseCallback;
        }

        public bool ResponseTimeout()
        {
            if (Duration > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - RequestTime >= Duration)
            {
                ResponseHandler.InvokeResponse(0, null, AckResponseCode.Timeout, ExtraResponseCallback);
                if (ExtraResponseCallback != null)
                    ExtraResponseCallback.Invoke(AckResponseCode.Timeout, null);
                return true;
            }
            return false;
        }

        public void Response(long connectionId, NetDataReader reader, AckResponseCode responseCode)
        {
            ResponseHandler.InvokeResponse(connectionId, reader, responseCode, ExtraResponseCallback);
        }
    }
}
