using LiteNetLib;
using LiteNetLib.Utils;
using NUnit.Framework;
using System.Threading.Tasks;

namespace LiteNetLibManager.Tests
{
    public class RequestFailureTests
    {
        private class OtherMessage<T> : INetSerializable
        {
            public OtherMessage() { }
            public void Serialize(NetDataWriter writer) { }
            public void Deserialize(NetDataReader reader) { }
        }

        private sealed class SilentLogger : ILogger
        {
            public void LogInformation(string message, params object[] args) { }
            public void LogError(string message, params object[] args) { }
            public void LogWarning(string message, params object[] args) { }
            public void LogInformationQuiet(string message, params object[] args) { }
            public void LogErrorQuiet(string message, params object[] args) { }
            public void LogWarningQuiet(string message, params object[] args) { }
        }

        private sealed class SilentLoggerFactory : ILoggerFactory
        {
            private readonly SilentLogger _logger = new SilentLogger();
            public ILogger CreateLogger(string categoryName) => _logger;
            public void Dispose() { }
        }

        [Test]
        public async Task FailedRequests_ReportUnimplementedWithoutSendingPreviousPacket()
        {
            LoggerManager previousLogger = LogManager.LoggerManager;
            LogManager.LoggerManager = new LoggerManager(new SilentLoggerFactory());
            try
            {
                var transport = new OfflineTransport();
                var client = new LiteNetLibClient(transport);
                var server = new LiteNetLibServer(transport);

                client.SendPacket(0, DeliveryMethod.ReliableOrdered, 77, null);
                Assert.IsTrue(transport.ServerReceive(out TransportEventData clientPacket));
                Assert.AreEqual(77, clientPacket.reader.GetPackedUShort());

                AsyncResponseData<EmptyMessage> clientResult =
                    await client.SendRequestAsync<EmptyMessage, EmptyMessage>(99, EmptyMessage.Value);
                Assert.AreEqual(AckResponseCode.Unimplemented, clientResult.ResponseCode);
                Assert.IsFalse(transport.ServerReceive(out _));

                server.SendPacket(1, 0, DeliveryMethod.ReliableOrdered, 78, null);
                Assert.IsTrue(transport.ClientReceive(out TransportEventData serverPacket));
                Assert.AreEqual(78, serverPacket.reader.GetPackedUShort());

                AsyncResponseData<EmptyMessage> serverResult =
                    await server.SendRequestAsync<EmptyMessage, EmptyMessage>(1, 99, EmptyMessage.Value);
                Assert.AreEqual(AckResponseCode.Unimplemented, serverResult.ResponseCode);
                Assert.IsFalse(transport.ClientReceive(out _));

                Assert.IsFalse(client.SendRequest(99, EmptyMessage.Value));
                Assert.IsFalse(transport.ServerReceive(out _));

                client.RegisterResponseHandler<EmptyMessage, EmptyMessage>(5);
                Assert.IsFalse(client.SendRequest(5, new OtherMessage<int>()));
                Assert.IsFalse(transport.ServerReceive(out _));
            }
            finally
            {
                LogManager.LoggerManager = previousLogger;
            }
        }
    }
}
