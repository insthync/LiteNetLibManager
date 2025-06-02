namespace LiteNetLibManager
{
    public class WebSocketTransportFactory : BaseTransportFactory, IWebSocketTransportFactory
    {
        public bool Secure { get; set; } = false;
        public string CertificateFilePath { get; set; } = string.Empty;
        public string CertificateBase64String { get; set; } = string.Empty;
        public string CertificatePassword { get; set; } = string.Empty;

        public override ITransport Build()
        {
            return new WebSocketTransport(Secure, CertificateFilePath, CertificateBase64String, CertificatePassword);
        }
    }
}
