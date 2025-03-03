using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncElement : LiteNetLibElement
    {
        internal virtual bool WillSyncData(long connectionId, uint tick)
        {
            return Identity.Subscribers.Contains(connectionId);
        }
        internal abstract void WriteSyncData(NetDataWriter writer, uint tick);
        internal abstract void ReadSyncData(NetDataReader reader, uint tick);
    }
}