using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct LiteNetLibRequestCallback
    {
        public uint RequestId { get; private set; }
        public TransportHandler TransportHandler { get; private set; }
        public ILiteNetLibResponseHandler ResponseHandler { get; private set; }
        public ResponseDelegate<INetSerializable> ResponseDelegate { get; private set; }

        public LiteNetLibRequestCallback(
            uint requestId,
            TransportHandler transportHandler,
            ILiteNetLibResponseHandler responseHandler,
            ResponseDelegate<INetSerializable> responseDelegate)
        {
            RequestId = requestId;
            TransportHandler = transportHandler;
            ResponseHandler = responseHandler;
            ResponseDelegate = responseDelegate;
        }

        public void ResponseTimeout()
        {
            ResponseHandler.InvokeResponse(new ResponseHandlerData(RequestId, TransportHandler, -1, null), AckResponseCode.Timeout, ResponseDelegate);
        }

        public void Response(long connectionId, NetDataReader reader, AckResponseCode responseCode)
        {
            ResponseHandler.InvokeResponse(new ResponseHandlerData(RequestId, TransportHandler, connectionId, reader), responseCode, ResponseDelegate);
        }
    }
}
