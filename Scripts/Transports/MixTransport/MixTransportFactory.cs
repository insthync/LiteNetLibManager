namespace LiteNetLibManager
{
    public class MixTransportFactory : BaseTransportFactory, IWebSocketTransportFactory
    {
        public string ConnectKey { get; set; } = "SampleConnectKey";
        public int WebSocketPortOffset { get; set; } = 100;
        public byte ClientDataChannelsCount { get; set; } = 16;
        public byte ServerDataChannelsCount { get; set; } = 16;
        public bool ShouldUseWebSocket { get; set; } = false;
        public bool Secure { get; set; } = false;
        public string CertificateFilePath { get; set; } = string.Empty;
        public string CertificateBase64String { get; set; } = string.Empty;
        public string CertificatePassword { get; set; } = string.Empty;

        public override ITransport Build()
        {
            return new MixTransport(ShouldUseWebSocket, ConnectKey, ClientDataChannelsCount, ServerDataChannelsCount,
                WebSocketPortOffset, Secure, CertificateFilePath, CertificateBase64String, CertificatePassword);
        }
    }
}
