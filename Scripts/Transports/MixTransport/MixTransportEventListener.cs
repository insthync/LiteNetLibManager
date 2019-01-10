using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class MixTransportEventListener : INetEventListener
    {
        private MixTransport mixTransport;
        private Queue<TransportEventData> eventQueue;
        private Dictionary<long, NetPeer> peersDict;
        private Dictionary<long, long> peerIdsDict;
        private long tempConnectionId;

        public MixTransportEventListener(MixTransport mixTransport, Queue<TransportEventData> eventQueue, Dictionary<long, NetPeer> peersDict)
        {
            this.mixTransport = mixTransport;
            this.eventQueue = eventQueue;
            this.peersDict = peersDict;
            peerIdsDict = new Dictionary<long, long>();
        }

        public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                endPoint = endPoint,
                socketErrorCode = socketErrorCode,
            });
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                connectionId = peer.ConnectId,
                reader = reader.Clone(),
            });
        }

        public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnPeerConnected(NetPeer peer)
        {
            tempConnectionId = mixTransport.GetNewConnectionID();
            peersDict[tempConnectionId] = peer;
            peerIdsDict[peer.ConnectId] = tempConnectionId;

            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = peer.ConnectId,
            });
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            tempConnectionId = peerIdsDict[peer.ConnectId];
            peersDict.Remove(tempConnectionId);
            peerIdsDict.Remove(peer.ConnectId);

            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = peer.ConnectId,
                disconnectInfo = disconnectInfo,
            });
        }
    }
}
