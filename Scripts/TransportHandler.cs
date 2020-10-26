using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public abstract class TransportHandler
    {
        protected readonly NetDataWriter writer = new NetDataWriter();

        public ITransport Transport { get; protected set; }
        protected readonly Dictionary<ushort, MessageHandlerDelegate> messageHandlers = new Dictionary<ushort, MessageHandlerDelegate>();
        protected readonly Dictionary<uint, AckMessageCallback> ackCallbacks = new Dictionary<uint, AckMessageCallback>();
        protected readonly Dictionary<uint, long> requestTimes = new Dictionary<uint, long>();
        protected readonly Dictionary<uint, long> requestDurations = new Dictionary<uint, long>();
        protected uint nextAckId = 1;
        protected TransportEventData tempEventData;
        protected bool isNetworkActive;

        public int AckCallbacksCount { get { return ackCallbacks.Count; } }

        public TransportHandler(ITransport transport)
        {
            Transport = transport;
        }

        public virtual void Update()
        {
            if (!isNetworkActive)
                return;
            if (AckCallbacksCount > 0)
            {
                List<uint> ackIds = new List<uint>(requestTimes.Keys);
                foreach (uint ackId in ackIds)
                {
                    if (requestDurations[ackId] > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - requestTimes[ackId] >= requestDurations[ackId])
                    {
                        // Timeout
                        ReadResponse(ackId, AckResponseCode.Timeout, new BaseAckMessage());
                    }
                }
            }
        }

        protected void ReadPacket(long connectionId, NetDataReader reader)
        {
            ushort msgType = reader.GetPackedUShort();
            MessageHandlerDelegate handlerDelegate;
            if (messageHandlers.TryGetValue(msgType, out handlerDelegate))
            {
                LiteNetLibMessageHandler messageHandler = new LiteNetLibMessageHandler(msgType, this, connectionId, reader);
                handlerDelegate.Invoke(messageHandler);
            }
        }

        public void RegisterMessage(ushort msgType, MessageHandlerDelegate handlerDelegate)
        {
            messageHandlers[msgType] = handlerDelegate;
        }

        public void UnregisterMessage(ushort msgType)
        {
            messageHandlers.Remove(msgType);
        }

        protected uint CreateRequest(AckMessageCallback callback, long duration)
        {
            uint ackId = nextAckId++;
            lock (ackCallbacks)
            {
                ackCallbacks.Add(ackId, callback);
            }
            lock (requestTimes)
            {
                requestTimes.Add(ackId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            lock (requestDurations)
            {
                requestDurations.Add(ackId, duration);
            }
            return ackId;
        }

        public void ReadResponse<T>(uint ackId, AckResponseCode responseCode, T messageData) where T : BaseAckMessage
        {
            lock (ackCallbacks)
            {
                AckMessageCallback ackCallback;
                if (ackCallbacks.TryGetValue(ackId, out ackCallback))
                {
                    ackCallbacks.Remove(ackId);
                    ackCallback(responseCode, messageData);
                }
            }
            lock (requestTimes)
            {
                requestTimes.Remove(ackId);
            }
            lock (requestDurations)
            {
                requestDurations.Remove(ackId);
            }
        }
    }
}
