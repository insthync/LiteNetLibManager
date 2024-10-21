using System.Collections.Generic;
using LiteNetLib.Utils;
#if !UNITY_WEBGL || UNITY_EDITOR
using WebSocketSharp;
using WebSocketSharp.Server;
#endif

namespace LiteNetLibManager
{
#if !UNITY_WEBGL || UNITY_EDITOR
    public class WebSocketServerBehavior : WebSocketBehavior
    {
        public long ConnectionId { get; private set; }
        private Queue<TransportEventData> eventQueue;
        private Dictionary<long, WebSocketServerBehavior> serverPeers;

        public void Initialize(long connectionId, Queue<TransportEventData> eventQueue, Dictionary<long, WebSocketServerBehavior> serverPeers)
        {
            ConnectionId = connectionId;
            this.eventQueue = eventQueue;
            this.serverPeers = serverPeers;
        }

        protected override void OnOpen()
        {
            base.OnOpen();
            if (serverPeers != null)
                serverPeers[ConnectionId] = this;
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = ConnectionId,
            });
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                connectionId = ConnectionId,
                reader = new NetDataReader(e.RawData),
            });
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            base.OnError(e);
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
            });
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            if (serverPeers != null)
                serverPeers.Remove(ConnectionId);
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = ConnectionId,
            });
        }
    }
#endif
}
