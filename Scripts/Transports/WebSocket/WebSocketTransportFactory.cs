using System.Security.Authentication;
using UnityEngine;

namespace LiteNetLibManager
{
    public class WebSocketTransportFactory : BaseTransportFactory, IWebSocketTransportFactory
    {
        [SerializeField] 
        private bool secure = false;
        [SerializeField]
        private SslProtocols sslProtocols = SslProtocols.None;
        [SerializeField]
        private string certificateFilePath = string.Empty;
        [SerializeField]
        private string certificatePassword = string.Empty;
        public bool Secure { get { return secure; } set { secure = value; } }
        public SslProtocols SslProtocols { get { return sslProtocols; } set { sslProtocols = value; } }
        public string CertificateFilePath { get { return certificateFilePath; } set { certificateFilePath = value; } }
        public string CertificatePassword { get { return certificatePassword; } set { certificatePassword = value; } }

        public override ITransport Build()
        {
            return new WebSocketTransport(secure, sslProtocols, certificateFilePath, certificatePassword);
        }
    }
}
