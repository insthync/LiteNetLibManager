using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLibHighLevel.Messages;

namespace LiteNetLibHighLevel
{
    public class LiteNetLibMessageHandler
    {
        public short msgType;
        public NetPeer peer;
        public NetDataReader reader;

        public T ReadMessage<T>() where T : LiteNetLibMessageBase, new()
        {
            var msg = new T();
            msg.Deserialize(reader);
            return msg;
        }

        public void ReadMessage<T>(T msg) where T : LiteNetLibMessageBase
        {
            msg.Deserialize(reader);
        }
    }
}
