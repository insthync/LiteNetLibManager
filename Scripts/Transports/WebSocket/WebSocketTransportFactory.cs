namespace LiteNetLibManager
{
    public class WebSocketTransportFactory : BaseTransportFactory
    {
        public override bool CanUseWithWebGL { get { return true; } }
        public bool secure = false;
        public string certificateFilePath = string.Empty;
        public string certificatePassword = string.Empty;

        public override ITransport Build()
        {
            return new WebSocketTransport(secure, certificateFilePath, certificatePassword);
        }
    }
}
