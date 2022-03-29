using LiteNetLib;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;
using System;
using System.Runtime.InteropServices;
using System.Text;
#if !UNITY_WEBGL || UNITY_EDITOR
using NetCoreServer;
#endif

namespace LiteNetLibManager
{
    public class WsClientWrapper
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int SocketCreate(string url);
        [DllImport("__Internal")]
        private static extern int GetSocketState(int wsNativeInstance);
        [DllImport("__Internal")]
        private static extern int GetSocketEventType(int wsNativeInstance);
        [DllImport("__Internal")]
        private static extern int GetSocketErrorCode(int wsNativeInstance);
        [DllImport("__Internal")]
        private static extern int GetSocketDataLength(int wsNativeInstance);
        [DllImport("__Internal")]
        private static extern void GetSocketData(int wsNativeInstance, byte[] ptr, int length);
        [DllImport("__Internal")]
        private static extern void SocketEventDequeue(int wsNativeInstance);
        [DllImport("__Internal")]
        private static extern void SocketSend(int wsNativeInstance, byte[] ptr, int length);
        [DllImport("__Internal")]
        private static extern void SocketClose(int wsNativeInstance);

        private int wsNativeInstance = -1;
        private byte[] tempBuffers;
        private bool dirtyIsConnected = false;
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private WsTransportClient wsClient;
        private WssTransportClient wssClient;
#endif
        private readonly ConcurrentQueue<TransportEventData> clientEventQueue;
        private readonly bool secure;
        private readonly SslProtocols sslProtocols;

        public bool IsClientStarted
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (secure)
                    return wssClient != null && wssClient.IsConnected;
                else
                    return wsClient != null && wsClient.IsConnected;
#else
                return GetSocketState(wsNativeInstance) == 1;
#endif
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
            {
                Logging.Log(nameof(WsClientWrapper), "Client started, so it can't be started again");
                return false;
            }
#if !UNITY_WEBGL || UNITY_EDITOR
            IPAddress[] ipAddresses = Dns.GetHostAddresses(address);
            if (ipAddresses.Length == 0)
            {
                Logging.Log(nameof(WsClientWrapper), "Cannot find IP addresses from " + address);
                return false;
            }

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
            {
                Logging.Log(nameof(WsClientWrapper), "Cannot find index of address from " + address);
                return false;
            }

            string url = (secure ? "wss://" : "ws://") + ipAddresses[indexOfAddress] + ":" + port;
            Logging.Log(nameof(WsClientWrapper), $"Connecting to {url}");
            if (secure)
            {
                SslContext context = new SslContext(sslProtocols, new X509Certificate2(), CertValidationCallback);
                wssClient = new WssTransportClient(clientEventQueue, context, ipAddresses[indexOfAddress], port);
                wssClient.OptionDualMode = true;
                wssClient.OptionNoDelay = true;
                return wssClient.ConnectAsync();
            }
            else
            {
                wsClient = new WsTransportClient(clientEventQueue, ipAddresses[indexOfAddress], port);
                wsClient.OptionDualMode = true;
                wsClient.OptionNoDelay = true;
                return wsClient.ConnectAsync();
            }
#else
            string url = (secure ? "wss://" : "ws://") + address + ":" + port;
            Logging.Log(nameof(WsClientWrapper), $"Connecting to {url}");
            wsNativeInstance = SocketCreate(url.ToString());
            return true;
#endif
        }

        public void StopClient()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (wssClient != null)
                wssClient.Dispose();
            wssClient = null;
            if (wsClient != null)
                wsClient.Dispose();
            wsClient = null;
#else
            SocketClose(wsNativeInstance);
#endif
        }

        private bool CertValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
#if !UNITY_WEBGL || UNITY_EDITOR
            if (clientEventQueue.Count == 0)
                return false;
            return clientEventQueue.TryDequeue(out eventData);
#else
            int eventType = GetSocketEventType(wsNativeInstance);
            if (eventType < 0)
                return false;
            switch ((ENetworkEvent)eventType)
            {
                case ENetworkEvent.DataEvent:
                    eventData.type = ENetworkEvent.DataEvent;
                    eventData.reader = new NetDataReader(GetSocketData());
                    break;
                case ENetworkEvent.ConnectEvent:
                    eventData.type = ENetworkEvent.ConnectEvent;
                    break;
                case ENetworkEvent.DisconnectEvent:
                    eventData.type = ENetworkEvent.DisconnectEvent;
                    eventData.disconnectInfo = GetDisconnectInfo(GetSocketErrorCode(wsNativeInstance));
                    break;
                case ENetworkEvent.ErrorEvent:
                    eventData.type = ENetworkEvent.ErrorEvent;
                    eventData.errorMessage = GetErrorMessage(GetSocketErrorCode(wsNativeInstance));
                    break;
            }
            SocketEventDequeue(wsNativeInstance);
            return true;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private byte[] GetSocketData()
        {
            int length = GetSocketDataLength(wsNativeInstance);
            if (length == 0)
                return null;
            byte[] buffer = new byte[length];
            GetSocketData(wsNativeInstance, buffer, length);
            return buffer;
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        public string GetErrorMessage(int errorCode)
        {
            // TODO: Implement this
            return string.Empty;
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        public DisconnectInfo GetDisconnectInfo(int errorCode)
        {
            // TODO: Implement this
            return default;
        }
#endif

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (!IsClientStarted)
                return false;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (secure)
                return wssClient.SendBinaryAsync(writer.Data, 0, writer.Data.Length);
            else
                return wsClient.SendBinaryAsync(writer.Data, 0, writer.Data.Length);
#else
            SocketSend(wsNativeInstance, writer.Data, writer.Data.Length);
            return true;
#endif
        }
    }
}
