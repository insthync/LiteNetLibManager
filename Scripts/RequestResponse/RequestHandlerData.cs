using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class RequestHandlerData
    {
        public ushort RequestType { get; private set; }
        public uint RequestId { get; private set; }
        public TransportHandler TransportHandler { get; private set; }
        public long ConnectionId { get; private set; }
        public NetDataReader Reader { get; private set; }

        public RequestHandlerData(ushort requestType, uint requestId, TransportHandler transportHandler, long connectionId, NetDataReader reader)
        {
            RequestType = requestType;
            RequestId = requestId;
            TransportHandler = transportHandler;
            ConnectionId = connectionId;
            Reader = reader;
        }
    }
}
