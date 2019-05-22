using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class ServerTimeMessage : INetSerializable
    {
        public int serverUnixTime;
        public float serverTime;

        public void Deserialize(NetDataReader reader)
        {
            serverUnixTime = reader.GetInt();
            serverTime = reader.GetFloat();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(serverUnixTime);
            writer.Put(serverTime);
        }
    }
}
