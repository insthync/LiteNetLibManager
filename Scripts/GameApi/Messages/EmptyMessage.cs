using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    /// <summary>
    /// An empty message may be used as empty request or response
    /// </summary>
    public sealed class EmptyMessage : INetSerializable
    {
        public void Serialize(NetDataWriter writer)
        {
        }

        public void Deserialize(NetDataReader reader)
        {
        }
    }
}
