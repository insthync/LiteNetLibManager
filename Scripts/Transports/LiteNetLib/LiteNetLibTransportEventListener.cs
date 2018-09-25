using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections;
using System.Collections.Generic;

namespace LiteNetLibManager
{
    public class LiteNetLibTransportEventListener : INetEventListener
    {
        private Queue<LiteNetLibTransportEventData> eventQueue;

        public LiteNetLibTransportEventListener(Queue<LiteNetLibTransportEventData> eventQueue)
        {
            this.eventQueue = eventQueue;
        }

        public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            eventQueue.Enqueue(new LiteNetLibTransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                connectionId = peer.ConnectId,
                reader = reader,
            });
        }

        public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnPeerConnected(NetPeer peer)
        {
            eventQueue.Enqueue(new LiteNetLibTransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = peer.ConnectId,
            });
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            eventQueue.Enqueue(new LiteNetLibTransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = peer.ConnectId,
                disconnectInfo = disconnectInfo,
            });
        }
    }
}
