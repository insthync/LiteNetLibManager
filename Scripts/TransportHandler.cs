using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public abstract class TransportHandler
    {
        protected readonly NetDataWriter writer = new NetDataWriter();

        public ITransport Transport { get; protected set; }
        public string ConnectKey { get; protected set; }
        public bool IsClientConnected { get { return Transport.IsClientStarted(); } }
        public bool IsServerRunning { get { return Transport.IsServerStarted(); } }
        protected long clientConnectionId;
        public long ClientConnectionId { get { return clientConnectionId; } }
        public int ServerPort { get; protected set; }
        protected readonly Dictionary<ushort, MessageHandlerDelegate> messageHandlers = new Dictionary<ushort, MessageHandlerDelegate>();
        protected readonly Dictionary<uint, AckMessageCallback> ackCallbacks = new Dictionary<uint, AckMessageCallback>();
        protected uint nextAckId = 1;
        protected TransportEventData tempEventData;
        protected bool isNetworkActive;

        public int AckCallbacksCount { get { return ackCallbacks.Count; } }

        public TransportHandler(ITransport transport, string connectKey)
        {
            Transport = transport;
            ConnectKey = connectKey;
        }

        public virtual void OnClientReceive(TransportEventData eventData) { }

        public virtual void OnServerReceive(TransportEventData eventData) { }

        public void Update()
        {
            while (Transport.ServerReceive(out tempEventData))
            {
                OnServerReceive(tempEventData);
            }
            while (Transport.ClientReceive(out tempEventData))
            {
                OnClientReceive(tempEventData);
            }
        }

        public bool StartClient(string address, int port)
        {
            if (isNetworkActive)
            {
                Debug.LogWarning("[TransportHandler] Cannot Start Client, network already active");
                return false;
            }
            isNetworkActive = true;
            // Reset acks
            ackCallbacks.Clear();
            nextAckId = 1;
            return Transport.StartClient(ConnectKey, address, port, out clientConnectionId);
        }

        public void StopClient()
        {
            if (isNetworkActive)
                isNetworkActive = false;
            Transport.StopClient();
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (isNetworkActive)
            {
                Debug.LogWarning("[TransportHandler] Cannot Start Server, network already active");
                return false;
            }
            isNetworkActive = true;
            // Reset acks
            ackCallbacks.Clear();
            nextAckId = 1;
            ServerPort = port;
            return Transport.StartServer(ConnectKey, port, maxConnections);
        }

        public void StopServer()
        {
            if (isNetworkActive)
                isNetworkActive = false;
            Transport.StopServer();
            ServerPort = 0;
        }

        protected void ReadPacket(long connectionId, NetDataReader reader)
        {
            var msgType = reader.GetPackedUShort();
            MessageHandlerDelegate handlerDelegate;
            if (messageHandlers.TryGetValue(msgType, out handlerDelegate))
            {
                var messageHandler = new LiteNetLibMessageHandler(msgType, this, connectionId, reader);
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

        public uint AddAckCallback(AckMessageCallback callback)
        {
            var ackId = nextAckId++;
            lock (ackCallbacks)
                ackCallbacks.Add(ackId, callback);
            return ackId;
        }

        public uint ClientSendAckPacket<T>(SendOptions options, ushort msgType, T messageData, AckMessageCallback callback) where T : BaseAckMessage
        {
            messageData.ackId = AddAckCallback(callback);
            ClientSendPacket(options, msgType, messageData.Serialize);
            return messageData.ackId;
        }

        public uint ServerSendAckPacket<T>(long connectionId, SendOptions options, ushort msgType, T messageData, AckMessageCallback callback) where T : BaseAckMessage
        {
            messageData.ackId = AddAckCallback(callback);
            ServerSendPacket(connectionId, options, msgType, messageData.Serialize);
            return messageData.ackId;
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

        public void ClientSendPacket(SendOptions options, ushort msgType, System.Action<NetDataWriter> serializer)
        {
            writer.Reset();
            writer.PutPackedUShort(msgType);
            if (serializer != null)
                serializer(writer);
            Transport.ClientSend(options, writer);
        }

        public void ServerSendPacket(long connectionId, SendOptions options, ushort msgType, System.Action<NetDataWriter> serializer)
        {
            writer.Reset();
            writer.PutPackedUShort(msgType);
            if (serializer != null)
                serializer(writer);
            Transport.ServerSend(connectionId, options, writer);
        }
    }
}
