using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct ResponseHandlerData
    {
        public uint RequestId { get; private set; }
        public TransportHandler TransportHandler { get; private set; }
        public long ConnectionId { get; private set; }
        public NetDataReader Reader { get; private set; }

        public ResponseHandlerData(uint requestId, TransportHandler transportHandler, long connectionId, NetDataReader reader)
        {
            RequestId = requestId;
            TransportHandler = transportHandler;
            ConnectionId = connectionId;
            Reader = reader;
        }
    }
}
