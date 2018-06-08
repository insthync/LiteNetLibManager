using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLibManager.Utils;

namespace LiteNetLibManager
{
    public class LiteNetLibServer : LiteNetLibPeerHandler
    {
        public LiteNetLibServer(LiteNetLibManager manager, int maxConnections, string connectKey) : base(manager, maxConnections, connectKey)
        {
        }

        public override void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            base.OnNetworkError(endPoint, socketErrorCode);
            if (Manager.LogError) Debug.LogError("[" + Manager.name + "] LiteNetLibServer::OnNetworkError endPoint: " + endPoint + " socketErrorCode " + socketErrorCode);
            Manager.OnPeerNetworkError(endPoint, socketErrorCode);
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
            if (messageType == UnconnectedMessageType.DiscoveryRequest)
                Manager.OnServerReceivedDiscoveryRequest(remoteEndPoint, StringBytesConverter.ConvertToString(reader.Data));
        }

        public override void OnPeerConnected(NetPeer peer)
        {
            base.OnPeerConnected(peer);
            if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibServer::OnPeerConnected peer.ConnectId: " + peer.ConnectId);
            Manager.AddPeer(peer);
            Manager.OnPeerConnected(peer);
        }

        public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            base.OnPeerDisconnected(peer, disconnectInfo);
            if (Manager.LogInfo) Debug.Log("[" + Manager.name + "] LiteNetLibServer::OnPeerDisconnected peer.ConnectId: " + peer.ConnectId + " disconnectInfo.Reason: " + disconnectInfo.Reason);
            Manager.RemovePeer(peer);
            Manager.OnPeerDisconnected(peer, disconnectInfo);
        }
    }
}
