using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public sealed class LiteNetLibTransport : ITransport
    {
        private readonly Dictionary<long, NetPeer> _serverPeers;
        private readonly Queue<TransportEventData> _clientEventQueue;
        private readonly Queue<TransportEventData> _serverEventQueue;
        private readonly byte _clientDataChannelsCount;
        private readonly byte _serverDataChannelsCount;

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
        public bool HasImplementedPing => true;
        public bool IsReliableOnly => false;

        public LiteNetLibTransport(string connectKey, byte clientDataChannelsCount, byte serverDataChannelsCount)
        {
            ConnectKey = connectKey;
            _serverPeers = new Dictionary<long, NetPeer>();
            _clientEventQueue = new Queue<TransportEventData>();
            _serverEventQueue = new Queue<TransportEventData>();
            _clientDataChannelsCount = clientDataChannelsCount;
            _serverDataChannelsCount = serverDataChannelsCount;
        }

        public bool StartClient(string address, int port)
        {
            if (IsClientStarted)
            {
                Logging.Log(nameof(LiteNetLibTransport), "Client started, so it can't be started again");
                return false;
            }
            _clientEventQueue.Clear();
            Client = new NetManager(new LiteNetLibTransportClientEventListener(_clientEventQueue));
            Client.ChannelsCount = _clientDataChannelsCount;
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
            eventData = default;
            if (Client == null)
                return false;
            Client.PollEvents();
            if (_clientEventQueue.Count == 0)
                return false;
            eventData = _clientEventQueue.Dequeue();
            return true;
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (IsClientStarted)
            {
                Client.FirstPeer.Send(writer, dataChannel, deliveryMethod);
                return true;
            }
            return false;
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (IsServerStarted)
                return false;
            ServerMaxConnections = maxConnections;
            _serverPeers.Clear();
            _serverEventQueue.Clear();
            Server = new NetManager(new LiteNetLibTransportServerEventListener(this, ConnectKey, _serverEventQueue, _serverPeers));
            Server.ChannelsCount = _serverDataChannelsCount;
            return Server.Start(port);
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default;
            if (Server == null)
                return false;
            Server.PollEvents();
            if (_serverEventQueue.Count == 0)
                return false;
            eventData = _serverEventQueue.Dequeue();
            return true;
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (IsServerStarted && _serverPeers.ContainsKey(connectionId) && _serverPeers[connectionId].ConnectionState == ConnectionState.Connected)
            {
                _serverPeers[connectionId].Send(writer, dataChannel, deliveryMethod);
                return true;
            }
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
            if (IsServerStarted && _serverPeers.ContainsKey(connectionId))
            {
                Server.DisconnectPeer(_serverPeers[connectionId]);
                _serverPeers.Remove(connectionId);
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

        public long GetClientRtt()
        {
            return Client.FirstPeer.RoundTripTime;
        }

        public long GetServerRtt(long connectionId)
        {
            return _serverPeers[connectionId].RoundTripTime;
        }
    }
}
