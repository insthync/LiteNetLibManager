using System.Security.Authentication;
using UnityEngine;

namespace LiteNetLibManager
{
    public class MixTransportFactory : BaseTransportFactory, IWebSocketTransportFactory
    {
        [SerializeField]
        private string connectKey = "SampleConnectKey";
        [SerializeField]
        private int webSocketPortOffset = 100;
        [SerializeField]
        private bool webSocketSecure = false;
        [SerializeField]
        private SslProtocols webSocketSslProtocols = SslProtocols.None;
        [SerializeField]
        private string webSocketCertificateFilePath = string.Empty;
        [SerializeField]
        private string webSocketCertificatePassword = string.Empty;
        [Range(1, 64)]
        [SerializeField]
        private byte clientDataChannelsCount = 16;
        [Range(1, 64)]
        [SerializeField]
        private byte serverDataChannelsCount = 16;
        public bool Secure { get { return webSocketSecure; } set { webSocketSecure = value; } }
        public SslProtocols SslProtocols { get { return webSocketSslProtocols; } set { webSocketSslProtocols = value; } }
        public string CertificateFilePath { get { return webSocketCertificateFilePath; } set { webSocketCertificateFilePath = value; } }
        public string CertificatePassword { get { return webSocketCertificatePassword; } set { webSocketCertificatePassword = value; } }


        public override ITransport Build()
        {
            return new MixTransport(connectKey, webSocketPortOffset, webSocketSecure, webSocketSslProtocols, webSocketCertificateFilePath, webSocketCertificatePassword, clientDataChannelsCount, serverDataChannelsCount);
        }
    }
}
