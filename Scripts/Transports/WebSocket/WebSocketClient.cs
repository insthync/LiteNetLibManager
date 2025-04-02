using Cysharp.Threading.Tasks;
using LiteNetLib;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net.WebSockets;
using UnityEngine;

namespace LiteNetLibManager
{
    public class WebSocketClient
    {
        /// <summary>
        /// https://developer.mozilla.org/en-US/docs/Web/API/CloseEvent/code
        /// </summary>
        public enum WebSocketCloseCode : ushort
        {
            NormalClosure = 1000,
            EndpointUnavailable = 1001,
            ProtocolError = 1002,
            InvalidMessageType = 1003,
            Empty = 1005,
            AbnormalClosure = 1006,
            InvalidPayloadData = 1007,
            PolicyViolation = 1008,
            MessageTooBig = 1009,
            MandatoryExtension = 1010,
            InternalServerError = 1011,
            ServiceRestart = 1012,
            TryAgainLater = 1013,
            BadGateway = 1014,
            TlsHandshakeFailure = 1015
        }

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

        public void Connect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _wsNativeInstance = SocketCreate_LnlM(_url);
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
                _socket_OnError(ex.Message);
                _socket_OnClose(WebSocketCloseCode.AbnormalClosure, ex.Message, false);
                _tokenSource?.Cancel();
                _socket?.Dispose();
            }
            Receive();
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
            catch (Exception)
            {
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
                reader = new LiteNetLib.Utils.NetDataReader(rawData),
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
                disconnectInfo = GetDisconnectInfo((int)code, reason, wasClean),
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
            if (_socket != null)
                _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, _cancellationToken).GetAwaiter().GetResult();
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
                    eventData.reader = new LiteNetLib.Utils.NetDataReader(GetSocketData());
                    break;
                case ENetworkEvent.ConnectEvent:
                    eventData.type = ENetworkEvent.ConnectEvent;
                    break;
                case ENetworkEvent.DisconnectEvent:
                    eventData.type = ENetworkEvent.DisconnectEvent;
                    eventData.disconnectInfo = GetDisconnectInfo(
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

        /// <summary>
        /// https://developer.mozilla.org/en-US/docs/Web/API/CloseEvent/
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        /// <param name="wasClean"></param>
        /// <returns></returns>
        public DisconnectInfo GetDisconnectInfo(int code, string reason, bool wasClean)
        {
            // TODO: Implement this
            WebSocketCloseCode castedCode = (WebSocketCloseCode)code;
            DisconnectReason disconnectReason = DisconnectReason.ConnectionFailed;
            SocketError socketErrorCode = SocketError.ConnectionReset;
            if (castedCode == WebSocketCloseCode.NormalClosure)
                socketErrorCode = SocketError.Success;
            return new DisconnectInfo()
            {
                Reason = disconnectReason,
                SocketErrorCode = socketErrorCode,
            };
        }

        public bool ClientSend(byte[] buffer)
        {
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