using System.Collections.Concurrent;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

#if !UNITY_WEBGL || UNITY_EDITOR
using System.Security.Cryptography.X509Certificates;
#endif

namespace LiteNetLibManager
{
    public class WebSocketTransport : ITransport
    {
        private string _path = "netcode";
        private bool _secure;
        private string _certificateFilePath;
        private string _certificateBase64String;
        private string _certificatePassword;
        private WebSocketClient _client;
#if !UNITY_WEBGL || UNITY_EDITOR
        private WebSocketServer _server;
        private readonly ConcurrentQueue<TransportEventData> _serverEventQueue;
#endif

        public int ServerPeersCount
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                return _server.PeersCount;
#else
                return 0;
#endif
            }
        }
        public int ServerMaxConnections { get; private set; }
        public bool IsClientStarted
        {
            get { return _client != null && _client.IsOpen; }
        }
        public bool IsServerStarted
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                return _server != null && _server.IsRunning;
#else
                return false;
#endif
            }
        }

        public bool HasImplementedPing => false;

        public WebSocketTransport(bool secure, string certificateFilePath, string certificateBase64String, string certificatePassword)
        {
            _secure = secure;
            _certificateFilePath = certificateFilePath;
            _certificateBase64String = certificateBase64String;
            _certificatePassword = certificatePassword;
#if !UNITY_WEBGL || UNITY_EDITOR
            _serverEventQueue = new ConcurrentQueue<TransportEventData>();
#endif
        }

        public bool StartClient(string address, int port)
        {
            if (IsClientStarted)
                return false;
            string protocol = _secure ? "wss" : "ws";
            string url = $"{protocol}://{address}:{port}/{_path}/";
            Logging.Log($"[WebSocketTransport] Connecting to {url}");
            _client = new WebSocketClient(url);
            return _client.Connect();
        }

        public void StopClient()
        {
            if (_client != null)
                _client.Close();
            _client = null;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default;
            if (_client != null)
                return _client.ClientReceive(out eventData);
            return false;
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (IsClientStarted)
            {
                _client.ClientSend(writer);
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
            string location = _secure ? $"wss://0.0.0.0:{port}/{_path}/" : $"ws://0.0.0.0:{port}/{_path}/";
            X509Certificate2 cert = null;
            if (_secure)
            {
                if (!string.IsNullOrEmpty(_certificateFilePath))
                {
                    if (!string.IsNullOrEmpty(_certificatePassword))
                        cert = new X509Certificate2(_certificateFilePath, _certificatePassword);
                    else
                        cert = new X509Certificate2(_certificateFilePath);
                }
                if (!string.IsNullOrEmpty(_certificateBase64String))
                {
                    byte[] bytes = System.Convert.FromBase64String(_certificateBase64String);
                    if (!string.IsNullOrEmpty(_certificatePassword))
                        cert = new X509Certificate2(bytes, _certificatePassword);
                    else
                        cert = new X509Certificate2(bytes);
                }
            }
            _server = new WebSocketServer(location, cert, _serverEventQueue);
            return _server.StartServer();
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
            _serverEventQueue.TryDequeue(out eventData);
            return true;
#else
            return false;
#endif
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted)
                return _server.Send(connectionId, writer);
#endif
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted)
                return _server.Disconnect(connectionId);
#endif
            return false;
        }

        public void StopServer()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_server != null)
                _server.Stop();
            _server = null;
#endif
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
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
