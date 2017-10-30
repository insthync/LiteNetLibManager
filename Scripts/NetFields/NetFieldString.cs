using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class NetFieldString : LiteNetLibNetField<string>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetString();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }
    }
}
