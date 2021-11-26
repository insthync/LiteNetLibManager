using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLibManager
{
    public class MixTransportEventListener : INetEventListener
    {
        private MixTransport mixTransport;
        private Queue<TransportEventData> eventQueue;
        private Dictionary<long, NetPeer> peersDict;
        private Dictionary<long, long> peerIdsDict;

        public MixTransportEventListener(MixTransport mixTransport, Queue<TransportEventData> eventQueue)
        {
            // This is constructor for client
            this.mixTransport = mixTransport;
            this.eventQueue = eventQueue;
        }

        public MixTransportEventListener(MixTransport mixTransport, Queue<TransportEventData> eventQueue, Dictionary<long, NetPeer> peersDict) : this(mixTransport, eventQueue)
        {
            // This is constructor for server
            this.peersDict = peersDict;
            peerIdsDict = new Dictionary<long, long>();
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            if (mixTransport.Server.ConnectedPeersCount < mixTransport.ServerMaxConnections)
                request.AcceptIfKey(mixTransport.ConnectKey);
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
            long tempConnectionId = 0;
            if (peerIdsDict != null && peersDict != null)
                tempConnectionId = peerIdsDict[peer.Id];

            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                connectionId = tempConnectionId,
                reader = reader,
            });
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnPeerConnected(NetPeer peer)
        {
            long tempConnectionId = 0;
            if (peerIdsDict != null && peersDict != null)
            {
                tempConnectionId = mixTransport.GetNewConnectionID();
                peersDict[tempConnectionId] = peer;
                peerIdsDict[peer.Id] = tempConnectionId;
            }

            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = tempConnectionId,
            });
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            long tempConnectionId = 0;
            if (peerIdsDict != null && peersDict != null)
            {
                tempConnectionId = peerIdsDict[peer.Id];
                peersDict.Remove(tempConnectionId);
                peerIdsDict.Remove(peer.Id);
            }

            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = tempConnectionId,
                disconnectInfo = disconnectInfo,
            });
        }
    }
}
