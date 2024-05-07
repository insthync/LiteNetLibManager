using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface ILiteNetLibRequestHandler
    {
        void InvokeRequest(RequestHandlerData requestHandlerData, RequestProceededDelegate responseProceedHandler);
    }

    public struct LiteNetLibRequestHandler<TRequest, TResponse> : ILiteNetLibRequestHandler
        where TRequest : INetSerializable, new()
        where TResponse : INetSerializable, new()
    {
        private TransportHandler _transportHandler;
        private RequestDelegate<TRequest, TResponse> _requestHandler;

        public LiteNetLibRequestHandler(TransportHandler transportHandler, RequestDelegate<TRequest, TResponse> requestHandler)
        {
            _transportHandler = transportHandler;
            _requestHandler = requestHandler;
        }

        public void InvokeRequest(RequestHandlerData requestHandlerData, RequestProceededDelegate responseProceedHandler)
        {
            TRequest request = new TRequest();
            string logTag = _transportHandler == null ? string.Empty : _transportHandler.LogTag;
            try
            {
                if (requestHandlerData.Reader != null)
                    request.Deserialize(requestHandlerData.Reader);
                if (_requestHandler != null)
                    _requestHandler.Invoke(requestHandlerData, request, (responseCode, response) => responseProceedHandler.Invoke(requestHandlerData.ConnectionId, requestHandlerData.RequestId, responseCode, response));
            }
            catch (System.Exception ex)
            {
                responseProceedHandler.Invoke(requestHandlerData.ConnectionId, requestHandlerData.RequestId, AckResponseCode.Error, new TResponse());
                Logging.LogError(logTag, $"Error occuring while proceed request {requestHandlerData.RequestType}");
                Logging.LogException(logTag, ex);
            }
        }
    }
}
