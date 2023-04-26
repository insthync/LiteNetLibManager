namespace LiteNetLibManager
{
    public class WebSocketTransportFactory : BaseTransportFactory, IWebSocketTransportFactory
    {
        public bool Secure { get; set; }
        public string CertificateFilePath { get; set; } = string.Empty;
        public string CertificatePassword { get; set; } = string.Empty;

        public override ITransport Build()
        {
            return new WebSocketTransport(Secure, CertificateFilePath, CertificatePassword);
        }
    }
}
