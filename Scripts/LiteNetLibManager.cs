using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using Cysharp.Threading.Tasks;

namespace LiteNetLibManager
{
    public class LiteNetLibManager : MonoBehaviour
    {
        public LiteNetLibClient Client { get; protected set; }
        public LiteNetLibServer Server { get; protected set; }
        public bool IsServer { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsClientConnected { get { return Client.IsNetworkActive; } }
        public bool IsNetworkActive { get { return Server.IsNetworkActive || Client.IsNetworkActive; } }
        public bool LogDev { get { return currentLogLevel.IsLogDev(); } }
        public bool LogDebug { get { return currentLogLevel.IsLogDebug(); } }
        public bool LogInfo { get { return currentLogLevel.IsLogInfo(); } }
        public bool LogWarn { get { return currentLogLevel.IsLogWarn(); } }
        public bool LogError { get { return currentLogLevel.IsLogError(); } }
        public bool LogFatal { get { return currentLogLevel.IsLogFatal(); } }

        [Header("Client & Server Settings")]
        public ELogLevel currentLogLevel = ELogLevel.Info;
        public string networkAddress = "localhost";
        public int networkPort = 7770;
        public bool useWebSocket = false;
        public bool webSocketSecure = false;
        public string webSocketCertificateFilePath = string.Empty;
        public string webSocketCertificatePassword = string.Empty;

        [Header("Server Only Settings")]
        public int maxConnections = 4;

        [Header("Transport Layer Settings")]
        [SerializeField]
        private BaseTransportFactory transportFactory;
        public BaseTransportFactory TransportFactory
        {
            get { return transportFactory; }
            set { transportFactory = value; }
        }

        private ITransport offlineTransport;
        private ITransport clientTransport;
        public ITransport ClientTransport
        {
            get { return IsOfflineConnection ? offlineTransport : clientTransport; }
        }
        private ITransport serverTransport;
        public ITransport ServerTransport
        {
            get { return IsOfflineConnection ? offlineTransport : serverTransport; }
        }

        public bool IsOfflineConnection { get; protected set; }

        private string logTag;
        public virtual string LogTag
        {
            get
            {
                if (string.IsNullOrEmpty(logTag))
                    logTag = $"{name}({GetType().Name})";
                return logTag;
            }
        }

        protected virtual void Start()
        {
            InitTransportAndHandlers();
        }

        protected void PrepareTransportFactory()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Force to use websocket transport if it's running as webgl
            if (transportFactory == null || !(transportFactory is IWebSocketTransportFactory))
            {
                WebSocketTransportFactory webSocketTransportFactory = gameObject.AddComponent<WebSocketTransportFactory>();
                webSocketTransportFactory.Secure = webSocketSecure;
                webSocketTransportFactory.CertificateFilePath = webSocketCertificateFilePath;
                webSocketTransportFactory.CertificatePassword = webSocketCertificatePassword;
                transportFactory = webSocketTransportFactory;
            }
#else
            if (useWebSocket)
            {
                if (transportFactory == null || !(transportFactory is IWebSocketTransportFactory))
                {
                    WebSocketTransportFactory webSocketTransportFactory = gameObject.AddComponent<WebSocketTransportFactory>();
                    webSocketTransportFactory.Secure = webSocketSecure;
                    webSocketTransportFactory.CertificateFilePath = webSocketCertificateFilePath;
                    webSocketTransportFactory.CertificatePassword = webSocketCertificatePassword;
                    transportFactory = webSocketTransportFactory;
                }
            }
            else
            {
                if (transportFactory == null)
                    transportFactory = gameObject.AddComponent<LiteNetLibTransportFactory>();
            }
#endif
        }

        public void PrepareClientTransport()
        {
            PrepareTransportFactory();
            if (clientTransport != null)
                clientTransport.Destroy();
            clientTransport = transportFactory.Build();
        }

        public void PrepareServerTransport()
        {
            PrepareTransportFactory();
            if (serverTransport != null)
                serverTransport.Destroy();
            serverTransport = transportFactory.Build();
        }

        protected void InitTransportAndHandlers()
        {
            offlineTransport = new OfflineTransport();
            Client = new LiteNetLibClient(this);
            Server = new LiteNetLibServer(this);
            RegisterMessages();
        }

        protected virtual void FixedUpdate()
        {
            if (IsServer)
                Server.Update();
            if (IsClient)
                Client.Update();
        }

