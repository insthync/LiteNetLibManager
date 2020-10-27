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

        public LiteNetLibClient Client { get; protected set; }
        public LiteNetLibServer Server { get; protected set; }
        public bool IsServer { get { return Server != null; } }
        public bool IsClient { get { return Client != null; } }
        public bool IsClientConnected { get { return Client != null && Client.IsNetworkActive; } }
        public bool IsNetworkActive { get { return Server != null || Client != null; } }
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

        private OfflineTransport offlineTransport;
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

            if (offlineTransport == null)
                offlineTransport = new OfflineTransport();
        }

        protected virtual void Start() { }

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
            Transport.Destroy();
        }

        protected virtual void OnApplicationQuit()
        {
#if UNITY_EDITOR
            StopHost();
            Transport.Destroy();
#endif
        }

        /// <summary>
        /// Override this function to register messages that calling from clients to do anything at server
        /// </summary>
        protected virtual void RegisterServerMessages() { }

        /// <summary>
        /// Override this function to register messages that calling from server to do anything at clients
        /// </summary>
        protected virtual void RegisterClientMessages() { }

        public virtual bool StartServer()
        {
            if (Server != null)
                return true;

            Server = new LiteNetLibServer(this);
            RegisterServerMessages();
            if (!Server.StartServer(networkPort, maxConnections))
            {
                if (LogError) Logging.LogError(LogTag, "StartServer cannot start server at port: " + networkPort);
                Server = null;
                return false;
            }
            OnStartServer();
            return true;
        }

        public virtual LiteNetLibClient StartClient()
        {
            return StartClient(networkAddress, networkPort);
        }

        public virtual LiteNetLibClient StartClient(string networkAddress, int networkPort)
        {
            if (Client != null)
                return Client;

            this.networkAddress = networkAddress;
            this.networkPort = networkPort;
            if (LogDev) Logging.Log(LogTag, "Client connecting to " + networkAddress + ":" + networkPort);
            Client = new LiteNetLibClient(this);
            RegisterClientMessages();
            Client.StartClient(networkAddress, networkPort);
            OnStartClient(Client);
            return Client;
        }

        public virtual LiteNetLibClient StartHost(bool isOfflineConnection = false)
        {
            OnStartHost();
            this.isOfflineConnection = isOfflineConnection;
            if (StartServer())
                return ConnectLocalClient();
            return null;
        }

        protected virtual LiteNetLibClient ConnectLocalClient()
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
            if (Server == null)
                return;

            if (LogInfo) Logging.Log(LogTag, "StopServer");
            Server.StopServer();
            Server = null;
            isOfflineConnection = false;

            OnStopServer();
        }

        public void StopClient()
        {
            if (Client == null)
                return;

            if (LogInfo) Logging.Log(LogTag, "StopClient");
            Client.StopClient();
            Client = null;
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
        public void ClientSendPacket(DeliveryMethod options, ushort msgType, System.Action<NetDataWriter> serializer)
        {
            Client.SendPacket(options, msgType, serializer);
        }

        public void ClientSendPacket<T>(DeliveryMethod options, ushort msgType, T messageData, System.Action<NetDataWriter> extraSerializer = null) where T : INetSerializable
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

        public void ServerSendPacket(long connectionId, DeliveryMethod deliveryMethod, ushort msgType, System.Action<NetDataWriter> serializer)
        {
            Server.SendPacket(connectionId, deliveryMethod, msgType, serializer);
        }

        public void ServerSendPacket<T>(long connectionId, DeliveryMethod deliveryMethod, ushort msgType, T messageData, System.Action<NetDataWriter> extraSerializer = null) where T : INetSerializable
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

        public bool ClientSendRequest<TRequest>(ushort requestType, TRequest request, System.Action<NetDataWriter> extraSerializer = null, long duration = 30)
            where TRequest : INetSerializable
        {
            return Client.SendRequest(requestType, request, extraSerializer, duration);
        }

        public bool ServerSendRequest<TRequest>(long connectionId, ushort msgType, TRequest request, System.Action<NetDataWriter> extraSerializer = null, long duration = 30)
            where TRequest : INetSerializable
        {
            return Server.SendRequest(connectionId, msgType, request, extraSerializer, duration);
        }
        #endregion

        #region Relates components functions
        public void ServerSendPacketToAllConnections(DeliveryMethod deliveryMethod, ushort msgType, System.Action<NetDataWriter> serializer)
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
            Server.RegisterMessage(msgType, handlerDelegate);
        }

        public void UnregisterServerMessage(ushort msgType)
        {
            Server.UnregisterMessage(msgType);
        }

        public void RegisterClientMessage(ushort msgType, MessageHandlerDelegate handlerDelegate)
        {
            Client.RegisterMessage(msgType, handlerDelegate);
        }

        public void UnregisterClientMessage(ushort msgType)
        {
            Client.UnregisterMessage(msgType);
        }

        public bool EnableClientRequestResponse(ushort requestMessageType, ushort responseMessageType)
        {
            return Client.EnableRequestResponse(requestMessageType, responseMessageType);
        }

        public bool EnableServerRequestResponse(ushort requestMessageType, ushort responseMessageType)
        {
            return Server.EnableRequestResponse(requestMessageType, responseMessageType);
        }

        /// <summary>
        /// Use this to register request from client to server (server is request handler)
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="reqType"></param>
        /// <param name="requestDelegate"></param>
        /// <param name="responseDelegate"></param>
        public void RegisterServerRequestHandler<TRequest, TResponse>(ushort reqType, RequestDelegate<TRequest, TResponse> requestDelegate, ResponseDelegate<TResponse> responseDelegate)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            Server.RegisterRequestHandler(reqType, requestDelegate);
            Client.RegisterResponseHandler<TRequest, TResponse>(reqType, responseDelegate);
        }

        public void UnregisterServerRequestHandler(ushort reqType)
        {
            Server.UnregisterRequestHandler(reqType);
            Client.UnregisterResponseHandler(reqType);
        }

        /// <summary>
        /// Use this to register request from server to client (client is request handler)
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="reqType"></param>
        /// <param name="requestDelegate"></param>
        /// <param name="responseDelegate"></param>
        public void RegisterClientRequestHandler<TRequest, TResponse>(ushort reqType, RequestDelegate<TRequest, TResponse> requestDelegate, ResponseDelegate<TResponse> responseDelegate)
            where TRequest : INetSerializable, new()
            where TResponse : INetSerializable, new()
        {
            Client.RegisterRequestHandler(reqType, requestDelegate);
            Server.RegisterResponseHandler<TRequest, TResponse>(reqType, responseDelegate);
        }

        public void UnregisterClientRequestHandler(ushort reqType)
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
