using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibPeerHandler : INetEventListener
    {
        public NetManager NetManager { get; protected set; }
        protected readonly NetDataWriter writer = new NetDataWriter();
        protected readonly Dictionary<ushort, MessageHandlerDelegate> messageHandlers = new Dictionary<ushort, MessageHandlerDelegate>();
        protected readonly Dictionary<uint, AckMessageCallback> ackCallbacks = new Dictionary<uint, AckMessageCallback>();
        protected uint nextAckId = 1;

        public int AckCallbacksCount { get { return ackCallbacks.Count; } }

        public LiteNetLibPeerHandler(int maxConnections, string connectKey)
        {
            NetManager = new NetManager(this, maxConnections, connectKey);
        }

        public void PollEvents()
        {
            NetManager.PollEvents();
        }

        public bool Start()
        {
            // Reset acks
            ackCallbacks.Clear();
            nextAckId = 1;
            return NetManager.Start();
        }

        public bool Start(int port)
        {
            // Reset acks
            ackCallbacks.Clear();
            nextAckId = 1;
            return NetManager.Start(port);
        }

        public NetPeer Connect(string address, int port)
        {
            return NetManager.Connect(address, port);
        }

        public void Stop()
        {
            NetManager.Stop();
        }

        public virtual void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
        }

        public virtual void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public virtual void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            ReadPacket(peer, reader);
        }

        public virtual void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
        }

        public virtual void OnPeerConnected(NetPeer peer)
        {
        }

        public virtual void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
        }
        
        protected void ReadPacket(NetPeer peer, NetDataReader reader)
        {
            var msgType = reader.GetPackedUShort();
            MessageHandlerDelegate handlerDelegate;
            if (messageHandlers.TryGetValue(msgType, out handlerDelegate))
            {
                var messageHandler = new LiteNetLibMessageHandler(msgType, this, peer, reader);
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

        public uint SendAckPacket<T>(SendOptions options, NetPeer peer, ushort msgType, T messageData, AckMessageCallback callback) where T : BaseAckMessage
        {
            var ackId = nextAckId++;
            lock (ackCallbacks)
                ackCallbacks.Add(ackId, callback);
            messageData.ackId = ackId;
            LiteNetLibPacketSender.SendPacket(options, peer, msgType, messageData);
            return ackId;
        }

        public void TriggerAck<T>(uint ackId, AckResponseCode responseCode, T messageData) where T : BaseAckMessage
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
        }
    }
}
