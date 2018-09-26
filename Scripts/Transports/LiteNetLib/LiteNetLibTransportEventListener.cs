using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections;
using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibTransportEventListener : INetEventListener
    {
        private Queue<TransportEventData> eventQueue;
        private Dictionary<long, NetPeer> peersDict;

        public LiteNetLibTransportEventListener(Queue<TransportEventData> eventQueue)
        {
            this.eventQueue = eventQueue;
        }

        public LiteNetLibTransportEventListener(Queue<TransportEventData> eventQueue, Dictionary<long, NetPeer> peersDict) : this(eventQueue)
        {
            this.peersDict = peersDict;
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
            if (peersDict != null)
                peersDict[peer.ConnectId] = peer;

            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = peer.ConnectId,
            });
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (peersDict != null)
                peersDict.Remove(peer.ConnectId);

            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = peer.ConnectId,
                disconnectInfo = disconnectInfo,
            });
        }
    }
}
