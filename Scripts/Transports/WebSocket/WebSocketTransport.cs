using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
#if !UNITY_WEBGL || UNITY_EDITOR
using WebSocketSharp;
using WebSocketSharp.Server;
#endif

namespace LiteNetLibManager
{
    public class WebSocketTransport : ITransport
    {
        private WebSocket client;
#if !UNITY_WEBGL || UNITY_EDITOR
        private WebSocketServer server;
        private long nextConnectionId = 1;
        private long tempConnectionId;
        private readonly Dictionary<long, WSBehavior> serverPeers;
        private readonly Queue<TransportEventData> serverEventQueue;
#endif
        private bool dirtyIsConnected;
        private byte[] tempBuffers;

        public WebSocketTransport()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            serverPeers = new Dictionary<long, WSBehavior>();
            serverEventQueue = new Queue<TransportEventData>();
#endif
        }

        public bool IsClientStarted()
        {
            return client != null && client.IsConnected;
        }

        public bool StartClient(string connectKey, string address, int port)
        {
            client = new WebSocket(new System.Uri("ws://" + address + ":" + port));
            client.Connect();
            return true;
        }

        public void StopClient()
        {
            if (client != null)
                client.Close();
            client = null;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (client == null)
                return false;
            if (dirtyIsConnected != client.IsConnected)
            {
                dirtyIsConnected = client.IsConnected;
                if (client.IsConnected)
                {
                    // Connect state changed to connected, so it's connect event
                    eventData.type = ENetworkEvent.ConnectEvent;
                }
                else
                {
                    // Connect state changed to not connected, so it's disconnect event
                    eventData.type = ENetworkEvent.DisconnectEvent;
                }
                return true;
            }
            else
            {
                tempBuffers = client.Recv();
                if (tempBuffers != null)
                {
                    eventData.type = ENetworkEvent.DataEvent;
                    eventData.reader = new NetDataReader(tempBuffers);
                    return true;
                }
            }
            return false;
        }

        public bool ClientSend(SendOptions sendOptions, NetDataWriter writer)
        {
            if (IsClientStarted())
            {
                client.Send(writer.Data);
                return true;
            }
            return false;
        }

        public bool IsServerStarted()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return server != null;
#else
            return false;
#endif
        }

        public bool StartServer(string connectKey, int port, int maxConnections)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            serverPeers.Clear();
            server = new WebSocketServer(port);
            server.AddWebSocketService("/", () =>
            {
                tempConnectionId = nextConnectionId++;
                WSBehavior behavior = new WSBehavior(tempConnectionId, serverEventQueue);
                serverPeers[tempConnectionId] = behavior;
                return behavior;
            });
            server.Start();
            return true;
#else
            return false;
#endif
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
#if !UNITY_WEBGL || UNITY_EDITOR
            if (server == null)
                return false;
            if (serverEventQueue.Count == 0)
                return false;
            eventData = serverEventQueue.Dequeue();
            return true;
#else
            return false;
#endif
        }

        public bool ServerSend(long connectionId, SendOptions sendOptions, NetDataWriter writer)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId))
            {
                serverPeers[connectionId].Context.WebSocket.Send(writer.Data);
                return true;
            }
#endif
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId))
            {
                serverPeers[connectionId].Context.WebSocket.Close();
                return true;
            }
#endif
            return false;
        }

        public void StopServer()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (server != null)
                server.Stop();
            nextConnectionId = 1;
            server = null;
#endif
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }

        public int GetFreePort()
        {
            Socket socketV4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socketV4.Bind(new IPEndPoint(IPAddress.Any, 0));
            int port = ((IPEndPoint)socketV4.LocalEndPoint).Port;
            socketV4.Close();
            return port;
        }
    }
}
