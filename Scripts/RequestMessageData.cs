using LiteNetLib.Utils;
using System;

namespace LiteNetLibManager
{
    public abstract class BaseRequestMessageData
    {
        public uint AckId { get; protected set; }
        public long RequestTime { get; protected set; }
        public long Duration { get; protected set; }

        public abstract bool ResponseTimeout();
        public abstract void Response(NetDataReader reader);
    }

    public class RequestMessageData<T> : BaseRequestMessageData where T : BaseAckMessage, new()
    {
        private AckMessageCallback<T> _callback;

        public RequestMessageData(uint ackId, AckMessageCallback<T> callback, long duration)
        {
            AckId = ackId;
            RequestTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Duration = duration;
            _callback = callback;
        }

        public override bool ResponseTimeout()
        {
            if (Duration > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - RequestTime >= Duration)
            {
                _callback.Invoke(new T()
                {
                    ackId = AckId,
                    responseCode = AckResponseCode.Error,
                });
                return true;
            }
            return false;
        }

        public override void Response(NetDataReader reader)
        {
            T message = new T();
            message.Deserialize(reader);
            _callback.Invoke(message);
        }
    }
}
