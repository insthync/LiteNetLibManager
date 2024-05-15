using Cysharp.Threading.Tasks;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public delegate void SerializerDelegate(NetDataWriter writer);
    public delegate void MessageHandlerDelegate(MessageHandlerData messageHandler);
    public delegate void RequestProceedResultDelegate<TResponse>(AckResponseCode responseCode, TResponse response, SerializerDelegate extraResponseSerializer = null)
        where TResponse : INetSerializable;
    public delegate void RequestProceededDelegate(long connectionId, uint requestId, AckResponseCode responseCode, INetSerializable response, SerializerDelegate extraResponseSerializer);
    public delegate UniTaskVoid RequestDelegate<TRequest, TResponse>(RequestHandlerData requestHandler, TRequest request, RequestProceedResultDelegate<TResponse> responseProceedResult)
        where TRequest : INetSerializable
        where TResponse : INetSerializable;
    public delegate void ResponseDelegate<TResponse>(ResponseHandlerData responseHandler, AckResponseCode responseCode, TResponse response)
        where TResponse : INetSerializable;
    public delegate void LogicUpdateDelegate(LogicUpdater updater);

    public static class DelegateExtensions
    {
        public static void InvokeSuccess<TResponse>(this RequestProceedResultDelegate<TResponse> target, TResponse response, SerializerDelegate extraResponseSerializer = null)
            where TResponse : INetSerializable
        {
            target.Invoke(AckResponseCode.Success, response, extraResponseSerializer);
        }

        public static void InvokeError<TResponse>(this RequestProceedResultDelegate<TResponse> target, TResponse response, SerializerDelegate extraResponseSerializer = null)
            where TResponse : INetSerializable
        {
            target.Invoke(AckResponseCode.Error, response, extraResponseSerializer);
        }
    }
}
