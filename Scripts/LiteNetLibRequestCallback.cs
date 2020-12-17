using LiteNetLib.Utils;
using System;

namespace LiteNetLibManager
{
    public class LiteNetLibRequestCallback
    {
        public uint AckId { get; protected set; }
        public TransportHandler TransportHandler { get; protected set; }
        public LiteNetLibResponseHandler ResponseHandler { get; protected set; }
        public ResponseDelegate ResponseDelegate { get; protected set; }

        public LiteNetLibRequestCallback(
            uint ackId,
            TransportHandler transportHandler,
            LiteNetLibResponseHandler responseHandler,
            ResponseDelegate responseDelegate)
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
