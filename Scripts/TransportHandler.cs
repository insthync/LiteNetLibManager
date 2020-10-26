using LiteNetLib.Utils;
using System.Collections.Generic;

namespace LiteNetLibManager
{
    public abstract class TransportHandler
    {
        protected readonly NetDataWriter writer = new NetDataWriter();

        public ITransport Transport { get; protected set; }
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
