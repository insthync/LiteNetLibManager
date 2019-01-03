namespace LiteNetLibManager
{
    public class WebSocketTransportFactory : BaseTransportFactory
    {
        public override ITransport Build()
        {
            return new WebSocketTransport();
        }
    }
}
