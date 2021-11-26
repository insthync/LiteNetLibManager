using System.Collections.Generic;
using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Security.Authentication;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Security.Cryptography.X509Certificates;
using NetCoreServer;
#endif

namespace LiteNetLibManager
{
    public sealed class MixTransport : ITransport, ITransportConnectionGenerator
    {
        private long nextConnectionId = 1;
        private bool webSocketSecure;
        private string webSocketCertificateFilePath;
        private string webSocketCertificatePassword;

        // WebSocket data
#if UNITY_WEBGL
        private NativeWebSocket.WebSocket wsClient;
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
        private WsTransportServer wsServer;
        private WssTransportServer wssServer;
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
                if (!webSocketSecure)
                {
                    if (wsServer != null)
                        result += wsServer.PeersCount;
                }
                else
                {
                    if (wssServer != null)
                        result += wssServer.PeersCount;
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
                return wsClient != null && wsClient.State == NativeWebSocket.WebSocketState.Open;
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
                if (Server == null)
                    return false;
                if (!webSocketSecure)
                    return wsServer != null && wsServer.IsStarted;
                else
                    return wssServer != null && wssServer.IsStarted;
#endif
            }
        }
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
            string url = (webSocketSecure ? "wss://" : "ws://") + address + ":" + (port + webSocketPortOffset);
            Logging.Log(nameof(WebSocketTransport), $"Connecting to {url}");
            wsClient = new NativeWebSocket.WebSocket(url);
            wsClient.OnOpen += OnWsClientOpen;
            wsClient.OnMessage += OnWsClientMessage;
            wsClient.OnError += OnWsClientError;
            wsClient.OnClose += OnWsClientClose;
            _ = wsClient.Connect();
            return true;
#else
            clientEventQueue.Clear();
            Client = new NetManager(new MixTransportEventListener(this, clientEventQueue));
            Client.ChannelsCount = clientDataChannelsCount;
            return Client.Start() && Client.Connect(address, port, ConnectKey) != null;
#endif
        }

        public void StopClient()
        {
#if UNITY_WEBGL
            if (wsClient != null)
                _ = wsClient.Close();
            wsClient = null;
#else
            if (Client != null)
                Client.Stop();
            Client = null;
#endif
        }

        private void OnWsClientOpen()
        {
            clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
            });
        }

        private void OnWsClientMessage(byte[] data)
        {
            clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                reader = new NetDataReader(data),
            });
        }

        private void OnWsClientError(string errorMsg)
        {
            clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                errorMessage = errorMsg,
            });
        }

        private void OnWsClientClose(NativeWebSocket.WebSocketCloseCode closeCode)
        {
            clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                disconnectInfo = GetDisconnectInfo(closeCode),
            });
        }

        private DisconnectInfo GetDisconnectInfo(NativeWebSocket.WebSocketCloseCode closeCode)
        {
            // TODO: Implement this
            DisconnectInfo info = new DisconnectInfo();
            return info;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
#if UNITY_WEBGL
            eventData = default(TransportEventData);
            if (wsClient == null)
                return false;
            if (clientEventQueue.Count == 0)
                return false;
            eventData = clientEventQueue.Dequeue();
            return true;
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
            if (!webSocketSecure)
            {
                wsServer = new WsTransportServer(this, IPAddress.Any, port + webSocketPortOffset, maxConnections);
                wsServer.OptionDualMode = true;
                wsServer.OptionNoDelay = true;
                if (!wsServer.Start())
                    return false;
            }
            else
            {
                wssServer = new WssTransportServer(this, new SslContext(SslProtocols.Tls12, new X509Certificate2(webSocketCertificateFilePath, webSocketCertificatePassword)), IPAddress.Any, port + webSocketPortOffset, maxConnections);
                wssServer.OptionDualMode = true;
                wssServer.OptionNoDelay = true;
                if (!wssServer.Start())
                    return false;
            }

            // Start LiteNetLib Server
            serverPeers.Clear();
            Server = new NetManager(new MixTransportEventListener(this, serverEventQueue, serverPeers));
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
            if (!IsServerStarted)
                return false;

            if (!webSocketSecure)
            {
                if (wsServer.EventQueue.Count > 0)
                {
                    wsServer.EventQueue.TryDequeue(out eventData);
                    return true;
                }
            }
            else
            {
                if (wssServer.EventQueue.Count > 0)
                {
                    wssServer.EventQueue.TryDequeue(out eventData);
                    return true;
                }
            }

            Server.PollEvents();
            if (serverEventQueue.Count > 0)
            {
                eventData = serverEventQueue.Dequeue();
                return true;
            }

            return false;
#endif
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if !UNITY_WEBGL
            // WebSocket Server Send
            if (!webSocketSecure)
            {
                if (wsServer != null && wsServer.SendAsync(connectionId, writer.Data))
                    return true;
            }
            else
            {
                if (wssServer != null && wssServer.SendAsync(connectionId, writer.Data))
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
            if (!webSocketSecure)
            {
                if (wsServer != null && wsServer.Disconnect(connectionId))
                    return true;
            }
            else
            {
                if (wssServer != null && wssServer.Disconnect(connectionId))
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
                wsServer.Dispose();
            if (wssServer != null)
                wssServer.Dispose();
            wsServer = null;
            wssServer = null;
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
    }
}
