using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public sealed class LiteNetLibTransport : ITransport
    {
        public NetManager client { get; private set; }
        public NetManager server { get; private set; }
        public string connectKey { get; private set; }
        public int maxConnections { get; private set; }
        private readonly Dictionary<long, NetPeer> serverPeers;
        private readonly Queue<TransportEventData> clientEventQueue;
        private readonly Queue<TransportEventData> serverEventQueue;

        public LiteNetLibTransport(string connectKey)
        {
            this.connectKey = connectKey;
            serverPeers = new Dictionary<long, NetPeer>();
            clientEventQueue = new Queue<TransportEventData>();
            serverEventQueue = new Queue<TransportEventData>();
        }

        public bool IsClientStarted()
        {
            return client != null && client.FirstPeer != null && client.FirstPeer.ConnectionState == ConnectionState.Connected;
        }

        public bool StartClient(string address, int port)
        {
            if (IsClientStarted())
                return false;
            clientEventQueue.Clear();
            client = new NetManager(new LiteNetLibTransportEventListener(this, clientEventQueue));
            return client.Start() && client.Connect(address, port, connectKey) != null;
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

        public bool ClientSend(DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (IsClientStarted())
            {
                client.FirstPeer.Send(writer, deliveryMethod);
                return true;
            }
            return false;
        }

        public bool IsServerStarted()
        {
            return server != null;
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (IsServerStarted())
                return false;
            serverPeers.Clear();
            serverEventQueue.Clear();
            server = new NetManager(new LiteNetLibTransportEventListener(this, serverEventQueue, serverPeers));
            this.maxConnections = maxConnections;
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

        public bool ServerSend(long connectionId, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId) && serverPeers.ContainsKey(connectionId) && serverPeers[connectionId].ConnectionState == ConnectionState.Connected)
            {
                serverPeers[connectionId].Send(writer, deliveryMethod);
                return true;
            }
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
            if (IsServerStarted() && serverPeers.ContainsKey(connectionId))
            {
                server.DisconnectPeer(serverPeers[connectionId]);
                serverPeers.Remove(connectionId);
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

        public int GetServerPeersCount()
        {
            if (server != null)
                return server.ConnectedPeersCount;
            return 0;
        }
    }
}
