using LiteNetLib.Utils;

namespace LiteNetLibManager
{
    public abstract class LiteNetLibSyncElement : LiteNetLibElement
    {
        internal virtual bool WillSyncData(long connectionId)
        {
            return Identity.Subscribers.Contains(connectionId);
        }
        internal abstract void WriteSyncData(NetDataWriter writer);
        internal abstract void ReadSyncData(NetDataReader reader);
    }
}