using UnityEngine;

namespace LiteNetLibManager
{
    public class MixTransportFactory : BaseTransportFactory, IWebSocketTransportFactory
    {
        [SerializeField]
        private bool shouldUseWebSocket = false;
        [SerializeField]
        private string connectKey = "SampleConnectKey";
        [SerializeField]
        private int webSocketPortOffset = 100;
        [SerializeField]
        private bool webSocketSecure = false;
        [SerializeField]
        private string webSocketCertificateFilePath = string.Empty;
        [SerializeField]
        private string webSocketCertificateBase64String = string.Empty;
        [SerializeField]
        private string webSocketCertificatePassword = string.Empty;
        [Range(1, 64)]
        [SerializeField]
        private byte clientDataChannelsCount = 16;
        [Range(1, 64)]
        [SerializeField]
        private byte serverDataChannelsCount = 16;
        public bool ShouldUseWebSocket { get { return shouldUseWebSocket; } set { shouldUseWebSocket = value; } }
        public bool Secure { get { return webSocketSecure; } set { webSocketSecure = value; } }
        public string CertificateFilePath { get { return webSocketCertificateFilePath; } set { webSocketCertificateFilePath = value; } }
        public string CertificateBase64String { get { return webSocketCertificateBase64String; } set { webSocketCertificateBase64String = value; } }
        public string CertificatePassword { get { return webSocketCertificatePassword; } set { webSocketCertificatePassword = value; } }

        public override ITransport Build()
        {
            return new MixTransport(shouldUseWebSocket, connectKey, clientDataChannelsCount, serverDataChannelsCount,
                webSocketPortOffset, webSocketSecure, webSocketCertificateFilePath, webSocketCertificateBase64String, webSocketCertificatePassword);
        }
    }
}
