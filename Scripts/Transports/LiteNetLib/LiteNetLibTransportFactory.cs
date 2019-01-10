namespace LiteNetLibManager
{
    public class LiteNetLibTransportFactory : BaseTransportFactory
    {
        public override bool CanUseWithWebGL { get { return false; } }

        public override ITransport Build()
        {
            return new LiteNetLibTransport();
        }
    }
}
