using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLibManager.Utils;

namespace LiteNetLibManager
{
    public class LiteNetLibClient : LiteNetLibPeerHandler
    {
        public LiteNetLibManager Manager { get; protected set; }
        public NetPeer Peer { get; protected set; }
        public bool IsConnected { get { return Peer != null && Peer.ConnectionState == ConnectionState.Connected; } }

        public LiteNetLibClient(LiteNetLibManager manager, string connectKey) : base(1, connectKey)
        {
            Manager = manager;
        }

        public override void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            base.OnNetworkError(endPoint, socketErrorCode);
            if (Manager.LogError) Debug.LogError("[" + Manager.name + "] LiteNetLibClient::OnNetworkError endPoint: " + endPoint + " socketErrorCode " + socketErrorCode);
            Manager.OnClientNetworkError(endPoint, socketErrorCode);
        }

        public override void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            base.OnNetworkLatencyUpdate(peer, latency);
        }

        public override void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            base.OnNetworkReceive(peer, reader);
        }

        public override void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            base.OnNetworkReceiveUnconnected(remoteEndPoint, reader, messageType);
            if (messageType == UnconnectedMessageType.DiscoveryResponse)
                Manager.OnClientReceivedDiscoveryResponse(remoteEndPoint, StringBytesConverter.ConvertToString(reader.Data));
        }

        public override void OnPeerConnected(NetPeer peer)
        {
            base.OnPeerConnected(peer);
            if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibClient::OnPeerConnected peer.ConnectId: " + peer.ConnectId);
            Peer = peer;
            Manager.OnClientConnected(peer);
        }

        public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            base.OnPeerDisconnected(peer, disconnectInfo);
            if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibClient::OnPeerDisconnected peer.ConnectId: " + peer.ConnectId + " disconnectInfo.Reason: " + disconnectInfo.Reason);
            Manager.StopClient();
            Manager.OnClientDisconnected(peer, disconnectInfo);
            Peer = null;
        }
    }
}