        protected virtual void OnDestroy()
        {
            StopHost();
            if (clientTransport != null)
                clientTransport.Destroy();
            if (serverTransport != null)
                serverTransport.Destroy();
        }

        protected virtual void OnApplicationQuit()
        {
#if UNITY_EDITOR
            StopHost();
            if (clientTransport != null)
                clientTransport.Destroy();
            if (serverTransport != null)
                serverTransport.Destroy();
#endif
        }

        /// <summary>
        /// Override this function to register messages
        /// </summary>
        protected virtual void RegisterMessages() { }

        public virtual bool StartServer()
        {
            if (IsServer)
            {
                if (LogError) Logging.LogError(LogTag, "Cannot start server because it was started.");
                return false;
            }
            PrepareServerTransport();
            Server.Transport = ServerTransport;
            if (!Server.StartServer(networkPort, maxConnections))
            {
                if (LogError) Logging.LogError(LogTag, $"Cannot start server at port: {networkPort}.");
                return false;
            }
            IsServer = true;
            OnStartServer();
            return true;
        }

        public virtual bool StartClient()
        {
            return StartClient(networkAddress, networkPort);
        }

        public virtual bool StartClient(string networkAddress, int networkPort)
        {
            if (IsClient)
            {
                if (LogError) Logging.LogError(LogTag, "Cannot start client because it was started.");
                return false;
            }
            this.networkAddress = networkAddress;
            this.networkPort = networkPort;
            if (LogDev) Logging.Log(LogTag, $"Connecting to {networkAddress}:{networkPort}.");
            PrepareClientTransport();
            Client.Transport = ClientTransport;
            if (!Client.StartClient(networkAddress, networkPort))
            {
                if (LogError) Logging.LogError(LogTag, $"Cannot connect to {networkAddress}:{networkPort}.");
                return false;
            }
            IsClient = true;
            OnStartClient(Client);
            return true;
        }

        public virtual bool StartHost(bool isOfflineConnection = false)
        {
            IsOfflineConnection = isOfflineConnection;
            if (StartServer() && ConnectLocalClient())
            {
                OnStartHost();
                return true;
            }
            return false;
        }

        protected virtual bool ConnectLocalClient()
        {
            return StartClient("localhost", Server.ServerPort);
        }

        public void StopHost()
        {
            OnStopHost();
            StopClient();
            StopServer();
        }

        public void StopServer()
        {
            if (!IsServer)
                return;

            if (LogInfo) Logging.Log(LogTag, "StopServer");
            IsServer = false;
            Server.StopServer();
            OnStopServer();

            if (IsOfflineConnection && IsClient)
            {
                StopClient();
                IsOfflineConnection = false;
            }
        }

        public void StopClient()
        {
            if (!IsClient)
                return;

            if (LogInfo) Logging.Log(LogTag, "StopClient");
            IsClient = false;
            Client.StopClient();
            OnStopClient();

            if (IsOfflineConnection && IsServer)
            {
                StopServer();
                IsOfflineConnection = false;
            }
        }

        public bool ContainsConnectionId(long connectionId)
        {
            return Server.ConnectionIds.Contains(connectionId);
        }

        public IEnumerable<long> GetConnectionIds()
        {
            return Server.ConnectionIds;
        }

        #region Packets send / read
        public void ClientSendMessage(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            Client.SendMessage(dataChannel, deliveryMethod, writer);
        }

        public void ClientSendPacket(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            Client.SendPacket(dataChannel, deliveryMethod, msgType, serializer);
        }

        public void ClientSendPacket<T>(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, T messageData, SerializerDelegate extraSerializer = null) where T : INetSerializable
        {
            ClientSendPacket(dataChannel, deliveryMethod, msgType, (writer) =>
            {
                messageData.Serialize(writer);
                if (extraSerializer != null)
                    extraSerializer.Invoke(writer);
            });
        }

        public void ClientSendPacket(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType)
        {
            ClientSendPacket(dataChannel, deliveryMethod, msgType, null);
        }

        public void ServerSendMessage(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            Server.SendMessage(connectionId, dataChannel, deliveryMethod, writer);
        }

        public void ServerSendPacket(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            Server.SendPacket(connectionId, dataChannel, deliveryMethod, msgType, serializer);
        }

        public void ServerSendPacket<T>(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, T messageData, SerializerDelegate extraSerializer = null) where T : INetSerializable
        {
            ServerSendPacket(connectionId, dataChannel, deliveryMethod, msgType, (writer) =>
            {
                messageData.Serialize(writer);
                if (extraSerializer != null)
                    extraSerializer.Invoke(writer);
            });
        }

