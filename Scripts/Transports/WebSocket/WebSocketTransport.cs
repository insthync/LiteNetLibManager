using System.Collections.Concurrent;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Threading;
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
        private string _certificatePassword;
        private string _certificateBase64String;
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

        public WebSocketTransport(bool secure, string certificateFilePath, string certificatePassword, string certificateBase64String)
        {
            _secure = secure;
            _certificateFilePath = certificateFilePath;
            _certificatePassword = certificatePassword;
            _certificateBase64String = certificateBase64String;
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
            Debug.Log($"[WebSocketTransport] Connecting to {url}");
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
                _client.ClientSend(writer.CopyData());
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
            string prefix1 = _secure ? $"https://localhost:{port}/{_path}/" : $"http://localhost:{port}/{_path}/";
            string prefix2 = _secure ? $"https://127.0.0.1:{port}/{_path}/" : $"http://127.0.0.1:{port}/{_path}/";
            string prefix3 = _secure ? $"https://0.0.0.0:{port}/{_path}/" : $"http://0.0.0.0:{port}/{_path}/";
            _server = new WebSocketServer(new string[] { prefix1, prefix2, prefix3 }, _serverEventQueue);
            /*
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
            }*/
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
                return _server.Send(connectionId, writer.CopyData());
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
