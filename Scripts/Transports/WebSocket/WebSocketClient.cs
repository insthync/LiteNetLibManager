using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;
using LiteNetLib;

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
        private WebSocketSharp.WebSocket _socket;
#endif
        private readonly Queue<TransportEventData> _clientEventQueue;

        private readonly string _url;

        public WebSocketClient(string url)
        {
            _url = url;
            _clientEventQueue = new Queue<TransportEventData>();
        }

        public void Connect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _wsNativeInstance = SocketCreate_LnlM(_url);
#else
            if (IsOpen)
                _socket?.Close();
            _socket = new WebSocketSharp.WebSocket(_url);
            _socket.OnMessage += _socket_OnMessage;
            _socket.OnOpen += _socket_OnOpen;
            _socket.OnClose += _socket_OnClose;
            _socket.OnError += _socket_OnError;
            _socket.ConnectAsync();
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private void _socket_OnMessage(object sender, WebSocketSharp.MessageEventArgs e)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DataEvent,
                reader = new LiteNetLib.Utils.NetDataReader(e.RawData),
            });
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private void _socket_OnOpen(object sender, EventArgs e)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ConnectEvent,
            });
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private void _socket_OnClose(object sender, WebSocketSharp.CloseEventArgs e)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.DisconnectEvent,
                disconnectInfo = GetDisconnectInfo(e.Code, e.Reason, e.WasClean),
            });
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private void _socket_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            _clientEventQueue.Enqueue(new TransportEventData()
            {
                type = ENetworkEvent.ErrorEvent,
                errorMessage = e.Message,
            });
        }
#endif

        public void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SocketClose_LnlM(_wsNativeInstance);
#else
            _socket?.CloseAsync();
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
        /// <param name="errorCode"></param>
        /// <returns></returns>
        public DisconnectInfo GetDisconnectInfo(int code, string reason, bool wasClean)
        {
            // TODO: Implement this
            return new DisconnectInfo()
            {

            };
        }

        public bool ClientSend(byte[] buffer)
        {
            if (!IsOpen)
                return false;
#if UNITY_WEBGL && !UNITY_EDITOR
            SocketSend_LnlM(_wsNativeInstance, buffer, buffer.Length);
#else
            _socket?.Send(buffer);
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
                return _socket != null && _socket.ReadyState == WebSocketSharp.WebSocketState.Open;
#endif
            }
        }
    }
}