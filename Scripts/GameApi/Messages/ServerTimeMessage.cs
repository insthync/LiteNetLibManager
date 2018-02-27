using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class ServerTimeMessage : ILiteNetLibMessage
    {
        public float serverTime;

        public void Deserialize(NetDataReader reader)
        {
            serverTime = reader.GetFloat();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(serverTime);
        }
    }
}
