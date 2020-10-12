using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace LiteNetLibManager
{
    public sealed class TcpTransport : ITransport
    {
        public class Client
        {
            public const int MAX_DATA_SIZE = 16 * 1024;
            public TcpClient TcpClient { get; set; }
            private byte[] readBuffer;
            private byte[] writerBuffer;

            public Client(TcpClient tcpClient)
            {
                TcpClient = tcpClient;
                readBuffer = new byte[MAX_DATA_SIZE];
                writerBuffer = new byte[4 + MAX_DATA_SIZE];
                TcpClient.NoDelay = true;
            }

            public void Dispose()
            {
                TcpClient.Close();
                TcpClient.Dispose();
            }

            public bool Connected { get { return TcpClient.Connected; } }

            private bool ReadBytes(NetworkStream networkStream, int expectLength)
            {
                if (!networkStream.DataAvailable)
                    return false;
                int bytesRead = 0;
                while (bytesRead < expectLength)
                {
                    int remaining = expectLength - bytesRead;
                    try
                    {
                        int readLength = networkStream.Read(readBuffer, bytesRead, remaining);
                        bytesRead += readLength;
                    }
                    catch
                    {
                        return false;
                    }
                }
                return true;
            }

            public NetDataReader ReadData()
            {
                if (!Connected)
                    return null;
                if (!ReadBytes(TcpClient.GetStream(), 4))
                    return null;
                int length = (readBuffer[0] << 24) |
                    (readBuffer[1] << 16) |
                    (readBuffer[2] << 8) |
                    readBuffer[3];
                if (length >= MAX_DATA_SIZE)
                {
                    Debug.LogError("Cannot read data, its size is too high");
                    return null;
                }
                if (ReadBytes(TcpClient.GetStream(), length))
                    return new NetDataReader(readBuffer, 0, length);
                return null;
            }

            public void WriteData(NetDataWriter writer)
            {
                int length = writer.Data.Length;
                if (length >= MAX_DATA_SIZE)
                {
                    Debug.LogError("Cannot write data, its size is too high");
                    return;
                }
                writerBuffer[0] = (byte)(length >> 24);
                writerBuffer[1] = (byte)(length >> 16);
                writerBuffer[2] = (byte)(length >> 8);
                writerBuffer[3] = (byte)length;
                Buffer.BlockCopy(writer.Data, 0, writerBuffer, 4, length);
                TcpClient.GetStream().Write(writerBuffer, 0, 4 + length);
            }
        }

        private long nextConnectionId = 1;
        private int maxConnections;
        private Client client;
        private TcpListener listener;
        private readonly ConcurrentQueue<TransportEventData> serverEventQueue;
        private readonly ConcurrentDictionary<long, Client> acceptedClients;
        private bool dirtyIsConnected;
        private Thread updateThread;
        private Thread acceptThread;
        private bool updating;
        private bool accepting;

        public TcpTransport()
        {
            serverEventQueue = new ConcurrentQueue<TransportEventData>();
            acceptedClients = new ConcurrentDictionary<long, Client>();
        }

        public void Destroy()
        {
            StopClient();
            StopServer();
        }

        public bool IsClientStarted()
        {
            return client != null && client.Connected;
        }

        public bool ClientReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (client == null)
                return false;
            if (dirtyIsConnected != client.Connected)
            {
                dirtyIsConnected = client.Connected;
                if (client.Connected)
                {
                    // Connect state changed to connected, so it's connect event
                    eventData.type = ENetworkEvent.ConnectEvent;
                }
                else
                {
                    // Connect state changed to not connected, so it's disconnect event
                    eventData.type = ENetworkEvent.DisconnectEvent;
                }
                return true;
            }
            else
            {
                if (!client.Connected)
                    return false;
                NetDataReader reader = client.ReadData();
                if (reader != null)
                {
                    eventData.type = ENetworkEvent.DataEvent;
                    eventData.reader = reader;
                    return true;
                }
            }
            return false;
        }

        public bool ClientSend(DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (IsClientStarted())
            {
                client.WriteData(writer);
                return true;
            }
            return false;
        }

        public bool StartClient(string address, int port)
        {
            dirtyIsConnected = false;
            client = new Client(new TcpClient(AddressFamily.InterNetworkV6));
            client.TcpClient.Client.DualMode = true;
            client.TcpClient.Connect(address, port);
            return true;
        }

        public void StopClient()
        {
            if (client != null)
                client.Dispose();
            client = null;
        }

        public bool IsServerStarted()
        {
            return listener != null;
        }

        public bool ServerDisconnect(long connectionId)
        {
            if (IsServerStarted() && acceptedClients.ContainsKey(connectionId))
            {
                Client client;
                if (acceptedClients.TryRemove(connectionId, out client))
                    client.Dispose();
            }
            return false;
        }

        public bool ServerReceive(out TransportEventData eventData)
        {
            eventData = default(TransportEventData);
            if (!IsServerStarted())
                return false;
            if (serverEventQueue.Count == 0)
                return false;
            return serverEventQueue.TryDequeue(out eventData);
        }

        public bool ServerSend(long connectionId, DeliveryMethod deliveryMethod, NetDataWriter writer)
        {
            if (IsServerStarted() && acceptedClients.ContainsKey(connectionId) && acceptedClients[connectionId].Connected)
            {
                acceptedClients[connectionId].WriteData(writer);
                return true;
            }
            return false;
        }

        public bool StartServer(int port, int maxConnections)
        {
            if (IsServerStarted())
                return false;
            this.maxConnections = maxConnections;
            while (serverEventQueue.Count > 0)
                serverEventQueue.TryDequeue(out _);
            acceptedClients.Clear();
            listener = TcpListener.Create(port);
            listener.Server.NoDelay = true;
            listener.Start();

            if (updateThread != null)
            {
                updateThread.Abort();
                updateThread.Join();
            }

            if (acceptThread != null)
            {
                acceptThread.Abort();
                acceptThread.Join();
            }

            updating = true;
            updateThread = new Thread(UpdateThreadFunction);
            updateThread.IsBackground = true;
            updateThread.Start();

            accepting = true;
            acceptThread = new Thread(AcceptThreadFunction);
            acceptThread.IsBackground = true;
            acceptThread.Start();
            return true;
        }

        public void StopServer()
        {
            updating = false;
            if (updateThread != null)
            {
                updateThread.Abort();
                updateThread.Join();
            }
            updateThread = null;

            accepting = false;
            if (acceptThread != null)
            {
                acceptThread.Abort();
                acceptThread.Join();
            }
            acceptThread = null;

            if (listener != null)
                listener.Stop();
            listener = null;
        }

        public int GetServerPeersCount()
        {
            if (IsServerStarted())
                return acceptedClients.Count;
            return 0;
        }

        private void AcceptThreadFunction()
        {
            try
            {
                TcpClient newClient;
                long newConnectionId;
                while (accepting)
                {
                    // Get the socket that handles the client request.
                    newClient = listener.AcceptTcpClient();
                    if (acceptedClients.Count >= maxConnections)
                    {
                        newClient.Close();
                        newClient.Dispose();
                        continue;
                    }
                    // Create new connection id for this client
                    newConnectionId = nextConnectionId++;

                    // Store client to dictionary
                    if (!acceptedClients.TryAdd(newConnectionId, new Client(newClient)))
                    {
                        newClient.Close();
                        newClient.Dispose();
                        continue;
                    }

                    // Store network event to queue
                    serverEventQueue.Enqueue(new TransportEventData()
                    {
                        type = ENetworkEvent.ConnectEvent,
                        connectionId = newConnectionId,
                    });
                }
            }
            catch (ThreadAbortException)
            {
                // Happen when abort, do nothing
            }
            catch (Exception e)
            {
                Debug.LogError("Accept thread exception: " + e);
            }
        }

        private void UpdateThreadFunction()
        {
            try
            {
                List<long> connectionIds = new List<long>();
                Client client;
                while (updating)
                {
                    // Check disconnected connections
                    if (acceptedClients.Count == 0)
                        continue;
                    connectionIds.Clear();
                    connectionIds.AddRange(acceptedClients.Keys);
                    foreach (long connectionId in connectionIds)
                    {
                        if (!acceptedClients.TryGetValue(connectionId, out client))
                            continue;
                        if (client.Connected)
                        {
                            NetDataReader reader = client.ReadData();
                            if (reader == null)
                                continue;
                            serverEventQueue.Enqueue(new TransportEventData()
                            {
                                type = ENetworkEvent.DataEvent,
                                reader = reader,
                                connectionId = connectionId,
                            });
                        }
                        else
                        {
                            if (!acceptedClients.TryRemove(connectionId, out _))
                                continue;
                            serverEventQueue.Enqueue(new TransportEventData()
                            {
                                type = ENetworkEvent.DisconnectEvent,
                                connectionId = connectionId,
                            });
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // Happen when abort, do nothing
            }
            catch (Exception e)
            {
                Debug.LogError("Update thread exception: " + e);
            }
        }
    }
}
