using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct TransportEventData
    {
        public ENetworkEvent type;
        public long connectionId;
        public NetDataReader reader;
        public DisconnectInfo disconnectInfo;
        public NetEndPoint endPoint;
        public int socketErrorCode;
    }
}
