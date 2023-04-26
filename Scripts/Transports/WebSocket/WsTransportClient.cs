﻿using LiteNetLib.Utils;
using NetCoreServer;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLibManager
{
    public class WsTransportClient : WsClient
    {
        private readonly ConcurrentQueue<TransportEventData> eventQueue;

        public WsTransportClient(ConcurrentQueue<TransportEventData> eventQueue, IPAddress address, int port) : base(address, port)
        {
            this.eventQueue = eventQueue;
        }

        public override void OnWsConnecting(HttpRequest request)
        {
            Uri uri = new Uri($"wss://{((IPEndPoint)Endpoint).Address}:{((IPEndPoint)Endpoint).Port}");
            request.SetBegin("GET", uri.PathAndQuery);
            request.SetHeader("Host", ((IPEndPoint)Endpoint).Port == 80 ? uri.DnsSafeHost : uri.Authority);
            request.SetHeader("Upgrade", "websocket");
            request.SetHeader("Connection", "Upgrade");
            request.SetHeader("Sec-WebSocket-Key", Convert.ToBase64String(WsNonce));
            request.SetHeader("Sec-WebSocket-Version", "13");
            request.Cache.Append("\r\n");
        }

        public override void OnWsConnected(HttpResponse response)
        {
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
            });
        }

        protected override void OnDisconnected()
        {
            base.OnDisconnected();
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
            });
        }

        public override void OnWsError(string error)
        {
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                errorMessage = error,
            });
        }

        protected override void OnError(SocketError error)
        {
            base.OnError(error);
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                socketError = error,
            });
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            byte[] coppiedBuffer = new byte[size];
            Array.Copy(buffer, offset, coppiedBuffer, 0, size);
            eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                reader = new NetDataReader(coppiedBuffer),
            });
        }
    }
}
