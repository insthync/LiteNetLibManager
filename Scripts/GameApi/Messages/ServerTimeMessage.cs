using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public struct ServerTimeMessage : INetSerializable
    {
        public int serverUnixTime;

        public void Deserialize(NetDataReader reader)
        {
            serverUnixTime = reader.GetPackedInt();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedInt(serverUnixTime);
        }
    }
}
