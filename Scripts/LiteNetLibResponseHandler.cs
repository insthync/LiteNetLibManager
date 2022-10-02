using System;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface ILiteNetLibResponseHandler
    {
        void InvokeResponse(ResponseHandlerData responseHandlerData, AckResponseCode responseCode, ResponseDelegate<INetSerializable> anotherResponseHandler);
        bool IsRequestTypeValid(Type type);
    }

    public struct LiteNetLibResponseHandler<TRequest, TResponse> : ILiteNetLibResponseHandler
        where TRequest : INetSerializable, new()
        where TResponse : INetSerializable, new()
    {
        private ResponseDelegate<TResponse> responseHandler;

        public LiteNetLibResponseHandler(ResponseDelegate<TResponse> responseHandler)
        {
            this.responseHandler = responseHandler;
        }

        public void InvokeResponse(ResponseHandlerData responseHandlerData, AckResponseCode responseCode, ResponseDelegate<INetSerializable> anotherResponseHandler)
        {
            TResponse response = new TResponse();
            if (responseCode != AckResponseCode.Timeout &&
                responseCode != AckResponseCode.Unimplemented)
            {
                if (responseHandlerData.Reader != null)
                    response.Deserialize(responseHandlerData.Reader);
            }
            if (responseHandler != null)
                responseHandler.Invoke(responseHandlerData, responseCode, response);
            if (anotherResponseHandler != null)
                anotherResponseHandler.Invoke(responseHandlerData, responseCode, response);
        }

        public bool IsRequestTypeValid(Type type)
        {
            return typeof(TRequest) == type;
        }
    }
}
