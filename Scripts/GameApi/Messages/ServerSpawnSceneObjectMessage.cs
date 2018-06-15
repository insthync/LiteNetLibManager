using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibManager
{
    public class ServerSpawnSceneObjectMessage : ILiteNetLibMessage
    {
        public uint objectId;
        public Vector3 position;
        public Quaternion rotation;

        public void Deserialize(NetDataReader reader)
        {
            objectId = reader.GetPackedUInt();
            position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            rotation = Quaternion.Euler(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.PutPackedUInt(objectId);
            writer.Put(position.x);
            writer.Put(position.y);
            writer.Put(position.z);
            writer.Put(rotation.eulerAngles.x);
            writer.Put(rotation.eulerAngles.y);
            writer.Put(rotation.eulerAngles.z);
        }
    }
}
