using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

public class LiteNetLibClient : INetEventListener
{
    public LiteNetLibManager manager { get; protected set; }
    public NetManager netManager { get; protected set; }
    public LiteNetLibClient(LiteNetLibManager manager, string connectKey)
    {
        this.manager = manager;
        netManager = new NetManager(this, connectKey);
    }

    public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
    {
        if (manager.LogError) Debug.LogError("[" + manager.name + "] LiteNetLibClient::OnNetworkError endPoint: " + endPoint + " socketErrorCode " + socketErrorCode);
        manager.OnClientNetworkError(endPoint, socketErrorCode);
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
    {
    }

    public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
    {
        if (messageType == UnconnectedMessageType.DiscoveryResponse)
            manager.OnClientReceivedDiscoveryResponse(remoteEndPoint, StringBytesConverter.ConvertToString(reader.Data));
    }

    public void OnPeerConnected(NetPeer peer)
    {
        if (manager.LogInfo) Debug.Log("[" + manager.name + "] LiteNetLibClient::OnPeerConnected peer.ConnectId: " + peer.ConnectId);
        manager.OnClientConnected(peer);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (manager.LogInfo) Debug.Log("[" + manager.name + "] LiteNetLibClient::OnPeerDisconnected peer.ConnectId: " + peer.ConnectId + " disconnectInfo.Reason: " + disconnectInfo.Reason);
        manager.OnClientDisconnected(peer, disconnectInfo);
    }
}
