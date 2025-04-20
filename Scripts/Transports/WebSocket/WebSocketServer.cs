using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using UnityEngine;

namespace LiteNetLibManager
{
    public class WebSocketServer
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        private Fleck.WebSocketServer _listener = null;
        private readonly string _location;
        private readonly X509Certificate2 _cert;
        private readonly ConcurrentQueue<TransportEventData> _eventQueue;
        private readonly ConcurrentDictionary<long, Fleck.IWebSocketConnection> _peers = new ConcurrentDictionary<long, Fleck.IWebSocketConnection>();
        private long _connectionIdOffsets = 1000000;
        private long _nextConnectionId = 1;
#endif

        public bool IsRunning
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                return _listener != null;
#else
                return false;
#endif
            }
        }
        public int PeersCount
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                return _peers.Count;
#else
                return 0;
#endif
            }
        }

        public WebSocketServer(string location, X509Certificate2 cert, ConcurrentQueue<TransportEventData> eventQueue)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            _location = location;
            _cert = cert;
            _eventQueue = eventQueue;
#endif
        }

        public bool StartServer()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            try
            {
                _listener = new Fleck.WebSocketServer(_location);
                _listener.Certificate = _cert;
                _listener.Start(OnClientConnected);
                _nextConnectionId = 1;
                Debug.Log($"[WebSocketServer] Started on {_location}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocketServer] Unable to start server: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            return true;
#else
            return false;
#endif
        }

        public void Stop()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            _listener?.Dispose();
            _listener = null;
#endif
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        private void OnClientConnected(Fleck.IWebSocketConnection conn)
        {
            long connectionId = GetNewConnectionID();
            conn.OnOpen = () => _socket_OnOpen(conn, connectionId);
            conn.OnBinary = (data) => _socket_OnMessage(conn, connectionId, data.ToArray());
            conn.OnClose = () => _socket_OnClose(conn, connectionId, WebSocketCloseCode.NormalClosure, string.Empty, true);
            conn.OnError = (ex) => _socket_OnError(conn, ex.Message);
        }

        private void _socket_OnMessage(Fleck.IWebSocketConnection conn, long connectionId, byte[] rawData)
        {
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                connectionId = connectionId,
                reader = new NetDataReader(rawData),
            });
        }

        private void _socket_OnOpen(Fleck.IWebSocketConnection conn, long connectionId)
        {
            _peers[connectionId] = conn;
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = connectionId,
            });
        }

        private void _socket_OnClose(Fleck.IWebSocketConnection conn, long connectionId, WebSocketCloseCode code, string reason, bool wasClean)
        {
            _peers.TryRemove(connectionId, out _);
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = connectionId,
                disconnectInfo = WebSocketUtils.GetDisconnectInfo((int)code, reason, wasClean),
            });
        }

        private void _socket_OnError(Fleck.IWebSocketConnection conn, string message)
        {
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                endPoint = new IPEndPoint(conn.ConnectionInfo.ClientIpAddress, conn.ConnectionInfo.ClientPort),
                errorMessage = message,
            });
        }

        public long GetNewConnectionID()
        {
            return _connectionIdOffsets + Interlocked.Increment(ref _nextConnectionId);
        }
#endif

        public bool Send(long connectionId, NetDataWriter writer)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!_peers.TryGetValue(connectionId, out var ws) || !ws.IsAvailable)
                return false;
            var msgBuffer = new Fleck.MemoryBuffer(writer.Length);
            Buffer.BlockCopy(writer.Data, 0, msgBuffer.Data, 0, writer.Length);
            ws.Send(msgBuffer);
            return true;
#else
            return false;
#endif
        }

        public bool Disconnect(long connectionId)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!_peers.TryRemove(connectionId, out var ws))
                return false;
            ws.Close();
            return true;
#else
            return false;
#endif
        }
    }
}
