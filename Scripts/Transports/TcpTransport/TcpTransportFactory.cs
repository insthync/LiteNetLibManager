namespace LiteNetLibManager
{
    public class TcpTransportFactory : BaseTransportFactory
    {
        public override bool CanUseWithWebGL { get { return false; } }

        public override ITransport Build()
        {
            return new TcpTransport();
        }
    }
}
