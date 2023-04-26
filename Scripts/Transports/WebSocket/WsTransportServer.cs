using NetCoreServer;
using System.Collections.Concurrent;
using System.Net;

namespace LiteNetLibManager
{
    public class WsTransportServer : WsServer
    {
        public ConcurrentDictionary<long, WsTransportSession> AcceptedClients { get; private set; }
        public ConcurrentQueue<TransportEventData> EventQueue { get; private set; }
        public int PeersCount { get { return AcceptedClients.Count; } }
        public int MaxConnections { get; private set; }

        private ITransportConnectionGenerator connectionGenerator;

        public WsTransportServer(ITransportConnectionGenerator connectionGenerator, IPAddress address, int port, int maxConnections) : base(address, port)
        {
            this.connectionGenerator = connectionGenerator;
            AcceptedClients = new ConcurrentDictionary<long, WsTransportSession>();
            EventQueue = new ConcurrentQueue<TransportEventData>();
            MaxConnections = maxConnections;
        }

        protected override TcpSession CreateSession()
        {
            WsTransportSession newSession = new WsTransportSession(connectionGenerator.GetNewConnectionID(), this);
            if (PeersCount >= MaxConnections)
            {
                newSession.Disconnect();
                return null;
            }
            AcceptedClients.TryAdd(newSession.ConnectionId, newSession);
            return newSession;
        }

        internal bool SendAsync(long connectionId, byte[] buffer)
        {
            return AcceptedClients.ContainsKey(connectionId) && AcceptedClients[connectionId].SendBinaryAsync(buffer, 0, buffer.Length);
        }

        internal bool Disconnect(long connectionId)
        {
            WsTransportSession session;
            if (AcceptedClients.TryRemove(connectionId, out session))
            {
                session.Dispose();
                return true;
            }
            return false;
        }
    }
}
