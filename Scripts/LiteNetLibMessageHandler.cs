using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class LiteNetLibMessageHandler
    {
        public short msgType;
        public NetPeer peer;
        public NetDataReader reader;
    }
}
