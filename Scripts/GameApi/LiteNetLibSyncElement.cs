using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncElement : LiteNetLibElement
    {
        internal virtual bool WillSyncData(uint tick)
        {
            return true;
        }
        internal abstract bool WriteSyncData(uint tick, NetDataWriter writer);
        internal abstract bool ReadSyncData(uint tick, NetDataReader reader);
    }
}