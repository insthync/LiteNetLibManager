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
        protected readonly Dictionary<uint, BaseRequestMessageData> requests = new Dictionary<uint, BaseRequestMessageData>();
        protected uint nextAckId = 1;
        protected TransportEventData tempEventData;
        protected bool isNetworkActive;

        public int RequestsCount { get { return requests.Count; } }

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
                List<uint> ackIds = new List<uint>(requests.Keys);
                foreach (uint ackId in ackIds)
                {
                    if (requests[ackId].ResponseTimeout())
                    {
                        lock (requests)
                        {
                            requests.Remove(ackId);
                        }
                    }
                }
            }
        }

        protected void ReadPacket(long connectionId, NetDataReader reader)
        {
            ushort msgType = reader.GetPackedUShort();
            if (RequestResponseEnabled && RequestMessageType == msgType)
            {
                HandleRequest(connectionId, reader);
                return;
            }
            if (RequestResponseEnabled && ResponseMessageType == msgType)
            {
                HandleResponse(connectionId, reader);
                return;
            }
            MessageHandlerDelegate handlerDelegate;
            if (!messageHandlers.TryGetValue(msgType, out handlerDelegate))
                return;
            LiteNetLibMessageHandler messageHandler = new LiteNetLibMessageHandler(msgType, this, connectionId, reader);
            handlerDelegate.Invoke(messageHandler);
        }

        private void HandleRequest(long connectionId, NetDataReader reader)
        {
            ushort requestType = reader.GetPackedUShort();
            uint ackId = reader.GetPackedUInt();
            // Send response
        }

        private void HandleResponse(long connectionId, NetDataReader reader)
        {
            uint ackId = reader.GetPackedUInt();
            AckResponseCode responseCode = reader.GetValue<AckResponseCode>();
            if (requests.ContainsKey(ackId))
            {
                requests[ackId].Response(reader);
                lock (requests)
                {
                    requests.Remove(ackId);
                }
            }
        }

        public void RegisterRequest<TRequest, TResponse>(ushort requestType, RequestDelegate<TRequest> requestDelegate, ResponseDelegate<TResponse> responseDelegate)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {

        }

        protected uint CreateRequest<TRequest>(ushort requestType, long duration)
            where TRequest : INetSerializable, new()
        {
            uint ackId = nextAckId++;
            lock (requests)
            {
                // Get response callback by request type
                requests.Add(ackId, new RequestMessageData<T>(ackId, callback, duration));
            }
            return ackId;
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

        public void UnregisterMessage(ushort msgType)
        {
            messageHandlers.Remove(msgType);
        }

        protected uint CreateRequest<T>(AckMessageCallback<T> callback, long duration)
            where T : BaseAckMessage, new()
        {
            uint ackId = nextAckId++;
            lock (requests)
            {
                requests.Add(ackId, new RequestMessageData<T>(ackId, callback, duration));
            }
            return ackId;
        }

        public void ReadResponse(NetDataReader reader)
        {
            uint ackId = reader.PeekUInt();
            if (requests.ContainsKey(ackId))
            {
                requests[ackId].Response(reader);
                lock (requests)
                {
                    requests.Remove(ackId);
                }
            }
        }
    }
}
