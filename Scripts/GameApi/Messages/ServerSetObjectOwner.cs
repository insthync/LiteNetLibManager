using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class ServerSetObjectOwner : INetSerializable
    {
        public uint objectId;
        public long connectionId;

        public void Deserialize(NetDataReader reader)
        {
            objectId = reader.GetPackedUInt();
            connectionId = reader.GetPackedLong();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUInt(objectId);
            writer.PutPackedLong(connectionId);
        }
    }
}
