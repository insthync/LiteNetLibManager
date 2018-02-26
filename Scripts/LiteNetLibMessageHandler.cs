using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class LiteNetLibMessageHandler
    {
        public short msgType { get; private set; }
        public NetPeer peer { get; private set; }
        public NetDataReader reader { get; private set; }

        public LiteNetLibMessageHandler(short msgType, NetPeer peer, NetDataReader reader)
        {
            this.msgType = msgType;
            this.peer = peer;
            this.reader = reader;
        }

        public T ReadMessage<T>() where T : ILiteNetLibMessage, new()
        {
            var msg = new T();
            msg.Deserialize(reader);
            return msg;
        }

        public void ReadMessage<T>(T msg) where T : ILiteNetLibMessage
        {
            msg.Deserialize(reader);
        }
    }
}
