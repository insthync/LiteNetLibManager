using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Authentication;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Security.Cryptography.X509Certificates;
using NetCoreServer;
#endif

namespace LiteNetLibManager
{
    public class WebSocketTransport : ITransport
    {
        private bool secure;
        private string certificateFilePath;
        private string certificatePassword;
        private NativeWebSocket.WebSocket client;
        private readonly Queue<TransportEventData> clientEventQueue;
#if !UNITY_WEBGL || UNITY_EDITOR
        private WsTransportServer wsServer;
        private WssTransportServer wssServer;
#endif

        public bool IsClientStarted
        {
            get { return client != null && client.State == NativeWebSocket.WebSocketState.Open; }
        }
        public bool IsServerStarted
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (!secure)
                    return wsServer != null && wsServer.IsStarted;
                else
                    return wssServer != null && wssServer.IsStarted;
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
                if (!secure)
                {
                    if (wsServer != null)
                        return wsServer.PeersCount;
                }
                else
                {
                    if (wssServer != null)
                        return wssServer.PeersCount;
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
                if (!secure)
                {
                    if (wsServer != null)
                        return wsServer.MaxConnections;
                }
                else
                {
                    if (wssServer != null)
                        return wssServer.MaxConnections;
                }
#endif
                return 0;
            }
        }

        public WebSocketTransport(bool secure, string certificateFilePath, string certificatePassword)
        {
            this.secure = secure;
            this.certificateFilePath = certificateFilePath;
            this.certificatePassword = certificatePassword;
            clientEventQueue = new Queue<TransportEventData>();
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
            if (!secure)
            {
                wsServer = new WsTransportServer(IPAddress.Any, port, maxConnections);
                wsServer.OptionDualMode = true;
                wsServer.OptionNoDelay = true;
                return wsServer.Start();
            }
            else
            {
                wssServer = new WssTransportServer(new SslContext(SslProtocols.Tls12, new X509Certificate2(certificateFilePath, certificatePassword)), IPAddress.Any, port, maxConnections);
                wssServer.OptionDualMode = true;
                wssServer.OptionNoDelay = true;
                return wssServer.Start();
            }
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
            if (!secure)
            {
                if (wsServer.EventQueue.Count == 0)
                    return false;
                return wsServer.EventQueue.TryDequeue(out eventData);
            }
            else
            {
                if (wssServer.EventQueue.Count == 0)
                    return false;
                return wssServer.EventQueue.TryDequeue(out eventData);
            }
#else
            return false;
#endif
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!secure)
                return wsServer != null && wsServer.SendAsync(connectionId, writer.Data);
            else
                return wssServer != null && wssServer.SendAsync(connectionId, writer.Data);
#else
            return false;
#endif
        }

        public bool ServerDisconnect(long connectionId)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!secure)
                return wsServer != null && wsServer.Disconnect(connectionId);
            else
                return wssServer != null && wssServer.Disconnect(connectionId);
#else
            return false;
#endif
        }

        public void StopServer()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (wsServer != null)
                wsServer.Dispose();
            if (wssServer != null)
                wssServer.Dispose();
            wsServer = null;
            wssServer = null;
#endif
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }
    }
}
