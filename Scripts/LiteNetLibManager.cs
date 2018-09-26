using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;
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
        public bool IsClientConnected { get { return Client != null && Client.IsClientConnected; } }
        public bool IsNetworkActive { get { return Server != null || Client != null; } }
        public bool LogDev { get { return currentLogLevel <= LogLevel.Developer; } }
        public bool LogDebug { get { return currentLogLevel <= LogLevel.Debug; } }
        public bool LogInfo { get { return currentLogLevel <= LogLevel.Info; } }
        public bool LogWarn { get { return currentLogLevel <= LogLevel.Warn; } }
        public bool LogError { get { return currentLogLevel <= LogLevel.Error; } }
        public bool LogFatal { get { return currentLogLevel <= LogLevel.Fatal; } }

        [Header("Client & Server Configs")]
        public LogLevel currentLogLevel = LogLevel.Info;
        public string connectKey = "SampleConnectKey";
        public string networkAddress = "localhost";
        public int networkPort = 7770;

        [Header("Server Only Configs")]
        public int maxConnections = 4;

        public ITransport transport = new LiteNetLibTransport();

        protected readonly HashSet<long> ConnectionIds = new HashSet<long>();

        protected virtual void Awake() { }

        protected virtual void Start() { }

        protected virtual void Update()
        {
            if (IsServer)
                Server.Update();
            if (IsClient)
                Client.Update();
        }

        protected virtual void OnDestroy()
        {
            StopHost();
            transport.Destroy();
        }

        protected virtual void OnApplicationQuit()
        {
            StopHost();
            transport.Destroy();
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
            return StartServer(false);
        }

        protected virtual bool StartServer(bool isOffline)
        {
            if (Server != null)
                return true;

            Server = new LiteNetLibServer(this, connectKey);
            RegisterServerMessages();
            var canStartServer = !isOffline ? Server.StartServer(networkPort, maxConnections) : Server.StartServer(GetAvailablePort(Random.Range(5000, 10000)), 1);
            if (!canStartServer)
            {
                if (LogError) Debug.LogError("[" + name + "] LiteNetLibManager::StartServer cannot start server at port: " + networkPort);
                Server = null;
                return false;
            }
            OnStartServer();
            return true;
        }

        public virtual LiteNetLibClient StartClient()
        {
            return StartClient(networkAddress, networkPort, connectKey);
        }

        public virtual LiteNetLibClient StartClient(string networkAddress, int networkPort)
        {
            return StartClient(networkAddress, networkPort, connectKey);
        }

        public virtual LiteNetLibClient StartClient(string networkAddress, int networkPort, string connectKey)
        {
            if (Client != null)
                return Client;

            this.networkAddress = networkAddress;
            this.networkPort = networkPort;
            this.connectKey = connectKey;
            if (LogDev) Debug.Log("Client connecting to " + networkAddress + ":" + networkPort);
            Client = new LiteNetLibClient(this, connectKey);
            RegisterClientMessages();
            Client.StartClient(networkAddress, networkPort);
            OnStartClient(Client);
            return Client;
        }

        public virtual LiteNetLibClient StartHost(bool isOffline = false)
        {
            OnStartHost();
            if (StartServer(isOffline))
                return ConnectLocalClient();
            return null;
        }

        protected virtual LiteNetLibClient ConnectLocalClient()
        {
            return StartClient("localhost", Server.ServerPort, connectKey);
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

            if (LogInfo) Debug.Log("[" + name + "] LiteNetLibManager::StopServer");
            Server.StopServer();
            Server = null;

            OnStopServer();
        }

        public void StopClient()
        {
            if (Client == null)
                return;

            if (LogInfo) Debug.Log("[" + name + "] LiteNetLibManager::StopClient");
            Client.StopClient();
            Client = null;

            OnStopClient();
        }

        internal void AddConnectionId(long connectionId)
        {
            ConnectionIds.Add(connectionId);
        }

        internal bool RemoveConnectionId(long connectionId)
        {
            return ConnectionIds.Remove(connectionId);
        }

        internal bool ContainsConnectionId(long connectionId)
        {
            return ConnectionIds.Contains(connectionId);
        }

        internal IEnumerable<long> GetConnectionIds()
        {
            return ConnectionIds;
        }

        #region Packets send / read
        public void ClientSendPacket(SendOptions options, ushort msgType, System.Action<NetDataWriter> serializer)
        {
            Client.ClientSendPacket(options, msgType, serializer);
        }

        public void ClientSendPacket<T>(SendOptions options, ushort msgType, T messageData) where T : ILiteNetLibMessage
        {
            ClientSendPacket(options, msgType, messageData.Serialize);
        }

        public void ClientSendPacket(SendOptions options, ushort msgType)
        {
            ClientSendPacket(options, msgType, null);
        }

        public void ServerSendPacket(long connectionId, SendOptions options, ushort msgType, System.Action<NetDataWriter> serializer)
        {
            Server.ServerSendPacket(connectionId, options, msgType, serializer);
        }

        public void ServerSendPacket<T>(long connectionId, SendOptions options, ushort msgType, T messageData) where T : ILiteNetLibMessage
        {
            ServerSendPacket(connectionId, options, msgType, messageData.Serialize);
        }

        public void ServerSendPacket(long connectionId, SendOptions options, ushort msgType)
        {
            ServerSendPacket(connectionId, options, msgType, null);
        }
        #endregion

        #region Relates components functions
        public void ServerSendPacketToAllConnections(SendOptions options, ushort msgType, System.Action<NetDataWriter> serializer)
        {
            foreach (var connectionId in ConnectionIds)
            {
                ServerSendPacket(connectionId, options, msgType, serializer);
            }
        }

        public void ServerSendPacketToAllConnections<T>(SendOptions options, ushort msgType, T messageData) where T : ILiteNetLibMessage
        {
            foreach (var connectionId in ConnectionIds)
            {
                ServerSendPacket(connectionId, options, msgType, messageData);
            }
        }

        public void ServerSendPacketToAllConnections(SendOptions options, ushort msgType)
        {
            foreach (var connectionId in ConnectionIds)
            {
                ServerSendPacket(connectionId, options, msgType);
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
        #endregion

        #region Network Events Callbacks
        /// <summary>
        /// This event will be called at server when there are any network error
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="socketErrorCode"></param>
        public virtual void OnPeerNetworkError(NetEndPoint endPoint, int socketErrorCode) { }

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
        /// <param name="socketErrorCode"></param>
        public virtual void OnClientNetworkError(NetEndPoint endPoint, int socketErrorCode) { }

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
            if (LogInfo) Debug.Log("[" + name + "] LiteNetLibManager::OnStartHost");
        }

        /// <summary>
        /// This hook is invoked when a server is started - including when a host is started.
        /// </summary>
        public virtual void OnStartServer()
        {
            if (LogInfo) Debug.Log("[" + name + "] LiteNetLibManager::OnStartServer");
        }

        /// <summary>
        /// This is a hook that is invoked when the client is started.
        /// </summary>
        /// <param name="client"></param>
        public virtual void OnStartClient(LiteNetLibClient client)
        {
            if (LogInfo) Debug.Log("[" + name + "] LiteNetLibManager::OnStartClient");
        }

        /// <summary>
        /// This hook is called when a server is stopped - including when a host is stopped.
        /// </summary>
        public virtual void OnStopServer()
        {
            if (LogInfo) Debug.Log("[" + name + "] LiteNetLibManager::OnStopServer");
        }

        /// <summary>
        /// This hook is called when a client is stopped.
        /// </summary>
        public virtual void OnStopClient()
        {
            if (LogInfo) Debug.Log("[" + name + "] LiteNetLibManager::OnStopClient");
        }

        /// <summary>
        /// This hook is called when a host is stopped.
        /// </summary>
        public virtual void OnStopHost()
        {
            if (LogInfo) Debug.Log("[" + name + "] LiteNetLibManager::OnStopHost");
        }
        #endregion

        #region Utilities
        /// <summary>
        /// checks for used ports and retrieves the first free port
        /// </summary>
        /// <returns>the free port or 0 if it did not find a free port</returns>
        public static int GetAvailablePort(int startingPort)
        {
            IPEndPoint[] endPoints;
            List<int> portArray = new List<int>();

            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

            //getting active connections
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();
            portArray.AddRange(from n in connections
                               where n.LocalEndPoint.Port >= startingPort
                               select n.LocalEndPoint.Port);

            //getting active tcp listners - WCF service listening in tcp
            endPoints = properties.GetActiveTcpListeners();
            portArray.AddRange(from n in endPoints
                               where n.Port >= startingPort
                               select n.Port);

            //getting active udp listeners
            endPoints = properties.GetActiveUdpListeners();
            portArray.AddRange(from n in endPoints
                               where n.Port >= startingPort
                               select n.Port);

            portArray.Sort();

            for (int i = startingPort; i < System.UInt16.MaxValue; i++)
                if (!portArray.Contains(i))
                    return i;

            return 0;
        }
        #endregion
    }
}
