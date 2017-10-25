using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLibHighLevel.Utils;

namespace LiteNetLibHighLevel
{
    public class LiteNetLibClient : INetEventListener
    {
        public LiteNetLibManager Manager { get; protected set; }
        public NetManager NetManager { get; protected set; }
        public NetPeer Peer { get; protected set; }
        public bool IsConnected { get { return Peer != null && Peer.ConnectionState == ConnectionState.Connected; } }

        public LiteNetLibClient(LiteNetLibManager manager, string connectKey)
        {
            Manager = manager;
            NetManager = new NetManager(this, connectKey);
        }

        public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            if (Manager.LogError) Debug.LogError("[" + Manager.name + "] LiteNetLibClient::OnNetworkError endPoint: " + endPoint + " socketErrorCode " + socketErrorCode);
            Manager.OnClientNetworkError(endPoint, socketErrorCode);
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            Manager.ClientReadPacket(peer, reader);
        }

        public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            if (messageType == UnconnectedMessageType.DiscoveryResponse)
                Manager.OnClientReceivedDiscoveryResponse(remoteEndPoint, StringBytesConverter.ConvertToString(reader.Data));
        }

        public void OnPeerConnected(NetPeer peer)
        {
            if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibClient::OnPeerConnected peer.ConnectId: " + peer.ConnectId);
            Peer = peer;
            Manager.OnClientConnected(peer);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibClient::OnPeerDisconnected peer.ConnectId: " + peer.ConnectId + " disconnectInfo.Reason: " + disconnectInfo.Reason);
            Manager.StopClient();
            Manager.OnClientDisconnected(peer, disconnectInfo);
            Peer = null;
        }
    }
}
