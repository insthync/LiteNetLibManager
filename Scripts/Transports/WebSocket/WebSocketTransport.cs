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
    public class WebSocketTransport : ITransport, ITransportConnectionGenerator
    {
        private long _nextConnectionId = 1;
        private bool _secure;
        private string _certificateFilePath;
        private string _certificatePassword;
        private readonly ConcurrentQueue<TransportEventData> _clientEventQueue;
        private WsClientWrapper _wsClient;
#if !UNITY_WEBGL || UNITY_EDITOR
        private WsTransportServer _wsServer;
        private WssTransportServer _wssServer;
#endif

        public bool IsClientStarted
        {
            get { return _wsClient.IsClientStarted; }
        }

        public bool IsServerStarted
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (!_secure)
                    return _wsServer != null && _wsServer.IsStarted;
                else
                    return _wssServer != null && _wssServer.IsStarted;
#else
                return false;
#endif
            }
        }

        public int ServerPeersCount
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (!_secure)
                {
                    if (_wsServer != null)
                        return _wsServer.PeersCount;
                }
                else
                {
                    if (_wssServer != null)
                        return _wssServer.PeersCount;
                }
#endif
                return 0;
            }
        }

        public int ServerMaxConnections
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (!_secure)
                {
                    if (_wsServer != null)
                        return _wsServer.MaxConnections;
                }
                else
                {
                    if (_wssServer != null)
                        return _wssServer.MaxConnections;
                }
#endif
                return 0;
            }
        }

        public bool HasImplementedPing
        {
            get { return false; }
        }

        public WebSocketTransport(bool secure, string certificateFilePath, string certificatePassword)
        {
            _secure = secure;
            _certificateFilePath = certificateFilePath;
            _certificatePassword = certificatePassword;
            _clientEventQueue = new ConcurrentQueue<TransportEventData>();
            _wsClient = new WsClientWrapper(_clientEventQueue, secure, SslProtocols.Tls12);
        }

        public bool StartClient(string address, int port)
        {
            while (_clientEventQueue.TryDequeue(out _)) { }
            return _wsClient.StartClient(address, port);
        }

        public void StopClient()
        {
            _wsClient.StopClient();
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            return _wsClient.ClientReceive(out eventData);
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            return _wsClient.ClientSend(dataChannel, deliveryMethod, writer);
        }

        public bool StartServer(int port, int maxConnections)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted)
                return false;
            if (!_secure)
            {
                _wsServer = new WsTransportServer(this, IPAddress.Any, port, maxConnections);
                _wsServer.OptionDualMode = true;
                _wsServer.OptionNoDelay = true;
                return _wsServer.Start();
            }
            else
            {
                SslContext context = new SslContext(SslProtocols.Tls12, new X509Certificate2(_certificateFilePath, _certificatePassword), CertValidationCallback);
                _wssServer = new WssTransportServer(this, context, IPAddress.Any, port, maxConnections);
                _wssServer.OptionDualMode = true;
                _wssServer.OptionNoDelay = true;
                return _wssServer.Start();
            }
#else
            return false;
#endif
        }

        private bool CertValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!IsServerStarted)
                return false;
            if (!_secure)
            {
                if (_wsServer.EventQueue.Count == 0)
                    return false;
                return _wsServer.EventQueue.TryDequeue(out eventData);
            }
            else
            {
                if (_wssServer.EventQueue.Count == 0)
                    return false;
                return _wssServer.EventQueue.TryDequeue(out eventData);
            }
#else
            return false;
#endif
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!_secure)
                return _wsServer != null && _wsServer.SendAsync(connectionId, writer.Data);
            else
                return _wssServer != null && _wssServer.SendAsync(connectionId, writer.Data);
#else
            return false;
#endif
        }

        public bool ServerDisconnect(long connectionId)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!_secure)
                return _wsServer != null && _wsServer.Disconnect(connectionId);
            else
                return _wssServer != null && _wssServer.Disconnect(connectionId);
#else
            return false;
#endif
        }

        public void StopServer()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_wsServer != null)
                _wsServer.Dispose();
            if (_wssServer != null)
                _wssServer.Dispose();
            _wsServer = null;
            _wssServer = null;
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
            throw new System.NotImplementedException();
        }

        public long GetServerRtt(long connectionId)
        {
            throw new System.NotImplementedException();
        }
    }
}
