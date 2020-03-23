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
        protected readonly Dictionary<uint, long> ackTimes = new Dictionary<uint, long>();
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
                foreach (uint ackId in ackTimes.Keys)
                {
                    if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ackTimes[ackId] >= 30)
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

        protected uint AddAckCallback(AckMessageCallback callback)
        {
            uint ackId = nextAckId++;
            lock (ackCallbacks)
                ackCallbacks.Add(ackId, callback);
            lock (ackTimes)
                ackTimes.Add(ackId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
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
            lock (ackTimes)
            {
                ackTimes.Remove(ackId);
            }
        }
    }
}
