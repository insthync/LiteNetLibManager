using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLibHighLevel.Utils;

namespace LiteNetLibHighLevel
{
    public class LiteNetLibServer : INetEventListener
    {
        public LiteNetLibManager Manager { get; protected set; }
        public NetManager NetManager { get; protected set; }

        public LiteNetLibServer(LiteNetLibManager manager, int maxConnections, string connectKey)
        {
            this.Manager = manager;
            NetManager = new NetManager(this, maxConnections, connectKey);
        }

        public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            if (Manager.LogError) Debug.LogError("[" + Manager.name + "] LiteNetLibServer::OnNetworkError endPoint: " + endPoint + " socketErrorCode " + socketErrorCode);
            Manager.OnServerNetworkError(endPoint, socketErrorCode);
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            Manager.ServerReadPacket(peer, reader);
        }

        public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            if (messageType == UnconnectedMessageType.DiscoveryRequest)
                Manager.OnServerReceivedDiscoveryRequest(remoteEndPoint, StringBytesConverter.ConvertToString(reader.Data));
        }

        public void OnPeerConnected(NetPeer peer)
        {
            if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibServer::OnPeerConnected peer.ConnectId: " + peer.ConnectId);
            Manager.AddPeer(peer);
            Manager.OnServerConnected(peer);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibServer::OnPeerDisconnected peer.ConnectId: " + peer.ConnectId + " disconnectInfo.Reason: " + disconnectInfo.Reason);
            Manager.RemovePeer(peer);
            Manager.OnServerDisconnected(peer, disconnectInfo);
        }
    }
}
