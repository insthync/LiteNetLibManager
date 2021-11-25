using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net.Sockets;
using System.Collections.Concurrent;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp;
using WebSocketSharp.Server;
#endif

namespace LiteNetLibManager
{
    public class WebSocketTransport : ITransport
    {
        private bool secure;
        private string certificateFilePath;
        private string certificatePassword;
        private byte[] tempBuffers;
        private NativeWebSocket.WebSocket client;
        private readonly Queue<TransportEventData> clientEventQueue;
#if !UNITY_WEBGL || UNITY_EDITOR
        private WebSocketServer server;
        private long nextConnectionId = 1;
        private long tempConnectionId;
        private readonly Dictionary<long, WebSocketServerBehavior> serverPeers;
        private readonly Queue<TransportEventData> serverEventQueue;
#endif

        public int ServerPeersCount
        {
            get
            {
                int result = 0;
#if !UNITY_WEBGL || UNITY_EDITOR
                if (server != null)
                {
                    foreach (WebSocketServiceHost host in server.WebSocketServices.Hosts)
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
            get { return client != null && client.State == NativeWebSocket.WebSocketState.Open; }
        }
        public bool IsServerStarted
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                return server != null;
#else
                return false;
#endif
            }
        }

        public WebSocketTransport(bool secure, string certificateFilePath, string certificatePassword)
        {
            this.secure = secure;
            this.certificateFilePath = certificateFilePath;
            this.certificatePassword = certificatePassword;
            clientEventQueue = new Queue<TransportEventData>();
#if !UNITY_WEBGL || UNITY_EDITOR
            serverPeers = new Dictionary<long, WebSocketServerBehavior>();
            serverEventQueue = new Queue<TransportEventData>();
#endif
        }

        public bool StartClient(string address, int port)
        {
            if (IsClientStarted)
                return false;
            string url = (secure ? "wss://" : "ws://") + address + ":" + port;
            Logging.Log(nameof(WebSocketTransport), $"Connecting to {url}");
            client = new NativeWebSocket.WebSocket(url);
            client.OnOpen += OnClientOpen;
            client.OnMessage += OnClientMessage;
            client.OnError += OnClientError;
            client.OnClose += OnClientClose;
            _ = client.Connect();
            return true;
        }

        public void StopClient()
        {
            if (client != null)
                _ = client.Close();
            client = null;
        }

        private void OnClientOpen()
        {
            clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
            });
        }

        private void OnClientMessage(byte[] data)
        {
            clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                reader = new NetDataReader(data),
            });
        }

        private void OnClientError(string errorMsg)
        {
            clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                errorMessage = errorMsg,
            });
        }

        private void OnClientClose(NativeWebSocket.WebSocketCloseCode closeCode)
        {
            clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                disconnectInfo = GetDisconnectInfo(closeCode),
            });
        }

        private DisconnectInfo GetDisconnectInfo(NativeWebSocket.WebSocketCloseCode closeCode)
        {
            DisconnectInfo info = new DisconnectInfo();
            return info;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (client == null)
                return false;
            client.DispatchMessageQueue();
            if (clientEventQueue.Count == 0)
                return false;
            eventData = clientEventQueue.Dequeue();
            return true;
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (IsClientStarted)
            {
                client.Send(writer.Data);
                return true;
            }
            return false;
        }

        public bool StartServer(int port, int maxConnections)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted)
                return false;
            ServerMaxConnections = maxConnections;
            serverPeers.Clear();
            server = new WebSocketServer(port, secure);
            if (secure)
                server.SslConfiguration.ServerCertificate = new X509Certificate2(certificateFilePath, certificatePassword);
            server.AddWebSocketService<WebSocketServerBehavior>("/", (behavior) =>
            {
                tempConnectionId = nextConnectionId++;
                behavior.Initialize(tempConnectionId, serverEventQueue, serverPeers);
            });
            server.Start();
            return true;
#else
            return false;
#endif
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!IsServerStarted)
                return false;
            if (serverEventQueue.Count == 0)
                return false;
            eventData = serverEventQueue.Dequeue();
            return true;
#else
            return false;
#endif
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted && serverPeers.ContainsKey(connectionId) && serverPeers[connectionId].ConnectionState == WebSocketState.Open)
            {
                serverPeers[connectionId].Context.WebSocket.Send(writer.Data);
                return true;
            }
#endif
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted && serverPeers.ContainsKey(connectionId))
            {
                serverPeers[connectionId].Context.WebSocket.Close();
                serverPeers.Remove(connectionId);
                return true;
            }
#endif
            return false;
        }

        public void StopServer()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (server != null)
                server.Stop();
            nextConnectionId = 1;
            server = null;
#endif
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }
    }
}
