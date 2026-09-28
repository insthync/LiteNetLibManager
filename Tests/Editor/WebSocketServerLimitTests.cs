using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace LiteNetLibManager.Tests
{
    public class WebSocketServerLimitTests
    {
        [Test]
        public async Task AdditionalConnection_IsRejectedAtConfiguredLimit()
        {
            var portReservation = new TcpListener(IPAddress.Loopback, 0);
            portReservation.Start();
            int port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
            portReservation.Stop();

            var server = new WebSocketServer($"ws://127.0.0.1:{port}/netcode/", null,
                new ConcurrentQueue<TransportEventData>(), 1);
            var first = new ClientWebSocket();
            var second = new ClientWebSocket();
            try
            {
                Assert.IsTrue(server.StartServer());
                var uri = new Uri($"ws://127.0.0.1:{port}/netcode/");
                await first.ConnectAsync(uri, CancellationToken.None);
                for (int i = 0; i < 200 && server.PeersCount != 1; ++i)
                    await Task.Delay(10);
                Assert.AreEqual(1, server.PeersCount);

                try
                {
                    await second.ConnectAsync(uri, CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    // The server may close during the second handshake.
                }
                await Task.Delay(100);
                Assert.AreEqual(1, server.PeersCount);
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                server.Stop();
            }
        }
    }
}
