using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class ServerDestroyObjectMessage : ILiteNetLibMessage
    {
        public uint objectId;

        public void Deserialize(NetDataReader reader)
        {
            objectId = reader.GetUInt();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(objectId);
        }
    }
}
