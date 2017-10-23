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
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
    {
    }

    public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
    {
    }

    public void OnPeerConnected(NetPeer peer)
    {
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
    }
}
