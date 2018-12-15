using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class LiteNetLibMessageHandler
    {
        public ushort msgType { get; private set; }
        public TransportHandler transportHandler { get; private set; }
        public long connectionId { get; private set; }
        public NetDataReader reader { get; private set; }

        public LiteNetLibMessageHandler(ushort msgType, TransportHandler peerHandler, long connectionId, NetDataReader reader)
        {
            this.msgType = msgType;
            this.transportHandler = peerHandler;
            this.connectionId = connectionId;
            this.reader = reader;
        }

        public T ReadMessage<T>() where T : INetSerializable, new()
        {
            var msg = new T();
            msg.Deserialize(reader);
            return msg;
        }

        public void ReadMessage<T>(T msg) where T : INetSerializable
        {
            msg.Deserialize(reader);
        }
    }
}
