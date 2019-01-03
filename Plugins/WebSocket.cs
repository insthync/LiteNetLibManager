using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;

public class WebSocket
{
    private Uri mUrl;

    public WebSocket(Uri url)
    {
        mUrl = url;

        string protocol = mUrl.Scheme;
        if (!protocol.Equals("ws") && !protocol.Equals("wss"))
            throw new ArgumentException("Unsupported protocol: " + protocol);
    }

    public void SendString(string str)
    {
        Send(Encoding.UTF8.GetBytes(str));
    }

    public string RecvString()
    {
        byte[] retval = Recv();
        if (retval == null)
            return null;
        return Encoding.UTF8.GetString(retval);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
	[DllImport("__Internal")]
	private static extern int SocketCreate(string url);

	[DllImport("__Internal")]
	private static extern int SocketState(int socketInstance);

	[DllImport("__Internal")]
	private static extern void SocketSend(int socketInstance, byte[] ptr, int length);

	[DllImport("__Internal")]
	private static extern void SocketRecv(int socketInstance, byte[] ptr, int length);

	[DllImport("__Internal")]
	private static extern int SocketRecvLength(int socketInstance);

	[DllImport("__Internal")]
	private static extern void SocketClose(int socketInstance);

	[DllImport("__Internal")]
	private static extern int SocketError(int socketInstance, byte[] ptr, int length);

	private int m_NativeRef = 0;
#else
    private WebSocketSharp.WebSocket m_Socket;
    private Queue<byte[]> m_Messages = new Queue<byte[]>();
    private string m_Error = null;
#endif

    public void Connect()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
		m_NativeRef = SocketCreate(mUrl.ToString());
#else
        m_Socket = new WebSocketSharp.WebSocket(mUrl.ToString());
        m_Socket.OnMessage += (sender, e) => m_Messages.Enqueue(e.RawData);
        m_Socket.OnError += (sender, e) => m_Error = e.Message;
        m_Socket.ConnectAsync();
#endif
    }

    public void Close()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SocketClose(m_NativeRef);
#else
        m_Socket.Close();
#endif
    }

    public void Send(byte[] buffer)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SocketSend(m_NativeRef, buffer, buffer.Length);
#else
        m_Socket.Send(buffer);
#endif
    }

    public byte[] Recv()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        int length = SocketRecvLength(m_NativeRef);
        if (length == 0)
            return null;
        byte[] buffer = new byte[length];
        SocketRecv(m_NativeRef, buffer, length);
        return buffer;
#else
        if (m_Messages.Count == 0)
            return null;
        return m_Messages.Dequeue();
#endif
    }

    public bool IsConnected
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SocketState(m_NativeRef) == 1;
#else
            return m_Socket.ReadyState == WebSocketSharp.WebSocketState.Open;
#endif
        }
    }

    public string Error
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
			const int bufsize = 1024;
			byte[] buffer = new byte[bufsize];
			int result = SocketError(m_NativeRef, buffer, bufsize);

			if (result == 0)
				return null;

			return Encoding.UTF8.GetString (buffer);
#else
            return m_Error;
#endif
        }
    }
}