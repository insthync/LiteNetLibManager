using LiteNetLib;
using LiteNetLib.Utils;
using System;
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
        protected readonly Dictionary<uint, LiteNetLibRequestCallback> requestCallbacks = new Dictionary<uint, LiteNetLibRequestCallback>();
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

        public virtual void Update()
        {
            if (!IsNetworkActive)
                return;
            if (RequestsCount > 0)
            {
                List<uint> ackIds = new List<uint>(requestCallbacks.Keys);
                foreach (uint ackId in ackIds)
                {
                    if (requestCallbacks[ackId].ResponseTimeout())
                    {
                        lock (requestCallbacks)
                        {
                            requestCallbacks.Remove(ackId);
                        }
                    }
                }
            }
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
            messageHandlers[messageType].Invoke(new LiteNetLibMessageHandler(messageType, this, connectionId, reader));
        }

        protected void WritePacket(
            NetDataWriter writer,
            ushort messageType,
            Action<NetDataWriter> extraSerializer)
        {
            writer.Reset();
            writer.PutPackedUShort(messageType);
            if (extraSerializer != null)
                extraSerializer.Invoke(writer);
        }

        protected abstract void SendMessage(long connectionId, DeliveryMethod deliveryMethod, NetDataWriter writer);

        private uint CreateRequest(
            LiteNetLibResponseHandler responseHandler,
            long duration)
        {
            uint ackId = nextAckId++;
            lock (requestCallbacks)
            {
                // Get response callback by request type
                requestCallbacks.Add(ackId, new LiteNetLibRequestCallback(ackId, duration, responseHandler));
            }
            return ackId;
        }

        protected bool CreateAndWriteRequest<TRequest>(
            NetDataWriter writer,
            ushort requestType,
            TRequest request,
            Action<NetDataWriter> extraSerializer,
            long duration = 30)
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
            uint ackId = CreateRequest(responseHandlers[requestType], duration);
            // Write request
            writer.Reset();
            writer.PutPackedUShort(RequestMessageType);
            writer.PutPackedUShort(requestType);
            writer.PutPackedUInt(ackId);
            request.Serialize(writer);
            if (extraSerializer != null)
                extraSerializer.Invoke(writer);
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
                Logging.LogError($"Cannot proceed request {requestType} not registered.");
                return;
            }
            // Invoke request and create response
            requestHandlers[requestType].InvokeRequest(connectionId, reader, (responseCode, response, responseSerializer) =>
            {
                RequestProceeded(connectionId, ackId, responseCode, response, responseSerializer);
            });
        }

        private void RequestProceeded(long connectionId, uint ackId, AckResponseCode responseCode, INetSerializable response, Action<NetDataWriter> responseSerializer)
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
                lock (requestCallbacks)
                {
                    requestCallbacks.Remove(ackId);
                }
            }
        }

        public void RegisterRequestHandler<TRequest, TResponse>(
            ushort requestType,
            RequestDelegate<TRequest, TResponse> requestDelegate)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            requestHandlers[requestType] = new LiteNetLibRequestHandler<TRequest, TResponse>(requestDelegate);
        }

        public void UnregisterRequestHandler(ushort requestType)
        {
            requestHandlers.Remove(requestType);
        }

        public void RegisterResponseHandler<TRequest, TResponse>(
            ushort requestType,
            ResponseDelegate<TResponse> responseDelegate)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            responseHandlers[requestType] = new LiteNetLibResponseHandler<TRequest, TResponse>(responseDelegate);
        }

        public void UnregisterResponseHandler(ushort requestType)
        {
            responseHandlers.Remove(requestType);
        }

        public void RegisterMessage(
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

        public void UnregisterMessage(ushort messageType)
        {
            messageHandlers.Remove(messageType);
        }
    }
}
