using UnityEngine;

namespace LiteNetLibManager
{
    public class MixTransportFactory : BaseTransportFactory, IWebSocketTransportFactory
    {
        public string connectKey = "SampleConnectKey";
        public int webSocketPortOffset = 100;
        public bool webSocketSecure = false;
        public string webSocketCertificateFilePath = string.Empty;
        public string webSocketCertificatePassword = string.Empty;
        [Range(1, 64)]
        public byte clientDataChannelsCount = 16;
        [Range(1, 64)]
        public byte serverDataChannelsCount = 16;
        public bool Secure { get { return webSocketSecure; } set { webSocketSecure = value; } }
        public string CertificateFilePath { get { return webSocketCertificateFilePath; } set { webSocketCertificateFilePath = value; } }
        public string CertificatePassword { get { return webSocketCertificatePassword; } set { webSocketCertificatePassword = value; } }


        public override ITransport Build()
        {
            return new MixTransport(connectKey, webSocketPortOffset, webSocketSecure, webSocketCertificateFilePath, webSocketCertificatePassword, clientDataChannelsCount, serverDataChannelsCount);
        }
    }
}
