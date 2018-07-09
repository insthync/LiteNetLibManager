using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibManager
{
    public class ServerSpawnObjectMessage : ILiteNetLibMessage
    {
        public int hashAssetId;
        public uint objectId;
        public long connectId;
        public Vector3 position;
        public Quaternion rotation;

        public void Deserialize(NetDataReader reader)
        {
            hashAssetId = reader.GetInt();
            objectId = reader.GetPackedUInt();
            connectId = (long)reader.GetPackedULong();
            position = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            rotation = Quaternion.Euler(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(hashAssetId);
            writer.PutPackedUInt(objectId);
            writer.PutPackedULong((ulong)connectId);
            writer.Put(position.x);
            writer.Put(position.y);
            writer.Put(position.z);
            writer.Put(rotation.eulerAngles.x);
            writer.Put(rotation.eulerAngles.y);
            writer.Put(rotation.eulerAngles.z);
        }
    }
}
