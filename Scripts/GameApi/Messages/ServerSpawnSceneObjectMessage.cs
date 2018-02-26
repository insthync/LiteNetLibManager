using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class ServerSpawnSceneObjectMessage : ILiteNetLibMessage
    {
        public uint objectId;
        public Vector3 position;

        public void Deserialize(NetDataReader reader)
        {
            objectId = reader.GetUInt();
            position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(objectId);
            writer.Put(position.x);
            writer.Put(position.y);
            writer.Put(position.z);
        }
    }
}
