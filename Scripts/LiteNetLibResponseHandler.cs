using System;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface LiteNetLibResponseHandler
    {
        void InvokeResponse(ResponseHandlerData responseHandler, AckResponseCode responseCode, ResponseDelegate<INetSerializable> responseDelegate);
        bool IsRequestTypeValid(Type type);
    }

    public struct LiteNetLibResponseHandler<TRequest, TResponse> : LiteNetLibResponseHandler
        where TRequest : INetSerializable, new()
        where TResponse : INetSerializable, new()
    {
        private ResponseDelegate<TResponse> registeredDelegate;

        public LiteNetLibResponseHandler(ResponseDelegate<TResponse> responseDelegate)
        {
            registeredDelegate = responseDelegate;
        }

        public void InvokeResponse(ResponseHandlerData responseHandler, AckResponseCode responseCode, ResponseDelegate<INetSerializable> responseDelegate)
        {
            TResponse response = new TResponse();
            if (responseCode != AckResponseCode.Timeout &&
                responseCode != AckResponseCode.Unimplemented)
            {
                if (responseHandler.Reader != null)
                    response.Deserialize(responseHandler.Reader);
            }
            if (registeredDelegate != null)
                registeredDelegate.Invoke(responseHandler, responseCode, response);
            if (responseDelegate != null)
                responseDelegate.Invoke(responseHandler, responseCode, response);
        }

        public bool IsRequestTypeValid(Type type)
        {
            return typeof(TRequest) == type;
        }
    }
}