        public void ServerSendPacket(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType)
        {
            ServerSendPacket(connectionId, dataChannel, deliveryMethod, msgType, null);
        }

        public bool ClientSendRequest<TRequest>(ushort requestType, TRequest request, ResponseDelegate<INetSerializable> responseDelegate = null, int millisecondsTimeout = 30000, SerializerDelegate extraRequestSerializer = null)
            where TRequest : INetSerializable, new()
        {
            return Client.SendRequest(requestType, request, responseDelegate, millisecondsTimeout, extraRequestSerializer);
        }

        public bool ServerSendRequest<TRequest>(long connectionId, ushort requestType, TRequest request, ResponseDelegate<INetSerializable> responseDelegate = null, int millisecondsTimeout = 30000, SerializerDelegate extraRequestSerializer = null)
            where TRequest : INetSerializable, new()
        {
            return Server.SendRequest(connectionId, requestType, request, responseDelegate, millisecondsTimeout, extraRequestSerializer);
        }

        public bool ClientSendRequest<TRequest, TResponse>(ushort requestType, TRequest request, ResponseDelegate<TResponse> responseDelegate, int millisecondsTimeout = 30000, SerializerDelegate extraRequestSerializer = null)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            return Client.SendRequest(requestType, request, (requestHandler, responseCode, response) =>
            {
                if (!(response is TResponse))
                    response = default(TResponse);
                responseDelegate.Invoke(requestHandler, responseCode, (TResponse)response);
            }, millisecondsTimeout, extraRequestSerializer);
        }

