using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    /// <summary>
    /// An empty message may be used as empty request or response
    /// </summary>
    public struct EmptyMessage : INetSerializable
    {
        public static readonly EmptyMessage Value = new EmptyMessage();

        public void Deserialize(NetDataReader reader)
        {
        }

        public void Serialize(NetDataWriter writer)
        {
        }
    }
}
