using LiteNetLib.Utils;
using NetCoreServer;
using System;
using System.Net.Sockets;

namespace LiteNetLibManager
{
    public class WsTransportSession : WsSession
    {
        public long ConnectionId { get; private set; }

        private readonly WsTransportServer _server;

        public WsTransportSession(long connectionId, WsTransportServer server) : base(server)
        {
            ConnectionId = connectionId;
            _server = server;
        }

        public override void OnWsConnected(HttpRequest request)
        {
            _server.EventQueue.Enqueue(new TransportEventData()
            {
                connectionId = ConnectionId,
                type = ENetworkEvent.ConnectEvent,
            });
        }

        protected override void OnDisconnected()
        {
            base.OnDisconnected();
            _server.EventQueue.Enqueue(new TransportEventData()
            {
                connectionId = ConnectionId,
                type = ENetworkEvent.DisconnectEvent,
                disconnectInfo = new LiteNetLib.DisconnectInfo()
                {
                    Reason = LiteNetLib.DisconnectReason.RemoteConnectionClose,
                },
            });
        }

        public override void OnWsError(string error)
        {
            _server.EventQueue.Enqueue(new TransportEventData()
            {
                connectionId = ConnectionId,
                type = ENetworkEvent.ErrorEvent,
                errorMessage = error,
            });
        }

        protected override void OnError(SocketError error)
        {
            base.OnError(error);
            _server.EventQueue.Enqueue(new TransportEventData()
            {
                connectionId = ConnectionId,
                type = ENetworkEvent.ErrorEvent,
                socketError = error,
            });
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            byte[] coppiedBuffer = new byte[size];
            Array.Copy(buffer, offset, coppiedBuffer, 0, size);
            _server.EventQueue.Enqueue(new TransportEventData()
            {
                connectionId = ConnectionId,
                type = ENetworkEvent.DataEvent,
                reader = new NetDataReader(coppiedBuffer),
            });
        }
    }
}
