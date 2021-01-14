using LiteNetLib;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLibManager
{
    public class LiteNetLibTransportServerEventListener : INetEventListener
    {
        private readonly ITransport transport;
        private readonly string connectKey;
        private readonly Queue<TransportEventData> eventQueue;
        private readonly Dictionary<long, NetPeer> serverPeers;

        public LiteNetLibTransportServerEventListener(ITransport transport, string connectKey, Queue<TransportEventData> eventQueue, Dictionary<long, NetPeer> serverPeers)
        {
            this.transport = transport;
            this.connectKey = connectKey;
            this.eventQueue = eventQueue;
            this.serverPeers = serverPeers;
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            if (transport.ServerPeersCount < transport.ServerMaxConnections)
                request.AcceptIfKey(connectKey);
            else
                request.Reject();
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                endPoint = endPoint,
                socketError = socketError,
            });
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                connectionId = peer.Id,
                reader = reader,
            });
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnPeerConnected(NetPeer peer)
        {
            serverPeers[peer.Id] = peer;
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = peer.Id,
            });
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            serverPeers.Remove(peer.Id);
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = peer.Id,
                disconnectInfo = disconnectInfo,
            });
        }
    }
}
