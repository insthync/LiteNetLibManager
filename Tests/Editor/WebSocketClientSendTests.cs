using LiteNetLib.Utils;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LiteNetLibManager.Tests
{
    public class WebSocketClientSendTests
    {
        [Test]
        public async Task ReusedWriter_SendsOwnedPayloadsInOrder()
        {
            var portReservation = new TcpListener(IPAddress.Loopback, 0);
            portReservation.Start();
            int port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
            portReservation.Stop();

            string url = $"ws://127.0.0.1:{port}/netcode/";
            var events = new ConcurrentQueue<TransportEventData>();
            var server = new WebSocketServer(url, null, events);
            var client = new WebSocketClient(url);
            try
            {
                Assert.IsTrue(server.StartServer());
                Assert.IsTrue(client.Connect());
                for (int i = 0; i < 200 && !client.IsOpen; ++i)
                    await Task.Delay(10);
                Assert.IsTrue(client.IsOpen, "WebSocket client did not connect.");

                var writer = new NetDataWriter();
                for (int i = 0; i < 100; ++i)
                {
                    writer.Reset();
                    writer.Put(i);
                    Assert.IsTrue(client.ClientSend(writer));
                }
                writer.Reset();
                writer.Put(-1);

                int received = 0;
                for (int attempt = 0; attempt < 300 && received < 100; ++attempt)
                {
                    while (events.TryDequeue(out TransportEventData eventData))
                    {
                        if (eventData.type != ENetworkEvent.DataEvent)
                            continue;
                        Assert.AreEqual(received, eventData.reader.GetInt());
                        ++received;
                    }
                    if (received < 100)
                        await Task.Delay(10);
                }
                Assert.AreEqual(100, received);
            }
            finally
            {
                client.Close();
                server.Stop();
            }
        }
    }
}
