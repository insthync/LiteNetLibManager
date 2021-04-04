using System;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface LiteNetLibRequestHandler
    {
        void InvokeRequest(RequestHandlerData requestHandler, RequestProceededDelegate responseProceedResult);
    }

    public struct LiteNetLibRequestHandler<TRequest, TResponse> : LiteNetLibRequestHandler
        where TRequest : INetSerializable, new()
        where TResponse : INetSerializable, new()
    {
        private RequestDelegate<TRequest, TResponse> requestDelegate;

        public LiteNetLibRequestHandler(RequestDelegate<TRequest, TResponse> requestDelegate)
        {
            this.requestDelegate = requestDelegate;
        }

        public void InvokeRequest(RequestHandlerData requestHandler, RequestProceededDelegate responseProceedResult)
        {
            TRequest request = new TRequest();
            if (requestHandler.Reader != null)
                request.Deserialize(requestHandler.Reader);
            if (requestDelegate != null)
            {
                requestDelegate.Invoke(requestHandler, request, (responseCode, response, responseSerializer) =>
                {
                    responseProceedResult.Invoke(requestHandler.ConnectionId, requestHandler.RequestId, responseCode, response, responseSerializer);
                });
            }
        }
    }
}
