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
        public bool IsClientStarted { get { return Transport.IsClientStarted(); } }
        public bool IsServerStarted { get { return Transport.IsServerStarted(); } }
        public int ServerPort { get; protected set; }
        protected readonly Dictionary<ushort, MessageHandlerDelegate> messageHandlers = new Dictionary<ushort, MessageHandlerDelegate>();
        protected readonly Dictionary<uint, AckMessageCallback> ackCallbacks = new Dictionary<uint, AckMessageCallback>();
        protected uint nextAckId = 1;
        protected TransportEventData tempEventData;
        protected bool isClientActive;
        protected bool isServerActive;
        protected bool isNetworkActive { get { return isClientActive || isServerActive; } }

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
            if (isServerActive)
            {
                while (Transport.ServerReceive(out tempEventData))
                {
                    OnServerReceive(tempEventData);
                }
            }
            if (isClientActive)
            {
                while (Transport.ClientReceive(out tempEventData))
                {
                    OnClientReceive(tempEventData);
                }
            }
        }

        public bool StartClient(string address, int port)
        {
            if (isNetworkActive)
            {
                Debug.LogWarning("[TransportHandler] Cannot Start Client, network already active");
                return false;
            }
            isClientActive = true;
            // Reset acks
            ackCallbacks.Clear();
            nextAckId = 1;
            return Transport.StartClient(ConnectKey, address, port);
        }

        public void StopClient()
        {
            isClientActive = false;
            Transport.StopClient();
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (isNetworkActive)
            {
                Debug.LogWarning("[TransportHandler] Cannot Start Server, network already active");
                return false;
            }
            isServerActive = true;
            // Reset acks
            ackCallbacks.Clear();
            nextAckId = 1;
            ServerPort = port;
            return Transport.StartServer(ConnectKey, port, maxConnections);
        }

        public bool StartServerOffline()
        {
            return StartServer(Transport.GetFreePort(), 1);
        }

        public void StopServer()
        {
            isServerActive = false;
            Transport.StopServer();
            ServerPort = 0;
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

        public uint AddAckCallback(AckMessageCallback callback)
        {
            uint ackId = nextAckId++;
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
                serializer.Invoke(writer);
            Transport.ClientSend(options, writer);
        }

        public void ServerSendPacket(long connectionId, SendOptions options, ushort msgType, System.Action<NetDataWriter> serializer)
        {
            writer.Reset();
            writer.PutPackedUShort(msgType);
            if (serializer != null)
                serializer.Invoke(writer);
            Transport.ServerSend(connectionId, options, writer);
        }
    }
}
