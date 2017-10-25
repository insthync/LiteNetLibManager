using System.Collections;
using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;

namespace LiteNetLibHighLevel
{
    public class ServerDestroyObjectMessage : LiteNetLibMessageBase
    {
        public uint objectId;

        public override void Deserialize(NetDataReader reader)
        {
            objectId = reader.GetUInt();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(objectId);
        }
    }
}
