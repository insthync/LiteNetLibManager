using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct ServerTimeMessage : INetSerializable
    {
        public long serverUnixTime;

        public void Deserialize(NetDataReader reader)
        {
            serverUnixTime = reader.GetPackedLong();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(serverUnixTime);
        }
    }
}
