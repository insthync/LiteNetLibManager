using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public sealed class LiteNetLibTransport : ITransport
    {
        public NetManager Client { get; private set; }
        public NetManager Server { get; private set; }
        public string ConnectKey { get; private set; }
        public int ServerPeersCount
        {
            get
            {
                if (Server != null)
                    return Server.ConnectedPeersCount;
                return 0;
            }
        }
        public int ServerMaxConnections { get; private set; }
        public bool IsClientStarted
        {
            get { return Client != null && Client.FirstPeer != null && Client.FirstPeer.ConnectionState == ConnectionState.Connected; }
        }
        public bool IsServerStarted
        {
            get { return Server != null; }
        }
        private readonly Dictionary<long, NetPeer> serverPeers;
        private readonly Queue<TransportEventData> clientEventQueue;
        private readonly Queue<TransportEventData> serverEventQueue;
        private readonly byte clientDataChannelsCount;
        private readonly byte serverDataChannelsCount;

        public LiteNetLibTransport(string connectKey, byte clientDataChannelsCount, byte serverDataChannelsCount)
        {
            ConnectKey = connectKey;
            serverPeers = new Dictionary<long, NetPeer>();
            clientEventQueue = new Queue<TransportEventData>();
            serverEventQueue = new Queue<TransportEventData>();
            this.clientDataChannelsCount = clientDataChannelsCount;
            this.serverDataChannelsCount = serverDataChannelsCount;
        }

        public bool StartClient(string address, int port)
        {
            if (IsClientStarted)
                return false;
            clientEventQueue.Clear();
            Client = new NetManager(new LiteNetLibTransportClientEventListener(clientEventQueue));
            Client.ChannelsCount = clientDataChannelsCount;
            return Client.Start() && Client.Connect(address, port, ConnectKey) != null;
        }

        public void StopClient()
        {
            if (Client != null)
                Client.Stop();
            Client = null;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (Client == null)
                return false;
            Client.PollEvents();
            if (clientEventQueue.Count == 0)
                return false;
            eventData = clientEventQueue.Dequeue();
            return true;
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, byte[] data)
        {
            if (IsClientStarted)
            {
                Client.FirstPeer.Send(data, dataChannel, deliveryMethod);
                return true;
            }
            return false;
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (IsServerStarted)
                return false;
            ServerMaxConnections = maxConnections;
            serverPeers.Clear();
            serverEventQueue.Clear();
            Server = new NetManager(new LiteNetLibTransportServerEventListener(this, ConnectKey, serverEventQueue, serverPeers));
            Server.ChannelsCount = serverDataChannelsCount;
            return Server.Start(port);
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (Server == null)
                return false;
            Server.PollEvents();
            if (serverEventQueue.Count == 0)
                return false;
            eventData = serverEventQueue.Dequeue();
            return true;
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, byte[] data)
        {
            if (IsServerStarted && serverPeers.ContainsKey(connectionId) && serverPeers.ContainsKey(connectionId) && serverPeers[connectionId].ConnectionState == ConnectionState.Connected)
            {
                serverPeers[connectionId].Send(data, dataChannel, deliveryMethod);
                return true;
            }
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
            if (IsServerStarted && serverPeers.ContainsKey(connectionId))
            {
                Server.DisconnectPeer(serverPeers[connectionId]);
                serverPeers.Remove(connectionId);
                return true;
            }
            return false;
        }

        public void StopServer()
        {
            if (Server != null)
                Server.Stop();
            Server = null;
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }
    }
}
