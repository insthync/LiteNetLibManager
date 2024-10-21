using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
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
        private bool webSocketSecure;
        private string webSocketCertificateFilePath;
        private string webSocketCertificatePassword;

        // WebSocket data
#if UNITY_WEBGL
        private byte[] tempBuffers;
        private bool wsDirtyIsConnected;
        private WebSocket wsClient;
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
        private WebSocketServer wsServer;
        private readonly Dictionary<long, WebSocketServerBehavior> wsServerPeers;
#endif

        // LiteNetLib data
        public NetManager Client { get; private set; }
        public NetManager Server { get; private set; }
        public string ConnectKey { get; private set; }
        public int ServerPeersCount
        {
            get
            {
                int result = 0;
                if (Server != null)
                    result += Server.ConnectedPeersCount;
#if !UNITY_WEBGL
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
        }
        public int ServerMaxConnections { get; private set; }
        public bool IsClientStarted
        {
            get
            {
#if UNITY_WEBGL
                return wsClient != null && wsClient.IsConnected;
#else
                return Client != null && Client.FirstPeer != null && Client.FirstPeer.ConnectionState == ConnectionState.Connected;
#endif
            }
        }
        public bool IsServerStarted
        {
            get
            {
#if UNITY_WEBGL
                // Don't integrate server networking to WebGL clients
                return false;
#else
                return wsServer != null && Server != null;
#endif
            }
        }

        public bool HasImplementedPing => false;

        private readonly Dictionary<long, NetPeer> serverPeers;
        private readonly Queue<TransportEventData> clientEventQueue;
        private readonly Queue<TransportEventData> serverEventQueue;
        private readonly byte clientDataChannelsCount;
        private readonly byte serverDataChannelsCount;

        private readonly int webSocketPortOffset;

        public MixTransport(string connectKey, int webSocketPortOffset, bool webSocketSecure, string webSocketCertificateFilePath, string webSocketCertificatePassword, byte clientDataChannelsCount, byte serverDataChannelsCount)
        {
            ConnectKey = connectKey;
#if !UNITY_WEBGL
            wsServerPeers = new Dictionary<long, WebSocketServerBehavior>();
            serverPeers = new Dictionary<long, NetPeer>();
            clientEventQueue = new Queue<TransportEventData>();
            serverEventQueue = new Queue<TransportEventData>();
            this.clientDataChannelsCount = clientDataChannelsCount;
            this.serverDataChannelsCount = serverDataChannelsCount;
#endif
            this.webSocketPortOffset = webSocketPortOffset;
            this.webSocketSecure = webSocketSecure;
            this.webSocketCertificateFilePath = webSocketCertificateFilePath;
            this.webSocketCertificatePassword = webSocketCertificatePassword;
        }

        public bool StartClient(string address, int port)
        {
#if UNITY_WEBGL
            wsDirtyIsConnected = false;
            int wsPort = port + webSocketPortOffset;
            string url = (webSocketSecure ? "wss://" : "ws://") + address + ":" + port;
            Logging.Log(nameof(MixTransport), $"Connecting to {url}");
            wsClient = new WebSocket(new System.Uri(url));
            wsClient.Connect();
            return true;
#else
            clientEventQueue.Clear();
            Client = new NetManager(new LiteNetLibTransportClientEventListener(clientEventQueue));
            Client.ChannelsCount = clientDataChannelsCount;
            return Client.Start() && Client.Connect(address, port, ConnectKey) != null;
#endif
        }

        public void StopClient()
        {
#if UNITY_WEBGL
            if (wsClient != null)
                wsClient.Close();
            wsClient = null;
#else
            if (Client != null)
                Client.Stop();
            Client = null;
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
            if (Client == null)
                return false;
            Client.PollEvents();
            if (clientEventQueue.Count == 0)
                return false;
            eventData = clientEventQueue.Dequeue();
            return true;
#endif
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if UNITY_WEBGL
            if (IsClientStarted)
            {
                wsClient.Send(writer.Data);
                return true;
            }
#else
            if (IsClientStarted)
            {
                Client.FirstPeer.Send(writer, dataChannel, deliveryMethod);
                return true;
            }
#endif
            return false;
        }

        public bool StartServer(int port, int maxConnections)
        {
#if UNITY_WEBGL
            // Don't integrate server networking to WebGL clients
            return false;
#else
            if (IsServerStarted)
                return false;
            ServerMaxConnections = maxConnections;
            serverEventQueue.Clear();

            // Start WebSocket Server
            wsServerPeers.Clear();
            int wsPort = port + webSocketPortOffset;
            wsServer = new WebSocketServer(wsPort, webSocketSecure);
            if (webSocketSecure)
                wsServer.SslConfiguration.ServerCertificate = new X509Certificate2(webSocketCertificateFilePath, webSocketCertificatePassword);
            wsServer.AddWebSocketService<WebSocketServerBehavior>("/", (behavior) =>
            {
                tempConnectionId = GetNewConnectionID();
                behavior.Initialize(tempConnectionId, serverEventQueue, wsServerPeers);
            });
            wsServer.Start();

            // Start LiteNetLib Server
            serverPeers.Clear();
            Server = new NetManager(new LiteNetLibTransportServerEventListener(this, ConnectKey, serverEventQueue, serverPeers));
            Server.ChannelsCount = serverDataChannelsCount;
            return Server.Start(port);
#endif
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
#if UNITY_WEBGL
            // Don't integrate server networking to WebGL clients
            return false;
#else
            if (wsServer == null || Server == null)
                return false;
            Server.PollEvents();
            if (serverEventQueue.Count == 0)
                return false;
            eventData = serverEventQueue.Dequeue();
            return true;
#endif
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if !UNITY_WEBGL
            // WebSocket Server Send
            if (IsServerStarted && wsServerPeers.ContainsKey(connectionId) && wsServerPeers[connectionId].ConnectionState == WebSocketState.Open)
            {
                wsServerPeers[connectionId].Context.WebSocket.Send(writer.Data);
                return true;
            }
            // LiteNetLib Server Send
            if (IsServerStarted && serverPeers.ContainsKey(connectionId) && serverPeers[connectionId].ConnectionState == ConnectionState.Connected)
            {
                serverPeers[connectionId].Send(writer, dataChannel, deliveryMethod);
                return true;
            }
#endif
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
#if !UNITY_WEBGL
            // WebSocket Server Disconnect
            if (IsServerStarted && wsServerPeers.ContainsKey(connectionId))
            {
                wsServerPeers[connectionId].Context.WebSocket.Close();
                wsServerPeers.Remove(connectionId);
                return true;
            }
            // LiteNetLib Server Disconnect
            if (IsServerStarted && serverPeers.ContainsKey(connectionId))
            {
                Server.DisconnectPeer(serverPeers[connectionId]);
                serverPeers.Remove(connectionId);
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
            if (Server != null)
                Server.Stop();
            Server = null;
            nextConnectionId = 1;
#endif
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }

        public long GetNewConnectionID()
        {
            return nextConnectionId++;
        }

        public long GetClientRtt()
        {
            return 0;
        }

        public long GetServerRtt(long connectionId)
        {
            return 0;
        }
    }
}
