using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class BaseAckMessage : INetSerializable
    {
        public uint ackId;
        public AckResponseCode responseCode;

        public void Deserialize(NetDataReader reader)
        {
            ackId = reader.GetUInt();
            responseCode = (AckResponseCode)reader.GetByte();
            DeserializeData(reader);
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(ackId);
            writer.Put((byte)responseCode);
            SerializeData(writer);
        }

        public virtual void DeserializeData(NetDataReader reader) { }
        public virtual void SerializeData(NetDataWriter writer) { }
    }
}
