using System;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibResponseHandler
    {
        internal abstract void InvokeResponse(long connectionId, NetDataReader reader, AckResponseCode responseCode, ResponseDelegate responseDelegate);
        internal abstract bool IsRequestTypeValid(Type type);
    }

    public sealed class LiteNetLibResponseHandler<TRequest, TResponse> : LiteNetLibResponseHandler
        where TRequest : INetSerializable, new()
        where TResponse : INetSerializable, new()
    {
        private ResponseDelegate<TResponse> registeredDelegate;

        public LiteNetLibResponseHandler(
            ResponseDelegate<TResponse> responseDelegate)
        {
            registeredDelegate = responseDelegate;
        }

        internal override void InvokeResponse(long connectionId, NetDataReader reader, AckResponseCode responseCode, ResponseDelegate responseDelegate)
        {
            TResponse response = new TResponse();
            if (reader != null)
                response.Deserialize(reader);
            if (registeredDelegate != null)
                registeredDelegate.Invoke(connectionId, reader, responseCode, response);
            if (responseDelegate != null)
                responseDelegate.Invoke(connectionId, responseCode, response);
        }

        internal override bool IsRequestTypeValid(Type type)
        {
            return typeof(TRequest) == type;
        }
    }
}
