using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public sealed class OfflineTransport : ITransport
    {
        // Connection Id always 0 (first client id)

        private readonly Queue<TransportEventData> clientData = new Queue<TransportEventData>();
        private readonly Queue<TransportEventData> serverData = new Queue<TransportEventData>();
        public int ServerPeersCount { get; private set; }
        public int ServerMaxConnections { get { return 1; } }
        public bool IsClientStarted { get; private set; }
        public bool IsServerStarted { get; private set; }

        public bool StartClient(string address, int port)
        {
            clientData.Clear();
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.ConnectEvent;
            clientData.Enqueue(data);
            IsClientStarted = true;
            return true;
        }

        public void StopClient()
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DisconnectEvent;
            clientData.Enqueue(data);
            IsClientStarted = false;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (serverData.Count == 0)
                return false;
            eventData = serverData.Dequeue(); 
            return true;
        }

        public bool ClientSend(DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DataEvent;
            data.reader = new NetDataReader(writer.CopyData());
            clientData.Enqueue(data);
            return true;
        }

        public bool StartServer(int port, int maxConnections)
        {
            serverData.Clear();
            ServerPeersCount = 0;
            IsServerStarted = true;
            return true;
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (clientData.Count == 0)
                return false;
            eventData = clientData.Dequeue();
            switch (eventData.type)
            {
                case ENetworkEvent.ConnectEvent:
                    TransportEventData data = new TransportEventData();
                    data.type = ENetworkEvent.ConnectEvent;
                    serverData.Enqueue(data);
                    ServerPeersCount++;
                    break;
                case ENetworkEvent.DisconnectEvent:
                    ServerPeersCount--;
                    IsClientStarted = false;
                    break;
            }
            return true;
        }

        public bool ServerSend(long connectionId, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DataEvent;
            data.reader = new NetDataReader(writer.CopyData());
            serverData.Enqueue(data);
            return true;
        }

        public bool ServerDisconnect(long connectionId)
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DisconnectEvent;
            serverData.Enqueue(data);
            return false;
        }

        public void StopServer()
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DisconnectEvent;
            serverData.Enqueue(data);
            IsServerStarted = false;
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }
    }
}
