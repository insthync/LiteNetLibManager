using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct RequestHandlerData
    {
        public ushort RequestType { get; private set; }
        public uint AckId { get; private set; }
        public TransportHandler TransportHandler { get; private set; }
        public long ConnectionId { get; private set; }
        public NetDataReader Reader { get; private set; }

        public RequestHandlerData(ushort requestType, uint ackId, TransportHandler transportHandler, long connectionId, NetDataReader reader)
        {
            RequestType = requestType;
            AckId = ackId;
            TransportHandler = transportHandler;
            ConnectionId = connectionId;
            Reader = reader;
        }
    }
}
