using UnityEngine;

namespace LiteNetLibManager
{
    public class WebSocketTransportFactory : BaseTransportFactory, IWebSocketTransportFactory
    {
        [SerializeField]
        private bool secure = false;
        [SerializeField]
        private string certificateFilePath = string.Empty;
        [SerializeField]
        private string certificateBase64String = string.Empty;
        [SerializeField]
        private string certificatePassword = string.Empty;
        public bool Secure { get { return secure; } set { secure = value; } }
        public string CertificateFilePath { get { return certificateFilePath; } set { certificateFilePath = value; } }
        public string CertificateBase64String { get { return certificateBase64String; } set { certificateBase64String = value; } }
        public string CertificatePassword { get { return certificatePassword; } set { certificatePassword = value; } }

        public override ITransport Build()
        {
            return new WebSocketTransport(secure, certificateFilePath, certificateBase64String, certificatePassword);
        }
    }
}
