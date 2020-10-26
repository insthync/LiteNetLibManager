using LiteNetLib;
using LiteNetLib.Utils;
using System;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibServer : TransportHandler
    {
        public LiteNetLibManager Manager { get; protected set; }
        public string LogTag { get { return $"{(Manager == null ? "(No Manager)" : Manager.LogTag)}->LiteNetLibServer"; } }
        public bool IsServerStarted { get { return Transport.IsServerStarted(); } }
        public int ServerPort { get; protected set; }

        public LiteNetLibServer(LiteNetLibManager manager) : base(manager.Transport)
        {
            Manager = manager;
        }

        public LiteNetLibServer(ITransport transport) : base(transport)
        {

        }

        public override void Update()
        {
            if (!isNetworkActive)
                return;
            base.Update();
            while (Transport.ServerReceive(out tempEventData))
            {
                OnServerReceive(tempEventData);
            }
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (isNetworkActive)
            {
                Logging.LogWarning(LogTag, "Cannot Start Server, network already active");
                return false;
            }
            isNetworkActive = true;
            // Reset acks
            ackCallbacks.Clear();
            requestTimes.Clear();
            requestDurations.Clear();
            nextAckId = 1;
            ServerPort = port;
            return Transport.StartServer(port, maxConnections);
        }

        public void StopServer()
        {
            isNetworkActive = false;
            Transport.StopServer();
            ServerPort = 0;
        }

        public virtual void OnServerReceive(TransportEventData eventData)
        {
            switch (eventData.type)
            {
                case ENetworkEvent.ConnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, "OnPeerConnected peer.ConnectionId: " + eventData.connectionId);
                    Manager.AddConnectionId(eventData.connectionId);
                    Manager.OnPeerConnected(eventData.connectionId);
                    break;
                case ENetworkEvent.DataEvent:
                    ReadPacket(eventData.connectionId, eventData.reader);
                    break;
                case ENetworkEvent.DisconnectEvent:
                    if (Manager.LogInfo) Logging.Log(LogTag, "OnPeerDisconnected peer.ConnectionId: " + eventData.connectionId + " disconnectInfo.Reason: " + eventData.disconnectInfo.Reason);
                    Manager.RemoveConnectionId(eventData.connectionId);
                    Manager.OnPeerDisconnected(eventData.connectionId, eventData.disconnectInfo);
                    break;
                case ENetworkEvent.ErrorEvent:
                    if (Manager.LogError) Logging.LogError(LogTag, "OnNetworkError endPoint: " + eventData.endPoint + " socketErrorCode " + eventData.socketError);
                    Manager.OnPeerNetworkError(eventData.endPoint, eventData.socketError);
                    break;
            }
        }

        public uint SendRequest<T>(long connectionId, ushort msgType, T messageData, AckMessageCallback callback, Action<NetDataWriter> extraSerializer = null, long duration = 30) where T : BaseAckMessage
        {
            messageData.ackId = CreateRequest(callback, duration);
            SendPacket(connectionId, DeliveryMethod.ReliableOrdered, msgType, (writer) =>
            {
                messageData.Serialize(writer);
                if (extraSerializer != null)
                    extraSerializer.Invoke(writer);
            });
            return messageData.ackId;
        }

        public void SendResponse<T>(long connectionId, ushort msgType, T messageData, Action<NetDataWriter> extraSerializer = null) where T : BaseAckMessage
        {
            writer.Reset();
            writer.PutPackedUShort(msgType);
            messageData.Serialize(writer);
            if (extraSerializer != null)
                extraSerializer.Invoke(writer);
            Transport.ServerSend(connectionId, DeliveryMethod.ReliableOrdered, writer);
        }

        public void SendPacket(long connectionId, DeliveryMethod deliveryMethod, ushort msgType, Action<NetDataWriter> serializer)
        {
            writer.Reset();
            writer.PutPackedUShort(msgType);
            if (serializer != null)
                serializer.Invoke(writer);
            Transport.ServerSend(connectionId, deliveryMethod, writer);
        }
    }
}
