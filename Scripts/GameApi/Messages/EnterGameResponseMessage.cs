using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class EnterGameResponseMessage : BaseAckMessage
    {
        public long connectionId;
        public string serverSceneName;

        public override void SerializeData(NetDataWriter writer)
        {
            writer.PutPackedLong(connectionId);
            writer.Put(serverSceneName);
        }

        public override void DeserializeData(NetDataReader reader)
        {
            connectionId = reader.GetPackedLong();
            serverSceneName = reader.GetString();
        }
    }
}
