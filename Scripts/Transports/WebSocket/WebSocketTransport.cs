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
        private long nextConnectionId = 1;
        private bool secure;
        private string certificateFilePath;
        private string certificatePassword;
        private readonly ConcurrentQueue<TransportEventData> clientEventQueue;
        private WsClientWrapper wsClient;
#if !UNITY_WEBGL || UNITY_EDITOR
        private WsTransportServer wsServer;
        private WssTransportServer wssServer;
#endif

        public bool IsClientStarted
        {
            get { return wsClient.IsClientStarted; }
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
            clientEventQueue = new ConcurrentQueue<TransportEventData>();
            wsClient = new WsClientWrapper(clientEventQueue, secure, SslProtocols.Tls12);
        }

        public bool StartClient(string address, int port)
        {
            while (clientEventQueue.TryDequeue(out _)) { }
            return wsClient.StartClient(address, port);
        }

        public void StopClient()
        {
            wsClient.StopClient();
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            return wsClient.ClientReceive(out eventData);
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            return wsClient.ClientSend(dataChannel, deliveryMethod, writer);
        }

        public bool StartServer(int port, int maxConnections)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted)
                return false;
            if (!secure)
            {
                wsServer = new WsTransportServer(this, IPAddress.Any, port, maxConnections);
                wsServer.OptionDualMode = true;
                wsServer.OptionNoDelay = true;
                return wsServer.Start();
            }
            else
            {
                SslContext context = new SslContext(SslProtocols.Tls12, new X509Certificate2(certificateFilePath, certificatePassword), CertValidationCallback);
                wssServer = new WssTransportServer(this, context, IPAddress.Any, port, maxConnections);
                wssServer.OptionDualMode = true;
                wssServer.OptionNoDelay = true;
                return wssServer.Start();
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
            return Interlocked.Increment(ref nextConnectionId);
        }
    }
}
