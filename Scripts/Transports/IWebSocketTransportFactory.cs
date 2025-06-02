namespace LiteNetLibManager
{
    public interface IWebSocketTransportFactory
    {
        bool Secure { get; set; }
        string CertificateFilePath { get; set; }
        string CertificatePassword { get; set; }
        string CertificateBase64String { get; set; }
    }
}
