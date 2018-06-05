using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class BaseAckMessage : ILiteNetLibMessage
    {
        public uint ackId;

        public void Deserialize(NetDataReader reader)
        {
            ackId = reader.GetUInt();
            DeserializeData(reader);
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(ackId);
            SerializeData(writer);
        }

        public abstract void DeserializeData(NetDataReader reader);
        public abstract void SerializeData(NetDataWriter writer);
    }
}
