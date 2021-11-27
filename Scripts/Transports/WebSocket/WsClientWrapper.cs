using LiteNetLib;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;
#if !UNITY_WEBGL || UNITY_EDITOR
using NetCoreServer;
#endif

namespace LiteNetLibManager
{
    public class WsClientWrapper
    {
        private readonly ConcurrentQueue<TransportEventData> clientEventQueue;
        private NativeWebSocket.WebSocket wsClient;
#if !UNITY_WEBGL || UNITY_EDITOR
        private WssTransportClient wssClient;
#endif
        private bool secure;
        private SslProtocols sslProtocols;

        public bool IsClientStarted
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (secure)
                    return wssClient != null && wssClient.IsConnected;
#endif
                return wsClient != null && wsClient.State == NativeWebSocket.WebSocketState.Open;
            }
        }

        public WsClientWrapper(ConcurrentQueue<TransportEventData> clientEventQueue, bool secure, SslProtocols sslProtocols)
        {
            this.clientEventQueue = clientEventQueue;
            this.secure = secure;
            this.sslProtocols = sslProtocols;
        }

        public bool StartClient(string address, int port)
        {
            if (IsClientStarted)
                return false;
            string url = (secure ? "wss://" : "ws://") + address + ":" + port;
            Logging.Log(nameof(WebSocketTransport), $"Connecting to {url}");
#if !UNITY_WEBGL || UNITY_EDITOR
            if (secure)
            {
                IPAddress[] ipAddresses = Dns.GetHostAddresses(address);
                if (ipAddresses.Length == 0)
                    return false;

                int indexOfAddress = -1;
                for (int i = 0; i < ipAddresses.Length; ++i)
                {
                    if (ipAddresses[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        indexOfAddress = i;
                        break;
                    }
                }

                if (indexOfAddress < 0)
                    return false;

                SslContext context = new SslContext(sslProtocols, new X509Certificate2(), CertValidationCallback);
                wssClient = new WssTransportClient(clientEventQueue, context, ipAddresses[indexOfAddress], port);
                wssClient.OptionDualMode = true;
                wssClient.OptionNoDelay = true;
                return wssClient.ConnectAsync();
            }
#endif
            wsClient = new NativeWebSocket.WebSocket(url);
            wsClient.OnOpen += OnClientOpen;
            wsClient.OnMessage += OnClientMessage;
            wsClient.OnError += OnClientError;
            wsClient.OnClose += OnClientClose;
            _ = wsClient.Connect();
            return true;
        }

        public void StopClient()
        {
            if (wsClient != null)
                _ = wsClient.Close();
            wsClient = null;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (wssClient != null)
                wssClient.Dispose();
            wssClient = null;
#endif
        }

        private bool CertValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (wsClient != null)
            {
#if !UNITY_WEBGL
                wsClient.DispatchMessageQueue();
#endif
            }
            if (clientEventQueue.Count == 0)
                return false;
            return clientEventQueue.TryDequeue(out eventData);
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (!IsClientStarted)
                return false;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (secure)
                return wssClient.SendBinaryAsync(writer.Data, 0, writer.Data.Length);
#endif
            wsClient.Send(writer.Data);
            return true;
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
            // TODO: Implement this
            DisconnectInfo info = new DisconnectInfo();
            return info;
        }
    }
}
