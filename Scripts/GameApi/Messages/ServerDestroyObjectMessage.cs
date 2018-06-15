using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class ServerDestroyObjectMessage : ILiteNetLibMessage
    {
        public uint objectId;
        public DestroyObjectReasons reasons;

        public void Deserialize(NetDataReader reader)
        {
            objectId = reader.GetPackedUInt();
            reasons = (DestroyObjectReasons)reader.GetByte();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUInt(objectId);
            writer.Put((byte)reasons);
        }
    }
}
