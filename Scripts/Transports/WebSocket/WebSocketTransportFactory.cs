namespace LiteNetLibManager
{
    public class WebSocketTransportFactory : BaseTransportFactory
    {
        public override bool CanUseWithWebGL { get { return false; } }

        public override ITransport Build()
        {
            return new WebSocketTransport();
        }
    }
}
