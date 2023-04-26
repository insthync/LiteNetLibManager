using NetCoreServer;
using System.Collections.Concurrent;
using System.Net;

namespace LiteNetLibManager
{
    public class WssTransportServer : WssServer
    {
        public ConcurrentDictionary<long, WssTransportSession> AcceptedClients { get; private set; }
        public ConcurrentQueue<TransportEventData> EventQueue { get; private set; }
        public int PeersCount { get { return AcceptedClients.Count; } }
        public int MaxConnections { get; private set; }

        private ITransportConnectionGenerator connectionGenerator;

        public WssTransportServer(ITransportConnectionGenerator connectionGenerator, SslContext context, IPAddress address, int port, int maxConnections) : base(context, address, port)
        {
            this.connectionGenerator = connectionGenerator;
            AcceptedClients = new ConcurrentDictionary<long, WssTransportSession>();
            EventQueue = new ConcurrentQueue<TransportEventData>();
            MaxConnections = maxConnections;
        }

        protected override SslSession CreateSession()
        {
            WssTransportSession newSession = new WssTransportSession(connectionGenerator.GetNewConnectionID(), this);
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
            WssTransportSession session;
            if (AcceptedClients.TryRemove(connectionId, out session))
            {
                session.Dispose();
                return true;
            }
            return false;
        }
    }
}
