using Cysharp.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LiteNetLibManager
{
    public abstract class TransportHandler
    {
        internal readonly NetDataWriter Writer = new NetDataWriter();
        public abstract string LogTag { get; }
        public abstract bool IsNetworkActive { get; }
        public ITransport Transport { get; set; }
        public bool RequestResponseEnabled { get; protected set; }
        public ushort RequestMessageType { get; protected set; }
        public ushort ResponseMessageType { get; protected set; }
        protected readonly Dictionary<ushort, MessageHandlerDelegate> messageHandlers = new Dictionary<ushort, MessageHandlerDelegate>();
        protected readonly Dictionary<ushort, ILiteNetLibRequestHandler> requestHandlers = new Dictionary<ushort, ILiteNetLibRequestHandler>();
        protected readonly Dictionary<ushort, ILiteNetLibResponseHandler> responseHandlers = new Dictionary<ushort, ILiteNetLibResponseHandler>();
        protected readonly ConcurrentDictionary<uint, LiteNetLibRequestCallback> requestCallbacks = new ConcurrentDictionary<uint, LiteNetLibRequestCallback>();
        protected uint nextRequestId;
        protected TransportEventData tempEventData;

        public int RequestsCount { get { return requestCallbacks.Count; } }

        public TransportHandler()
        {
            RequestResponseEnabled = false;
            RequestMessageType = 0;
            ResponseMessageType = 0;
            nextRequestId = 1;
        }

        public TransportHandler(ITransport transport) : this()
        {
            Transport = transport;
        }

        public bool EnableRequestResponse(ushort requestMessageType, ushort responseMessageType)
        {
            if (requestMessageType == responseMessageType ||
                messageHandlers.ContainsKey(requestMessageType) ||
                messageHandlers.ContainsKey(responseMessageType))
            {
                Logging.LogError(LogTag, $"Cannot enable request-response feature, request/response message type must be different and not registered.");
                DisableRequestResponse();
                return false;
            }
            RequestResponseEnabled = true;
            RequestMessageType = requestMessageType;
            ResponseMessageType = responseMessageType;
            return true;
        }

        public void DisableRequestResponse()
        {
            RequestResponseEnabled = false;
            RequestMessageType = 0;
            ResponseMessageType = 0;
        }

        protected void ReadPacket(long connectionId, NetDataReader reader)
        {
            ushort messageType = reader.GetPackedUShort();
            if (RequestResponseEnabled && RequestMessageType == messageType)
            {
                ProceedRequest(connectionId, reader);
                return;
            }
            if (RequestResponseEnabled && ResponseMessageType == messageType)
            {
                ProceedResponse(connectionId, reader);
                return;
            }
            if (!messageHandlers.ContainsKey(messageType))
                return;
            messageHandlers[messageType].Invoke(new MessageHandlerData(messageType, this, connectionId, reader));
        }

        public static void WritePacket(
            NetDataWriter writer,
            ushort messageType)
        {
            writer.Reset();
            writer.PutPackedUShort(messageType);
        }

        public static void WritePacket(
            NetDataWriter writer,
            ushort messageType,
            SerializerDelegate extraSerializer)
        {
            WritePacket(writer, messageType);
            if (extraSerializer != null)
                extraSerializer.Invoke(writer);
        }

        public abstract void SendMessage(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer);

        /// <summary>
        /// Create new request callback with a new request ID
        /// </summary>
        /// <param name="responseHandler"></param>
        /// <param name="responseDelegate"></param>
        /// <returns></returns>
        private uint CreateRequest(
            ILiteNetLibResponseHandler responseHandler,
            ResponseDelegate<INetSerializable> responseDelegate)
        {
            uint requestId = nextRequestId++;
            // Get response callback by request type
            requestCallbacks.TryAdd(requestId, new LiteNetLibRequestCallback(requestId, this, responseHandler, responseDelegate));
            return requestId;
        }

        /// <summary>
        /// Delay and do something when request timeout
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="millisecondsTimeout"></param>
        /// <returns></returns>
        private async UniTaskVoid HandleRequestTimeout(uint requestId, int millisecondsTimeout)
        {
            if (millisecondsTimeout > 0)
            {
                await UniTask.Delay(millisecondsTimeout);
                LiteNetLibRequestCallback callback;
                if (requestCallbacks.TryRemove(requestId, out callback))
                    callback.ResponseTimeout();
            }
        }

        /// <summary>
        /// Create a new request will being sent to target later
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <param name="writer"></param>
        /// <param name="requestType"></param>
        /// <param name="request"></param>
        /// <param name="responseDelegate"></param>
        /// <param name="millisecondsTimeout"></param>
        /// <param name="extraRequestSerializer"></param>
        /// <returns></returns>
        protected bool CreateAndWriteRequest<TRequest>(
            NetDataWriter writer,
            ushort requestType,
            TRequest request,
            ResponseDelegate<INetSerializable> responseDelegate,
            int millisecondsTimeout,
            SerializerDelegate extraRequestSerializer)
            where TRequest : INetSerializable, new()
        {
            if (!responseHandlers.ContainsKey(requestType))
            {
                responseDelegate.Invoke(new ResponseHandlerData(nextRequestId++, this, -1, null), AckResponseCode.Unimplemented, EmptyMessage.Value);
                Logging.LogError(LogTag, $"Cannot create request. Request type: {requestType} not registered.");
                return false;
            }
            if (!responseHandlers[requestType].IsRequestTypeValid(typeof(TRequest)))
            {
                responseDelegate.Invoke(new ResponseHandlerData(nextRequestId++, this, -1, null), AckResponseCode.Unimplemented, EmptyMessage.Value);
                Logging.LogError(LogTag, $"Cannot create request. Request type: {requestType}, {typeof(TRequest)} is not valid message type.");
                return false;
            }
            // Create request
            uint requestId = CreateRequest(responseHandlers[requestType], responseDelegate);
            HandleRequestTimeout(requestId, millisecondsTimeout).Forget();
            // Write request
            writer.Reset();
            writer.PutPackedUShort(RequestMessageType);
            writer.PutPackedUShort(requestType);
            writer.PutPackedUInt(requestId);
            writer.Put(request);
            if (extraRequestSerializer != null)
                extraRequestSerializer.Invoke(writer);
            return true;
        }

        /// <summary>
        /// Proceed request which reveived from server or client
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="reader"></param>
        private void ProceedRequest(
            long connectionId,
            NetDataReader reader)
        {
            ushort requestType = reader.GetPackedUShort();
            uint requestId = reader.GetPackedUInt();
            if (!requestHandlers.ContainsKey(requestType))
            {
                // No request-response handler
                RequestProceeded(connectionId, requestId, AckResponseCode.Unimplemented, EmptyMessage.Value, null);
                Logging.LogError(LogTag, $"Cannot proceed request {requestType} not registered.");
                return;
            }
            // Invoke request and create response
            requestHandlers[requestType].InvokeRequest(new RequestHandlerData(requestType, requestId, this, connectionId, reader), RequestProceeded);
        }

        /// <summary>
        /// Send response to the requester
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="requestId"></param>
        /// <param name="responseCode"></param>
        /// <param name="response"></param>
        /// <param name="extraResponseSerializer"></param>
        private void RequestProceeded(long connectionId, uint requestId, AckResponseCode responseCode, INetSerializable response, SerializerDelegate extraResponseSerializer)
        {
            // Write response
            Writer.Reset();
            Writer.PutPackedUShort(ResponseMessageType);
            Writer.PutPackedUInt(requestId);
            Writer.PutValue(responseCode);
            Writer.Put(response);
            if (extraResponseSerializer != null)
                extraResponseSerializer.Invoke(Writer);
            // Send response
            SendMessage(connectionId, 0, DeliveryMethod.ReliableUnordered, Writer);
        }

        /// <summary>
        /// Proceed response which reveived from server or client
        /// </summary>
        /// <param name="networkConnection"></param>
        /// <param name="responseMessage"></param>
        private void ProceedResponse(long connectionId, NetDataReader reader)
        {
            uint requestId = reader.GetPackedUInt();
            AckResponseCode responseCode = reader.GetValue<AckResponseCode>();
            if (requestCallbacks.ContainsKey(requestId))
            {
                requestCallbacks[requestId].Response(connectionId, reader, responseCode);
                requestCallbacks.TryRemove(requestId, out _);
            }
        }

        /// <summary>
        /// Register request handler which will read request message and response to requester peer
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="requestType"></param>
        /// <param name="handlerDelegate"></param>
        public void RegisterRequestHandler<TRequest, TResponse>(
            ushort requestType,
            RequestDelegate<TRequest, TResponse> handlerDelegate)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            requestHandlers[requestType] = new LiteNetLibRequestHandler<TRequest, TResponse>(handlerDelegate);
        }

        public void UnregisterRequestHandler(ushort requestType)
        {
            requestHandlers.Remove(requestType);
        }

        /// <summary>
        /// Register response handler which will read response message and do something by requester
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="requestType"></param>
        /// <param name="handlerDelegate"></param>
        public void RegisterResponseHandler<TRequest, TResponse>(
            ushort requestType,
            ResponseDelegate<TResponse> handlerDelegate = null)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            responseHandlers[requestType] = new LiteNetLibResponseHandler<TRequest, TResponse>(handlerDelegate);
        }

        public void UnregisterResponseHandler(ushort requestType)
        {
            responseHandlers.Remove(requestType);
        }

        /// <summary>
        /// Register message handler for messages which will be received by other peers to do something when receive message
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="handlerDelegate"></param>
        public void RegisterMessageHandler(
            ushort messageType,
            MessageHandlerDelegate handlerDelegate)
        {
            if (RequestResponseEnabled && (RequestMessageType == messageType || ResponseMessageType == messageType))
            {
                Logging.LogError(LogTag, $"Cannot register message, message type must be difference to request/response message types.");
                return;
            }
            messageHandlers[messageType] = handlerDelegate;
        }

        public void UnregisterMessageHandler(ushort messageType)
        {
            messageHandlers.Remove(messageType);
        }
    }
}
