using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace LiteNetLibManager
{
    public class WebSocketTransport : ITransport
    {
        private WebSocket client;
        private WebSocketServer server;
        private long nextConnectionId = 1;
        private long tempConnectionId;
        private readonly Dictionary<long, WSBehavior> serverPeers;
        private readonly Queue<TransportEventData> serverEventQueue;
        private bool dirtyIsConnected;
        private byte[] tempBuffers;

        public WebSocketTransport()
        {
            serverPeers = new Dictionary<long, WSBehavior>();
            serverEventQueue = new Queue<TransportEventData>();
        }

        public bool IsClientStarted()
        {
            return client != null && client.IsConnected;
        }

        public bool StartClient(string connectKey, string address, int port)
        {
            client = new WebSocket(new System.Uri("ws://" + address + ":" + port));
            client.Connect();
            return true;
        }

        public void StopClient()
        {
            if (client != null)
                client.Close();
            client = null;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (client == null)
                return false;
            if (dirtyIsConnected != client.IsConnected)
            {
                dirtyIsConnected = client.IsConnected;
                if (client.IsConnected)
                {
                    // Connect state changed to connected, so it's connect event
                    eventData.type = ENetworkEvent.ConnectEvent;
                }
                else
                {
                    // Connect state changed to not connected, so it's disconnect event
                    eventData.type = ENetworkEvent.DisconnectEvent;
                }
                return true;
            }
            else
            {
                tempBuffers = client.Recv();
                if (tempBuffers != null)
                {
                    eventData.type = ENetworkEvent.DataEvent;
                    eventData.reader = new NetDataReader(tempBuffers);
                    return true;
                }
            }
            return false;
        }

        public bool ClientSend(SendOptions sendOptions, NetDataWriter writer)
        {
            if (IsClientStarted())
            {
                client.Send(writer.Data);
                return true;
            }
            return false;
        }

        public bool IsServerStarted()
        {
            return server != null;
        }

        public bool StartServer(string connectKey, int port, int maxConnections)
        {
            serverPeers.Clear();
            server = new WebSocketServer(port);
            server.AddWebSocketService("/", () =>
            {
                tempConnectionId = nextConnectionId++;
                var behavior = new WSBehavior(tempConnectionId, serverEventQueue);
                serverPeers[tempConnectionId] = behavior;
                return behavior;
            });
            server.Start();
            return true;
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (server == null)
                return false;
            if (serverEventQueue.Count == 0)
                return false;
            eventData = serverEventQueue.Dequeue();
            return true;
        }

        public bool ServerSend(long connectionId, SendOptions sendOptions, NetDataWriter writer)
        {
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId))
            {
                serverPeers[connectionId].Context.WebSocket.Send(writer.Data);
                return true;
            }
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId))
            {
                serverPeers[connectionId].Context.WebSocket.Close();
                return true;
            }
            return false;
        }

        public void StopServer()
        {
            if (server != null)
                server.Stop();
            nextConnectionId = 1;
            server = null;
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }

        public int GetFreePort()
        {
            Socket socketV4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socketV4.Bind(new IPEndPoint(IPAddress.Any, 0));
            int port = ((IPEndPoint)socketV4.LocalEndPoint).Port;
            socketV4.Close();
            return port;
        }

        private class WSBehavior : WebSocketBehavior
        {
            public long connectionId { get; }
            private readonly Queue<TransportEventData> eventQueue;

            public WSBehavior(long connectionId, Queue<TransportEventData> eventQueue)
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
    }
}
