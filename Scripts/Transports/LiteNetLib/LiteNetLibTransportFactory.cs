namespace LiteNetLibManager
{
    public class LiteNetLibTransportFactory : BaseTransportFactory
    {
        public override ITransport Build()
        {
            return new LiteNetLibTransport();
        }
    }
}
