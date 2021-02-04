using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct LiteNetLibRequestCallback
    {
        public uint AckId { get; private set; }
        public TransportHandler TransportHandler { get; private set; }
        public LiteNetLibResponseHandler ResponseHandler { get; private set; }
        public ResponseDelegate<INetSerializable> ResponseDelegate { get; private set; }

        public LiteNetLibRequestCallback(
            uint ackId,
            TransportHandler transportHandler,
            LiteNetLibResponseHandler responseHandler,
            ResponseDelegate<INetSerializable> responseDelegate)
        {
            AckId = ackId;
            TransportHandler = transportHandler;
            ResponseHandler = responseHandler;
            ResponseDelegate = responseDelegate;
        }

        public void ResponseTimeout()
        {
            ResponseHandler.InvokeResponse(new ResponseHandlerData(AckId, TransportHandler, -1, null), AckResponseCode.Timeout, ResponseDelegate);
        }

        public void Response(long connectionId, NetDataReader reader, AckResponseCode responseCode)
        {
            ResponseHandler.InvokeResponse(new ResponseHandlerData(AckId, TransportHandler, connectionId, reader), responseCode, ResponseDelegate);
        }
    }
}
