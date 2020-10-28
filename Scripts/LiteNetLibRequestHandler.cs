using System;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibRequestHandler
    {
        internal abstract void InvokeRequest(RequestHandlerData requestHandler, RequestProceedResultDelegate<INetSerializable> responseProceedResult);
    }

    public class LiteNetLibRequestHandler<TRequest, TResponse> : LiteNetLibRequestHandler
        where TRequest : INetSerializable, new()
        where TResponse : INetSerializable, new()
    {
        private RequestDelegate<TRequest, TResponse> requestDelegate;

        public LiteNetLibRequestHandler(RequestDelegate<TRequest, TResponse> requestDelegate)
        {
            this.requestDelegate = requestDelegate;
        }

        internal override void InvokeRequest(RequestHandlerData requestHandler, RequestProceedResultDelegate<INetSerializable> responseProceedResult)
        {
            TRequest request = new TRequest();
            if (requestHandler.Reader != null)
                request.Deserialize(requestHandler.Reader);
            if (requestDelegate != null)
            {
                requestDelegate.Invoke(requestHandler, request, (responseCode, response, responseSerializer) =>
                {
                    responseProceedResult.Invoke(responseCode, response, responseSerializer);
                });
            }
        }
    }
}
