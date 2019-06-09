using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
#if !UNITY_WEBGL || UNITY_EDITOR
using WebSocketSharp;
using WebSocketSharp.Server;
#endif

namespace LiteNetLibManager
{
    public sealed class MixTransport : ITransport
    {
        private long nextConnectionId = 1;
        private long tempConnectionId;
        private byte[] tempBuffers;

        // WebSocket data
        private WebSocket wsClient;
#if UNITY_WEBGL
        private bool wsDirtyIsConnected;
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
        private WebSocketServer wsServer;
        private readonly Dictionary<long, WSBehavior> wsServerPeers;
#endif

        // LiteNetLib data
        public NetManager client { get; private set; }
        public NetManager server { get; private set; }
        public string connectKey { get; private set; }
        public int maxConnections { get; private set; }
        private readonly Dictionary<long, NetPeer> serverPeers;
        private readonly Queue<TransportEventData> clientEventQueue;
        private readonly Queue<TransportEventData> serverEventQueue;

        private readonly int webSocketPortOffset;

        public MixTransport(string connectKey, int webSocketPortOffset)
        {
            this.connectKey = connectKey;
#if !UNITY_WEBGL
            wsServerPeers = new Dictionary<long, WSBehavior>();
            serverPeers = new Dictionary<long, NetPeer>();
            clientEventQueue = new Queue<TransportEventData>();
            serverEventQueue = new Queue<TransportEventData>();
#endif
            this.webSocketPortOffset = webSocketPortOffset;
        }

        public bool IsClientStarted()
        {
#if UNITY_WEBGL
            return wsClient != null && wsClient.IsConnected;
#else
            return client != null && client.FirstPeer != null && client.FirstPeer.ConnectionState == ConnectionState.Connected;
#endif
        }

        public bool StartClient(string address, int port)
        {
#if UNITY_WEBGL
            wsDirtyIsConnected = false;
            int wsPort = port + webSocketPortOffset;
            wsClient = new WebSocket(new System.Uri("ws://" + address + ":" + wsPort));
            wsClient.Connect();
            return true;
#else
            clientEventQueue.Clear();
            client = new NetManager(new MixTransportEventListener(this, clientEventQueue));
            return client.Start() && client.Connect(address, port, connectKey) != null;
#endif
        }

        public void StopClient()
        {
#if UNITY_WEBGL
            if (wsClient != null)
                wsClient.Close();
            wsClient = null;
#else
            if (client != null)
                client.Stop();
            client = null;
#endif
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
#if UNITY_WEBGL
            if (wsClient == null)
                return false;
            if (wsDirtyIsConnected != wsClient.IsConnected)
            {
                wsDirtyIsConnected = wsClient.IsConnected;
                if (wsClient.IsConnected)
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
                tempBuffers = wsClient.Recv();
                if (tempBuffers != null)
                {
                    eventData.type = ENetworkEvent.DataEvent;
                    eventData.reader = new NetDataReader(tempBuffers);
                    return true;
                }
            }
            return false;
#else
            if (client == null)
                return false;
            client.PollEvents();
            if (clientEventQueue.Count == 0)
                return false;
            eventData = clientEventQueue.Dequeue();
            return true;
#endif
        }

        public bool ClientSend(DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if UNITY_WEBGL
            if (IsClientStarted())
            {
                wsClient.Send(writer.Data);
                return true;
            }
#else
            if (IsClientStarted())
            {
                client.FirstPeer.Send(writer, deliveryMethod);
                return true;
            }
#endif
            return false;
        }

        public bool IsServerStarted()
        {
#if UNITY_WEBGL
            // Don't integrate server networking to WebGL clients
            return false;
#else
            return wsServer != null && server != null;
#endif
        }

        public bool StartServer(int port, int maxConnections)
        {
#if UNITY_WEBGL
            // Don't integrate server networking to WebGL clients
            return false;
#else
            // Start WebSocket Server
            wsServerPeers.Clear();
            int wsPort = port + webSocketPortOffset;
            wsServer = new WebSocketServer(wsPort);
            wsServer.AddWebSocketService("/", () =>
            {
                tempConnectionId = GetNewConnectionID();
                WSBehavior behavior = new WSBehavior(tempConnectionId, serverEventQueue);
                wsServerPeers[tempConnectionId] = behavior;
                return behavior;
            });
            wsServer.Start();

            // Start LiteNetLib Server
            serverPeers.Clear();
            serverEventQueue.Clear();
            server = new NetManager(new MixTransportEventListener(this, serverEventQueue, serverPeers));
            this.maxConnections = maxConnections;
            return server.Start(port);
#endif
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
#if UNITY_WEBGL
            // Don't integrate server networking to WebGL clients
            return false;
#else
            if (wsServer == null || server == null)
                return false;
            server.PollEvents();
            if (serverEventQueue.Count == 0)
                return false;
            eventData = serverEventQueue.Dequeue();
            return true;
#endif
        }

        public bool ServerSend(long connectionId, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if !UNITY_WEBGL
            // WebSocket Server Send
            if (IsServerStarted() && wsServerPeers.ContainsKey(connectionId))
            {
                wsServerPeers[connectionId].Context.WebSocket.Send(writer.Data);
                return true;
            }
            // LiteNetLib Server Send
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId))
            {
                serverPeers[connectionId].Send(writer, deliveryMethod);
                return true;
            }
#endif
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
#if !UNITY_WEBGL
            // WebSocket Server Disconnect
            if (IsServerStarted() && wsServerPeers.ContainsKey(connectionId))
            {
                wsServerPeers[connectionId].Context.WebSocket.Close();
                return true;
            }
            // LiteNetLib Server Disconnect
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId))
            {
                server.DisconnectPeer(serverPeers[connectionId]);
                return true;
            }
#endif
            return false;
        }

        public void StopServer()
        {
#if !UNITY_WEBGL
            if (wsServer != null)
                wsServer.Stop();
            wsServer = null;
            if (server != null)
                server.Stop();
            server = null;
            nextConnectionId = 1;
#endif
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }

        public int GetServerPeersCount()
        {
            int result = 0;
            if (server != null)
                result += server.PeersCount;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (wsServer != null)
            {
                foreach (WebSocketServiceHost host in wsServer.WebSocketServices.Hosts)
                {
                    result += host.Sessions.Count;
                }
            }
#endif
            return result;
        }

        public long GetNewConnectionID()
        {
            return nextConnectionId++;
        }
    }
}
