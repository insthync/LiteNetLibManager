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
        private int peerCount;

        public bool IsClientStarted()
        {
            return true;
        }

        public bool StartClient(string address, int port)
        {
            clientData.Clear();
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.ConnectEvent;
            clientData.Enqueue(data);
            return true;
        }

        public void StopClient()
        {
            TransportEventData data = new TransportEventData();
            data.type = ENetworkEvent.DisconnectEvent;
            clientData.Enqueue(data);
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

        public bool IsServerStarted()
        {
            return true;
        }

        public bool StartServer(int port, int maxConnections)
        {
            serverData.Clear();
            peerCount = 0;
            return true;
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (clientData.Count == 0)
                return false;
            eventData = clientData.Dequeue();
            if (eventData.type == ENetworkEvent.ConnectEvent)
            {
                TransportEventData data = new TransportEventData();
                data.type = ENetworkEvent.ConnectEvent;
                serverData.Enqueue(data);
                peerCount++;
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
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }

        public int GetServerPeersCount()
        {
            return peerCount;
        }
    }
}
