using LiteNetLib;
using LiteNetLib.Utils;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace LiteNetLibManager.Tests
{
    public class ServerRestartTests
    {
        [Test]
        public void StopServer_ClearsConnectionIds()
        {
            var server = new LiteNetLibServer(new OfflineTransport());
            Assert.IsTrue(server.StartServer(0, 2));
            server.ConnectionIds.Add(11);
            server.ConnectionIds.Add(12);

            server.StopServer();
            CollectionAssert.IsEmpty(server.ConnectionIds);
            Assert.IsTrue(server.StartServer(0, 2));
            CollectionAssert.IsEmpty(server.ConnectionIds);
            server.StopServer();
        }

        [Test]
        public void OfflineServerRestart_DropsQueuedPacketsFromPreviousRun()
        {
            var transport = new OfflineTransport();
            Assert.IsTrue(transport.StartServer(0, 1));
            var writer = new NetDataWriter();
            writer.Put(42);
            transport.ClientSend(0, DeliveryMethod.ReliableOrdered, writer);

            transport.StopServer();
            Assert.IsTrue(transport.StartServer(0, 1));
            Assert.IsFalse(transport.ServerReceive(out _));
            transport.StopServer();
        }

        [Test]
        public async Task WebSocketServerRestart_DropsEventsFromPreviousRun()
        {
            var firstReservation = new TcpListener(IPAddress.Loopback, 0);
            var secondReservation = new TcpListener(IPAddress.Loopback, 0);
            firstReservation.Start();
            secondReservation.Start();
            int firstPort = ((IPEndPoint)firstReservation.LocalEndpoint).Port;
            int secondPort = ((IPEndPoint)secondReservation.LocalEndpoint).Port;
            firstReservation.Stop();
            secondReservation.Stop();

            var transport = new WebSocketTransport(false, null, null, null);
            var client = new ClientWebSocket();
            try
            {
                Assert.IsTrue(transport.StartServer(firstPort, 2));
                await client.ConnectAsync(new Uri($"ws://127.0.0.1:{firstPort}/netcode/"), CancellationToken.None);
                for (int i = 0; i < 200 && transport.ServerPeersCount != 1; ++i)
                    await Task.Delay(10);
                Assert.AreEqual(1, transport.ServerPeersCount);

                transport.StopServer();
                Assert.IsTrue(transport.StartServer(secondPort, 2));
                Assert.IsFalse(transport.ServerReceive(out _));
            }
            finally
            {
                client.Dispose();
                transport.StopServer();
            }
        }
    }
}
