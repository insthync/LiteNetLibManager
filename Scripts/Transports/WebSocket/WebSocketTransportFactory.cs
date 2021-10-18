namespace LiteNetLibManager
{
    public class WebSocketTransportFactory : BaseTransportFactory
    {
        public override bool CanUseWithWebGL { get { return true; } }
        public bool secure;

        public override ITransport Build()
        {
            return new WebSocketTransport(secure);
        }
    }
}
