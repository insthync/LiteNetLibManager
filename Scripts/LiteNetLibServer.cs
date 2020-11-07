using LiteNetLib;
using LiteNetLib.Utils;
using System;

namespace LiteNetLibManager
{
    public class LiteNetLibServer : TransportHandler
    {
        public LiteNetLibManager Manager { get; protected set; }
        public override string LogTag { get { return $"{(Manager == null ? "(No Manager)" : Manager.LogTag)}->LiteNetLibServer"; } }
        private bool isNetworkActive;
        public override bool IsNetworkActive { get { return isNetworkActive; } }
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
            if (!IsNetworkActive)
                return;
            base.Update();
            while (Transport.ServerReceive(out tempEventData))
            {
                OnServerReceive(tempEventData);
            }
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (IsNetworkActive)
            {
                Logging.LogWarning(LogTag, "Cannot Start Server, network already active");
                return false;
            }
            // Reset acks
            requestCallbacks.Clear();
            nextAckId = 1;
            ServerPort = port;
            return isNetworkActive = Transport.StartServer(port, maxConnections);
        }

        public void StopServer()
        {
            Transport.StopServer();
            ServerPort = 0;
            isNetworkActive = false;
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

        protected override void SendMessage(long connectionId, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            Transport.ServerSend(connectionId, deliveryMethod, writer);
        }

        public void SendPacket(long connectionId, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            WritePacket(writer, msgType, serializer);
            SendMessage(connectionId, deliveryMethod, writer);
        }

        public bool SendRequest<TRequest>(long connectionId, ushort requestType, TRequest request, SerializerDelegate extraRequestSerializer = null, long duration = 30, ResponseDelegate responseDelegate = null)
            where TRequest : INetSerializable
        {
            if (!CreateAndWriteRequest(writer, requestType, request, extraRequestSerializer, duration, responseDelegate))
                return false;
            SendMessage(connectionId, DeliveryMethod.ReliableOrdered, writer);
            return true;
        }
    }
}
