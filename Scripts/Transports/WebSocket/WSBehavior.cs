using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
#if !UNITY_WEBGL || UNITY_EDITOR
using WebSocketSharp;
using WebSocketSharp.Server;
#endif

namespace LiteNetLibManager
{
#if !UNITY_WEBGL || UNITY_EDITOR
    public class WSBehavior : WebSocketBehavior
    {
        public long connectionId { get; private set; }
        private Queue<TransportEventData> eventQueue;

        public void Initialize(long connectionId, Queue<TransportEventData> eventQueue)
        {
            this.connectionId = connectionId;
            this.eventQueue = eventQueue;
        }


        protected override void OnOpen()
        {
            base.OnOpen();
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = connectionId,
            });
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                connectionId = connectionId,
                reader = new NetDataReader(e.RawData),
            });
        }

        protected override void OnError(ErrorEventArgs e)
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
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = connectionId,
            });
        }
    }
#endif
}
