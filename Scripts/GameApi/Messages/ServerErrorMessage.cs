using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    public class ServerErrorMessage : ILiteNetLibMessage
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