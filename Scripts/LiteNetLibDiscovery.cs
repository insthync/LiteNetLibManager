using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibDiscovery : MonoBehaviour
    {
        public float broadcastDelay = 3f;
        public int broadcastPort = 19050;
        public ushort broadcastKey = 1;
        [Tooltip("Will send this manager's address and port to discovery clients")]
        public LiteNetLibManager manager;
        [Tooltip("Data which will send from discovery server to clients")]
        public string data;

        public bool IsServer { get { return _server != null; } }
        public bool IsClient { get { return _client != null; } }
        private EventBasedNetListener _serverListener;
        private EventBasedNetListener _clientListener;
        private NetManager _server;
        private NetManager _client;
        private NetDataWriter _serverWriter;
        private NetDataWriter _clientWriter;
        private float _broadcastElapsed;

        public delegate void ReceiveBroadcastDelegate(System.Net.IPEndPoint remoteEndPoint, string data);
        public ReceiveBroadcastDelegate onReceivedBroadcast;

        private void Awake()
        {
            _serverWriter = new NetDataWriter();
            _clientWriter = new NetDataWriter();

            _serverListener = new EventBasedNetListener();
            _serverListener.ConnectionRequestEvent += _serverListener_ConnectionRequestEvent;
            _serverListener.NetworkErrorEvent += _serverListener_NetworkErrorEvent;
            _serverListener.NetworkLatencyUpdateEvent += _serverListener_NetworkLatencyUpdateEvent;
            _serverListener.NetworkReceiveEvent += _serverListener_NetworkReceiveEvent;
            _serverListener.NetworkReceiveUnconnectedEvent += _serverListener_NetworkReceiveUnconnectedEvent;
            _serverListener.PeerConnectedEvent += _serverListener_PeerConnectedEvent;
            _serverListener.PeerDisconnectedEvent += _serverListener_PeerDisconnectedEvent;

            _clientListener = new EventBasedNetListener();
            _clientListener.ConnectionRequestEvent += _clientListener_ConnectionRequestEvent;
            _clientListener.NetworkErrorEvent += _clientListener_NetworkErrorEvent;
            _clientListener.NetworkLatencyUpdateEvent += _clientListener_NetworkLatencyUpdateEvent;
            _clientListener.NetworkReceiveEvent += _clientListener_NetworkReceiveEvent;
            _clientListener.NetworkReceiveUnconnectedEvent += _clientListener_NetworkReceiveUnconnectedEvent;
            _clientListener.PeerConnectedEvent += _clientListener_PeerConnectedEvent;
            _clientListener.PeerDisconnectedEvent += _clientListener_PeerDisconnectedEvent;
        }

        public bool StartServer()
        {
            _server = new NetManager(_serverListener);
            _server.BroadcastReceiveEnabled = true;
            if (!_server.Start(broadcastPort))
            {
                _server = null;
                Logging.LogError("Cannot start discovery server");
                return false;
            }
            return true;
        }

        public bool StartClient()
        {
            _client = new NetManager(_clientListener);
            _client.UnconnectedMessagesEnabled = true;
            if (!_client.Start())
            {
                _client = null;
                Logging.LogError("Cannot start discovery client");
                return false;
            }

            return true;
        }

        public void StopServer()
        {
            if (!IsServer)
                return;
            _server.Stop();
            _server = null;
        }

        public void StopClient()
        {
            if (!IsClient)
                return;
            _client.Stop();
            _client = null;
        }

        private void Update()
        {
            if (IsClient)
            {
                _client.PollEvents();
                _broadcastElapsed -= Time.deltaTime;
                if (_broadcastElapsed <= 0f)
                {
                    _clientWriter.Reset();
                    _clientWriter.Put(broadcastKey);
                    _client.SendBroadcast(_clientWriter, broadcastPort);
                    _broadcastElapsed = broadcastDelay;
                }
            }

            if (IsServer)
            {
                _server.PollEvents();
            }
        }

        private void _clientListener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {

        }

        private void _clientListener_PeerConnectedEvent(NetPeer peer)
        {

        }

        private void _clientListener_NetworkReceiveUnconnectedEvent(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            // Receive server data
            if (reader.GetUShort() == broadcastKey)
            {
                if (onReceivedBroadcast != null)
                    onReceivedBroadcast.Invoke(remoteEndPoint, reader.GetString());
            }
        }

        private void _clientListener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {

        }

        private void _clientListener_NetworkLatencyUpdateEvent(NetPeer peer, int latency)
        {

        }

        private void _clientListener_NetworkErrorEvent(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {

        }

        private void _clientListener_ConnectionRequestEvent(ConnectionRequest request)
        {

        }

        private void _serverListener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {

        }

        private void _serverListener_PeerConnectedEvent(NetPeer peer)
        {

        }

        private void _serverListener_NetworkReceiveUnconnectedEvent(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            // Send back server data
            if (reader.GetUShort() == broadcastKey)
            {
                _serverWriter.Reset();
                _serverWriter.Put(broadcastKey);
                _serverWriter.Put(data);
                _server.SendUnconnectedMessage(_serverWriter, remoteEndPoint);
            }
        }

        private void _serverListener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {

        }

        private void _serverListener_NetworkLatencyUpdateEvent(NetPeer peer, int latency)
        {

        }

        private void _serverListener_NetworkErrorEvent(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {

        }

        private void _serverListener_ConnectionRequestEvent(ConnectionRequest request)
        {

        }
    }
}
