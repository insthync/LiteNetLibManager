using LiteNetLib;
using LiteNetLib.Utils;
using NUnit.Framework;

namespace LiteNetLibManager.Tests
{
    public class TransportHandlerPacketTests
    {
        private sealed class TestHandler : TransportHandler
        {
            public override string LogTag => "PacketTests";
            public override bool IsNetworkActive => true;

            public void Read(NetDataReader reader) => ReadPacket(1, reader);

            public override void SendMessage(long connectionId, byte dataChannel,
                DeliveryMethod deliveryMethod, NetDataWriter writer) { }
        }

        private sealed class RecordingLogger : ILogger
        {
            public int Errors;
            public void LogInformation(string message, params object[] args) { }
            public void LogError(string message, params object[] args) => ++Errors;
            public void LogWarning(string message, params object[] args) { }
            public void LogInformationQuiet(string message, params object[] args) { }
            public void LogErrorQuiet(string message, params object[] args) { }
            public void LogWarningQuiet(string message, params object[] args) { }
        }

        private sealed class RecordingLoggerFactory : ILoggerFactory
        {
            public readonly RecordingLogger Logger = new RecordingLogger();
            public ILogger CreateLogger(string categoryName) => Logger;
            public void Dispose() { }
        }

        [Test]
        public void TruncatedPacketHeaders_AreDroppedWithoutStoppingLaterPackets()
        {
            LoggerManager previousLogger = LogManager.LoggerManager;
            var loggerFactory = new RecordingLoggerFactory();
            LogManager.LoggerManager = new LoggerManager(loggerFactory);
            try
            {
                var handler = new TestHandler();
                Assert.IsTrue(handler.EnableRequestResponse(1, 2));
                int received = 0;
                handler.RegisterMessageHandler(3, data => ++received);

                Assert.DoesNotThrow(() => handler.Read(new NetDataReader(new byte[0])));
                Assert.DoesNotThrow(() => handler.Read(new NetDataReader(new byte[] { 1 })));
                Assert.DoesNotThrow(() => handler.Read(new NetDataReader(new byte[] { 2 })));

                var writer = new NetDataWriter();
                TransportHandler.WritePacket(writer, 3);
                handler.Read(new NetDataReader(writer.CopyData()));

                Assert.AreEqual(1, received);
                Assert.AreEqual(6, loggerFactory.Logger.Errors);
            }
            finally
            {
                LogManager.LoggerManager = previousLogger;
            }
        }
    }
}
