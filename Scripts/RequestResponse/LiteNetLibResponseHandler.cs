using System;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface ILiteNetLibResponseHandler
    {
        void InvokeResponse(ResponseHandlerData responseHandlerData, AckResponseCode responseCode, ResponseDelegate<INetSerializable> anotherResponseHandler);
        bool IsRequestTypeValid(Type type);
    }

    public class LiteNetLibResponseHandler<TRequest, TResponse> : ILiteNetLibResponseHandler
        where TRequest : INetSerializable, new()
        where TResponse : INetSerializable, new()
    {
        private ResponseDelegate<TResponse> _responseHandler;

        public LiteNetLibResponseHandler(ResponseDelegate<TResponse> responseHandler)
        {
            _responseHandler = responseHandler;
        }

        public void InvokeResponse(ResponseHandlerData responseHandlerData, AckResponseCode responseCode, ResponseDelegate<INetSerializable> anotherResponseHandler)
        {
            TResponse response = new TResponse();
            if (responseCode != AckResponseCode.Exception &&
                responseCode != AckResponseCode.Timeout &&
                responseCode != AckResponseCode.Unimplemented)
            {
                if (responseHandlerData.Reader != null)
                    response.Deserialize(responseHandlerData.Reader);
            }
            if (_responseHandler != null)
                _responseHandler.Invoke(responseHandlerData, responseCode, response);
            if (anotherResponseHandler != null)
                anotherResponseHandler.Invoke(responseHandlerData, responseCode, response);
        }

        public bool IsRequestTypeValid(Type type)
        {
            return typeof(TRequest) == type;
        }
    }
}
