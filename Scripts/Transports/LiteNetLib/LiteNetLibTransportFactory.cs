namespace LiteNetLibManager
{
    public class LiteNetLibTransportFactory : BaseTransportFactory
    {
        public string ConnectKey { get; set; } = "SampleConnectKey";
        public byte ClientDataChannelsCount { get; set; } = 16;
        public byte ServerDataChannelsCount { get; set; } = 16;

        public override ITransport Build()
        {
            return new LiteNetLibTransport(ConnectKey, ClientDataChannelsCount, ServerDataChannelsCount);
        }
    }
}
