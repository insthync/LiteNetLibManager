using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
#if !UNITY_WEBGL || UNITY_EDITOR
using NetCoreServer;
#endif

namespace LiteNetLibManager
{
    public sealed class MixTransport : ITransport, ITransportConnectionGenerator
    {
        private long _nextConnectionId = 1;
        private bool _webSocketSecure;
        private string _webSocketCertificateFilePath;
        private string _webSocketCertificatePassword;

        // WebSocket data
#if UNITY_WEBGL
        private WsClientWrapper _wsClient;
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
        private WsTransportServer _wsServer;
        private WssTransportServer _wssServer;
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
                if (!_webSocketSecure)
                {
                    if (_wsServer != null)
                        result += _wsServer.PeersCount;
                }
                else
                {
                    if (_wssServer != null)
                        result += _wssServer.PeersCount;
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
                return _wsClient.IsClientStarted;
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
                if (!_webSocketSecure)
                    return _wsServer != null && _wsServer.IsStarted;
                else
                    return _wssServer != null && _wssServer.IsStarted;
#endif
            }
        }

        public bool HasImplementedPing
        {
            get
            {
#if UNITY_WEBGL
                return false;
#else
                return true;
#endif
            }
        }

        private readonly Dictionary<long, NetPeer> _serverPeers = new Dictionary<long, NetPeer>();
        private readonly ConcurrentQueue<TransportEventData> _clientEventQueue = new ConcurrentQueue<TransportEventData>();
        private readonly ConcurrentQueue<TransportEventData> _serverEventQueue = new ConcurrentQueue<TransportEventData>();
        private readonly byte _clientDataChannelsCount;
        private readonly byte _serverDataChannelsCount;
        private readonly int _webSocketPortOffset;

        public MixTransport(string connectKey, int webSocketPortOffset, bool webSocketSecure, string webSocketCertificateFilePath, string webSocketCertificatePassword, byte clientDataChannelsCount, byte serverDataChannelsCount)
        {
            ConnectKey = connectKey;
            _clientDataChannelsCount = clientDataChannelsCount;
            _serverDataChannelsCount = serverDataChannelsCount;
            _webSocketPortOffset = webSocketPortOffset;
            _webSocketSecure = webSocketSecure;
            _webSocketCertificateFilePath = webSocketCertificateFilePath;
            _webSocketCertificatePassword = webSocketCertificatePassword;
#if UNITY_WEBGL
            _wsClient = new WsClientWrapper(_clientEventQueue, webSocketSecure, SslProtocols.Tls12);
#endif
        }

        public bool StartClient(string address, int port)
        {
            while (_clientEventQueue.TryDequeue(out _)) { }
#if UNITY_WEBGL
            return _wsClient.StartClient(address, port);
#else
            Client = new NetManager(new MixTransportEventListener(this, _clientEventQueue));
            Client.ChannelsCount = _clientDataChannelsCount;
            return Client.Start() && Client.Connect(address, port, ConnectKey) != null;
#endif
        }

        public void StopClient()
        {
#if UNITY_WEBGL
            _wsClient.StopClient();
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
            return _wsClient.ClientReceive(out eventData);
#else
            if (Client == null)
                return false;
            Client.PollEvents();
            if (_clientEventQueue.Count == 0)
                return false;
            return _clientEventQueue.TryDequeue(out eventData);
#endif
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if UNITY_WEBGL
            return _wsClient.ClientSend(dataChannel, deliveryMethod, writer);
#else
            if (IsClientStarted)
            {
                Client.FirstPeer.Send(writer, dataChannel, deliveryMethod);
                return true;
            }
            return false;
#endif
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
            while (_serverEventQueue.TryDequeue(out _)) { }

            // Start WebSocket Server
            if (!_webSocketSecure)
            {
                _wsServer = new WsTransportServer(this, IPAddress.Any, port + _webSocketPortOffset, maxConnections);
                _wsServer.OptionDualMode = true;
                _wsServer.OptionNoDelay = true;
                if (!_wsServer.Start())
                    return false;
            }
            else
            {
                SslContext context = new SslContext(SslProtocols.Tls12, new X509Certificate2(_webSocketCertificateFilePath, _webSocketCertificatePassword), CertValidationCallback);
                _wssServer = new WssTransportServer(this, context, IPAddress.Any, port + _webSocketPortOffset, maxConnections);
                _wssServer.OptionDualMode = true;
                _wssServer.OptionNoDelay = true;
                if (!_wssServer.Start())
                    return false;
            }

            // Start LiteNetLib Server
            _serverPeers.Clear();
            Server = new NetManager(new MixTransportEventListener(this, _serverEventQueue, _serverPeers));
            Server.ChannelsCount = _serverDataChannelsCount;
            return Server.Start(port);
#endif
        }

        private bool CertValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return sslPolicyErrors == SslPolicyErrors.None;
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

            if (!_webSocketSecure)
            {
                if (_wsServer.EventQueue.Count > 0)
                    return _wsServer.EventQueue.TryDequeue(out eventData);
            }
            else
            {
                if (_wssServer.EventQueue.Count > 0)
                    return _wssServer.EventQueue.TryDequeue(out eventData);
            }

            Server.PollEvents();
            if (_serverEventQueue.Count > 0)
                return _serverEventQueue.TryDequeue(out eventData);

            return false;
#endif
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if !UNITY_WEBGL
            // WebSocket Server Send
            if (!_webSocketSecure)
            {
                if (_wsServer != null && _wsServer.SendAsync(connectionId, writer.Data))
                    return true;
            }
            else
            {
                if (_wssServer != null && _wssServer.SendAsync(connectionId, writer.Data))
                    return true;
            }

            // LiteNetLib Server Send
            if (IsServerStarted && _serverPeers.ContainsKey(connectionId) && _serverPeers[connectionId].ConnectionState == ConnectionState.Connected)
            {
                _serverPeers[connectionId].Send(writer, dataChannel, deliveryMethod);
                return true;
            }
#endif
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
#if !UNITY_WEBGL
            // WebSocket Server Disconnect
            if (!_webSocketSecure)
            {
                if (_wsServer != null && _wsServer.Disconnect(connectionId))
                    return true;
            }
            else
            {
                if (_wssServer != null && _wssServer.Disconnect(connectionId))
                    return true;
            }

            // LiteNetLib Server Disconnect
            if (IsServerStarted && _serverPeers.ContainsKey(connectionId))
            {
                Server.DisconnectPeer(_serverPeers[connectionId]);
                _serverPeers.Remove(connectionId);
                return true;
            }
#endif
            return false;
        }

        public void StopServer()
        {
#if !UNITY_WEBGL
            if (_wsServer != null)
                _wsServer.Dispose();
            if (_wssServer != null)
                _wssServer.Dispose();
            _wsServer = null;
            _wssServer = null;
            if (Server != null)
                Server.Stop();
            Server = null;
            _nextConnectionId = 1;
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
#if UNITY_WEBGL
            return 0;
#else
            return Client.FirstPeer.RoundTripTime;
#endif
        }

        public long GetServerRtt(long connectionId)
        {
#if UNITY_WEBGL
            return 0;
#else
            return _serverPeers[connectionId].RoundTripTime;
#endif
        }
    }
}
