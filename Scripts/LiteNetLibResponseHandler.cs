using System;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibResponseHandler
    {
        internal abstract void InvokeResponse(ResponseHandlerData responseHandler, AckResponseCode responseCode, ResponseDelegate responseDelegate);
        internal abstract bool IsRequestTypeValid(Type type);
    }

    public sealed class LiteNetLibResponseHandler<TRequest, TResponse> : LiteNetLibResponseHandler
        where TRequest : INetSerializable, new()
        where TResponse : INetSerializable, new()
    {
        private ResponseDelegate<TResponse> registeredDelegate;

        public LiteNetLibResponseHandler(ResponseDelegate<TResponse> responseDelegate)
        {
            registeredDelegate = responseDelegate;
        }

        internal override void InvokeResponse(ResponseHandlerData responseHandler, AckResponseCode responseCode, ResponseDelegate responseDelegate)
        {
            TResponse response = new TResponse();
            if (responseHandler.Reader != null)
                response.Deserialize(responseHandler.Reader);
            if (registeredDelegate != null)
                registeredDelegate.Invoke(responseHandler, responseCode, response);
            if (responseDelegate != null)
                responseDelegate.Invoke(responseHandler, responseCode, response);
        }

        internal override bool IsRequestTypeValid(Type type)
        {
            return typeof(TRequest) == type;
        }
    }
}
