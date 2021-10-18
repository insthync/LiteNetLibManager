using UnityEngine;

namespace LiteNetLibManager
{
    public class LiteNetLibTransportFactory : BaseTransportFactory
    {
        public string connectKey = "SampleConnectKey";
        [Range(1, 64)]
        public byte clientDataChannelsCount = 16;
        [Range(1, 64)]
        public byte serverDataChannelsCount = 16;

        public override ITransport Build()
        {
            return new LiteNetLibTransport(connectKey, clientDataChannelsCount, serverDataChannelsCount);
        }
    }
}
