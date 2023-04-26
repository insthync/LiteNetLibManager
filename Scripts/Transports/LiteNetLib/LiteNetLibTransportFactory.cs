namespace LiteNetLibManager
{
    public class LiteNetLibTransportFactory : BaseTransportFactory
    {
        public string connectKey = "SampleConnectKey";
        public byte clientDataChannelsCount = 16;
        public byte serverDataChannelsCount = 16;

        public override ITransport Build()
        {
            return new LiteNetLibTransport(connectKey, clientDataChannelsCount, serverDataChannelsCount);
        }
    }
}
