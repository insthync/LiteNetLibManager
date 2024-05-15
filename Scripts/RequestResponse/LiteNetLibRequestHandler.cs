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
        private RequestDelegate<TRequest, TResponse> _requestHandler;

        public LiteNetLibRequestHandler(RequestDelegate<TRequest, TResponse> requestHandler)
        {
            _requestHandler = requestHandler;
        }

        public void InvokeRequest(RequestHandlerData requestHandlerData, RequestProceededDelegate responseProceedHandler)
        {
            TRequest request = new TRequest();
            if (requestHandlerData.Reader != null)
                request.Deserialize(requestHandlerData.Reader);
            if (_requestHandler != null)
                _requestHandler.Invoke(requestHandlerData, request, (responseCode, response, extraResponseSerializer) => responseProceedHandler.Invoke(requestHandlerData.ConnectionId, requestHandlerData.RequestId, responseCode, response, extraResponseSerializer));
        }
    }
}
