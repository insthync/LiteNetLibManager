using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class LiteNetLibTransport : ITransport
    {
        private NetManager client;
        private NetManager server;
        private readonly Dictionary<long, NetPeer> serverPeers;
        private readonly Queue<TransportEventData> clientEventQueue;
        private readonly Queue<TransportEventData> serverEventQueue;

        public LiteNetLibTransport()
        {
            serverPeers = new Dictionary<long, NetPeer>();
            clientEventQueue = new Queue<TransportEventData>();
            serverEventQueue = new Queue<TransportEventData>();
        }

        public bool IsClientStarted()
        {
            return client != null && client.GetFirstPeer() != null && client.GetFirstPeer().ConnectionState == ConnectionState.Connected;
        }

        public bool StartClient(string connectKey, string address, int port)
        {
            clientEventQueue.Clear();
            client = new NetManager(new LiteNetLibTransportEventListener(clientEventQueue), connectKey);
            return client.Start() && client.Connect(address, port) != null;
        }

        public void StopClient()
        {
            if (client != null)
                client.Stop();
            client = null;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (client == null)
                return false;
            client.PollEvents();
            if (clientEventQueue.Count == 0)
                return false;
            eventData = clientEventQueue.Dequeue();
            return true;
        }

        public bool ClientSend(SendOptions sendOptions, NetDataWriter writer)
        {
            if (IsClientStarted())
            {
                client.GetFirstPeer().Send(writer, sendOptions);
                return true;
            }
            return false;
        }

        public bool IsServerStarted()
        {
            return server != null;
        }

        public bool StartServer(string connectKey, int port, int maxConnections)
        {
            serverPeers.Clear();
            serverEventQueue.Clear();
            server = new NetManager(new LiteNetLibTransportEventListener(serverEventQueue, serverPeers), maxConnections, connectKey);
            return server.Start(port);
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (server == null)
                return false;
            server.PollEvents();
            if (serverEventQueue.Count == 0)
                return false;
            eventData = serverEventQueue.Dequeue();
            return true;
        }

        public bool ServerSend(long connectionId, SendOptions sendOptions, NetDataWriter writer)
        {
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId))
            {
                serverPeers[connectionId].Send(writer, sendOptions);
                return true;
            }
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId))
            {
                server.DisconnectPeer(serverPeers[connectionId]);
                return true;
            }
            return false;
        }

        public void StopServer()
        {
            if (server != null)
                server.Stop();
            server = null;
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
