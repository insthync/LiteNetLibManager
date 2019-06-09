using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibClient : TransportHandler
    {
        public LiteNetLibManager Manager { get; protected set; }
        
        public LiteNetLibClient(LiteNetLibManager manager) : base(manager.Transport)
        {
            Manager = manager;
        }

        public override void OnClientReceive(TransportEventData eventData)
        {
            switch (eventData.type)
            {
                case ENetworkEvent.ConnectEvent:
                    if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibClient::OnPeerConnected");
                    Manager.OnClientConnected();
                    break;
                case ENetworkEvent.DataEvent:
                    ReadPacket(eventData.connectionId, eventData.reader);
                    break;
                case ENetworkEvent.DisconnectEvent:
                    if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibClient::OnPeerDisconnected peer. disconnectInfo.Reason: " + eventData.disconnectInfo.Reason);
                    Manager.StopClient();
                    Manager.OnClientDisconnected(eventData.disconnectInfo);
                    break;
                case ENetworkEvent.ErrorEvent:
                    if (Manager.LogError) Debug.LogError("[" + Manager.name + "] LiteNetLibClient::OnNetworkError endPoint: " + eventData.endPoint + " socketErrorCode " + eventData.socketError);
                    Manager.OnClientNetworkError(eventData.endPoint, eventData.socketError);
                    break;
            }
        }
    }
}
