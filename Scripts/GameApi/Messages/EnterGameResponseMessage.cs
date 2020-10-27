using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class EnterGameResponseMessage : INetSerializable
    {
        public long connectionId;
        public string serverSceneName;

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedLong(connectionId);
            writer.Put(serverSceneName);
        }

        public void Deserialize(NetDataReader reader)
        {
            connectionId = reader.GetPackedLong();
            serverSceneName = reader.GetString();
        }
    }
}
