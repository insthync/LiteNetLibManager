using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class AsyncResponseData<TResponse>
        where TResponse : INetSerializable
    {
        public ResponseHandlerData RequestHandler { get; private set; }
        public AckResponseCode ResponseCode { get; private set; }
        public TResponse Response { get; private set; }
        public bool IsSuccess { get { return ResponseCode == AckResponseCode.Success; } }

        public AsyncResponseData(ResponseHandlerData requestHandler, AckResponseCode responseCode, TResponse response)
        {
            RequestHandler = requestHandler;
            ResponseCode = responseCode;
            Response = response;
        }
    }
}