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
        public ITransport Transport { get; protected set; }
        public bool RequestResponseEnabled { get; protected set; }
        public ushort RequestMessageType { get; protected set; }
        public ushort ResponseMessageType { get; protected set; }
        protected readonly Dictionary<ushort, MessageHandlerDelegate> messageHandlers = new Dictionary<ushort, MessageHandlerDelegate>();
        protected readonly Dictionary<ushort, LiteNetLibRequestHandler> requestHandlers = new Dictionary<ushort, LiteNetLibRequestHandler>();
        protected readonly Dictionary<uint, LiteNetLibRequestCallback> requestCallbacks = new Dictionary<uint, LiteNetLibRequestCallback>();
        protected uint nextAckId = 1;
        protected TransportEventData tempEventData;
        protected bool isNetworkActive;

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
                RequestResponseEnabled = false;
                RequestMessageType = 0;
                ResponseMessageType = 0;
                Logging.LogError($"Cannot enable request-response feature, request/response message type must be different and not registered.");
                return false;
            }
            RequestResponseEnabled = true;
            RequestMessageType = requestMessageType;
            ResponseMessageType = responseMessageType;
            return true;
        }

        public virtual void Update()
        {
            if (!isNetworkActive)
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
                HandleRequest(connectionId, reader);
                return;
            }
            if (RequestResponseEnabled && ResponseMessageType == messageType)
            {
                HandleResponse(connectionId, reader);
                return;
            }
            if (!messageHandlers.ContainsKey(messageType))
                return;
            messageHandlers[messageType].Invoke(new LiteNetLibMessageHandler(messageType, this, connectionId, reader));
        }

        protected void WritePacket(NetDataWriter writer, ushort messageType, Action<NetDataWriter> serializer)
        {
            writer.Reset();
            writer.PutPackedUShort(messageType);
            if (serializer != null)
                serializer.Invoke(writer);
        }

        protected abstract void SendMessage(long connectionId, DeliveryMethod deliveryMethod, NetDataWriter writer);

        private uint CreateRequest(LiteNetLibRequestHandler requestHandler, long duration)
        {
            uint ackId = nextAckId++;
            lock (requestCallbacks)
            {
                // Get response callback by request type
                requestCallbacks.Add(ackId, new LiteNetLibRequestCallback(ackId, duration, requestHandler));
            }
            return ackId;
        }

        protected bool CreateAndWriteRequest<TRequest>(NetDataWriter writer, ushort requestType, TRequest request, long duration = 30)
            where TRequest : INetSerializable
        {
            if (!requestHandlers.ContainsKey(requestType))
            {
                Logging.LogError($"Cannot create request. Request type: {requestType} not registered.");
                return false;
            }
            if (!requestHandlers[requestType].IsRequestTypeValid(typeof(TRequest)))
            {
                Logging.LogError($"Cannot create request. Request type: {requestType}, {typeof(TRequest)} is not valid message type.");
                return false;
            }
            // Create request
            uint ackId = CreateRequest(requestHandlers[requestType], duration);
            // Write request
            writer.Reset();
            writer.PutPackedUShort(RequestMessageType);
            writer.PutPackedUShort(requestType);
            writer.PutPackedUInt(ackId);
            request.Serialize(writer);
            return true;
        }

        private void HandleRequest(long connectionId, NetDataReader reader)
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
            AckResponseCode responseCode;
            INetSerializable response;
            requestHandlers[requestType].InvokeRequest(connectionId, reader, out responseCode, out response);
            // Write response
            writer.Reset();
            writer.PutPackedUShort(ResponseMessageType);
            writer.PutPackedUInt(ackId);
            writer.PutValue(responseCode);
            response.Serialize(writer);
            // Send response
            SendMessage(connectionId, DeliveryMethod.ReliableOrdered, writer);
        }

        private void HandleResponse(long connectionId, NetDataReader reader)
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

        public void RegisterRequest<TRequest, TResponse>(ushort requestType, RequestDelegate<TRequest, TResponse> requestDelegate, ResponseDelegate<TResponse> responseDelegate)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            requestHandlers[requestType] = new LiteNetLibRequestHandler<TRequest, TResponse>(requestDelegate, responseDelegate);
        }

        public void UnregisterRequest(ushort requestType)
        {
            messageHandlers.Remove(requestType);
        }

        public void RegisterMessage(ushort messageType, MessageHandlerDelegate handlerDelegate)
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
