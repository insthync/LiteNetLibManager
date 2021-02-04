using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct ResponseHandlerData
    {
        public uint AckId { get; private set; }
        public TransportHandler TransportHandler { get; private set; }
        public long ConnectionId { get; private set; }
        public NetDataReader Reader { get; private set; }

        public ResponseHandlerData(uint ackId, TransportHandler transportHandler, long connectionId, NetDataReader reader)
        {
            AckId = ackId;
            TransportHandler = transportHandler;
            ConnectionId = connectionId;
            Reader = reader;
        }
    }
}
