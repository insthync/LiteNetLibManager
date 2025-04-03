#if !UNITY_WEBGL || UNITY_EDITOR
using Cysharp.Threading.Tasks;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LiteNetLibManager
{
    public class WebSocketServer
    {
        private string[] _prefixes;
        private HttpListener _listener = null;
        private readonly ConcurrentQueue<TransportEventData> _eventQueue;
        private readonly ConcurrentDictionary<long, WebSocket> _peers = new ConcurrentDictionary<long, WebSocket>();
        private long _connectionIdOffsets = 1000000;
        private long _nextConnectionId = 1;

        public bool IsRunning { get; private set; } = false;
        public int PeersCount => _peers.Count;

        public WebSocketServer(string[] prefixes, ConcurrentQueue<TransportEventData> eventQueue)
        {
            _prefixes = prefixes;
            _eventQueue = eventQueue;
        }

        public bool StartServer()
        {
            try
            {
                _listener = new HttpListener();
                string prefixesStr = string.Empty;
                for (int i = 0; i < _prefixes.Length; ++i)
                {
                    string prefix = _prefixes[i];
                    if (!string.IsNullOrEmpty(prefixesStr))
                        prefixesStr += ", ";
                    prefixesStr += prefix;
                    Debug.Log($"[WebSocketServer] Adding prefix: {prefix}");
                    _listener.Prefixes.Add(prefix);
                }
                _listener.Start();
                Debug.Log($"[WebSocketServer] Started on {prefixesStr}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocketServer] Unable to start server: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            Listen();
            return true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public async void Listen()
        {
            await ListenTask();
        }

        public async UniTask ListenTask()
        {
            IsRunning = true;

            while (IsRunning)
            {
                try
                {
                    Debug.LogError("Listening");
                    HttpListenerContext context = await _listener.GetContextAsync();

                    if (!IsRunning)
                        break; // Stop accepting if shutting down

                    _ = Task.Run(() => HandleConnection(context)); // Handle client in a separate task
                }
                catch (HttpListenerException)
                {
                    if (!IsRunning)
                        break; // Ignore errors if shutting down
                }
            }

            _listener?.Stop();
            _listener?.Close();
            _nextConnectionId = 1;
        }

        private async UniTask HandleConnection(HttpListenerContext context)
        {
            if (!context.Request.IsWebSocketRequest)
            {
                Debug.LogError("Not ws");
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }
            HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
            await ReceiveTask(context, wsContext);
        }

        private async UniTask ReceiveTask(HttpListenerContext context, HttpListenerWebSocketContext wsContext)
        {
            WebSocket socket = wsContext.WebSocket;
            long connectionId = GetNewConnectionID();
            _socket_OnOpen(context, wsContext, connectionId);
            WebSocketCloseCode closeCode = WebSocketCloseCode.AbnormalClosure;
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[8192]);
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = null;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        do
                        {
                            result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                        while (!result.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            _socket_OnMessage(context, wsContext, connectionId, ms.ToArray());
                        }
                        else if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            _socket_OnMessage(context, wsContext, connectionId, ms.ToArray());
                        }
                        else if (result.MessageType == WebSocketMessageType.Close)
                        {
                            closeCode = (WebSocketCloseCode)result.CloseStatus;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _socket_OnError(context, wsContext, ex.Message);
                // Cancellation
            }
            finally
            {
                _socket_OnClose(context, wsContext, connectionId, closeCode, string.Empty, false);
            }
        }

        private void _socket_OnMessage(HttpListenerContext context, HttpListenerWebSocketContext wsContext, long connectionId, byte[] rawData)
        {
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                connectionId = connectionId,
                reader = new NetDataReader(rawData),
            });
        }

        private void _socket_OnOpen(HttpListenerContext context, HttpListenerWebSocketContext wsContext, long connectionId)
        {
            if (_peers != null)
                _peers[connectionId] = wsContext.WebSocket;
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
                connectionId = connectionId,
            });
        }

        private void _socket_OnClose(HttpListenerContext context, HttpListenerWebSocketContext wsContext, long connectionId, WebSocketCloseCode code, string reason, bool wasClean)
        {
            if (_peers != null)
                _peers.TryRemove(connectionId, out _);
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                connectionId = connectionId,
                disconnectInfo = WebSocketUtils.GetDisconnectInfo((int)code, reason, wasClean),
            });
        }

        private void _socket_OnError(HttpListenerContext context, HttpListenerWebSocketContext wsContext, string message)
        {
            _eventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                endPoint = context.Request.RemoteEndPoint,
                errorMessage = message,
            });
        }

        public long GetNewConnectionID()
        {
            return _connectionIdOffsets + Interlocked.Increment(ref _nextConnectionId);
        }

        public bool Send(long connectionId, byte[] buffer)
        {
            if (!_peers.TryGetValue(connectionId, out WebSocket ws) || ws.State != WebSocketState.Open)
                return false;
            ws.SendAsync(buffer, WebSocketMessageType.Binary, true, CancellationToken.None);
            return true;
        }

        public bool Disconnect(long connectionId)
        {
            if (!_peers.TryRemove(connectionId, out WebSocket ws))
                return false;
            ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).GetAwaiter().GetResult();
            return true;
        }
    }
}
#endif