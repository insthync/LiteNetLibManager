using Cysharp.Threading.Tasks;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace LiteNetLibManager
{
    public class WebSocketClient
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int SocketCreate_LnlM(string url);

        [DllImport("__Internal")]
        private static extern int GetSocketState_LnlM(int wsNativeInstance);

        [DllImport("__Internal")]
        private static extern int GetSocketEventType_LnlM(int wsNativeInstance);

        [DllImport("__Internal")]
        private static extern int GetSocketDataLength_LnlM(int wsNativeInstance);

        [DllImport("__Internal")]
        private static extern void GetSocketData_LnlM(int wsNativeInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        private static extern string GetSocketErrorMessage_LnlM(int wsNativeInstance);

        [DllImport("__Internal")]
        private static extern int GetSocketDisconnectCode_LnlM(int wsNativeInstance);

        [DllImport("__Internal")]
        private static extern string GetSocketDisconnectReason_LnlM(int wsNativeInstance);

        [DllImport("__Internal")]
        private static extern bool GetSocketDisconnectWasClean_LnlM(int wsNativeInstance);

        [DllImport("__Internal")]
        private static extern void SocketEventDequeue_LnlM(int wsNativeInstance);

        [DllImport("__Internal")]
        private static extern void SocketSend_LnlM(int wsNativeInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        private static extern void SocketClose_LnlM(int wsNativeInstance);

        private int _wsNativeInstance = 0;
#else
        private ClientWebSocket _socket = null;
        private CancellationTokenSource _tokenSource;
        private CancellationToken _cancellationToken;
#endif
        private readonly ConcurrentQueue<TransportEventData> _clientEventQueue;

        private readonly string _url;

        public WebSocketClient(string url)
        {
            _url = url;
            _clientEventQueue = new ConcurrentQueue<TransportEventData>();
        }

        public bool Connect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _wsNativeInstance = SocketCreate_LnlM(_url);
            return IsOpen;
#else
            _tokenSource = new CancellationTokenSource();
            _cancellationToken = _tokenSource.Token;
            try
            {
                _socket = new ClientWebSocket();
                _socket.ConnectAsync(new Uri(_url), _cancellationToken).GetAwaiter().GetResult();
                _socket_OnOpen();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSockerClient] Unable to connect to {_url}, {ex.Message}\n{ex.StackTrace}");
                _socket_OnError(ex.Message);
                _socket_OnClose(WebSocketCloseCode.AbnormalClosure, ex.Message, false);
                _tokenSource?.Cancel();
                _socket?.Dispose();
                _socket = null;
                return false;
            }
            Receive();
            return true;
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        public async void Receive()
        {
            await ReceiveTask();
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        public async UniTask ReceiveTask()
        {
            WebSocketCloseCode closeCode = WebSocketCloseCode.AbnormalClosure;
            await UniTask.SwitchToThreadPool();
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[8192]);
            try
            {
                while (_socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = null;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        do
                        {
                            result = await _socket.ReceiveAsync(buffer, _cancellationToken);
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                        while (!result.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            _socket_OnMessage(ms.ToArray());
                        }
                        else if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            _socket_OnMessage(ms.ToArray());
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
                Debug.LogError($"[WebSockerClient] Error occuring while proceed receiving, {ex.Message}\n{ex.StackTrace}");
                _socket_OnError(ex.Message);
                _tokenSource?.Cancel();
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                _socket_OnClose(closeCode, string.Empty, false);
            }
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private void _socket_OnMessage(byte[] rawData)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                reader = new NetDataReader(rawData),
            });
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private void _socket_OnOpen()
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
            });
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private void _socket_OnClose(WebSocketCloseCode code, string reason, bool wasClean)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                disconnectInfo = WebSocketUtils.GetDisconnectInfo((int)code, reason, wasClean),
            });
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private void _socket_OnError(string message)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                errorMessage = message,
            });
        }
#endif

        public void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SocketClose_LnlM(_wsNativeInstance);
#else
            _tokenSource?.Cancel();
            try
            {
                if (_socket != null && _socket.State == WebSocketState.Open)
                    _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocketClient] Error occuring while closing, {ex.Message}\n{ex.StackTrace}");
            }
#endif
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default;
#if UNITY_WEBGL && !UNITY_EDITOR
            int eventType = GetSocketEventType_LnlM(_wsNativeInstance);
            if (eventType < 0)
                return false;
            switch ((ENetworkEvent)eventType)
            {
                case ENetworkEvent.DataEvent:
                    eventData.type = ENetworkEvent.DataEvent;
                    eventData.reader = new NetDataReader(GetSocketData());
                    break;
                case ENetworkEvent.ConnectEvent:
                    eventData.type = ENetworkEvent.ConnectEvent;
                    break;
                case ENetworkEvent.DisconnectEvent:
                    eventData.type = ENetworkEvent.DisconnectEvent;
                    eventData.disconnectInfo = WebSocketUtils.GetDisconnectInfo(
                        GetSocketDisconnectCode_LnlM(_wsNativeInstance),
                        GetSocketDisconnectReason_LnlM(_wsNativeInstance),
                        GetSocketDisconnectWasClean_LnlM(_wsNativeInstance));
                    break;
                case ENetworkEvent.ErrorEvent:
                    eventData.type = ENetworkEvent.ErrorEvent;
                    eventData.errorMessage = GetSocketErrorMessage_LnlM(_wsNativeInstance);
                    break;
            }
            SocketEventDequeue_LnlM(_wsNativeInstance);
            return true;
#else
            if (_clientEventQueue.Count == 0)
                return false;
            return _clientEventQueue.TryDequeue(out eventData);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private byte[] GetSocketData()
        {
            int length = GetSocketDataLength_LnlM(_wsNativeInstance);
            if (length == 0)
                return null;
            byte[] buffer = new byte[length];
            GetSocketData_LnlM(_wsNativeInstance, buffer, length);
            return buffer;
        }
#endif

        public bool ClientSend(NetDataWriter writer)
        {
            var buffer = writer.CopyData();
            if (!IsOpen)
                return false;
#if UNITY_WEBGL && !UNITY_EDITOR
            SocketSend_LnlM(_wsNativeInstance, buffer, buffer.Length);
#else
            _socket?.SendAsync(buffer, WebSocketMessageType.Binary, true, _cancellationToken);
#endif
            return true;
        }

        public bool IsOpen
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return GetSocketState_LnlM(_wsNativeInstance) == 1;
#else
                return _socket != null && _socket.State == WebSocketState.Open;
#endif
            }
        }
    }
}