using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class ServerDestroyObjectMessage : ILiteNetLibMessage
    {
        public uint objectId;
        public DestroyObjectReasons reasons;

        public void Deserialize(NetDataReader reader)
        {
            objectId = reader.GetUInt();
            reasons = (DestroyObjectReasons)reader.GetByte();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(objectId);
            writer.Put((byte)reasons);
        }
    }
}
