using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibServer : TransportHandler
    {
        public LiteNetLibManager Manager { get; protected set; }

        public LiteNetLibServer(LiteNetLibManager manager, string connectKey) : base(manager.Transport, connectKey)
        {
            Manager = manager;
        }

        public override void OnServerReceive(TransportEventData eventData)
        {
            switch (eventData.type)
            {
                case ENetworkEvent.ConnectEvent:
                    if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibServer::OnPeerConnected peer.ConnectionId: " + eventData.connectionId);
                    Manager.AddConnectionId(eventData.connectionId);
                    Manager.OnPeerConnected(eventData.connectionId);
                    break;
                case ENetworkEvent.DataEvent:
                    ReadPacket(eventData.connectionId, eventData.reader);
                    break;
                case ENetworkEvent.DisconnectEvent:
                    if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibServer::OnPeerDisconnected peer.ConnectionId: " + eventData.connectionId + " disconnectInfo.Reason: " + eventData.disconnectInfo.Reason);
                    Manager.RemoveConnectionId(eventData.connectionId);
                    Manager.OnPeerDisconnected(eventData.connectionId, eventData.disconnectInfo);
                    break;
                case ENetworkEvent.ErrorEvent:
                    if (Manager.LogError) Debug.LogError("[" + Manager.name + "] LiteNetLibServer::OnNetworkError endPoint: " + eventData.endPoint + " socketErrorCode " + eventData.socketError);
                    Manager.OnPeerNetworkError(eventData.endPoint, eventData.socketError);
                    break;
            }
        }
    }
}
