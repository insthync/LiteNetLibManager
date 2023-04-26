using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct MessageHandlerData
    {
        public ushort MessageType { get; private set; }
        public TransportHandler TransportHandler { get; private set; }
        public long ConnectionId { get; private set; }
        public NetDataReader Reader { get; private set; }

        public MessageHandlerData(ushort messageType, TransportHandler transportHandler, long connectionId, NetDataReader reader)
        {
            MessageType = messageType;
            TransportHandler = transportHandler;
            ConnectionId = connectionId;
            Reader = reader;
        }

        public T ReadMessage<T>() where T : INetSerializable, new()
        {
            T msg = new T();
            msg.Deserialize(Reader);
            return msg;
        }
    }
}
