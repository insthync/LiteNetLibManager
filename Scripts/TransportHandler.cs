using Cysharp.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LiteNetLibManager
{
    public abstract class TransportHandler
    {
        protected readonly NetDataWriter writer = new NetDataWriter();
        public abstract string LogTag { get; }
        public abstract bool IsNetworkActive { get; }
        public ITransport Transport { get; protected set; }
        public bool RequestResponseEnabled { get; protected set; }
        public ushort RequestMessageType { get; protected set; }
        public ushort ResponseMessageType { get; protected set; }
        protected readonly Dictionary<ushort, MessageHandlerDelegate> messageHandlers = new Dictionary<ushort, MessageHandlerDelegate>();
        protected readonly Dictionary<ushort, LiteNetLibRequestHandler> requestHandlers = new Dictionary<ushort, LiteNetLibRequestHandler>();
        protected readonly Dictionary<ushort, LiteNetLibResponseHandler> responseHandlers = new Dictionary<ushort, LiteNetLibResponseHandler>();
        protected readonly ConcurrentDictionary<uint, LiteNetLibRequestCallback> requestCallbacks = new ConcurrentDictionary<uint, LiteNetLibRequestCallback>();
        protected uint nextAckId = 1;
        protected TransportEventData tempEventData;

        public int RequestsCount { get { return requestCallbacks.Count; } }

        public TransportHandler(ITransport transport)
        {
            Transport = transport;
        }

        public bool EnableRequestResponse(ushort requestMessageType, ushort responseMessageType)
        {
            if (requestMessageType == responseMessageType ||
                messageHandlers.ContainsKey(requestMessageType) ||
                messageHandlers.ContainsKey(responseMessageType))
            {
                Logging.LogError($"Cannot enable request-response feature, request/response message type must be different and not registered.");
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

        protected void WritePacket(
            NetDataWriter writer,
            ushort messageType,
            SerializerDelegate extraSerializer)
        {
            writer.Reset();
            writer.PutPackedUShort(messageType);
            if (extraSerializer != null)
                extraSerializer.Invoke(writer);
        }

        protected abstract void SendMessage(long connectionId, DeliveryMethod deliveryMethod, NetDataWriter writer);

        private uint CreateRequest(
            LiteNetLibResponseHandler responseHandler,
            int millisecondsTimeout,
            ResponseDelegate responseDelegate)
        {
            uint ackId = nextAckId++;
            // Get response callback by request type
            requestCallbacks.TryAdd(ackId, new LiteNetLibRequestCallback(ackId, this, responseHandler, responseDelegate));
            RequestTimeout(ackId, millisecondsTimeout).Forget();
            return ackId;
        }

        private async UniTaskVoid RequestTimeout(uint ackId, int millisecondsTimeout)
        {
            if (millisecondsTimeout > 0)
            {
                await UniTask.Delay(millisecondsTimeout);
                LiteNetLibRequestCallback callback;
                if (requestCallbacks.TryRemove(ackId, out callback))
                    callback.ResponseTimeout();
            }
        }

        protected bool CreateAndWriteRequest<TRequest>(
            NetDataWriter writer,
            ushort requestType,
            TRequest request,
            SerializerDelegate extraRequestSerializer,
            int millisecondsTimeout,
            ResponseDelegate responseDelegate)
            where TRequest : INetSerializable
        {
            if (!responseHandlers.ContainsKey(requestType))
            {
                Logging.LogError($"Cannot create request. Request type: {requestType} not registered.");
                return false;
            }
            if (!responseHandlers[requestType].IsRequestTypeValid(typeof(TRequest)))
            {
                Logging.LogError($"Cannot create request. Request type: {requestType}, {typeof(TRequest)} is not valid message type.");
                return false;
            }
            // Create request
            uint ackId = CreateRequest(responseHandlers[requestType], millisecondsTimeout, responseDelegate);
            // Write request
            writer.Reset();
            writer.PutPackedUShort(RequestMessageType);
            writer.PutPackedUShort(requestType);
            writer.PutPackedUInt(ackId);
            request.Serialize(writer);
            if (extraRequestSerializer != null)
                extraRequestSerializer.Invoke(writer);
            return true;
        }

        private void ProceedRequest(
            long connectionId,
            NetDataReader reader)
        {
            ushort requestType = reader.GetPackedUShort();
            uint ackId = reader.GetPackedUInt();
            if (!requestHandlers.ContainsKey(requestType))
            {
                // No request-response handler
                RequestProceeded(connectionId, ackId, AckResponseCode.Unimplemented, EmptyMessage.Value, null);
                Logging.LogError($"Cannot proceed request {requestType} not registered.");
                return;
            }
            // Invoke request and create response
            requestHandlers[requestType].InvokeRequest(new RequestHandlerData(requestType, ackId, this, connectionId, reader), (responseCode, response, responseSerializer) =>
            {
                RequestProceeded(connectionId, ackId, responseCode, response, responseSerializer);
            });
        }

        private void RequestProceeded(long connectionId, uint ackId, AckResponseCode responseCode, INetSerializable response, SerializerDelegate responseSerializer)
        {
            // Write response
            writer.Reset();
            writer.PutPackedUShort(ResponseMessageType);
            writer.PutPackedUInt(ackId);
            writer.PutValue(responseCode);
            response.Serialize(writer);
            if (responseSerializer != null)
                responseSerializer.Invoke(writer);
            // Send response
            SendMessage(connectionId, DeliveryMethod.ReliableOrdered, writer);
        }

        private void ProceedResponse(long connectionId, NetDataReader reader)
        {
            uint ackId = reader.GetPackedUInt();
            AckResponseCode responseCode = reader.GetValue<AckResponseCode>();
            if (requestCallbacks.ContainsKey(ackId))
            {
                requestCallbacks[ackId].Response(connectionId, reader, responseCode);
                requestCallbacks.TryRemove(ackId, out _);
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
                Logging.LogError($"Cannot register message, message type must be difference to request/response message types.");
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
