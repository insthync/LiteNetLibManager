using LiteNetLib.Utils;

namespace LiteNetLibHighLevel
{
    [System.Serializable]
    public class SyncFieldInt : LiteNetLibSyncFieldBase<int>
    {
        public override void Deserialize(NetDataReader reader)
        {
            Value = reader.GetInt();
        }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Value);
        }
    }
}
