using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public class ServerErrorMessage : INetSerializable
    {
        public bool shouldDisconnect;
        public string errorMessage;

        public void Deserialize(NetDataReader reader)
        {
            shouldDisconnect = reader.GetBool();
            errorMessage = reader.GetString();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(shouldDisconnect);
            writer.Put(errorMessage);
        }
    }
}