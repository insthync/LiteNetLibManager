using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public sealed class MixTransport : ITransport
    {
        private bool _useWebSocketForClient;
        private LiteNetLibTransport _lnlTransport;
        private WebSocketTransport _wsTransport;
        private int _webSocketPortOffset;

        public MixTransport(bool useWebSocketForClient, string connectKey, byte clientDataChannelsCount, byte serverDataChannelsCount,
            int webSocketPortOffset, bool secure, string certificateFilePath, string certificatePassword, string certificateBase64String)
        {
            _useWebSocketForClient = useWebSocketForClient;
            _lnlTransport = new LiteNetLibTransport(connectKey, clientDataChannelsCount, serverDataChannelsCount);
            _wsTransport = new WebSocketTransport(secure, certificateFilePath, certificatePassword, certificateBase64String);
            _webSocketPortOffset = webSocketPortOffset;
        }

        public int ServerPeersCount => _lnlTransport.ServerPeersCount + _wsTransport.ServerPeersCount;
        public int ServerMaxConnections => _lnlTransport.ServerMaxConnections + _wsTransport.ServerMaxConnections;
        public bool IsClientStarted => !_useWebSocketForClient ? _lnlTransport.IsClientStarted : _wsTransport.IsClientStarted;
        public bool IsServerStarted => _lnlTransport.IsServerStarted || _wsTransport.IsServerStarted;
        public bool HasImplementedPing => false;
        public bool IsReliableOnly => true;

        public long GetClientRtt()
        {
            if (!_useWebSocketForClient)
                return _lnlTransport.GetClientRtt();
            else
                return _wsTransport.GetClientRtt();
        }

        public long GetServerRtt(long connectionId)
        {
            if (!_useWebSocketForClient)
                return _lnlTransport.GetServerRtt(connectionId);
            else
                return _wsTransport.GetServerRtt(connectionId);
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            if (!_useWebSocketForClient)
                return _lnlTransport.ClientReceive(out eventData);
            else
                return _wsTransport.ClientReceive(out eventData);
        }

        public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (!_useWebSocketForClient)
                return _lnlTransport.ClientSend(dataChannel, deliveryMethod, writer);
            else
                return _wsTransport.ClientSend(dataChannel, deliveryMethod, writer);
        }

        public void Destroy()
        {
            _lnlTransport.Destroy();
            _wsTransport.Destroy();
        }

        public bool ServerDisconnect(long connectionId)
        {
            if (_lnlTransport.ServerDisconnect(connectionId))
                return true;
            if (_wsTransport.ServerDisconnect(connectionId))
                return true;
            return false;
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            if (_lnlTransport.ServerReceive(out eventData))
                return true;
            if (_wsTransport.ServerReceive(out eventData))
                return true;
            return false;
        }

        public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (_lnlTransport.ServerSend(connectionId, dataChannel, deliveryMethod, writer))
                return true;
            if (_wsTransport.ServerSend(connectionId, dataChannel, deliveryMethod, writer))
                return true;
            return false;
        }

        public bool StartClient(string address, int port)
        {
            if (!_useWebSocketForClient)
                return _lnlTransport.StartClient(address, port);
            else
                return _wsTransport.StartClient(address, port + _webSocketPortOffset);
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (_lnlTransport.StartServer(port, maxConnections / 2) &&
                _wsTransport.StartServer(port + _webSocketPortOffset, maxConnections / 2))
                return true;
            return false;
        }

        public void StopClient()
        {
            if (!_useWebSocketForClient)
                _lnlTransport.StopClient();
            else
                _wsTransport.StopClient();
        }

        public void StopServer()
        {
            _lnlTransport.StopServer();
            _wsTransport.StopServer();
        }
    }
}
