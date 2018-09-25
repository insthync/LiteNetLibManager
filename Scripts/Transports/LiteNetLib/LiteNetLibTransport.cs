using System.Collections;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class LiteNetLibTransport : ITransport
    {
        private NetManager client;
        private NetManager server;
        private readonly Dictionary<long, NetPeer> serverPeers = new Dictionary<long, NetPeer>();
        private readonly Queue<LiteNetLibTransportEventData> clientEventQueue = new Queue<LiteNetLibTransportEventData>();
        private readonly Queue<LiteNetLibTransportEventData> serverEventQueue = new Queue<LiteNetLibTransportEventData>();

        public bool IsClientConnected()
        {
            return client != null && client.GetFirstPeer() != null && client.GetFirstPeer().ConnectionState == ConnectionState.Connected;
        }

        public void ClientConnect(string connectKey, string address, int port)
        {
            clientEventQueue.Clear();
            client = new NetManager(new LiteNetLibTransportEventListener(clientEventQueue), connectKey);
            client.Start();
            client.Connect(address, port);
        }

        public void ClientDisconnect()
        {
            if (client != null)
                client.Stop();
            client = null;
        }

        public ENetworkEvent ClientReceive(out NetDataReader reader, out DisconnectInfo disconnectInfo)
        {
            reader = null;
            disconnectInfo = default(DisconnectInfo);
            if (!IsClientConnected())
                return ENetworkEvent.Nothing;
            client.PollEvents();
            var eventData = clientEventQueue.Dequeue();
            reader = eventData.reader;
            disconnectInfo = eventData.disconnectInfo;
            return eventData.type;
        }

        public bool ClientSend(SendOptions sendOptions, NetDataWriter writer)
        {
            if (IsClientConnected())
            {
                client.GetFirstPeer().Send(writer, sendOptions);
                return true;
            }
            return false;
        }

        public bool IsServerActive()
        {
            return server != null;
        }

        public void ServerStart(string connectKey, int port, int maxConnections)
        {
            serverPeers.Clear();
            serverEventQueue.Clear();
            server = new NetManager(new LiteNetLibTransportEventListener(serverEventQueue), maxConnections, connectKey);
            server.Start(port);
        }

        public ENetworkEvent ServerReceive(out long connectionId, out NetDataReader reader, out DisconnectInfo disconnectInfo)
        {
            connectionId = 0;
            reader = null;
            disconnectInfo = default(DisconnectInfo);
            if (!IsServerActive())
                return ENetworkEvent.Nothing;
            server.PollEvents();
            var eventData = serverEventQueue.Dequeue();
            connectionId = eventData.connectionId;
            reader = eventData.reader;
            disconnectInfo = eventData.disconnectInfo;
            return eventData.type;
        }

        public bool ServerSend(long connectionId, SendOptions sendOptions, NetDataWriter writer)
        {
            if (IsServerActive() && serverPeers.ContainsKey(connectionId))
            {
                serverPeers[connectionId].Send(writer, sendOptions);
                return true;
            }
            return false;
        }

        public bool ServerDisconnect(long connectionId)
        {
            if (IsServerActive() && serverPeers.ContainsKey(connectionId))
            {
                server.DisconnectPeer(serverPeers[connectionId]);
                return true;
            }
            return false;
        }

        public void ServerStop()
        {
            if (server != null)
                server.Stop();
            server = null;
        }

        public void Destroy()
        {
            ClientDisconnect();
            ServerStop();
        }
    }
}
