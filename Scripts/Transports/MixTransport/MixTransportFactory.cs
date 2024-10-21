namespace LiteNetLibManager
{
    public class MixTransportFactory : BaseTransportFactory, IWebSocketTransportFactory
    {
        public string ConnectKey { get; set; } = "SampleConnectKey";
        public int WebSocketPortOffset { get; set; } = 100;
        public byte ClientDataChannelsCount { get; set; } = 16;
        public byte ServerDataChannelsCount { get; set; } = 16;
        public bool Secure { get; set; }
        public string CertificateFilePath { get; set; } = string.Empty;
        public string CertificatePassword { get; set; } = string.Empty;

        public override ITransport Build()
        {
            return new MixTransport(ConnectKey, WebSocketPortOffset, Secure, CertificateFilePath, CertificatePassword, ClientDataChannelsCount, ServerDataChannelsCount);
        }
    }
}
