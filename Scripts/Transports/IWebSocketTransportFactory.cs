using System.Security.Authentication;

namespace LiteNetLibManager
{
    public interface IWebSocketTransportFactory
    {
        bool Secure { get; set; }
        string CertificateFilePath { get; set; }
        string CertificatePassword { get; set; }
    }
}
