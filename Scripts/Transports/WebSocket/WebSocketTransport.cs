using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Threading;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp;
using WebSocketSharp.Server;
#endif

namespace LiteNetLibManager
{
    public class WebSocketTransport : ITransport
    {
        private string _path = "/netcode";
        private bool _secure;
        private string _certificateFilePath;
        private string _certificatePassword;
        private string _certificateBase64String;
        private NativeWebSocket.WebSocket _client;
        private readonly Queue<TransportEventData> _clientEventQueue;
#if !UNITY_WEBGL || UNITY_EDITOR
        private WebSocketServer _server;
        private long _nextConnectionId = 1;
        private readonly Dictionary<long, WebSocketServerBehavior> _serverPeers;
        private readonly Queue<TransportEventData> _serverEventQueue;
#endif

        public int ServerPeersCount
        {
            get
            {
                int result = 0;
#if !UNITY_WEBGL || UNITY_EDITOR
                if (_server != null)
                {
                    foreach (WebSocketServiceHost host in _server.WebSocketServices.Hosts)
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
            get { return _client != null && _client.State == NativeWebSocket.WebSocketState.Open; }
        }
        public bool IsServerStarted
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                return _server != null && _server.IsListening;
#else
                return false;
#endif
            }
        }

        public bool HasImplementedPing => false;

        public WebSocketTransport(bool secure, string certificateFilePath, string certificatePassword, string certificateBase64String)
        {
            _secure = secure;
            _certificateFilePath = certificateFilePath;
            _certificatePassword = certificatePassword;
            _certificateBase64String = certificateBase64String;
            _clientEventQueue = new Queue<TransportEventData>();
#if !UNITY_WEBGL || UNITY_EDITOR
            _serverPeers = new Dictionary<long, WebSocketServerBehavior>();
            _serverEventQueue = new Queue<TransportEventData>();
#endif
        }

        public bool StartClient(string address, int port)
        {
            if (IsClientStarted)
                return false;
            string protocol = _secure ? "wss" : "ws";
            string url = $"{protocol}://{address}:{port}{_path}";
            Logging.Log(nameof(WebSocketTransport), $"Connecting to {url}");
            _client = new NativeWebSocket.WebSocket(url);
            _client.OnOpen += OnClientOpen;
            _client.OnMessage += OnClientMessage;
            _client.OnError += OnClientError;
            _client.OnClose += OnClientClose;
            _ = _client.Connect();
            return true;
        }

        public void StopClient()
        {
            if (_client != null)
                _ = _client.Close();
            _client = null;
        }

        private void OnClientOpen()
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
            });
        }

        private void OnClientMessage(byte[] data)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                reader = new NetDataReader(data),
            });
        }

        private void OnClientError(string errorMsg)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                errorMessage = errorMsg,
            });
        }

        private void OnClientClose(NativeWebSocket.WebSocketCloseCode closeCode)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
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
            eventData = default;
            if (_client == null)
                return false;
            _client.DispatchMessageQueue();
            if (_clientEventQueue.Count == 0)
                return false;
            eventData = _clientEventQueue.Dequeue();
            return true;
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (IsClientStarted)
            {
                _client.Send(writer.Data);
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
            _serverPeers.Clear();
            _server = new WebSocketServer(port, _secure);
            if (_secure)
            {
                if (!string.IsNullOrEmpty(_certificateFilePath) && !string.IsNullOrEmpty(_certificatePassword))
                {
                    _server.SslConfiguration.ServerCertificate = new X509Certificate2(_certificateFilePath, _certificatePassword);
                }
                if (!string.IsNullOrEmpty(_certificateBase64String))
                {
                    byte[] bytes = System.Convert.FromBase64String(_certificateBase64String);
                    _server.SslConfiguration.ServerCertificate = new X509Certificate2(bytes);
                }
            }
            _server.AddWebSocketService<WebSocketServerBehavior>(_path, (behavior) =>
            {
                long newConnectionId = GetNewConnectionID();
                behavior.Initialize(newConnectionId, _serverEventQueue, _serverPeers);
            });
            _server.Start();
            return true;
#else
            return false;
#endif
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!IsServerStarted)
                return false;
            if (_serverEventQueue.Count == 0)
                return false;
            eventData = _serverEventQueue.Dequeue();
            return true;
#else
            return false;
#endif
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted && _serverPeers.ContainsKey(connectionId) && _serverPeers[connectionId].ConnectionState == WebSocketState.Open)
            {
                _serverPeers[connectionId].Context.WebSocket.Send(writer.Data);
                return true;
            }
#endif
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted && _serverPeers.ContainsKey(connectionId))
            {
                _serverPeers[connectionId].Context.WebSocket.Close();
                _serverPeers.Remove(connectionId);
                return true;
            }
#endif
            return false;
        }

        public void StopServer()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_server != null)
                _server.Stop();
            _nextConnectionId = 1;
            _server = null;
#endif
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }

        public long GetNewConnectionID()
        {
            return Interlocked.Increment(ref _nextConnectionId);
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
