using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct LiteNetLibTransportEventData
    {
        public ENetworkEvent type;
        public long connectionId;
        public NetDataReader reader;
        public DisconnectInfo disconnectInfo;
    }
}
