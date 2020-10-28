using System;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibResponseHandler
    {
        internal abstract void InvokeResponse(long connectionId, NetDataReader reader, AckResponseCode responseCode, ExtraResponseDelegate extraResponseCallback);
        internal abstract bool IsRequestTypeValid(Type type);
    }

    public sealed class LiteNetLibResponseHandler<TRequest, TResponse> : LiteNetLibResponseHandler
        where TRequest : INetSerializable, new()
        where TResponse : INetSerializable, new()
    {
        private ResponseDelegate<TResponse> responseDelegate;

        public LiteNetLibResponseHandler(
            ResponseDelegate<TResponse> responseDelegate)
        {
            this.responseDelegate = responseDelegate;
        }

        internal override void InvokeResponse(long connectionId, NetDataReader reader, AckResponseCode responseCode, ExtraResponseDelegate extraResponseCallback)
        {
            TResponse response = new TResponse();
            if (reader != null)
                response.Deserialize(reader);
            responseDelegate.Invoke(connectionId, reader, responseCode, response);
            if (extraResponseCallback != null)
                extraResponseCallback.Invoke(responseCode, response);
        }

        internal override bool IsRequestTypeValid(Type type)
        {
            return typeof(TRequest) == type;
        }
    }
}
