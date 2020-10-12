using LiteNetLib;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLibManager
{
    public class LiteNetLibTransportEventListener : INetEventListener
    {
        private LiteNetLibTransport transport;
        private Queue<TransportEventData> eventQueue;
        private Dictionary<long, NetPeer> serverPeers;

        public LiteNetLibTransportEventListener(LiteNetLibTransport transport, Queue<TransportEventData> eventQueue)
        {
            this.transport = transport;
            this.eventQueue = eventQueue;
        }

        public LiteNetLibTransportEventListener(LiteNetLibTransport transport, Queue<TransportEventData> eventQueue, Dictionary<long, NetPeer> serverPeers) : this(transport, eventQueue)
        {
            this.serverPeers = serverPeers;
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            if (transport.server.ConnectedPeersCount < transport.maxConnections)
                request.AcceptIfKey(transport.connectKey);
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
            if (serverPeers != null)
                serverPeers[peer.Id] = peer;
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = peer.Id,
            });
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (serverPeers != null)
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
