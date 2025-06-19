using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public sealed class OfflineTransport : ITransport
    {
        // Connection Id always 0 (first client id)
        private readonly Queue<TransportEventData> _clientData = new Queue<TransportEventData>();
        private readonly Queue<TransportEventData> _serverData = new Queue<TransportEventData>();

        public int ServerPeersCount { get; private set; }
        public int ServerMaxConnections { get { return 1; } }
        public bool IsClientStarted { get; private set; }
        public bool IsServerStarted { get; private set; }
        public bool HasImplementedPing => true;
        public bool IsReliableOnly => false;

        public bool StartClient(string address, int port)
        {
            _clientData.Clear();
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.ConnectEvent;
            _clientData.Enqueue(data);
            IsClientStarted = true;
            return true;
        }

        public void StopClient()
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DisconnectEvent;
            _clientData.Enqueue(data);
            IsClientStarted = false;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (_serverData.Count == 0)
                return false;
            eventData = _serverData.Dequeue(); 
            return true;
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DataEvent;
            data.reader = new NetDataReader(writer.CopyData());
            _clientData.Enqueue(data);
            return true;
        }

        public bool StartServer(int port, int maxConnections)
        {
            _serverData.Clear();
            ServerPeersCount = 0;
            IsServerStarted = true;
            return true;
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (_clientData.Count == 0)
                return false;
            eventData = _clientData.Dequeue();
            switch (eventData.type)
            {
                case ENetworkEvent.ConnectEvent:
                    TransportEventData data = new TransportEventData();
                    data.type = ENetworkEvent.ConnectEvent;
                    _serverData.Enqueue(data);
                    ServerPeersCount++;
                    break;
                case ENetworkEvent.DisconnectEvent:
                    ServerPeersCount--;
                    IsClientStarted = false;
                    break;
            }
            return true;
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DataEvent;
            data.reader = new NetDataReader(writer.CopyData());
            _serverData.Enqueue(data);
            return true;
        }

        public bool ServerDisconnect(long connectionId)
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DisconnectEvent;
            _serverData.Enqueue(data);
            return false;
        }

        public void StopServer()
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DisconnectEvent;
            _serverData.Enqueue(data);
            IsServerStarted = false;
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }

        public long GetClientRtt()
        {
            return 0;
        }

        public long GetServerRtt(long connectionId)
        {
            return 0;
        }
    }
}
