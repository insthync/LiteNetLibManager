using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

public class LiteNetLibServer : INetEventListener
{
    public LiteNetLibManager manager { get; protected set; }
    public NetManager netManager { get; protected set; }
    public LiteNetLibServer(LiteNetLibManager manager, int maxConnections, string connectKey)
    {
        this.manager = manager;
        netManager = new NetManager(this, maxConnections, connectKey);
    }

    public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
    {
        if (manager.LogError) Debug.LogError("[" + manager.name + "] LiteNetLibServer::OnNetworkError endPoint: " + endPoint + " socketErrorCode " + socketErrorCode);
        manager.OnServerNetworkError(endPoint, socketErrorCode);
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
    {
    }

    public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
    {
        if (messageType == UnconnectedMessageType.DiscoveryRequest)
            manager.OnServerReceivedDiscoveryRequest(remoteEndPoint, StringBytesConverter.ConvertToString(reader.Data));
    }

    public void OnPeerConnected(NetPeer peer)
    {
        if (manager.LogInfo) Debug.Log("[" + manager.name + "] LiteNetLibServer::OnPeerConnected peer.ConnectId: " + peer.ConnectId);
        manager.AddPeer(peer);
        manager.OnServerConnected(peer);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (manager.LogInfo) Debug.Log("[" + manager.name + "] LiteNetLibServer::OnPeerDisconnected peer.ConnectId: " + peer.ConnectId + " disconnectInfo.Reason: " + disconnectInfo.Reason);
        manager.RemovePeer(peer);
        manager.OnServerDisconnected(peer, disconnectInfo);
    }
}
