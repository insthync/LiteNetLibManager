using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public interface INetSerializableWithElement : INetSerializable
    {
        LiteNetLibElement Element { get; set; }
    }
}
