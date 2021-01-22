using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class LiteNetLibManager : MonoBehaviour
    {
        public enum LogLevel : byte
        {
            Developer = 0,
            Debug = 1,
            Info = 2,
            Warn = 3,
            Error = 4,
            Fatal = 5,
        }

        public LiteNetLibClient Client { get; private set; }
        public LiteNetLibServer Server { get; private set; }
        public bool IsServer { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsClientConnected { get { return Client.IsNetworkActive; } }
        public bool IsNetworkActive { get { return Server.IsNetworkActive || Client.IsNetworkActive; } }
        public bool LogDev { get { return currentLogLevel <= LogLevel.Developer; } }
        public bool LogDebug { get { return currentLogLevel <= LogLevel.Debug; } }
        public bool LogInfo { get { return currentLogLevel <= LogLevel.Info; } }
        public bool LogWarn { get { return currentLogLevel <= LogLevel.Warn; } }
        public bool LogError { get { return currentLogLevel <= LogLevel.Error; } }
        public bool LogFatal { get { return currentLogLevel <= LogLevel.Fatal; } }

        [Header("Client & Server Settings")]
        public LogLevel currentLogLevel = LogLevel.Info;
        public string networkAddress = "localhost";
        public int networkPort = 7770;
        public bool useWebSocket = false;

        [Header("Server Only Settings")]
        public int maxConnections = 4;

        [Header("Transport Layer Settings")]
        [SerializeField]
        private BaseTransportFactory transportFactory;
        public BaseTransportFactory TransportFactory
        {
            get { return transportFactory; }
        }

        private OfflineTransport offlineTransport = new OfflineTransport();
        private ITransport transport;
        public ITransport Transport
        {
            get
            {
                if (isOfflineConnection)
                    return offlineTransport;
                return transport;
            }
        }

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

        protected readonly HashSet<long> ConnectionIds = new HashSet<long>();

        private bool isOfflineConnection;

        protected virtual void Awake()
        {
            InitTransportAndHandlers();
        }

        protected void InitTransportAndHandlers()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Force to use websocket transport if it's running as webgl
            if (transportFactory == null || !transportFactory.CanUseWithWebGL)
                transportFactory = gameObject.AddComponent<WebSocketTransportFactory>();
#else
            if (useWebSocket)
            {
                if (transportFactory == null || !transportFactory.CanUseWithWebGL)
                    transportFactory = gameObject.AddComponent<WebSocketTransportFactory>();
            }
            else
            {
                if (transportFactory == null)
                    transportFactory = gameObject.AddComponent<LiteNetLibTransportFactory>();
            }
#endif
            transport = TransportFactory.Build();
            Client = new LiteNetLibClient(this);
            Server = new LiteNetLibServer(this);
            RegisterMessages();
        }

        protected virtual void LateUpdate()
        {
            if (IsServer)
                Server.Update();
            if (IsClient)
                Client.Update();
        }

        protected virtual void OnDestroy()
        {
            StopHost();
            if (Transport != null)
                Transport.Destroy();
        }

        protected virtual void OnApplicationQuit()
        {
#if UNITY_EDITOR
            StopHost();
            if (Transport != null)
                Transport.Destroy();
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
            this.isOfflineConnection = isOfflineConnection;
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
            isOfflineConnection = false;

            OnStopServer();
        }

        public void StopClient()
        {
            if (!IsClient)
                return;

            if (LogInfo) Logging.Log(LogTag, "StopClient");
            IsClient = false;
            Client.StopClient();
            isOfflineConnection = false;

            OnStopClient();
        }

        public void AddConnectionId(long connectionId)
        {
            ConnectionIds.Add(connectionId);
        }

        public bool RemoveConnectionId(long connectionId)
        {
            return ConnectionIds.Remove(connectionId);
        }

        public bool ContainsConnectionId(long connectionId)
        {
            return ConnectionIds.Contains(connectionId);
        }

        public IEnumerable<long> GetConnectionIds()
        {
            return ConnectionIds;
        }

        #region Packets send / read
        public void ClientSendPacket(DeliveryMethod options, ushort msgType, SerializerDelegate serializer)
        {
            Client.SendPacket(options, msgType, serializer);
        }

        public void ClientSendPacket<T>(DeliveryMethod options, ushort msgType, T messageData, SerializerDelegate extraSerializer = null) where T : INetSerializable
        {
            ClientSendPacket(options, msgType, (writer) =>
            {
                messageData.Serialize(writer);
                if (extraSerializer != null)
                    extraSerializer.Invoke(writer);
            });
        }

        public void ClientSendPacket(DeliveryMethod options, ushort msgType)
        {
            ClientSendPacket(options, msgType, null);
        }

        public void ServerSendPacket(long connectionId, DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            Server.SendPacket(connectionId, deliveryMethod, msgType, serializer);
        }

        public void ServerSendPacket<T>(long connectionId, DeliveryMethod deliveryMethod, ushort msgType, T messageData, SerializerDelegate extraSerializer = null) where T : INetSerializable
        {
            ServerSendPacket(connectionId, deliveryMethod, msgType, (writer) =>
            {
                messageData.Serialize(writer);
                if (extraSerializer != null)
                    extraSerializer.Invoke(writer);
            });
        }

        public void ServerSendPacket(long connectionId, DeliveryMethod options, ushort msgType)
        {
            ServerSendPacket(connectionId, options, msgType, null);
        }

        public bool ClientSendRequest<TRequest>(ushort requestType, TRequest request, SerializerDelegate extraRequestSerializer = null, int millisecondsTimeout = 30000, ResponseDelegate responseDelegate = null)
            where TRequest : INetSerializable
        {
            return Client.SendRequest(requestType, request, extraRequestSerializer, millisecondsTimeout, responseDelegate);
        }

        public bool ServerSendRequest<TRequest>(long connectionId, ushort msgType, TRequest request, SerializerDelegate extraRequestSerializer = null, int millisecondsTimeout = 30000, ResponseDelegate responseDelegate = null)
            where TRequest : INetSerializable
        {
            return Server.SendRequest(connectionId, msgType, request, extraRequestSerializer, millisecondsTimeout, responseDelegate);
        }

        public bool ClientSendRequest<TRequest, TResponse>(ushort requestType, TRequest request, SerializerDelegate extraRequestSerializer = null, int millisecondsTimeout = 30000, ResponseDelegate<TResponse> responseDelegate = null)
            where TRequest : INetSerializable
            where TResponse : INetSerializable
        {
            return Client.SendRequest(requestType, request, extraRequestSerializer, millisecondsTimeout, (requestHandler, responseCode, response) =>
            {
                if (responseDelegate != null)
                    return responseDelegate.Invoke(requestHandler, responseCode, (TResponse)response);
                return default;
            });
        }

        public bool ServerSendRequest<TRequest, TResponse>(long connectionId, ushort msgType, TRequest request, SerializerDelegate extraRequestSerializer = null, int millisecondsTimeout = 30000, ResponseDelegate<TResponse> responseDelegate = null)
            where TRequest : INetSerializable
            where TResponse : INetSerializable
        {
            return Server.SendRequest(connectionId, msgType, request, extraRequestSerializer, millisecondsTimeout, (requestHandler, responseCode, response) =>
            {
                if (responseDelegate != null)
                    return responseDelegate.Invoke(requestHandler, responseCode, (TResponse)response);
                return default;
            });
        }
        #endregion

        #region Relates components functions
        public void ServerSendPacketToAllConnections(DeliveryMethod deliveryMethod, ushort msgType, SerializerDelegate serializer)
        {
            foreach (long connectionId in ConnectionIds)
            {
                ServerSendPacket(connectionId, deliveryMethod, msgType, serializer);
            }
        }

        public void ServerSendPacketToAllConnections<T>(DeliveryMethod deliveryMethod, ushort msgType, T messageData) where T : INetSerializable
        {
            foreach (long connectionId in ConnectionIds)
            {
                ServerSendPacket(connectionId, deliveryMethod, msgType, messageData);
            }
        }

        public void ServerSendPacketToAllConnections(DeliveryMethod deliveryMethod, ushort msgType)
        {
            foreach (long connectionId in ConnectionIds)
            {
                ServerSendPacket(connectionId, deliveryMethod, msgType);
            }
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
