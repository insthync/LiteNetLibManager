using LiteNetLib;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLibManager
{
    public class LiteNetLibTransportServerEventListener : INetEventListener
    {
        private readonly ITransport _transport;
        private readonly string _connectKey;
        private readonly Queue<TransportEventData> _eventQueue;
        private readonly Dictionary<long, NetPeer> _serverPeers;

        public LiteNetLibTransportServerEventListener(ITransport transport, string connectKey, Queue<TransportEventData> eventQueue, Dictionary<long, NetPeer> serverPeers)
        {
            _transport = transport;
            _connectKey = connectKey;
            _eventQueue = eventQueue;
            _serverPeers = serverPeers;
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            if (_transport.ServerPeersCount < _transport.ServerMaxConnections)
                request.AcceptIfKey(_connectKey);
            else
                request.Reject();
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                endPoint = endPoint,
                socketError = socketError,
            });
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            _eventQueue.Enqueue(new TransportEventData()
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
            _serverPeers[peer.Id] = peer;
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = peer.Id,
            });
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _serverPeers.Remove(peer.Id);
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = peer.Id,
                disconnectInfo = disconnectInfo,
            });
        }
    }
}