        public UniTask<AsyncResponseData<TResponse>> ClientSendRequestAsync<TRequest, TResponse>(ushort requestType, TRequest request, int millisecondsTimeout = 30000, SerializerDelegate extraRequestSerializer = null)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            return Client.SendRequestAsync<TRequest, TResponse>(requestType, request, millisecondsTimeout, extraRequestSerializer);
        }

        public bool ServerSendRequest<TRequest, TResponse>(long connectionId, ushort requestType, TRequest request, ResponseDelegate<TResponse> responseDelegate, int millisecondsTimeout = 30000, SerializerDelegate extraRequestSerializer = null)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            return Server.SendRequest(connectionId, requestType, request, (requestHandler, responseCode, response) =>
            {
                if (!(response is TResponse))
                    response = default(TResponse);
                responseDelegate.Invoke(requestHandler, responseCode, (TResponse)response);
            }, millisecondsTimeout, extraRequestSerializer);
        }

        public UniTask<AsyncResponseData<TResponse>> ServerSendRequestAsync<TRequest, TResponse>(long connectionId, ushort requestType, TRequest request, int millisecondsTimeout = 30000, SerializerDelegate extraRequestSerializer = null)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            return Server.SendRequestAsync<TRequest, TResponse>(connectionId, requestType, request, millisecondsTimeout, extraRequestSerializer);
        }
        #endregion

        #region Relates components functions
        public void ServerSendMessageToAllConnections(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            Server.SendMessageToAllConnections(dataChannel, deliveryMethod, writer);
        }

        public void ServerSendPacketToAllConnections(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            Server.SendPacketToAllConnections(dataChannel, deliveryMethod, msgType, serializer);
        }

        public void ServerSendPacketToAllConnections<T>(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType, T messageData, SerializerDelegate extraSerializer = null) where T : INetSerializable
        {
            Server.SendPacketToAllConnections(dataChannel, deliveryMethod, msgType, (writer) =>
            {
                messageData.Serialize(writer);
                if (extraSerializer != null)
                    extraSerializer.Invoke(writer);
            });
        }

        public void ServerSendPacketToAllConnections(byte dataChannel, DeliveryMethod deliveryMethod, ushort msgType)
        {
            Server.SendPacketToAllConnections(dataChannel, deliveryMethod, msgType, null);
        }

        public void RegisterServerMessage(ushort msgType, MessageHandlerDelegate handlerDelegate)
        {
            Server.RegisterMessageHandler(msgType, handlerDelegate);
        }

        public void UnregisterServerMessage(ushort msgType)
        {
            Server.UnregisterMessageHandler(msgType);
        }

        public void RegisterClientMessage(ushort msgType, MessageHandlerDelegate handlerDelegate)
        {
            Client.RegisterMessageHandler(msgType, handlerDelegate);
        }

        public void UnregisterClientMessage(ushort msgType)
        {
            Client.UnregisterMessageHandler(msgType);
        }

        public bool EnableRequestResponse(ushort requestMessageType, ushort responseMessageType)
        {
            return Client.EnableRequestResponse(requestMessageType, responseMessageType) &&
                Server.EnableRequestResponse(requestMessageType, responseMessageType);
        }

        public void DisableRequestResponse()
        {
            Client.DisableRequestResponse();
            Server.DisableRequestResponse();
        }

        public void RegisterRequestToServer<TRequest, TResponse>(ushort reqType, RequestDelegate<TRequest, TResponse> requestHandlerDelegate, ResponseDelegate<TResponse> responseHandlerDelegate = null)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            Server.RegisterRequestHandler(reqType, requestHandlerDelegate);
            Client.RegisterResponseHandler<TRequest, TResponse>(reqType, responseHandlerDelegate);
        }

        public void UnregisterRequestToServer(ushort reqType)
        {
            Server.UnregisterRequestHandler(reqType);
            Client.UnregisterResponseHandler(reqType);
        }

        public void RegisterRequestToClient<TRequest, TResponse>(ushort reqType, RequestDelegate<TRequest, TResponse> requestHandlerDelegate, ResponseDelegate<TResponse> responseHandlerDelegate = null)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            Client.RegisterRequestHandler(reqType, requestHandlerDelegate);
            Server.RegisterResponseHandler<TRequest, TResponse>(reqType, responseHandlerDelegate);
        }

        public void UnregisterRequestToClient(ushort reqType)
        {
            Client.UnregisterRequestHandler(reqType);
            Server.UnregisterResponseHandler(reqType);
        }
        #endregion

        #region Network Events Callbacks
        /// <summary>
        /// This event will be called at server when there are any network error
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="socketError"></param>
        public virtual void OnPeerNetworkError(IPEndPoint endPoint, SocketError socketError) { }

        /// <summary>
        /// This event will be called at server when any client connected
        /// </summary>
        /// <param name="connectionId"></param>
        public virtual void OnPeerConnected(long connectionId) { }

        /// <summary>
        /// This event will be called at server when any client disconnected
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="disconnectInfo"></param>
        public virtual void OnPeerDisconnected(long connectionId, DisconnectInfo disconnectInfo) { }

        /// <summary>
        /// This event will be called at client when there are any network error
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="socketError"></param>
        public virtual void OnClientNetworkError(IPEndPoint endPoint, SocketError socketError) { }

        /// <summary>
        /// This event will be called at client when connected to server
        /// </summary>
        public virtual void OnClientConnected() { }

        /// <summary>
        /// This event will be called at client when disconnected from server
        /// </summary>
        public virtual void OnClientDisconnected(DisconnectInfo disconnectInfo) { }
        #endregion

        #region Start / Stop Callbacks
        // Since there are multiple versions of StartServer, StartClient and StartHost, to reliably customize
        // their functionality, users would need override all the versions. Instead these callbacks are invoked
        // from all versions, so users only need to implement this one case.
        /// <summary>
        /// This hook is invoked when a host is started.
        /// </summary>
        public virtual void OnStartHost()
        {
            if (LogInfo) Logging.Log(LogTag, "OnStartHost");
        }

        /// <summary>
        /// This hook is invoked when a server is started - including when a host is started.
        /// </summary>
        public virtual void OnStartServer()
        {
            if (LogInfo) Logging.Log(LogTag, "OnStartServer");
        }

        /// <summary>
        /// This is a hook that is invoked when the client is started.
        /// </summary>
        /// <param name="client"></param>
        public virtual void OnStartClient(LiteNetLibClient client)
        {
            if (LogInfo) Logging.Log(LogTag, "OnStartClient");
        }

        /// <summary>
        /// This hook is called when a server is stopped - including when a host is stopped.
        /// </summary>
        public virtual void OnStopServer()
        {
            if (LogInfo) Logging.Log(LogTag, "OnStopServer");
        }

        /// <summary>
        /// This hook is called when a client is stopped.
        /// </summary>
        public virtual void OnStopClient()
        {
            if (LogInfo) Logging.Log(LogTag, "OnStopClient");
        }

        /// <summary>
        /// This hook is called when a host is stopped.
        /// </summary>
        public virtual void OnStopHost()
        {
            if (LogInfo) Logging.Log(LogTag, "OnStopHost");
        }
        #endregion
    }
}
